using System;
using System.Linq;
using System.Reflection;

namespace EdgeCalendar.Infrastructure
{
    public static class GoogleOAuthClientProvider
    {
        private const string ClientIdMetadataKey = "GoogleOAuthClientId";
        private const string ClientSecretMetadataKey = "GoogleOAuthClientSecret";
        private const string ClientIdEnvironmentVariable = "EDGE_CALENDAR_GOOGLE_CLIENT_ID";
        private const string ClientSecretEnvironmentVariable = "EDGE_CALENDAR_GOOGLE_CLIENT_SECRET";

        public static GoogleCredentials? GetConfiguredCredentials()
        {
            var envClientId = Environment.GetEnvironmentVariable(ClientIdEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(envClientId))
            {
                return new GoogleCredentials
                {
                    ClientId = envClientId.Trim(),
                    ClientSecret = Environment.GetEnvironmentVariable(ClientSecretEnvironmentVariable)?.Trim() ?? string.Empty
                };
            }

            var metadataClientId = Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => string.Equals(a.Key, ClientIdMetadataKey, StringComparison.Ordinal))
                ?.Value;

            if (!string.IsNullOrWhiteSpace(metadataClientId))
            {
                var metadataClientSecret = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => string.Equals(a.Key, ClientSecretMetadataKey, StringComparison.Ordinal))
                    ?.Value;

                return new GoogleCredentials
                {
                    ClientId = metadataClientId.Trim(),
                    ClientSecret = metadataClientSecret?.Trim() ?? string.Empty
                };
            }

            return null;
        }
    }
}
