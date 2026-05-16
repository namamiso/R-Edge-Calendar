using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace EdgeCalendar.Infrastructure
{
    [SupportedOSPlatform("windows")]
    public sealed class GoogleCredentialStore
    {
        private readonly string _path;

        public GoogleCredentialStore(string? path = null)
        {
            _path = path ?? GetDefaultPath();
        }

        public async Task SaveAsync(GoogleCredentials credentials)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(credentials, JsonOptions.Default);
            var bytes = Encoding.UTF8.GetBytes(json);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_path, protectedBytes).ConfigureAwait(false);
        }

        public async Task<GoogleCredentials?> LoadAsync()
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            try
            {
                var protectedBytes = await File.ReadAllBytesAsync(_path).ConfigureAwait(false);
                var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<GoogleCredentials>(json, JsonOptions.Default);
            }
            catch
            {
                return null;
            }
        }

        private static string GetDefaultPath()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EdgeCalendar");
            return Path.Combine(baseDir, "google_credentials.dat");
        }
    }

    public sealed class GoogleCredentials
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
