using System.Text.Json;

namespace EdgeCalendar.Infrastructure
{
    internal static class JsonOptions
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
