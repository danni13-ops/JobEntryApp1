using JobEntryApp.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace JobEntryApp.Pages
{
    public class TaskTemplatesModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TaskTemplatesModel> _logger;

        public TaskTemplatesModel(IConfiguration config, ILogger<TaskTemplatesModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        public List<TaskTemplateItem> Items { get; set; } = new();
        public string? LoadError { get; set; }
        public bool HasIsActiveColumn { get; set; }
        public bool ShowingAllBecauseNoActive { get; set; }
        public int ActiveCount { get; set; }
        public bool UsingBuiltInDefaults { get; set; }

        public void OnGet()
        {
            var cs = _config.GetConnectionString("JobEntryDb");
            if (string.IsNullOrWhiteSpace(cs))
            {
                LoadError = "Database connection string is missing.";
                return;
            }

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();

                var columns = GetAvailableColumns(conn);
                if (columns.Count == 0)
                {
                    LoadError = "Task template table was not found.";
                    return;
                }

                HasIsActiveColumn = columns.Contains("IsActive");
                Items = ReadTemplates(conn, columns, activeOnly: HasIsActiveColumn);
                if (HasIsActiveColumn && Items.Count == 0)
                {
                    Items = ReadTemplates(conn, columns, activeOnly: false);
                    ShowingAllBecauseNoActive = Items.Count > 0;
                }

                ActiveCount = HasIsActiveColumn ? Items.Count(x => x.IsActive) : Items.Count;
                if (Items.Count == 0)
                {
                    Items = TaskTemplateDefaults.All.Select(x => new TaskTemplateItem
                    {
                        TaskNumber = x.TaskNumber,
                        TaskName = x.TaskName,
                        Stage = x.Stage,
                        DefaultAssignee = x.DefaultAssignee,
                        DaysOffset = x.DaysOffset,
                        Dependencies = x.Dependencies ?? string.Empty,
                        IsActive = true
                    }).ToList();
                    ActiveCount = Items.Count;
                    UsingBuiltInDefaults = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load task templates.");
                LoadError = "Unable to load task templates right now.";
            }
        }

        private static List<TaskTemplateItem> ReadTemplates(SqlConnection conn, HashSet<string> columns, bool activeOnly)
        {
            static string SqlCol(HashSet<string> cols, string colName, string alias) =>
                cols.Contains(colName) ? $"[{colName}] AS [{alias}]" : $"NULL AS [{alias}]";

            var selectDaysOffset = columns.Contains("DaysOffset")
                ? "[DaysOffset] AS [DaysOffset]"
                : (columns.Contains("OffsetFromStartDate")
                    ? "[OffsetFromStartDate] AS [DaysOffset]"
                    : "NULL AS [DaysOffset]");

            var whereActive = activeOnly && columns.Contains("IsActive") ? "WHERE IsActive = 1" : string.Empty;
            var orderBy = columns.Contains("TaskNumber") ? "ORDER BY TaskNumber" : "ORDER BY TaskName";

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT
                    {SqlCol(columns, "TaskNumber", "TaskNumber")},
                    {SqlCol(columns, "TaskName", "TaskName")},
                    {SqlCol(columns, "Stage", "Stage")},
                    {SqlCol(columns, "DefaultAssignee", "DefaultAssignee")},
                    {selectDaysOffset},
                    {SqlCol(columns, "Dependencies", "Dependencies")},
                    {(columns.Contains("IsActive") ? "CAST([IsActive] AS bit) AS [IsActive]" : "CAST(1 AS bit) AS [IsActive]")}
                FROM dbo.TaskTemplates
                {whereActive}
                {orderBy};";

            var result = new List<TaskTemplateItem>();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new TaskTemplateItem
                {
                    TaskNumber = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0)),
                    TaskName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Stage = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    DefaultAssignee = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    DaysOffset = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4)),
                    Dependencies = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    IsActive = !reader.IsDBNull(6) && reader.GetBoolean(6)
                });
            }

            return result;
        }

        private static HashSet<string> GetAvailableColumns(SqlConnection conn)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using SqlCommand colCmd = conn.CreateCommand();
            colCmd.CommandText = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'TaskTemplates';";
            using SqlDataReader colReader = colCmd.ExecuteReader();
            while (colReader.Read())
            {
                if (!colReader.IsDBNull(0))
                {
                    columns.Add(colReader.GetString(0));
                }
            }

            return columns;
        }

        public class TaskTemplateItem
        {
            public int TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string Stage { get; set; } = string.Empty;
            public string DefaultAssignee { get; set; } = string.Empty;
            public int? DaysOffset { get; set; }
            public string Dependencies { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }
    }
}
