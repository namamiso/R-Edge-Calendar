using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EdgeCalendar.Infrastructure
{
    public sealed class TokenStore
    {
        private readonly string _path;

        public TokenStore(string? path = null)
        {
            _path = path ?? GetDefaultPath();
        }

        public async Task SaveAsync(OAuthToken token)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(token, JsonOptions.Default);
            var bytes = Encoding.UTF8.GetBytes(json);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_path, protectedBytes).ConfigureAwait(false);
        }

        public async Task<OAuthToken?> LoadAsync()
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var protectedBytes = await File.ReadAllBytesAsync(_path).ConfigureAwait(false);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<OAuthToken>(json, JsonOptions.Default);
        }

        private static string GetDefaultPath()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EdgeCalendar");
            return Path.Combine(baseDir, "google_tokens.dat");
        }
    }

    public sealed class OAuthToken
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
