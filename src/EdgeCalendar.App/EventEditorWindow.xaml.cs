using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using EdgeCalendar.Core;

namespace EdgeCalendar.App
{
    public partial class EventEditorWindow : Window
    {
        private readonly DateTime _defaultDate;
        private readonly bool _isNew;
        private readonly bool _allowCalendarSelection;

        public EventEditorWindow(EventItem? item, DateTime defaultDate, IReadOnlyList<CalendarInfo> calendars, string? selectedCalendarId, bool allowCalendarSelection)
        {
            InitializeComponent();

            _defaultDate = defaultDate.Date;
            _isNew = item == null;
            _allowCalendarSelection = allowCalendarSelection;

            Item = item ?? new EventItem
            {
                StartLocal = _defaultDate.AddHours(9),
                EndLocal = _defaultDate.AddHours(10),
                IsAllDay = false,
                Source = "local",
                IsReadOnly = false
            };

            Title = _isNew ? "予定の追加" : "予定の編集";

            CalendarBox.ItemsSource = calendars;
            CalendarBox.IsEnabled = allowCalendarSelection && calendars.Count > 1;

            if (calendars.Count > 0)
            {
                var selected = calendars.FirstOrDefault(c => c.Id == selectedCalendarId) ?? calendars[0];
                CalendarBox.SelectedItem = selected;
            }

            TitleBox.Text = Item.Title;
            StartDatePicker.SelectedDate = Item.StartLocal.Date;
            StartTimeBox.Text = Item.StartLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
            EndDatePicker.SelectedDate = GetEndDateForUi(Item);
            EndTimeBox.Text = Item.EndLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
            AllDayCheckBox.IsChecked = Item.IsAllDay;
            LocationBox.Text = Item.Location ?? string.Empty;
            NotesBox.Text = Item.Notes ?? string.Empty;

            UpdateAllDayState();
        }

        public EventItem Item { get; }

        public string? SelectedCalendarId
        {
            get
            {
                return (CalendarBox.SelectedItem as CalendarInfo)?.Id;
            }
        }

        private void OnAllDayChanged(object sender, RoutedEventArgs e)
        {
            UpdateAllDayState();
        }

        private void UpdateAllDayState()
        {
            bool isAllDay = AllDayCheckBox.IsChecked == true;
            StartTimeBox.IsEnabled = !isAllDay;
            EndTimeBox.IsEnabled = !isAllDay;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (_allowCalendarSelection && SelectedCalendarId == null)
            {
                System.Windows.MessageBox.Show("カレンダーを選択してください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var title = TitleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                System.Windows.MessageBox.Show("タイトルを入力してください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var startDate = StartDatePicker.SelectedDate ?? _defaultDate;
            var endDate = EndDatePicker.SelectedDate ?? startDate;
            var isAllDay = AllDayCheckBox.IsChecked == true;

            DateTime start;
            DateTime end;

            if (isAllDay)
            {
                if (endDate < startDate)
                {
                    System.Windows.MessageBox.Show("終了日は開始日以降にしてください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                start = startDate.Date;
                end = endDate.Date.AddDays(1);
            }
            else
            {
                if (!TryParseTime(StartTimeBox.Text, out var startTime))
                {
                    System.Windows.MessageBox.Show("開始時刻は HH:mm 形式で入力してください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!TryParseTime(EndTimeBox.Text, out var endTime))
                {
                    System.Windows.MessageBox.Show("終了時刻は HH:mm 形式で入力してください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                start = startDate.Date.Add(startTime);
                end = endDate.Date.Add(endTime);
            }

            if (end < start)
            {
                System.Windows.MessageBox.Show("終了日時は開始日時以降にしてください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Item.Title = title;
            Item.StartLocal = start;
            Item.EndLocal = end;
            Item.IsAllDay = isAllDay;
            Item.Location = string.IsNullOrWhiteSpace(LocationBox.Text) ? null : LocationBox.Text.Trim();
            Item.Notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static bool TryParseTime(string input, out TimeSpan time)
        {
            return TimeSpan.TryParseExact(input.Trim(), new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out time);
        }

        private static DateTime GetEndDateForUi(EventItem item)
        {
            if (!item.IsAllDay)
            {
                return item.EndLocal.Date;
            }

            if (item.EndLocal.Date > item.StartLocal.Date)
            {
                return item.EndLocal.Date.AddDays(-1);
            }

            return item.EndLocal.Date;
        }
    }
}
