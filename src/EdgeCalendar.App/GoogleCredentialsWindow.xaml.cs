using System.Windows;
using EdgeCalendar.Infrastructure;

namespace EdgeCalendar.App
{
    public partial class GoogleCredentialsWindow : Window
    {
        public GoogleCredentialsWindow()
        {
            InitializeComponent();
        }

        public GoogleCredentials Credentials { get; private set; } = new();

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var credentials = new GoogleCredentials
            {
                ClientId = ClientIdBox.Text.Trim()
            };

            if (!credentials.IsComplete)
            {
                System.Windows.MessageBox.Show("Enter a Client ID.", "EdgeCalendar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Credentials = credentials;
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
