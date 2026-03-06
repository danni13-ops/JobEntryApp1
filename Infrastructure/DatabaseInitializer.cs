using Microsoft.Data.SqlClient;

namespace JobEntryApp.Infrastructure
{
    public static class DatabaseInitializer
    {
        public static void EnsurePerformanceAndConstraints(IConfiguration config, ILogger logger)
        {
            var cs = config.GetConnectionString("JobEntryDb");
            if (string.IsNullOrWhiteSpace(cs))
            {
                logger.LogWarning("Skipping DB bootstrap: connection string 'JobEntryDb' is missing.");
                return;
            }

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();

                try
                {
                    EnsureUniqueIndex(
                        conn,
                        logger,
                        tableName: "dbo.Jobs",
                        indexName: "UX_Jobs_JobNumber",
                        duplicateProbeSql: "SELECT JobNumber FROM dbo.Jobs GROUP BY JobNumber HAVING COUNT(*) > 1;",
                        createSql: "CREATE UNIQUE INDEX [UX_Jobs_JobNumber] ON [dbo].[Jobs]([JobNumber]);");

                    EnsureUniqueIndex(
                        conn,
                        logger,
                        tableName: "dbo.MailChart",
                        indexName: "UX_MailChart_JobNumber_Kit",
                        duplicateProbeSql: "SELECT JobNumber, Kit FROM dbo.MailChart GROUP BY JobNumber, Kit HAVING COUNT(*) > 1;",
                        createSql: "CREATE UNIQUE INDEX [UX_MailChart_JobNumber_Kit] ON [dbo].[MailChart]([JobNumber], [Kit]);");

                    EnsureIndex(
                        conn,
                        logger,
                        tableName: "dbo.MailChart",
                        indexName: "IX_MailChart_MailDate_AE_JobNumber",
                        createSql: "CREATE INDEX [IX_MailChart_MailDate_AE_JobNumber] ON [dbo].[MailChart]([MailDate], [AE], [JobNumber]);");

                    EnsureIndex(
                        conn,
                        logger,
                        tableName: "dbo.Tasks",
                        indexName: "IX_Tasks_JobNumber_DueDate_Status_AssignedTo",
                        createSql: "CREATE INDEX [IX_Tasks_JobNumber_DueDate_Status_AssignedTo] ON [dbo].[Tasks]([JobNumber], [DueDate], [Status], [AssignedTo]);");

                    EnsureIndex(
                        conn,
                        logger,
                        tableName: "dbo.Jobs",
                        indexName: "IX_Jobs_Status_MailDate",
                        createSql: "CREATE INDEX [IX_Jobs_Status_MailDate] ON [dbo].[Jobs]([Status], [MailDate]);");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Index bootstrap partially failed. Continuing with schedule bootstrap.");
                }

                EnsureFutureSchedulesForMissingJobs(conn, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database bootstrap failed.");
            }
        }

