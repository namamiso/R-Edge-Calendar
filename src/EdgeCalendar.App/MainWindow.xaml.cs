using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EdgeCalendar.Core;
using EdgeCalendar.Infrastructure;
using Forms = System.Windows.Forms;

namespace EdgeCalendar.App
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const int EdgeMinPx = 1;
        private const int EdgeMaxPx = 6;
        private const int DwellMs = 100;
        private const int HideGraceMs = 250;
        private const int PollNormalMs = 100;
        private const int PollNearEdgeMs = 16;
        private const int HotkeyId = 0xECAD;
        private const string DefaultEventColor = "#0067C0";
        private const int PanelInsetPx = 14;

        private readonly DispatcherTimer _timer;
        private readonly IEventRepository _repository;
        private readonly ICalendarRepository _calendarRepository;
        private readonly GoogleCalendarClient _calendarClient;
        private readonly GoogleCredentialStore _credentialStore;
        private readonly ConflictLogWriter _conflictLog;
        private readonly ObservableCollection<EventListItem> _events = new();
        private readonly ObservableCollection<CalendarDayItem> _calendarDays = new();
        private bool _isShown;
        private bool _allowClose;
        private bool _hotkeyRegistered;
        private DateTime _hideAfter = DateTime.MaxValue;
        private DateTime _edgeEnterAt = DateTime.MaxValue;
        private HwndSource? _source;
        private IntPtr _hwnd;
        private EventListItem? _selectedEvent;
        private DateTime _selectedDate = DateTime.Today;
        private DateTime _visibleMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _lastSyncUtc = DateTime.MinValue;
        private string _monthTitle = string.Empty;
        private string _selectedDateLabel = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _repository = new SqliteEventRepository();
            _calendarRepository = new SqliteCalendarRepository();
            var http = new HttpClient();
            var tokenStore = new TokenStore();
            _credentialStore = new GoogleCredentialStore();
            var auth = new GoogleAuthClient(http, tokenStore, _credentialStore);
            _calendarClient = new GoogleCalendarClient(http, auth);
            _conflictLog = new ConflictLogWriter();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollNormalMs) };
            _timer.Tick += (_, __) => Tick();

            Loaded += async (_, __) =>
            {
                HideInstant();
                await RunSafeAsync(InitializeAsync);
                _timer.Start();
                _ = RunGoogleSafeAsync(EnsureGoogleAuthenticatedAsync);
            };

            Closing += OnClosing;

            Closed += (_, __) =>
            {
                _timer.Stop();
                if (_hotkeyRegistered)
                {
                    UnregisterHotKey(_hwnd, HotkeyId);
                }
                if (_source != null)
                {
                    _source.RemoveHook(WndProc);
                }
            };
        }

        public ObservableCollection<EventListItem> Events => _events;
        public ObservableCollection<CalendarDayItem> CalendarDays => _calendarDays;
        public event PropertyChangedEventHandler? PropertyChanged;
        public string MonthTitle
        {
            get => _monthTitle;
            private set
            {
                _monthTitle = value;
                OnPropertyChanged(nameof(MonthTitle));
            }
        }

        public string SelectedDateLabel
        {
            get => _selectedDateLabel;
            private set
            {
                _selectedDateLabel = value;
                OnPropertyChanged(nameof(SelectedDateLabel));
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source.AddHook(WndProc);

            MakeToolWindowNoActivate();
            _hotkeyRegistered = RegisterHotKey(_hwnd, HotkeyId, MOD_WIN | MOD_ALT, (uint)Forms.Keys.C);
            if (!_hotkeyRegistered)
            {
                (System.Windows.Application.Current as App)?.ShowTrayMessage("Win+Alt+C の登録に失敗しました。");
            }
        }

        public void TogglePanel()
        {
            var wa = GetWorkingAreaForCursor();
            if (_isShown)
            {
                HideWithFade(wa);
            }
            else
            {
                ShowWithFade(wa);
            }
        }

        public void RequestClose()
        {
            _allowClose = true;
            Close();
        }

        private async Task InitializeAsync()
        {
            await _repository.InitializeAsync();
            await _calendarRepository.InitializeAsync();
            await RefreshCalendarDaysAsync();
            await LoadEventsForSelectedDateAsync();
        }

        private async Task LoadEventsForSelectedDateAsync()
        {
            var items = await _repository.GetByDateAsync(_selectedDate);
            var calendarColors = await GetCalendarColorsAsync();

            _events.Clear();
            foreach (var item in items)
            {
                _events.Add(new EventListItem(item, GetEventColor(item, calendarColors)));
            }

            _selectedEvent = null;
            EventsList.SelectedItem = null;
            UpdateButtons();
        }

        private async Task RefreshCalendarDaysAsync()
        {
            var (start, end) = GetCalendarRange();
            var rangeEvents = await _repository.GetByRangeAsync(start, end.AddDays(1));
            var calendarColors = await GetCalendarColorsAsync();
            var eventsByDate = GroupEventsByDate(start, end, rangeEvents, calendarColors);
            UpdateCalendarDays(eventsByDate);
        }

        private void UpdateCalendarDays(IReadOnlyDictionary<DateTime, IReadOnlyList<CalendarEventPreviewItem>> eventsByDate)
        {
            MonthTitle = _visibleMonth.ToString("yyyy年M月", CultureInfo.CurrentCulture);
            SelectedDateLabel = _selectedDate.ToString("yyyy年M月d日 (ddd)", CultureInfo.CurrentCulture);

            _calendarDays.Clear();
            var (start, end) = GetCalendarRange();
            var holidays = new Dictionary<DateTime, string>();
            for (int year = start.Year; year <= end.Year; year++)
            {
                foreach (var holiday in JapanHolidayCalendar.GetHolidays(year))
                {
                    holidays[holiday.Key] = holiday.Value;
                }
            }

            for (int i = 0; i < 42; i++)
            {
                var date = start.AddDays(i);
                holidays.TryGetValue(date.Date, out var holidayName);
                eventsByDate.TryGetValue(date.Date, out var dayEvents);
                _calendarDays.Add(new CalendarDayItem
                {
                    Date = date,
                    DayText = date.Day.ToString(CultureInfo.InvariantCulture),
                    IsCurrentMonth = date.Month == _visibleMonth.Month,
                    IsToday = date.Date == DateTime.Today,
                    IsSelected = date.Date == _selectedDate.Date,
                    IsRedDay = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidayName != null,
                    HolidayName = holidayName,
                    Events = dayEvents ?? Array.Empty<CalendarEventPreviewItem>()
                });
            }
        }

        private (DateTime Start, DateTime End) GetCalendarRange()
        {
            int offset = (int)_visibleMonth.DayOfWeek;
            var start = _visibleMonth.AddDays(-offset);
            return (start, start.AddDays(41));
        }

        private static IReadOnlyDictionary<DateTime, IReadOnlyList<CalendarEventPreviewItem>> GroupEventsByDate(
            DateTime start,
            DateTime end,
            IReadOnlyList<EventItem> events,
            IReadOnlyDictionary<string, string> calendarColors)
        {
            var map = new Dictionary<DateTime, IReadOnlyList<CalendarEventPreviewItem>>();

            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                var dayStart = date;
                var dayEnd = date.AddDays(1);
                var items = events
                    .Where(e => e.StartLocal < dayEnd && e.EndLocal > dayStart)
                    .OrderBy(e => e.IsAllDay ? 0 : 1)
                    .ThenBy(e => e.StartLocal)
                    .Select(e => new CalendarEventPreviewItem(e, FormatCalendarEventTime(e, date), GetEventColor(e, calendarColors)))
                    .ToList();

                if (items.Count > 0)
                {
                    map[date] = items;
                }
            }

            return map;
        }

        private static string FormatCalendarEventTime(EventItem item, DateTime date)
        {
            if (item.IsAllDay)
            {
                return "終日";
            }

            if (item.StartLocal.Date < date.Date)
            {
                return "継続";
            }

            return item.StartLocal.ToString("H:mm", CultureInfo.CurrentCulture);
        }

        private async Task<IReadOnlyDictionary<string, string>> GetCalendarColorsAsync()
        {
            var calendars = await _calendarRepository.GetAllAsync();
            return calendars
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .ToDictionary(
                    c => c.Id,
                    c => NormalizeColor(c.BackgroundColor),
                    StringComparer.Ordinal);
        }

        private static string GetEventColor(EventItem item, IReadOnlyDictionary<string, string> calendarColors)
        {
            if (!string.IsNullOrWhiteSpace(item.CalendarId) &&
                calendarColors.TryGetValue(item.CalendarId, out var color))
            {
                return color;
            }

            return DefaultEventColor;
        }

        private static string NormalizeColor(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return DefaultEventColor;
            }

            var value = color.Trim();
            if (value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit))
            {
                return value;
            }

            if (value.Length == 9 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit))
            {
                return value;
            }

            return DefaultEventColor;
        }

        private void Tick()
        {
            var p = GetCursor();
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y));
            var wa = screen.WorkingArea;

            bool atRightEdge = IsInEdgeZone(p.X, wa);
            bool insideWindow = _isShown && IsCursorInsideWindow(p.X, p.Y);

            UpdateTimerInterval(atRightEdge || insideWindow);

            if (!_isShown)
            {
                if (atRightEdge)
                {
                    if (_edgeEnterAt == DateTime.MaxValue)
                    {
                        _edgeEnterAt = DateTime.UtcNow;
                    }

                    if ((DateTime.UtcNow - _edgeEnterAt).TotalMilliseconds >= DwellMs)
                    {
                        if (!IsFullscreenSuppressed(screen))
                        {
                            ShowWithFade(wa);
                        }
                        else
                        {
                            _edgeEnterAt = DateTime.MaxValue;
                        }
                    }
                }
                else
                {
                    _edgeEnterAt = DateTime.MaxValue;
                }
            }
            else
            {
                if (insideWindow || atRightEdge)
                {
                    _hideAfter = DateTime.MaxValue;
                }
                else
                {
                    if (_hideAfter == DateTime.MaxValue)
                    {
                        _hideAfter = DateTime.UtcNow.AddMilliseconds(HideGraceMs);
                    }

                    if (DateTime.UtcNow >= _hideAfter)
                    {
                        HideWithFade(wa);
                    }
                }
            }
        }

        private void ShowWithFade(System.Drawing.Rectangle wa)
        {
            _isShown = true;
            _hideAfter = DateTime.MaxValue;

            Height = Math.Max(400, wa.Height - (PanelInsetPx * 2));
            Top = wa.Top + PanelInsetPx;

            BeginAnimation(LeftProperty, null);
            Left = wa.Right;
            Show();

            double visibleLeft = wa.Right - Width - PanelInsetPx;
            var duration = TimeSpan.FromMilliseconds(220);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var slide = new DoubleAnimation(wa.Right, visibleLeft, duration)
            {
                EasingFunction = easing
            };
            var fade = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = easing
            };
            BeginAnimation(LeftProperty, slide);
            BeginAnimation(OpacityProperty, fade);

            if (DateTime.UtcNow - _lastSyncUtc > TimeSpan.FromMinutes(10))
            {
                _ = RunGoogleSafeAsync(SyncAsync);
            }
        }

        private void HideWithFade(System.Drawing.Rectangle wa)
        {
            _isShown = false;
            _hideAfter = DateTime.MaxValue;
            _edgeEnterAt = DateTime.MaxValue;

            BeginAnimation(LeftProperty, null);
            var duration = TimeSpan.FromMilliseconds(180);
            var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
            var slide = new DoubleAnimation(Left, wa.Right, duration)
            {
                EasingFunction = easing
            };
            var fade = new DoubleAnimation(Opacity, 0, duration)
            {
                EasingFunction = easing
            };
            slide.Completed += (_, __) =>
            {
                HideInstant();
                Left = wa.Right;
            };
            BeginAnimation(LeftProperty, slide);
            BeginAnimation(OpacityProperty, fade);
        }

        private void HideInstant()
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            Hide();
        }

        private bool IsCursorInsideWindow(int x, int y)
        {
            var left = (int)Left;
            var top = (int)Top;
            var right = left + (int)ActualWidth;
            var bottom = top + (int)ActualHeight;
            return x >= left && x <= right && y >= top && y <= bottom;
        }

        private static System.Drawing.Rectangle GetWorkingAreaForCursor()
        {
            var p = GetCursor();
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y));
            return screen.WorkingArea;
        }

        private static bool IsInEdgeZone(int cursorX, System.Drawing.Rectangle wa)
        {
            int distance = wa.Right - cursorX;
            return distance >= EdgeMinPx && distance <= EdgeMaxPx;
        }

        private void UpdateTimerInterval(bool nearEdge)
        {
            int target = nearEdge ? PollNearEdgeMs : PollNormalMs;
            if ((int)_timer.Interval.TotalMilliseconds != target)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(target);
            }
        }

        private bool IsFullscreenSuppressed(Forms.Screen screen)
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == _hwnd)
            {
                return false;
            }

            if (!GetWindowRect(fg, out var rect))
            {
                return false;
            }

            var bounds = screen.Bounds;
            const int tolerance = 2;

            return rect.Left <= bounds.Left + tolerance &&
                   rect.Top <= bounds.Top + tolerance &&
                   rect.Right >= bounds.Right - tolerance &&
                   rect.Bottom >= bounds.Bottom - tolerance;
        }

        private async void OnCalendarDayClick(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { CommandParameter: DateTime date })
            {
                return;
            }

            _selectedDate = date.Date;
            _visibleMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);

            await RunSafeAsync(RefreshCalendarDaysAsync);
            await RunSafeAsync(LoadEventsForSelectedDateAsync);
        }

        private async void OnPreviousMonthClick(object sender, RoutedEventArgs e)
        {
            _visibleMonth = _visibleMonth.AddMonths(-1);
            _selectedDate = _visibleMonth;
            await RunSafeAsync(RefreshCalendarDaysAsync);
            await RunSafeAsync(LoadEventsForSelectedDateAsync);
        }

        private async void OnNextMonthClick(object sender, RoutedEventArgs e)
        {
            _visibleMonth = _visibleMonth.AddMonths(1);
            _selectedDate = _visibleMonth;
            await RunSafeAsync(RefreshCalendarDaysAsync);
            await RunSafeAsync(LoadEventsForSelectedDateAsync);
        }

        private void OnEventSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _selectedEvent = EventsList.SelectedItem as EventListItem;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool hasSelection = _selectedEvent != null;
            bool isReadOnly = _selectedEvent?.Item.IsReadOnly == true;

            AddButton.IsEnabled = true;
            EditButton.IsEnabled = hasSelection && !isReadOnly;
            DeleteButton.IsEnabled = hasSelection && !isReadOnly;
        }

        private async void OnAddClick(object sender, RoutedEventArgs e)
        {
            var calendars = await GetSelectedCalendarsAsync();
            if (calendars.Count == 0)
            {
                return;
            }

            var editor = new EventEditorWindow(null, _selectedDate, calendars, calendars[0].Id, true) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                var calendarId = editor.SelectedCalendarId;
                if (calendarId == null)
                {
                    return;
                }

                await RunGoogleSafeAsync(() => CreateEventAsync(calendarId, editor.Item));
            }
        }

        private async void OnEditClick(object sender, RoutedEventArgs e)
        {
            if (_selectedEvent == null)
            {
                return;
            }

            IReadOnlyList<CalendarInfo> calendars = Array.Empty<CalendarInfo>();
            if (_selectedEvent.Item.Source == "google")
            {
                calendars = await GetSelectedCalendarsAsync();
                if (calendars.Count == 0)
                {
                    return;
                }
            }

            var editor = new EventEditorWindow(_selectedEvent.Item.Clone(), _selectedEvent.Item.StartLocal, calendars, _selectedEvent.Item.CalendarId, false) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                await RunGoogleSafeAsync(() => UpdateEventAsync(editor.Item));
            }
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (_selectedEvent == null)
            {
                return;
            }

            var result = System.Windows.MessageBox.Show("予定を削除しますか？", "EdgeCalendar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await RunGoogleSafeAsync(() => DeleteEventAsync(_selectedEvent.Item));
            }
        }

        private async Task CreateEventAsync(string calendarId, EventItem item)
        {
            var created = await _calendarClient.CreateEventAsync(calendarId, item);

            created.Source = "google";
            created.CalendarId = calendarId;
            created.IsReadOnly = false;

            await _repository.CreateAsync(created);
            await RefreshCalendarDaysAsync();
            await LoadEventsForSelectedDateAsync();
        }

        private async Task UpdateEventAsync(EventItem item)
        {
            if (item.Source == "google")
            {
                if (string.IsNullOrEmpty(item.CalendarId) || string.IsNullOrEmpty(item.ExternalId))
                {
                    throw new InvalidOperationException("Googleイベントの識別子がありません。");
                }

                try
                {
                    var updated = await _calendarClient.UpdateEventAsync(item.CalendarId, item.ExternalId, item);
                    item.ETag = updated.ETag;
                }
                catch (ConflictException ex)
                {
                    await _conflictLog.SaveDraftAsync("update", item, ex.ServerJson);
                    await RunSafeAsync(SyncAsync);
                    throw new InvalidOperationException("競合が発生したため最新データを取得しました。");
                }
            }

            await _repository.UpdateAsync(item);
            await RefreshCalendarDaysAsync();
            await LoadEventsForSelectedDateAsync();
        }

        private async Task DeleteEventAsync(EventItem item)
        {
            if (item.Source == "google")
            {
                if (string.IsNullOrEmpty(item.CalendarId) || string.IsNullOrEmpty(item.ExternalId))
                {
                    throw new InvalidOperationException("Googleイベントの識別子がありません。");
                }

                try
                {
                    await _calendarClient.DeleteEventAsync(item.CalendarId, item.ExternalId, item.ETag);
                }
                catch (ConflictException ex)
                {
                    await _conflictLog.SaveDraftAsync("delete", item, ex.ServerJson);
                    await RunSafeAsync(SyncAsync);
                    throw new InvalidOperationException("競合が発生したため最新データを取得しました。");
                }
            }

            await _repository.DeleteAsync(item.Id);
            await RefreshCalendarDaysAsync();
            await LoadEventsForSelectedDateAsync();
        }

        private async void OnSyncClick(object sender, RoutedEventArgs e)
        {
            await RunGoogleSafeAsync(SyncAsync);
        }

        private async void OnCalendarsClick(object sender, RoutedEventArgs e)
        {
            await RunGoogleSafeAsync(ShowCalendarSelectionAsync);
        }

        private async Task ShowCalendarSelectionAsync()
        {
            var calendars = await EnsureCalendarsAsync();
            if (calendars.Count == 0)
            {
                System.Windows.MessageBox.Show("利用可能なカレンダーがありません。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new CalendarSelectWindow(calendars) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await _calendarRepository.UpdateSelectionAsync(dialog.SelectedCalendars);
            }
        }

        private async Task<List<CalendarInfo>> GetSelectedCalendarsAsync()
        {
            var calendars = (await _calendarRepository.GetAllAsync()).ToList();
            if (calendars.Count == 0)
            {
                calendars = await EnsureCalendarsAsync();
            }

            var selected = calendars.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                System.Windows.MessageBox.Show("同期するカレンダーが選択されていません。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return new List<CalendarInfo>();
            }

            return selected;
        }

        private async Task<List<CalendarInfo>> EnsureCalendarsAsync()
        {
            var existing = (await _calendarRepository.GetAllAsync()).ToList();
            if (existing.Count > 0)
            {
                return existing;
            }

            var calendars = (await _calendarClient.GetCalendarsAsync()).ToList();
            if (calendars.Count == 0)
            {
                return calendars;
            }

            foreach (var cal in calendars)
            {
                if (!cal.IsSelected)
                {
                    cal.IsSelected = true;
                }
            }

            await _calendarRepository.UpsertAsync(calendars);
            return calendars;
        }

        private async Task SyncAsync()
        {
            var windowStart = DateTime.Today.AddDays(-31);
            var windowEnd = DateTime.Today.AddDays(32);
            await SyncRangeAsync(windowStart, windowEnd);
        }

        private async Task EnsureGoogleAuthenticatedAsync()
        {
            await _calendarClient.EnsureAuthenticatedAsync();
        }

        private async Task SyncRangeAsync(DateTime windowStart, DateTime windowEnd)
        {
            var calendars = (await _calendarRepository.GetAllAsync()).ToList();
            if (calendars.Count == 0)
            {
                calendars = await EnsureCalendarsAsync();
            }

            var selected = calendars.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                System.Windows.MessageBox.Show("同期するカレンダーが選択されていません。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var calendar in selected)
            {
                var events = await _calendarClient.GetEventsAsync(calendar.Id, windowStart, windowEnd);
                await _repository.ReplaceExternalEventsAsync(calendar.Id, windowStart, windowEnd, events);
            }

            _lastSyncUtc = DateTime.UtcNow;
            await RefreshCalendarDaysAsync();
            await LoadEventsForSelectedDateAsync();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void MakeToolWindowNoActivate()
        {
            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;
            _isShown = false;
            _edgeEnterAt = DateTime.MaxValue;
            _hideAfter = DateTime.MaxValue;
            HideInstant();
        }

        private async Task RunSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"処理中にエラーが発生しました。{Environment.NewLine}{ex.Message}", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunGoogleSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (GoogleCredentialsMissingException)
            {
                if (!await PromptAndSaveGoogleCredentialsAsync())
                {
                    return;
                }

                await RunGoogleSafeAsync(action);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"処理中にエラーが発生しました。{Environment.NewLine}{ex.Message}", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<bool> PromptAndSaveGoogleCredentialsAsync()
        {
            var dialog = new GoogleCredentialsWindow { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            await _credentialStore.SaveAsync(dialog.Credentials);
            return true;
        }

        private static POINT GetCursor()
        {
            GetCursorPos(out var p);
            return p;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_WIN = 0x0008;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                TogglePanel();
                handled = true;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        public sealed class EventListItem
        {
            public EventListItem(EventItem item, string colorHex)
            {
                Item = item;
                Title = item.Title;
                TimeLabel = item.IsAllDay
                    ? "終日"
                    : string.Format(CultureInfo.InvariantCulture, "{0:HH:mm}-{1:HH:mm}", item.StartLocal, item.EndLocal);
                ColorHex = colorHex;
                IsReadOnly = item.IsReadOnly;
            }

            public EventItem Item { get; }
            public string Title { get; }
            public string TimeLabel { get; }
            public string ColorHex { get; }
            public bool IsReadOnly { get; }
        }
    }
}
