using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EdgeCalendar.Core;

namespace EdgeCalendar.Infrastructure
{
    public sealed class ConflictLogWriter
    {
        private readonly string _dir;

        public ConflictLogWriter(string? dir = null)
        {
            _dir = dir ?? GetDefaultDir();
        }

        public async Task SaveDraftAsync(string action, EventItem localDraft, string? serverJson)
        {
            Directory.CreateDirectory(_dir);

            var record = new ConflictRecord
            {
                Action = action,
                OccurredAtUtc = DateTime.UtcNow,
                LocalDraft = localDraft,
                ServerJson = serverJson
            };

            var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{action}_{localDraft.ExternalId ?? localDraft.Id.ToString()}.json";
            var path = Path.Combine(_dir, fileName);

            var json = JsonSerializer.Serialize(record, JsonOptions.Default);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }

        private static string GetDefaultDir()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EdgeCalendar",
                "ConflictLog");
            return baseDir;
        }

        private sealed class ConflictRecord
        {
            public string Action { get; set; } = string.Empty;
            public DateTime OccurredAtUtc { get; set; }
            public EventItem LocalDraft { get; set; } = new();
            public string? ServerJson { get; set; }
        }
    }
}
