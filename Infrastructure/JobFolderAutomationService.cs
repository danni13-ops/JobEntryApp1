using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace JobEntryApp.Infrastructure
{
    public static class JobFolderAutomationService
    {
        public static FolderSyncResult SyncJobArtifactsForJob(
            SqlConnection conn,
            string basePath,
            int jobNumber,
            ILogger? logger = null)
        {
            if (conn.State != System.Data.ConnectionState.Open)
            {
                throw new InvalidOperationException("The SQL connection must already be open.");
            }

            var context = LoadJobContext(conn, jobNumber);
            if (context is null)
            {
                return new FolderSyncResult();
            }

            try
            {
                return SyncJobArtifacts(conn, basePath, context, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Folder automation sync skipped for job {JobNumber}.", jobNumber);
                return new FolderSyncResult();
            }
        }

        private static FolderSyncResult SyncJobArtifacts(
            SqlConnection conn,
            string basePath,
            JobFolderContext context,
            ILogger? logger)
        {
            var result = new FolderSyncResult();
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return result;
            }

            var jobFolderPath = JobFolderService.BuildJobFolderPath(basePath, context.Customer, context.SubAccount, context.JobNumber, context.JobName);
            result.JobFolderPath = jobFolderPath;
            if (!Directory.Exists(jobFolderPath))
            {
                return result;
            }

            result.FolderExists = true;

            var countsFile = FindCountsWorkbook(jobFolderPath);
            if (!string.IsNullOrWhiteSpace(countsFile))
            {
                result.CountsFilePath = countsFile;
                CompleteMatchingTasks(conn, context.JobNumber, TaskMilestoneCatalog.IsCountsReceivedTask);

                if (CountsWorkbookReader.TryRead(countsFile, out var workbook) && workbook.TotalQuantity > 0)
                {
                    result.CountsWorkbook = workbook;
                    result.MailChartRowsTouched = UpsertMailChartCounts(conn, context, workbook);
                    UpdateJobQuantity(conn, context.JobNumber, workbook.TotalQuantity);
                }
                else
                {
                    logger?.LogInformation("Counts workbook was detected for job {JobNumber}, but no quantities were parsed from {File}.", context.JobNumber, countsFile);
                }
            }

            if (HasDataFiles(jobFolderPath))
            {
                result.DataFilesDetected = true;
                CompleteMatchingTasks(conn, context.JobNumber, TaskMilestoneCatalog.IsDataReceivedTask);
            }

            return result;
        }

        private static JobFolderContext? LoadJobContext(SqlConnection conn, int jobNumber)
        {
            var jobsTable = DatabaseSchema.FindTable(conn, null, "Jobs", "jobs");
            if (jobsTable is null)
            {
                return null;
            }

            var columns = DatabaseSchema.GetColumns(conn, jobsTable);
            var jobNumberColumn = DatabaseSchema.FindColumn(columns, "JobNumber", "job_number");
            var jobNameColumn = DatabaseSchema.FindColumn(columns, "JobName", "job_name");
            var customerColumn = DatabaseSchema.FindColumn(columns, "Customer", "customer");
            var subAccountColumn = DatabaseSchema.FindColumn(columns, "SubAccount", "sub_account");
            var csrColumn = DatabaseSchema.FindColumn(columns, "Csr", "CSR", "csr");
            var postageClassColumn = DatabaseSchema.FindColumn(columns, "PostageClass", "postage_class");
            var mailDateColumn = DatabaseSchema.FindColumn(columns, "MailDate", "mail_date");

            if (jobNumberColumn is null || jobNameColumn is null || customerColumn is null)
            {
                return null;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT
                    [{jobNumberColumn}],
                    [{jobNameColumn}],
                    [{customerColumn}],
                    {SelectColumnOrNull(subAccountColumn, "CAST(NULL AS nvarchar(200))")},
                    {SelectColumnOrNull(csrColumn, "CAST(NULL AS nvarchar(200))")},
                    {SelectColumnOrNull(postageClassColumn, "CAST(NULL AS nvarchar(100))")},
                    {SelectColumnOrNull(mailDateColumn, "CAST(NULL AS datetime2)")}
                FROM {jobsTable.SqlName}
                WHERE [{jobNumberColumn}] = @JobNumber;";
            cmd.Parameters.AddWithValue("@JobNumber", jobNumber);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new JobFolderContext(
                JobNumber: SqlReaderValue.ReadInt32(reader, 0),
                JobName: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Customer: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                SubAccount: reader.IsDBNull(3) ? null : reader.GetString(3),
                Csr: reader.IsDBNull(4) ? null : reader.GetString(4),
                PostageClass: reader.IsDBNull(5) ? null : reader.GetString(5),
                MailDate: reader.IsDBNull(6) ? null : reader.GetDateTime(6));
        }

        private static string? FindCountsWorkbook(string jobFolderPath)
        {
            return Directory.EnumerateFiles(jobFolderPath, "*.*", SearchOption.AllDirectories)
                .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .Where(path =>
                    Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(path).Equals(".xlsm", StringComparison.OrdinalIgnoreCase))
                .Where(path => Path.GetFileName(path).Contains("count", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static bool HasDataFiles(string jobFolderPath)
        {
            var dataFolderPath = Path.Combine(jobFolderPath, "DATA");
            if (!Directory.Exists(dataFolderPath))
            {
                return false;
            }

            return Directory.EnumerateFiles(dataFolderPath, "*.*", SearchOption.AllDirectories)
                .Any(path =>
                {
                    var fileName = Path.GetFileName(path);
                    return !fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase)
                        && !fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                        && !fileName.Contains("count", StringComparison.OrdinalIgnoreCase);
                });
        }

        private static void CompleteMatchingTasks(SqlConnection conn, int jobNumber, Func<string?, bool> matcher)
        {
            var tasksTable = DatabaseSchema.FindTable(conn, null, "Tasks", "tasks");
            if (tasksTable is null)
            {
                return;
            }

            var columns = DatabaseSchema.GetColumns(conn, tasksTable);
            var jobNumberColumn = DatabaseSchema.FindColumn(columns, "JobNumber", "job_number");
            var taskIdColumn = DatabaseSchema.FindColumn(columns, "TaskId", "task_id");
            var taskNameColumn = DatabaseSchema.FindColumn(columns, "TaskName", "task_name");
            var statusColumn = DatabaseSchema.FindColumn(columns, "Status", "status");
            var completedDateColumn = DatabaseSchema.FindColumn(columns, "CompletedDate", "completed_date");

            if (jobNumberColumn is null || taskIdColumn is null || taskNameColumn is null || statusColumn is null)
            {
                return;
            }

            using var selectCmd = conn.CreateCommand();
            selectCmd.CommandText = $@"
                SELECT [{taskIdColumn}], [{taskNameColumn}], [{statusColumn}]
                FROM {tasksTable.SqlName}
                WHERE [{jobNumberColumn}] = @JobNumber;";
            selectCmd.Parameters.AddWithValue("@JobNumber", jobNumber);

            var taskIdsToComplete = new List<Guid>();
            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var taskId = reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0);
                    var taskName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var status = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    if (taskId != Guid.Empty
                        && !string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
                        && matcher(taskName))
                    {
                        taskIdsToComplete.Add(taskId);
                    }
                }
            }

            foreach (var taskId in taskIdsToComplete)
            {
                using var updateCmd = conn.CreateCommand();
                var setClauses = new List<string> { $"[{statusColumn}] = 'Completed'" };
                if (completedDateColumn is not null)
                {
                    setClauses.Add($"[{completedDateColumn}] = GETDATE()");
                }

                updateCmd.CommandText = $@"
                    UPDATE {tasksTable.SqlName}
                    SET {string.Join(", ", setClauses)}
                    WHERE [{taskIdColumn}] = @TaskId;";
                updateCmd.Parameters.AddWithValue("@TaskId", taskId);
                updateCmd.ExecuteNonQuery();
            }
        }

        private static int UpsertMailChartCounts(SqlConnection conn, JobFolderContext context, CountWorkbookData workbook)
        {
            var mailChartTable = DatabaseSchema.FindTable(conn, null, "MailChart", "mailchart");
            if (mailChartTable is null)
            {
                return 0;
            }

            var columns = DatabaseSchema.GetColumns(conn, mailChartTable);
            var jobNumberColumn = DatabaseSchema.FindColumn(columns, "JobNumber", "job_number");
            var kitColumn = DatabaseSchema.FindColumn(columns, "Kit", "kit");
            if (jobNumberColumn is null || kitColumn is null)
            {
                return 0;
            }

            var rowsTouched = 0;
            var totalRows = new List<KitCount> { new(0, workbook.TotalQuantity) };
            totalRows.AddRange(workbook.Kits);

            foreach (var row in totalRows)
            {
                rowsTouched += UpsertMailChartRow(conn, mailChartTable, columns, context, row);
            }

            return rowsTouched;
        }

        private static int UpsertMailChartRow(
            SqlConnection conn,
            DatabaseTable mailChartTable,
            ISet<string> columns,
            JobFolderContext context,
            KitCount row)
        {
            var jobNumberColumn = DatabaseSchema.FindColumn(columns, "JobNumber", "job_number");
            var kitColumn = DatabaseSchema.FindColumn(columns, "Kit", "kit");
            var customerColumn = DatabaseSchema.FindColumn(columns, "Customer", "customer");
            var jobNameColumn = DatabaseSchema.FindColumn(columns, "JobName", "job_name");
            var classColumn = DatabaseSchema.FindColumn(columns, "Class", "class");
            var aeColumn = DatabaseSchema.FindColumn(columns, "AE", "ae");
            var mailDateColumn = DatabaseSchema.FindColumn(columns, "MailDate", "mail_date");
            var quantityColumn = DatabaseSchema.FindColumn(columns, "Quantity", "quantity");
            var styleTruckColumn = DatabaseSchema.FindColumn(columns, "StyleTruck", "style_truck");
            var comminglerColumn = DatabaseSchema.FindColumn(columns, "Commingler", "commingler");

            if (jobNumberColumn is null || kitColumn is null)
            {
                return 0;
            }

            using var existsCmd = conn.CreateCommand();
            existsCmd.CommandText = $@"
                SELECT COUNT(*)
                FROM {mailChartTable.SqlName}
                WHERE [{jobNumberColumn}] = @JobNumber
                  AND [{kitColumn}] = @Kit;";
            existsCmd.Parameters.AddWithValue("@JobNumber", context.JobNumber);
            existsCmd.Parameters.AddWithValue("@Kit", row.Kit);
            var exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;

            var values = new List<(string Column, string Parameter, object Value)>();
            AddValue(values, jobNumberColumn, "@JobNumber", context.JobNumber);
            AddValue(values, kitColumn, "@Kit", row.Kit);
            AddValue(values, customerColumn, "@Customer", context.Customer);
            AddValue(values, jobNameColumn, "@JobName", context.JobName);
            AddValue(values, classColumn, "@Class", (object?)context.PostageClass ?? DBNull.Value);
            AddValue(values, aeColumn, "@AE", (object?)context.Csr ?? DBNull.Value);
            AddValue(values, mailDateColumn, "@MailDate", context.MailDate.HasValue ? context.MailDate.Value : DBNull.Value);
            AddValue(values, quantityColumn, "@Quantity", row.Quantity);
            AddValue(values, styleTruckColumn, "@StyleTruck", string.Empty);
            AddValue(values, comminglerColumn, "@Commingler", string.Empty);

            if (exists)
            {
                var updateValues = values
                    .Where(v => !string.Equals(v.Column, jobNumberColumn, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(v.Column, kitColumn, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (updateValues.Count == 0)
                {
                    return 0;
                }

                using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = $@"
                    UPDATE {mailChartTable.SqlName}
                    SET {string.Join(", ", updateValues.Select(v => $"[{v.Column}] = {v.Parameter}"))}
                    WHERE [{jobNumberColumn}] = @JobNumber
                      AND [{kitColumn}] = @Kit;";

                updateCmd.Parameters.AddWithValue("@JobNumber", context.JobNumber);
                updateCmd.Parameters.AddWithValue("@Kit", row.Kit);
                foreach (var value in updateValues)
                {
                    updateCmd.Parameters.AddWithValue(value.Parameter, value.Value);
                }

                return updateCmd.ExecuteNonQuery();
            }

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO {mailChartTable.SqlName}
                (
                    {string.Join(", ", values.Select(v => $"[{v.Column}]"))}
                )
                VALUES
                (
                    {string.Join(", ", values.Select(v => v.Parameter))}
                );";
            foreach (var value in values)
            {
                insertCmd.Parameters.AddWithValue(value.Parameter, value.Value);
            }

            return insertCmd.ExecuteNonQuery();
        }

        private static void UpdateJobQuantity(SqlConnection conn, int jobNumber, int quantity)
        {
            if (quantity <= 0)
            {
                return;
            }

            var jobsTable = DatabaseSchema.FindTable(conn, null, "Jobs", "jobs");
            if (jobsTable is null)
            {
                return;
            }

            var columns = DatabaseSchema.GetColumns(conn, jobsTable);
            var jobNumberColumn = DatabaseSchema.FindColumn(columns, "JobNumber", "job_number");
            var quantityColumn = DatabaseSchema.FindColumn(columns, "Quantity", "quantity");
            if (jobNumberColumn is null || quantityColumn is null)
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                UPDATE {jobsTable.SqlName}
                SET [{quantityColumn}] = @Quantity
                WHERE [{jobNumberColumn}] = @JobNumber
                  AND (CAST([{quantityColumn}] AS bigint) <> @Quantity OR [{quantityColumn}] IS NULL);";
            cmd.Parameters.AddWithValue("@Quantity", quantity);
            cmd.Parameters.AddWithValue("@JobNumber", jobNumber);
            cmd.ExecuteNonQuery();
        }

        private static void AddValue(List<(string Column, string Parameter, object Value)> values, string? column, string parameter, object value)
        {
            if (!string.IsNullOrWhiteSpace(column))
            {
                values.Add((column, parameter, value));
            }
        }

        private static string SelectColumnOrNull(string? columnName, string nullExpression)
            => string.IsNullOrWhiteSpace(columnName) ? nullExpression : $"[{columnName}]";
    }

    public sealed class FolderSyncResult
    {
        public bool FolderExists { get; set; }
        public string? JobFolderPath { get; set; }
        public bool DataFilesDetected { get; set; }
        public string? CountsFilePath { get; set; }
        public CountWorkbookData? CountsWorkbook { get; set; }
        public int MailChartRowsTouched { get; set; }
    }

    internal sealed record JobFolderContext(
        int JobNumber,
        string JobName,
        string Customer,
        string? SubAccount,
        string? Csr,
        string? PostageClass,
        DateTime? MailDate);
}
