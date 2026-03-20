using Microsoft.Data.SqlClient;

namespace JobEntryApp.Infrastructure
{
    public static class TaskTemplateRepository
    {
        public static IReadOnlyList<TaskTemplateDefault> Load(SqlConnection conn, SqlTransaction? tx = null)
        {
            var table = DatabaseSchema.FindTable(conn, tx, "task_templates");
            if (table is null)
            {
                return TaskTemplateDefaults.All.ToList();
            }

            var columns = DatabaseSchema.GetColumns(conn, table, tx);
            if (!columns.Contains("task_name"))
            {
                return TaskTemplateDefaults.All.ToList();
            }

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $@"
                SELECT
                    {(columns.Contains("sort_order") ? "CONVERT(int, [sort_order])" : "ROW_NUMBER() OVER (ORDER BY [task_name])")} AS SortOrder,
                    [task_name] AS TaskName,
                    {(columns.Contains("stage") ? "[stage]" : "CAST(NULL AS nvarchar(100))")} AS Stage,
                    {(columns.Contains("assigned_to") ? "[assigned_to]" : "CAST(NULL AS nvarchar(200))")} AS AssignedTo,
                    {(columns.Contains("assigned_to_2") ? "[assigned_to_2]" : "CAST(NULL AS nvarchar(200))")} AS AssignedTo2,
                    {(columns.Contains("offset_days") ? "CONVERT(int, [offset_days])" : "CAST(0 AS int)")} AS OffsetDays,
                    {(columns.Contains("anchor_type") ? "[anchor_type]" : "CAST('StartDate' AS nvarchar(50))")} AS AnchorType,
                    {(columns.Contains("is_critical") ? "CAST([is_critical] AS bit)" : "CAST(NULL AS bit)")} AS IsCritical,
                    {(columns.Contains("job_speed") ? "[job_speed]" : "CAST(NULL AS nvarchar(50))")} AS JobSpeed
                FROM {table.SqlName}
                ORDER BY {(columns.Contains("sort_order") ? "[sort_order]" : "[task_name]")};";

            var templates = new List<TaskTemplateDefault>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                templates.Add(new TaskTemplateDefault(
                    SortOrder: reader.GetInt32(0),
                    TaskName: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Stage: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    AssignedTo: reader.IsDBNull(3) ? null : reader.GetString(3),
                    AssignedTo2: reader.IsDBNull(4) ? null : reader.GetString(4),
                    DaysOffset: reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    AnchorType: reader.IsDBNull(6) ? "StartDate" : reader.GetString(6),
                    IsCritical: reader.IsDBNull(7) ? null : reader.GetBoolean(7),
                    JobSpeed: reader.IsDBNull(8) ? null : reader.GetString(8)));
            }

            return templates.Count > 0 ? templates : TaskTemplateDefaults.All.ToList();
        }
    }
}
