using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EdgeCalendar.Infrastructure
{
    public sealed class GoogleAuthClient
    {
        private readonly HttpClient _http;
        private readonly TokenStore _store;
        private readonly string _clientId;
        private readonly string _clientSecret;

        public GoogleAuthClient(HttpClient http, TokenStore store)
        {
            _http = http;
            _store = store;
            _clientId = Environment.GetEnvironmentVariable("EDGE_CALENDAR_GOOGLE_CLIENT_ID") ?? string.Empty;
            _clientSecret = Environment.GetEnvironmentVariable("EDGE_CALENDAR_GOOGLE_CLIENT_SECRET") ?? string.Empty;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
            {
                throw new InvalidOperationException("Google APIのクライアントID/シークレットが未設定です。環境変数を設定してください。");
            }

            var token = await _store.LoadAsync().ConfigureAwait(false);
            if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                token = await AuthorizeAsync().ConfigureAwait(false);
                await _store.SaveAsync(token).ConfigureAwait(false);
                return token.AccessToken;
            }

            if (token.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(2))
            {
                token = await RefreshAsync(token.RefreshToken).ConfigureAwait(false);
                await _store.SaveAsync(token).ConfigureAwait(false);
            }

            return token.AccessToken;
        }

        private async Task<OAuthToken> AuthorizeAsync()
        {
            var codeVerifier = CreateCodeVerifier();
            var codeChallenge = CreateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString("N");
            int port = 32189;
            string redirectUri = $"http://127.0.0.1:{port}/callback/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            var authUrl = new StringBuilder("https://accounts.google.com/o/oauth2/v2/auth");
            authUrl.Append("?response_type=code");
            authUrl.Append("&client_id=").Append(Uri.EscapeDataString(_clientId));
            authUrl.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
            authUrl.Append("&scope=").Append(Uri.EscapeDataString("https://www.googleapis.com/auth/calendar.readonly"));
            authUrl.Append("&access_type=offline&prompt=consent");
            authUrl.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
            authUrl.Append("&code_challenge_method=S256");
            authUrl.Append("&state=").Append(Uri.EscapeDataString(state));

            Process.Start(new ProcessStartInfo(authUrl.ToString()) { UseShellExecute = true });

            var context = await listener.GetContextAsync().ConfigureAwait(false);
            var query = context.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];
            var error = query["error"];

            var responseHtml = "<html><body>認証が完了しました。ウィンドウを閉じてください。</body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer).ConfigureAwait(false);
            context.Response.OutputStream.Close();

            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"認証エラー: {error}");
            }

            if (string.IsNullOrEmpty(code) || !string.Equals(state, returnedState, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("認証コードの取得に失敗しました。");
            }

            var payload = new
            {
                code,
                client_id = _clientId,
                client_secret = _clientSecret,
                redirect_uri = redirectUri,
                code_verifier = codeVerifier,
                grant_type = "authorization_code"
            };

            var tokenResponse = await _http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"))
                .ConfigureAwait(false);

            tokenResponse.EnsureSuccessStatusCode();
            var json = await tokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var token = JsonSerializer.Deserialize<OAuthTokenResponse>(json, JsonOptions.Default)
                        ?? throw new InvalidOperationException("トークン応答の解析に失敗しました。");

            return new OAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            };
        }

        private async Task<OAuthToken> RefreshAsync(string refreshToken)
        {
            var payload = new
            {
                client_id = _clientId,
                client_secret = _clientSecret,
                refresh_token = refreshToken,
                grant_type = "refresh_token"
            };

            var response = await _http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"))
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var token = JsonSerializer.Deserialize<OAuthTokenResponse>(json, JsonOptions.Default)
                        ?? throw new InvalidOperationException("トークン応答の解析に失敗しました。");

            return new OAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = refreshToken,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            };
        }

        private static string CreateCodeVerifier()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string CreateCodeChallenge(string verifier)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
            return Base64UrlEncode(bytes);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private sealed class OAuthTokenResponse
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public int ExpiresIn { get; set; }
        }
    }
}
