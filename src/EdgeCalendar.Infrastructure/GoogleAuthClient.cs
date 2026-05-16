using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Runtime.Versioning;

namespace EdgeCalendar.Infrastructure
{
    [SupportedOSPlatform("windows")]
    public sealed class GoogleAuthClient
    {
        private readonly HttpClient _http;
        private readonly TokenStore _store;
        private readonly GoogleCredentialStore _credentialStore;

        public GoogleAuthClient(HttpClient http, TokenStore store, GoogleCredentialStore credentialStore)
        {
            _http = http;
            _store = store;
            _credentialStore = credentialStore;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            var credentials = await GetCredentialsAsync().ConfigureAwait(false);

            var token = await _store.LoadAsync().ConfigureAwait(false);
            if (token == null)
            {
                token = await AuthorizeAsync(credentials).ConfigureAwait(false);
                await _store.SaveAsync(token).ConfigureAwait(false);
                return token.AccessToken;
            }

            if (token.HasUsableAccessToken && token.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return token.AccessToken;
            }

            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                token = await RefreshAsync(token.RefreshToken, credentials).ConfigureAwait(false);
                await _store.SaveAsync(token).ConfigureAwait(false);
                return token.AccessToken;
            }

            token = await AuthorizeAsync(credentials).ConfigureAwait(false);
            await _store.SaveAsync(token).ConfigureAwait(false);
            return token.AccessToken;
        }

        private async Task<GoogleCredentials> GetCredentialsAsync()
        {
            var envCredentials = new GoogleCredentials
            {
                ClientId = Environment.GetEnvironmentVariable("EDGE_CALENDAR_GOOGLE_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("EDGE_CALENDAR_GOOGLE_CLIENT_SECRET") ?? string.Empty
            };
            if (envCredentials.IsComplete)
            {
                return envCredentials;
            }

            var storedCredentials = await _credentialStore.LoadAsync().ConfigureAwait(false);
            if (storedCredentials?.IsComplete == true)
            {
                return storedCredentials;
            }

            throw new GoogleCredentialsMissingException();
        }

        private async Task<OAuthToken> AuthorizeAsync(GoogleCredentials credentials)
        {
            var codeVerifier = CreateCodeVerifier();
            var codeChallenge = CreateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString("N");
            int port = GetFreePort();
            string redirectUri = $"http://127.0.0.1:{port}/callback/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            var authUrl = new StringBuilder("https://accounts.google.com/o/oauth2/v2/auth");
            authUrl.Append("?response_type=code");
            authUrl.Append("&client_id=").Append(Uri.EscapeDataString(credentials.ClientId));
            authUrl.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
            authUrl.Append("&scope=").Append(Uri.EscapeDataString("https://www.googleapis.com/auth/calendar"));
            authUrl.Append("&access_type=offline&prompt=consent");
            authUrl.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
            authUrl.Append("&code_challenge_method=S256");
            authUrl.Append("&state=").Append(Uri.EscapeDataString(state));

            Process.Start(new ProcessStartInfo(authUrl.ToString()) { UseShellExecute = true });

            var contextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
            var completed = await Task.WhenAny(contextTask, timeoutTask).ConfigureAwait(false);
            if (completed != contextTask)
            {
                listener.Stop();
                throw new InvalidOperationException("認証がタイムアウトしました。もう一度やり直してください。");
            }
            var context = await contextTask.ConfigureAwait(false);
            var query = context.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];
            var error = query["error"];

            var responseHtml = "<html><body>認証が完了しました。ウィンドウを閉じてください。</body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentEncoding = Encoding.UTF8;
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
                client_id = credentials.ClientId,
                client_secret = credentials.ClientSecret,
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
            if (string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException("アクセストークンが取得できませんでした。Google認証をやり直してください。");
            }

            return new OAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            };
        }

        private async Task<OAuthToken> RefreshAsync(string refreshToken, GoogleCredentials credentials)
        {
            var payload = new
            {
                client_id = credentials.ClientId,
                client_secret = credentials.ClientSecret,
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
            if (string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException("アクセストークンの更新に失敗しました。Google認証をやり直してください。");
            }

            return new OAuthToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = string.IsNullOrWhiteSpace(token.RefreshToken) ? refreshToken : token.RefreshToken,
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

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private sealed class OAuthTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
}
