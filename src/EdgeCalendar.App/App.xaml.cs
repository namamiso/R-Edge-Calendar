using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace EdgeCalendar.App
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var win = new MainWindow();
            MainWindow = win;

            win.Show();
            win.Hide();

            _tray = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "EdgeCalendar"
            };

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("表示/非表示", null, (_, __) => win.TogglePanel());
            menu.Items.Add("終了", null, (_, __) =>
            {
                win.RequestClose();
                Shutdown();
            });

            _tray.ContextMenuStrip = menu;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }

            base.OnExit(e);
        }

        internal void ShowTrayMessage(string message)
        {
            _tray?.ShowBalloonTip(3000, "EdgeCalendar", message, Forms.ToolTipIcon.Warning);
        }
    }
}
