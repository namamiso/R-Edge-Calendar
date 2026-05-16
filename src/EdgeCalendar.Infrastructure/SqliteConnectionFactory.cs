using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace EdgeCalendar.Infrastructure
{
    internal static class SqliteConnectionFactory
    {
        public static SqliteConnection Create(string dbPath)
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };

            return new SqliteConnection(builder.ToString());
        }

        public static InvalidOperationException CreateOpenException(string dbPath, Exception innerException)
        {
            return new InvalidOperationException($"SQLiteデータベースを開けませんでした: {dbPath}", innerException);
        }
    }
}