        private static void EnsureFutureSchedulesForMissingJobs(SqlConnection conn, ILogger logger)
        {
            if (!TableExists(conn, "dbo.Jobs") || !TableExists(conn, "dbo.Tasks"))
            {
                logger.LogWarning("Skipping schedule bootstrap: dbo.Jobs or dbo.Tasks table is missing.");
                return;
            }

            var templates = LoadSchedulingTemplatesFromDbOrDefaults(conn, logger);
            if (templates.Count == 0)
            {
                logger.LogWarning("Skipping schedule bootstrap: no templates available.");
                return;
            }

            var jobs = new List<JobForScheduling>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT j.JobNumber, j.MailDate, j.StartDate, j.Csr, j.DataProcessing
                    FROM dbo.Jobs j
                    WHERE CAST(j.MailDate AS date) >= @Today
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.Tasks t WHERE t.JobNumber = j.JobNumber
                      );";
                cmd.Parameters.AddWithValue("@Today", DateTime.Today);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    jobs.Add(new JobForScheduling
                    {
                        JobNumber = reader.GetInt32(0),
                        MailDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                        StartDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        Csr = reader.IsDBNull(3) ? null : reader.GetString(3),
                        DataProcessing = reader.IsDBNull(4) ? null : reader.GetString(4)
                    });
                }
            }

            if (jobs.Count == 0)
            {
                return;
            }

            using var tx = conn.BeginTransaction();
            var jobsProcessed = 0;
            var taskRowsCreated = 0;
            try
            {
                foreach (var job in jobs)
                {
                    foreach (var template in templates)
                    {
                        using var insertCmd = conn.CreateCommand();
                        insertCmd.Transaction = tx;
                        insertCmd.CommandText = @"
                            INSERT INTO dbo.Tasks (
                                JobNumber, TaskNumber, TaskName, Stage, AssignedTo, Assignee2,
                                DueDate, OffsetFromStartDate, OffsetFromMailDate, Dependencies, Status
                            ) VALUES (
                                @JobNumber, @TaskNumber, @TaskName, @Stage, @AssignedTo, @Assignee2,
                                @DueDate, @OffsetFromStartDate, @OffsetFromMailDate, @Dependencies, @Status
                            );";

                        var assignedTo = template.DefaultAssignee switch
                        {
                            "CSR" => string.IsNullOrWhiteSpace(job.Csr) ? "CSR" : job.Csr,
                            "DP" => string.IsNullOrWhiteSpace(job.DataProcessing) ? "DP" : job.DataProcessing,
                            null => "Unassigned",
                            _ => template.DefaultAssignee
                        };

                        var dueDate = ResolveDueDate(template, job);
                        var offsetStart = template.OffsetFromStartDate ?? template.DaysOffset;
                        var offsetMail = template.OffsetFromMailDate;

                        insertCmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
                        insertCmd.Parameters.AddWithValue("@TaskNumber", template.TaskNumber);
                        insertCmd.Parameters.AddWithValue("@TaskName", template.TaskName);
                        insertCmd.Parameters.AddWithValue("@Stage", (object?)template.Stage ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@AssignedTo", assignedTo);
                        insertCmd.Parameters.AddWithValue("@Assignee2", DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@DueDate", dueDate.HasValue ? dueDate.Value : (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@OffsetFromStartDate", (object?)offsetStart ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@OffsetFromMailDate", (object?)offsetMail ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Dependencies", (object?)template.Dependencies ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Status", "Scheduled");
                        insertCmd.ExecuteNonQuery();
                        taskRowsCreated++;
                    }

                    jobsProcessed++;
                }

                tx.Commit();
                logger.LogInformation(
                    "Backfilled schedules for {JobCount} future jobs ({TaskRows} task rows created).",
                    jobsProcessed,
                    taskRowsCreated);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static DateTime? ResolveDueDate(SchedulingTemplate template, JobForScheduling job)
        {
            if (template.OffsetFromStartDate.HasValue && job.StartDate.HasValue)
            {
                return job.StartDate.Value.AddDays(template.OffsetFromStartDate.Value);
            }

            if (template.OffsetFromMailDate.HasValue && job.MailDate.HasValue)
            {
                return job.MailDate.Value.AddDays(template.OffsetFromMailDate.Value);
            }

            if (template.DaysOffset.HasValue)
            {
                if (job.StartDate.HasValue)
                {
                    return job.StartDate.Value.AddDays(template.DaysOffset.Value);
                }

                if (job.MailDate.HasValue)
                {
                    return job.MailDate.Value.AddDays(template.DaysOffset.Value);
                }
            }

            return null;
        }

        private static List<SchedulingTemplate> LoadSchedulingTemplatesFromDbOrDefaults(SqlConnection conn, ILogger logger)
        {
            var templates = new List<SchedulingTemplate>();
            try
            {
                if (TableExists(conn, "dbo.TaskTemplates"))
                {
                    var columns = GetTaskTemplateColumns(conn);
                    if (columns.Contains("TaskNumber") && columns.Contains("TaskName"))
                    {
                        templates = LoadTemplatesFromDb(conn, columns);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed reading dbo.TaskTemplates. Falling back to built-in templates.");
            }

            if (templates.Count > 0)
            {
                return templates;
            }

            logger.LogWarning("Using built-in default task templates because dbo.TaskTemplates has no usable rows.");
            return GetBuiltInDefaultTemplates();
        }

        private static List<SchedulingTemplate> LoadTemplatesFromDb(SqlConnection conn, HashSet<string> columns)
        {
            var hasIsActive = columns.Contains("IsActive");
            var useActiveOnly = false;
            if (hasIsActive)
            {
                using var activeCmd = conn.CreateCommand();
                activeCmd.CommandText = "SELECT COUNT(*) FROM dbo.TaskTemplates WHERE IsActive = 1;";
                useActiveOnly = Convert.ToInt32(activeCmd.ExecuteScalar()) > 0;
            }

            var selectStage = columns.Contains("Stage")
                ? "[Stage] AS [Stage]"
                : "CAST(NULL AS nvarchar(100)) AS [Stage]";
            var selectAssignee = columns.Contains("DefaultAssignee")
                ? "[DefaultAssignee] AS [DefaultAssignee]"
                : "CAST(NULL AS nvarchar(100)) AS [DefaultAssignee]";
            var selectDaysOffset = columns.Contains("DaysOffset")
                ? "[DaysOffset] AS [DaysOffset]"
                : "CAST(NULL AS int) AS [DaysOffset]";
            var selectOffsetStart = columns.Contains("OffsetFromStartDate")
                ? "[OffsetFromStartDate] AS [OffsetFromStartDate]"
                : "CAST(NULL AS int) AS [OffsetFromStartDate]";
            var selectOffsetMail = columns.Contains("OffsetFromMailDate")
                ? "[OffsetFromMailDate] AS [OffsetFromMailDate]"
                : "CAST(NULL AS int) AS [OffsetFromMailDate]";
            var selectDependencies = columns.Contains("Dependencies")
                ? "[Dependencies] AS [Dependencies]"
                : "CAST(NULL AS nvarchar(200)) AS [Dependencies]";
            var whereClause = useActiveOnly ? "WHERE IsActive = 1" : string.Empty;

            var templates = new List<SchedulingTemplate>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT
                    [TaskNumber],
                    [TaskName],
                    {selectStage},
                    {selectAssignee},
                    {selectDaysOffset},
                    {selectOffsetStart},
                    {selectOffsetMail},
                    {selectDependencies}
                FROM dbo.TaskTemplates
                {whereClause}
                ORDER BY [TaskNumber];";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                templates.Add(new SchedulingTemplate
                {
                    TaskNumber = reader.GetInt32(0),
                    TaskName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Stage = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DefaultAssignee = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DaysOffset = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                    OffsetFromStartDate = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                    OffsetFromMailDate = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                    Dependencies = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }

            return templates;
        }

        private static HashSet<string> GetTaskTemplateColumns(SqlConnection conn)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'TaskTemplates';";
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

        private static List<SchedulingTemplate> GetBuiltInDefaultTemplates()
        {
            return
            [
                new SchedulingTemplate { TaskNumber = 1, TaskName = "Job Intake", Stage = "Intake", DefaultAssignee = "CSR", DaysOffset = 0, Dependencies = null },
                new SchedulingTemplate { TaskNumber = 2, TaskName = "Data Received", Stage = "Data", DefaultAssignee = "DP", DaysOffset = 0, Dependencies = "1" },
                new SchedulingTemplate { TaskNumber = 3, TaskName = "Data Review", Stage = "Data", DefaultAssignee = "DP", DaysOffset = 1, Dependencies = "2" },
                new SchedulingTemplate { TaskNumber = 4, TaskName = "Counts Approved", Stage = "Data", DefaultAssignee = "CSR", DaysOffset = 2, Dependencies = "3" },
                new SchedulingTemplate { TaskNumber = 5, TaskName = "Art Proof Ready", Stage = "Prepress", DefaultAssignee = "CSR", DaysOffset = 3, Dependencies = "4" },
                new SchedulingTemplate { TaskNumber = 6, TaskName = "Signoffs Due", Stage = "Prepress", DefaultAssignee = "CSR", DaysOffset = 5, Dependencies = "5" },
                new SchedulingTemplate { TaskNumber = 7, TaskName = "Signoffs Approved", Stage = "Prepress", DefaultAssignee = "CSR", DaysOffset = 7, Dependencies = "6" },
                new SchedulingTemplate { TaskNumber = 8, TaskName = "Merge/Purge", Stage = "Data", DefaultAssignee = "DP", DaysOffset = 8, Dependencies = "4" },
                new SchedulingTemplate { TaskNumber = 9, TaskName = "Print Files Ready", Stage = "Prepress", DefaultAssignee = "DP", DaysOffset = 9, Dependencies = "8" },
                new SchedulingTemplate { TaskNumber = 10, TaskName = "Print Production Start", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 10, Dependencies = "9" },
                new SchedulingTemplate { TaskNumber = 11, TaskName = "Print Quality Check", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 11, Dependencies = "10" },
                new SchedulingTemplate { TaskNumber = 12, TaskName = "Lettershop Setup", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 12, Dependencies = "11" },
                new SchedulingTemplate { TaskNumber = 13, TaskName = "Personalization", Stage = "Production", DefaultAssignee = "DP", DaysOffset = 13, Dependencies = "12" },
                new SchedulingTemplate { TaskNumber = 14, TaskName = "Insert Setup", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 14, Dependencies = "12" },
                new SchedulingTemplate { TaskNumber = 15, TaskName = "Match Setup", Stage = "Production", DefaultAssignee = "DP", DaysOffset = 15, Dependencies = "13" },
                new SchedulingTemplate { TaskNumber = 16, TaskName = "Addressing", Stage = "Production", DefaultAssignee = "DP", DaysOffset = 16, Dependencies = "15" },
                new SchedulingTemplate { TaskNumber = 17, TaskName = "Postal Prep", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 17, Dependencies = "16" },
                new SchedulingTemplate { TaskNumber = 18, TaskName = "Postal Docs Complete", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 18, Dependencies = "17" },
                new SchedulingTemplate { TaskNumber = 19, TaskName = "Production Out", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 19, Dependencies = "18" },
                new SchedulingTemplate { TaskNumber = 20, TaskName = "Final QC", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 20, Dependencies = "19" },
                new SchedulingTemplate { TaskNumber = 21, TaskName = "Truck/Style Confirmed", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 21, Dependencies = "20" },
                new SchedulingTemplate { TaskNumber = 22, TaskName = "Mail Date", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 22, Dependencies = "21" }
            ];
        }

        private static void EnsureUniqueIndex(
            SqlConnection conn,
            ILogger logger,
            string tableName,
            string indexName,
            string duplicateProbeSql,
            string createSql)
        {
            if (!TableExists(conn, tableName))
            {
                logger.LogWarning("Skipping index {IndexName}: table {TableName} does not exist.", indexName, tableName);
                return;
            }

            if (IndexExists(conn, tableName, indexName))
            {
                return;
            }

            if (HasRows(conn, duplicateProbeSql))
            {
                logger.LogWarning(
                    "Skipping unique index {IndexName} on {TableName}: duplicate rows already exist. Clean data first.",
                    indexName,
                    tableName);
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = createSql;
            cmd.ExecuteNonQuery();
            logger.LogInformation("Created unique index {IndexName}.", indexName);
        }

        private static void EnsureIndex(
            SqlConnection conn,
            ILogger logger,
            string tableName,
            string indexName,
            string createSql)
        {
            if (!TableExists(conn, tableName))
            {
                logger.LogWarning("Skipping index {IndexName}: table {TableName} does not exist.", indexName, tableName);
                return;
            }

            if (IndexExists(conn, tableName, indexName))
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = createSql;
            cmd.ExecuteNonQuery();
            logger.LogInformation("Created index {IndexName}.", indexName);
        }

        private static bool TableExists(SqlConnection conn, string tableName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NOT NULL THEN 1 ELSE 0 END;";
            cmd.Parameters.AddWithValue("@TableName", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        private static bool IndexExists(SqlConnection conn, string tableName, string indexName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(@TableName)
                  AND name = @IndexName;";
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@IndexName", indexName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool HasRows(SqlConnection conn, string query)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            using var reader = cmd.ExecuteReader();
            return reader.Read();
        }

        private sealed class SchedulingTemplate
        {
            public int TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string? Stage { get; set; }
            public string? DefaultAssignee { get; set; }
            public int? DaysOffset { get; set; }
            public int? OffsetFromStartDate { get; set; }
            public int? OffsetFromMailDate { get; set; }
            public string? Dependencies { get; set; }
        }

        private sealed class JobForScheduling
        {
            public int JobNumber { get; set; }
            public DateTime? MailDate { get; set; }
            public DateTime? StartDate { get; set; }
            public string? Csr { get; set; }
            public string? DataProcessing { get; set; }
        }
    }
}
