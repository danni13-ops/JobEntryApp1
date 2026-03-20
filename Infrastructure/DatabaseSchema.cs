using Microsoft.Data.SqlClient;

namespace JobEntryApp.Infrastructure
{
    public sealed record DatabaseTable(string Schema, string Name)
    {
        public string SqlName => $"[{Schema}].[{Name}]";
    }

    public static class DatabaseSchema
    {
        public static DatabaseTable? FindTable(SqlConnection conn, SqlTransaction? tx = null, params string[] tableNames)
        {
            if (tableNames.Length == 0)
            {
                return null;
            }

            var preferredOrder = tableNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((name, index) => new { name, index })
                .ToDictionary(i => i.name, i => i.index, StringComparer.OrdinalIgnoreCase);

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT TABLE_SCHEMA, TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE';";

            var matches = new List<DatabaseTable>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var schema = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (preferredOrder.ContainsKey(name))
                {
                    matches.Add(new DatabaseTable(schema, name));
                }
            }

            return matches
                .OrderBy(t => preferredOrder[t.Name])
                .ThenBy(t => string.Equals(t.Schema, "dbo", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();
        }

        public static HashSet<string> GetColumns(SqlConnection conn, DatabaseTable table, SqlTransaction? tx = null)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @Schema
                  AND TABLE_NAME = @TableName;";
            cmd.Parameters.AddWithValue("@Schema", table.Schema);
            cmd.Parameters.AddWithValue("@TableName", table.Name);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    columns.Add(reader.GetString(0));
                }
            }

            return columns;
        }

        public static string? FindColumn(ISet<string> columns, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (columns.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
