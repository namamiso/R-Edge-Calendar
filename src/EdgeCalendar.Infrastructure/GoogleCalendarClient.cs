using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using EdgeCalendar.Core;

namespace EdgeCalendar.Infrastructure
{
    public sealed class GoogleCalendarClient
    {
        private readonly HttpClient _http;
        private readonly GoogleAuthClient _auth;

        public GoogleCalendarClient(HttpClient http, GoogleAuthClient auth)
        {
            _http = http;
            _auth = auth;
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
                        var mapped = MapEvent(calendarId, e);
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

        private static EventItem? MapEvent(string calendarId, EventResource resource)
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
                IsReadOnly = true
            };
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
            public EventTime? Start { get; set; }
            public EventTime? End { get; set; }
        }

        private sealed class EventTime
        {
            public string? Date { get; set; }
            public string? DateTime { get; set; }
        }
    }
}
