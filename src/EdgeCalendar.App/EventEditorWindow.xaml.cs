using System;
using System.Globalization;
using System.Windows;
using EdgeCalendar.Core;

namespace EdgeCalendar.App
{
    public partial class EventEditorWindow : Window
    {
        private readonly DateTime _defaultDate;
        private readonly bool _isNew;

        public EventEditorWindow(EventItem? item, DateTime defaultDate)
        {
            InitializeComponent();

            _defaultDate = defaultDate.Date;
            _isNew = item == null;

            Item = item ?? new EventItem
            {
                StartLocal = _defaultDate.AddHours(9),
                EndLocal = _defaultDate.AddHours(10),
                IsAllDay = false,
                Source = "local",
                IsReadOnly = false
            };

            Title = _isNew ? "予定の追加" : "予定の編集";

            TitleBox.Text = Item.Title;
            StartDatePicker.SelectedDate = Item.StartLocal.Date;
            StartTimeBox.Text = Item.StartLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
            EndDatePicker.SelectedDate = Item.EndLocal.Date;
            EndTimeBox.Text = Item.EndLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
            AllDayCheckBox.IsChecked = Item.IsAllDay;
            LocationBox.Text = Item.Location ?? string.Empty;
            NotesBox.Text = Item.Notes ?? string.Empty;

            UpdateAllDayState();
        }

        public EventItem Item { get; }

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
            var title = TitleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("タイトルを入力してください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var startDate = StartDatePicker.SelectedDate ?? _defaultDate;
            var endDate = EndDatePicker.SelectedDate ?? startDate;
            var isAllDay = AllDayCheckBox.IsChecked == true;

            DateTime start;
            DateTime end;

            if (isAllDay)
            {
                start = startDate.Date;
                end = endDate.Date.AddDays(1);
            }
            else
            {
                if (!TryParseTime(StartTimeBox.Text, out var startTime))
                {
                    MessageBox.Show("開始時刻は HH:mm 形式で入力してください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!TryParseTime(EndTimeBox.Text, out var endTime))
                {
                    MessageBox.Show("終了時刻は HH:mm 形式で入力してください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                start = startDate.Date.Add(startTime);
                end = endDate.Date.Add(endTime);
            }

            if (end < start)
            {
                MessageBox.Show("終了日時は開始日時以降にしてください。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
