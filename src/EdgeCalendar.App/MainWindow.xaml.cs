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
    public partial class MainWindow : Window
    {
        private const int EdgeMinPx = 2;
        private const int EdgeMaxPx = 6;
        private const int DwellMs = 100;
        private const int HideGraceMs = 250;
        private const int PollNormalMs = 100;
        private const int PollNearEdgeMs = 16;
        private const int HotkeyId = 0xECAD;

        private readonly DispatcherTimer _timer;
        private readonly IEventRepository _repository;
        private readonly ICalendarRepository _calendarRepository;
        private readonly GoogleCalendarClient _calendarClient;
        private readonly ObservableCollection<EventListItem> _events = new();
        private bool _isShown;
        private bool _allowClose;
        private bool _hotkeyRegistered;
        private DateTime _hideAfter = DateTime.MaxValue;
        private DateTime _edgeEnterAt = DateTime.MaxValue;
        private HwndSource? _source;
        private IntPtr _hwnd;
        private EventListItem? _selectedEvent;
        private DateTime _lastSyncUtc = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _repository = new SqliteEventRepository();
            _calendarRepository = new SqliteCalendarRepository();
            var http = new HttpClient();
            var tokenStore = new TokenStore();
            var auth = new GoogleAuthClient(http, tokenStore);
            _calendarClient = new GoogleCalendarClient(http, auth);

            Loaded += async (_, __) =>
            {
                HideInstant();
                CalendarControl.SelectedDate = DateTime.Today;
                await RunSafeAsync(InitializeAsync);
                _timer.Start();
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

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollNormalMs) };
            _timer.Tick += (_, __) => Tick();
        }

        public ObservableCollection<EventListItem> Events => _events;

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
                (Application.Current as App)?.ShowTrayMessage("Win+Alt+C の登録に失敗しました。");
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
            await LoadEventsForSelectedDateAsync();
        }

        private async Task LoadEventsForSelectedDateAsync()
        {
            var date = CalendarControl.SelectedDate ?? DateTime.Today;
            var items = await _repository.GetByDateAsync(date);

            _events.Clear();
            foreach (var item in items)
            {
                _events.Add(new EventListItem(item));
            }

            _selectedEvent = null;
            EventsList.SelectedItem = null;
            UpdateButtons();
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

            Height = wa.Height;
            Top = wa.Top;

            Left = wa.Right;
            Show();
            Left = wa.Right - Width;

            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, anim);

            if (DateTime.UtcNow - _lastSyncUtc > TimeSpan.FromMinutes(10))
            {
                _ = RunSafeAsync(SyncAsync);
            }
        }

        private void HideWithFade(System.Drawing.Rectangle wa)
        {
            _isShown = false;
            _hideAfter = DateTime.MaxValue;
            _edgeEnterAt = DateTime.MaxValue;

            var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            anim.Completed += (_, __) =>
            {
                HideInstant();
                Left = wa.Right;
            };
            BeginAnimation(OpacityProperty, anim);
        }

        private void HideInstant()
        {
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

        private async void OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
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

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var date = CalendarControl.SelectedDate ?? DateTime.Today;
            var editor = new EventEditorWindow(null, date) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                _ = RunSafeAsync(() => CreateEventAsync(editor.Item));
            }
        }

        private void OnEditClick(object sender, RoutedEventArgs e)
        {
            if (_selectedEvent == null)
            {
                return;
            }

            var editor = new EventEditorWindow(_selectedEvent.Item.Clone(), _selectedEvent.Item.StartLocal) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                _ = RunSafeAsync(() => UpdateEventAsync(editor.Item));
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (_selectedEvent == null)
            {
                return;
            }

            var result = MessageBox.Show("予定を削除しますか？", "EdgeCalendar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _ = RunSafeAsync(() => DeleteEventAsync(_selectedEvent.Item.Id));
            }
        }

        private async Task CreateEventAsync(EventItem item)
        {
            item.Source = "local";
            item.IsReadOnly = false;
            await _repository.CreateAsync(item);
            await LoadEventsForSelectedDateAsync();
        }

        private async Task UpdateEventAsync(EventItem item)
        {
            await _repository.UpdateAsync(item);
            await LoadEventsForSelectedDateAsync();
        }

        private async Task DeleteEventAsync(long id)
        {
            await _repository.DeleteAsync(id);
            await LoadEventsForSelectedDateAsync();
        }

        private async void OnSyncClick(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(SyncAsync);
        }

        private async void OnCalendarsClick(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(ShowCalendarSelectionAsync);
        }

        private async Task ShowCalendarSelectionAsync()
        {
            var calendars = await EnsureCalendarsAsync();
            if (calendars.Count == 0)
            {
                MessageBox.Show("利用可能なカレンダーがありません。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new CalendarSelectWindow(calendars) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await _calendarRepository.UpdateSelectionAsync(dialog.SelectedCalendars);
            }
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
            var calendars = (await _calendarRepository.GetAllAsync()).ToList();
            if (calendars.Count == 0)
            {
                calendars = await EnsureCalendarsAsync();
            }

            var selected = calendars.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("同期するカレンダーが選択されていません。", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var windowStart = DateTime.Today.AddDays(-31);
            var windowEnd = DateTime.Today.AddDays(32);

            foreach (var calendar in selected)
            {
                var events = await _calendarClient.GetEventsAsync(calendar.Id, windowStart, windowEnd);
                await _repository.ReplaceExternalEventsAsync(calendar.Id, windowStart, windowEnd, events);
            }

            _lastSyncUtc = DateTime.UtcNow;
            await LoadEventsForSelectedDateAsync();
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
                MessageBox.Show($"処理中にエラーが発生しました。{Environment.NewLine}{ex.Message}", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            public EventListItem(EventItem item)
            {
                Item = item;
                Title = item.Title;
                TimeLabel = item.IsAllDay
                    ? "終日"
                    : string.Format(CultureInfo.InvariantCulture, "{0:HH:mm}-{1:HH:mm}", item.StartLocal, item.EndLocal);
                IsReadOnly = item.IsReadOnly;
            }

            public EventItem Item { get; }
            public string Title { get; }
            public string TimeLabel { get; }
            public bool IsReadOnly { get; }
        }
    }
}
