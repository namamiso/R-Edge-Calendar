using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using EdgeCalendar.Core;
using Microsoft.Data.Sqlite;

namespace EdgeCalendar.Infrastructure
{
    public sealed class SqliteEventRepository : IEventRepository
    {
        private readonly string _dbPath;

        public SqliteEventRepository(string? dbPath = null)
        {
            _dbPath = dbPath ?? GetDefaultDbPath();
        }

        public async Task InitializeAsync()
        {
            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Events (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    StartLocal TEXT NOT NULL,
    EndLocal TEXT NOT NULL,
    IsAllDay INTEGER NOT NULL,
    Location TEXT NULL,
    Notes TEXT NULL,
    UpdatedAtLocal TEXT NOT NULL,
    Source TEXT NOT NULL DEFAULT 'local',
    ExternalId TEXT NULL,
    CalendarId TEXT NULL,
    IsReadOnly INTEGER NOT NULL DEFAULT 0,
    ETag TEXT NULL
);
";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            await EnsureColumnAsync(conn, "Events", "Source", "TEXT NOT NULL DEFAULT 'local'");
            await EnsureColumnAsync(conn, "Events", "ExternalId", "TEXT NULL");
            await EnsureColumnAsync(conn, "Events", "CalendarId", "TEXT NULL");
            await EnsureColumnAsync(conn, "Events", "IsReadOnly", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync(conn, "Events", "ETag", "TEXT NULL");

            var indexCmd = conn.CreateCommand();
            indexCmd.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_Events_External
ON Events (Source, ExternalId, CalendarId);
";
            await indexCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<EventItem>> GetByDateAsync(DateTime dateLocal)
        {
            var dayStart = dateLocal.Date;
            var dayEnd = dayStart.AddDays(1);
            return await GetByRangeAsync(dayStart, dayEnd).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<EventItem>> GetByRangeAsync(DateTime startLocal, DateTime endLocal)
        {
            var results = new List<EventItem>();

            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Title, StartLocal, EndLocal, IsAllDay, Location, Notes, UpdatedAtLocal,
       Source, ExternalId, CalendarId, IsReadOnly, ETag
FROM Events
WHERE StartLocal < $next AND EndLocal > $start
ORDER BY StartLocal;
";
            cmd.Parameters.AddWithValue("$start", startLocal.Date.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$next", endLocal.Date.ToString("o", CultureInfo.InvariantCulture));

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(ReadEvent(reader));
            }

            return results;
        }

        public async Task<long> CreateAsync(EventItem item)
        {
            item.UpdatedAtLocal = DateTime.Now;
            if (string.IsNullOrWhiteSpace(item.Source))
            {
                item.Source = "local";
            }

            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Events (Title, StartLocal, EndLocal, IsAllDay, Location, Notes, UpdatedAtLocal, Source, ExternalId, CalendarId, IsReadOnly, ETag)
VALUES ($title, $start, $end, $allDay, $location, $notes, $updated, $source, $externalId, $calendarId, $readOnly, $etag);
SELECT last_insert_rowid();
";
            BindEvent(cmd, item);

            var id = (long)(await cmd.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
            item.Id = id;
            return id;
        }

        public async Task UpdateAsync(EventItem item)
        {
            item.UpdatedAtLocal = DateTime.Now;

            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
UPDATE Events
SET Title = $title,
    StartLocal = $start,
    EndLocal = $end,
    IsAllDay = $allDay,
    Location = $location,
    Notes = $notes,
    UpdatedAtLocal = $updated,
    Source = $source,
    ExternalId = $externalId,
    CalendarId = $calendarId,
    IsReadOnly = $readOnly,
    ETag = $etag
WHERE Id = $id;
";
            BindEvent(cmd, item);
            cmd.Parameters.AddWithValue("$id", item.Id);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task DeleteAsync(long id)
        {
            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Events WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task ReplaceExternalEventsAsync(string calendarId, DateTime windowStart, DateTime windowEnd, IReadOnlyList<EventItem> items)
        {
            var writable = new HashSet<string>(StringComparer.Ordinal);

            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var existingCmd = conn.CreateCommand();
            existingCmd.CommandText = @"
SELECT ExternalId
FROM Events
WHERE Source = 'google'
  AND CalendarId = $calendarId
  AND IsReadOnly = 0
  AND StartLocal < $end
  AND EndLocal > $start
  AND ExternalId IS NOT NULL;
";
            existingCmd.Parameters.AddWithValue("$calendarId", calendarId);
            existingCmd.Parameters.AddWithValue("$start", windowStart.ToString("o", CultureInfo.InvariantCulture));
            existingCmd.Parameters.AddWithValue("$end", windowEnd.ToString("o", CultureInfo.InvariantCulture));

            await using (var reader = await existingCmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    writable.Add(reader.GetString(0));
                }
            }

            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync().ConfigureAwait(false);

            var deleteCmd = conn.CreateCommand();
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = @"
DELETE FROM Events
WHERE Source = 'google'
  AND CalendarId = $calendarId
  AND StartLocal < $end
  AND EndLocal > $start;
";
            deleteCmd.Parameters.AddWithValue("$calendarId", calendarId);
            deleteCmd.Parameters.AddWithValue("$start", windowStart.ToString("o", CultureInfo.InvariantCulture));
            deleteCmd.Parameters.AddWithValue("$end", windowEnd.ToString("o", CultureInfo.InvariantCulture));
            await deleteCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            foreach (var item in items)
            {
                item.Source = "google";
                if (!string.IsNullOrEmpty(item.ExternalId) && writable.Contains(item.ExternalId))
                {
                    item.IsReadOnly = false;
                }

                var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
INSERT INTO Events (Title, StartLocal, EndLocal, IsAllDay, Location, Notes, UpdatedAtLocal, Source, ExternalId, CalendarId, IsReadOnly, ETag)
VALUES ($title, $start, $end, $allDay, $location, $notes, $updated, $source, $externalId, $calendarId, $readOnly, $etag);
";
                BindEvent(insertCmd, item);
                await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
        }

        private static void BindEvent(SqliteCommand cmd, EventItem item)
        {
            cmd.Parameters.AddWithValue("$title", item.Title);
            cmd.Parameters.AddWithValue("$start", item.StartLocal.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$end", item.EndLocal.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$allDay", item.IsAllDay ? 1 : 0);
            cmd.Parameters.AddWithValue("$location", (object?)item.Location ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$notes", (object?)item.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$updated", item.UpdatedAtLocal.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$source", item.Source);
            cmd.Parameters.AddWithValue("$externalId", (object?)item.ExternalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$calendarId", (object?)item.CalendarId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$readOnly", item.IsReadOnly ? 1 : 0);
            cmd.Parameters.AddWithValue("$etag", (object?)item.ETag ?? DBNull.Value);
        }

        private static EventItem ReadEvent(SqliteDataReader reader)
        {
            return new EventItem
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                StartLocal = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                EndLocal = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                IsAllDay = reader.GetInt64(4) != 0,
                Location = reader.IsDBNull(5) ? null : reader.GetString(5),
                Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                UpdatedAtLocal = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Source = reader.IsDBNull(8) ? "local" : reader.GetString(8),
                ExternalId = reader.IsDBNull(9) ? null : reader.GetString(9),
                CalendarId = reader.IsDBNull(10) ? null : reader.GetString(10),
                IsReadOnly = !reader.IsDBNull(11) && reader.GetInt64(11) != 0,
                ETag = reader.IsDBNull(12) ? null : reader.GetString(12)
            };
        }

        private static async Task EnsureColumnAsync(SqliteConnection conn, string table, string column, string definition)
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = $"PRAGMA table_info({table});";

            await using var reader = await checkCmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            await alterCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static string GetDefaultDbPath()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EdgeCalendar");
            return Path.Combine(baseDir, "edgecalendar.db");
        }

        private SqliteConnection CreateConnection()
        {
            return SqliteConnectionFactory.Create(_dbPath);
        }

        private async Task OpenAsync(SqliteConnection conn)
        {
            try
            {
                await conn.OpenAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
            {
                throw SqliteConnectionFactory.CreateOpenException(_dbPath, ex);
            }
        }
    }
}
