using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EdgeCalendar.Core;

namespace EdgeCalendar.App
{
    public partial class CalendarSelectWindow : Window
    {
        private readonly ObservableCollection<CalendarInfo> _calendars;

        public CalendarSelectWindow(IReadOnlyList<CalendarInfo> calendars)
        {
            InitializeComponent();
            _calendars = new ObservableCollection<CalendarInfo>(calendars.Select(c => new CalendarInfo
            {
                Id = c.Id,
                Summary = c.Summary,
                BackgroundColor = c.BackgroundColor,
                IsSelected = c.IsSelected
            }));
            CalendarList.ItemsSource = _calendars;
        }

        public IReadOnlyList<CalendarInfo> SelectedCalendars => _calendars.ToList();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
