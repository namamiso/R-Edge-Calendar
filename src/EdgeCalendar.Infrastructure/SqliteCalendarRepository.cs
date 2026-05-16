using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using EdgeCalendar.Core;
using Microsoft.Data.Sqlite;

namespace EdgeCalendar.Infrastructure
{
    public sealed class SqliteCalendarRepository : ICalendarRepository
    {
        private readonly string _dbPath;

        public SqliteCalendarRepository(string? dbPath = null)
        {
            _dbPath = dbPath ?? GetDefaultDbPath();
        }

        public async Task InitializeAsync()
        {
            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Calendars (
    Id TEXT PRIMARY KEY,
    Summary TEXT NOT NULL,
    BackgroundColor TEXT NULL,
    IsSelected INTEGER NOT NULL
);
";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CalendarInfo>> GetAllAsync()
        {
            var results = new List<CalendarInfo>();

            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Summary, BackgroundColor, IsSelected FROM Calendars ORDER BY Summary;";

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new CalendarInfo
                {
                    Id = reader.GetString(0),
                    Summary = reader.GetString(1),
                    BackgroundColor = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IsSelected = reader.GetInt64(3) != 0
                });
            }

            return results;
        }

        public async Task UpsertAsync(IReadOnlyList<CalendarInfo> calendars)
        {
            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync().ConfigureAwait(false);

            foreach (var calendar in calendars)
            {
                var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Calendars (Id, Summary, BackgroundColor, IsSelected)
VALUES ($id, $summary, $color, $selected)
ON CONFLICT(Id) DO UPDATE SET
    Summary = excluded.Summary,
    BackgroundColor = excluded.BackgroundColor;
";
                cmd.Parameters.AddWithValue("$id", calendar.Id);
                cmd.Parameters.AddWithValue("$summary", calendar.Summary);
                cmd.Parameters.AddWithValue("$color", (object?)calendar.BackgroundColor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$selected", calendar.IsSelected ? 1 : 0);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
        }

        public async Task UpdateSelectionAsync(IReadOnlyList<CalendarInfo> calendars)
        {
            await using var conn = CreateConnection();
            await OpenAsync(conn).ConfigureAwait(false);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync().ConfigureAwait(false);

            foreach (var calendar in calendars)
            {
                var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE Calendars SET IsSelected = $selected WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$id", calendar.Id);
                cmd.Parameters.AddWithValue("$selected", calendar.IsSelected ? 1 : 0);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
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
