using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EdgeCalendar.Core;
using System.Runtime.Versioning;

namespace EdgeCalendar.Infrastructure
{
    [SupportedOSPlatform("windows")]
    public sealed class GoogleCalendarClient
    {
        private readonly HttpClient _http;
        private readonly GoogleAuthClient _auth;

        public GoogleCalendarClient(HttpClient http, GoogleAuthClient auth)
        {
            _http = http;
            _auth = auth;
        }

        public async Task EnsureAuthenticatedAsync()
        {
            _ = await _auth.GetAccessTokenAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/users/me/calendarList");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<CalendarListResponse>(json, JsonOptions.Default)
                       ?? new CalendarListResponse();

            var list = new List<CalendarInfo>();
            if (data.Items != null)
            {
                foreach (var item in data.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Id))
                    {
                        continue;
                    }

                    list.Add(new CalendarInfo
                    {
                        Id = item.Id,
                        Summary = item.Summary ?? item.Id,
                        BackgroundColor = item.BackgroundColor,
                        IsSelected = item.Selected ?? false
                    });
                }
            }

            return list;
        }

        public async Task<IReadOnlyList<EventItem>> GetEventsAsync(string calendarId, DateTime windowStart, DateTime windowEnd)
        {
            var items = new List<EventItem>();
            var timeMin = windowStart.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            var timeMax = windowEnd.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            string? pageToken = null;

            do
            {
                var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events" +
                          $"?timeMin={Uri.EscapeDataString(timeMin)}&timeMax={Uri.EscapeDataString(timeMax)}" +
                          "&singleEvents=true&orderBy=startTime";

                if (!string.IsNullOrEmpty(pageToken))
                {
                    url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));

                var response = await _http.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var data = JsonSerializer.Deserialize<EventListResponse>(json, JsonOptions.Default)
                           ?? new EventListResponse();

                if (data.Items != null)
                {
                    foreach (var e in data.Items)
                    {
                        var mapped = MapEvent(calendarId, e, true);
                        if (mapped != null)
                        {
                            items.Add(mapped);
                        }
                    }
                }

                pageToken = data.NextPageToken;
            } while (!string.IsNullOrEmpty(pageToken));

            return items;
        }

        public async Task<EventItem> CreateEventAsync(string calendarId, EventItem item)
        {
            var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events";
            var payload = BuildEventWrite(item);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions.Default), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<EventResource>(json, JsonOptions.Default)
                       ?? throw new InvalidOperationException("イベント作成の応答を解析できません。");

            var mapped = MapEvent(calendarId, data, false);
            if (mapped == null)
            {
                throw new InvalidOperationException("イベント作成の応答が不正です。");
            }

            return mapped;
        }

        public async Task<EventItem> UpdateEventAsync(string calendarId, string eventId, EventItem item)
        {
            var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}";
            var payload = BuildEventWrite(item);
            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions.Default), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));

            if (!string.IsNullOrWhiteSpace(item.ETag))
            {
                request.Headers.TryAddWithoutValidation("If-Match", item.ETag);
            }

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                var serverJson = await GetEventJsonAsync(calendarId, eventId).ConfigureAwait(false);
                throw new ConflictException("更新競合が発生しました。", serverJson);
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<EventResource>(json, JsonOptions.Default)
                       ?? throw new InvalidOperationException("イベント更新の応答を解析できません。");

            var mapped = MapEvent(calendarId, data, false);
            if (mapped == null)
            {
                throw new InvalidOperationException("イベント更新の応答が不正です。");
            }

            return mapped;
        }

        public async Task DeleteEventAsync(string calendarId, string eventId, string? etag)
        {
            var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}";
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));

            if (!string.IsNullOrWhiteSpace(etag))
            {
                request.Headers.TryAddWithoutValidation("If-Match", etag);
            }

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                var serverJson = await GetEventJsonAsync(calendarId, eventId).ConfigureAwait(false);
                throw new ConflictException("削除競合が発生しました。", serverJson);
            }

            response.EnsureSuccessStatusCode();
        }

        private async Task<string?> GetEventJsonAsync(string calendarId, string eventId)
        {
            var url = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _auth.GetAccessTokenAsync().ConfigureAwait(false));

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static EventItem? MapEvent(string calendarId, EventResource resource, bool readOnly)
        {
            if (string.IsNullOrWhiteSpace(resource.Id) || resource.Status == "cancelled")
            {
                return null;
            }

            bool isAllDay = resource.Start?.Date != null;
            DateTime start;
            DateTime end;

            if (isAllDay)
            {
                if (!DateTime.TryParseExact(resource.Start?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
                {
                    return null;
                }

                if (!DateTime.TryParseExact(resource.End?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
                {
                    return null;
                }
            }
            else
            {
                if (!DateTimeOffset.TryParse(resource.Start?.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startOffset))
                {
                    return null;
                }

                if (!DateTimeOffset.TryParse(resource.End?.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endOffset))
                {
                    return null;
                }

                start = startOffset.ToLocalTime().DateTime;
                end = endOffset.ToLocalTime().DateTime;
            }

            return new EventItem
            {
                Title = resource.Summary ?? "(無題)",
                StartLocal = start,
                EndLocal = end,
                IsAllDay = isAllDay,
                Location = resource.Location,
                Notes = resource.Description,
                UpdatedAtLocal = DateTime.Now,
                Source = "google",
                ExternalId = resource.Id,
                CalendarId = calendarId,
                IsReadOnly = readOnly,
                ETag = resource.ETag
            };
        }

        private static EventWrite BuildEventWrite(EventItem item)
        {
            var write = new EventWrite
            {
                Summary = item.Title,
                Location = item.Location,
                Description = item.Notes,
                Start = new EventTimeWrite(),
                End = new EventTimeWrite()
            };

            if (item.IsAllDay)
            {
                write.Start.Date = item.StartLocal.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                write.End.Date = item.EndLocal.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else
            {
                write.Start.DateTime = new DateTimeOffset(item.StartLocal).ToString("o", CultureInfo.InvariantCulture);
                write.End.DateTime = new DateTimeOffset(item.EndLocal).ToString("o", CultureInfo.InvariantCulture);
            }

            return write;
        }

        private sealed class CalendarListResponse
        {
            public List<CalendarResource>? Items { get; set; }
        }

        private sealed class CalendarResource
        {
            public string? Id { get; set; }
            public string? Summary { get; set; }
            public string? BackgroundColor { get; set; }
            public bool? Selected { get; set; }
        }

        private sealed class EventListResponse
        {
            public List<EventResource>? Items { get; set; }
            public string? NextPageToken { get; set; }
        }

        private sealed class EventResource
        {
            public string? Id { get; set; }
            public string? Summary { get; set; }
            public string? Status { get; set; }
            public string? Description { get; set; }
            public string? Location { get; set; }
            public string? ETag { get; set; }
            public EventTime? Start { get; set; }
            public EventTime? End { get; set; }
        }

        private sealed class EventTime
        {
            public string? Date { get; set; }
            public string? DateTime { get; set; }
        }

        private sealed class EventWrite
        {
            public string? Summary { get; set; }
            public string? Location { get; set; }
            public string? Description { get; set; }
            public EventTimeWrite? Start { get; set; }
            public EventTimeWrite? End { get; set; }
        }

        private sealed class EventTimeWrite
        {
            public string? Date { get; set; }
            public string? DateTime { get; set; }
        }
    }
}
