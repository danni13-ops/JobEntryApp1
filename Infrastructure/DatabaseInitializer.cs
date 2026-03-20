using Microsoft.Data.SqlClient;

namespace JobEntryApp.Infrastructure
{
    public static class DatabaseInitializer
    {
        private static readonly HashSet<string> AllowedTableNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Jobs", "Tasks", "MailChart",
            "Customers", "SubAccounts", "CSR", "DataProcessing", "Sales", "JobComponents"
        };

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
                    EnsureLookupTables(conn, logger);
                    EnsureJobComponentsTable(conn, logger);
                    EnsureJobsNewColumns(conn, logger);
                    EnsureTasksSchema(conn, logger);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Schema extension bootstrap partially failed. Continuing.");
                }

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

                    EnsureUniqueIndex(
                        conn,
                        logger,
                        tableName: "dbo.Tasks",
                        indexName: "UX_Tasks_TaskId",
                        duplicateProbeSql: "SELECT TaskId FROM dbo.Tasks GROUP BY TaskId HAVING COUNT(*) > 1;",
                        createSql: "CREATE UNIQUE INDEX [UX_Tasks_TaskId] ON [dbo].[Tasks]([TaskId]);");

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
                    logger.LogWarning(ex, "Index bootstrap partially failed.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Database bootstrap failed: {ex}");
            }
        }

        private static void EnsureLookupTables(SqlConnection conn, ILogger logger)
        {
            EnsureTable(conn, logger, "dbo.Customers", @"
                CREATE TABLE [dbo].[Customers] (
                    [CustomerID]   INT           IDENTITY(1,1) PRIMARY KEY,
                    [CustomerName] NVARCHAR(200) NOT NULL
                );");

            EnsureTable(conn, logger, "dbo.SubAccounts", @"
                CREATE TABLE [dbo].[SubAccounts] (
                    [SubAccountID]   INT           IDENTITY(1,1) PRIMARY KEY,
                    [CustomerName]   NVARCHAR(200) NOT NULL,
                    [SubAccountName] NVARCHAR(200) NOT NULL
                );");

            EnsureTable(conn, logger, "dbo.CSR", @"
                CREATE TABLE [dbo].[CSR] (
                    [CSRID]   INT           IDENTITY(1,1) PRIMARY KEY,
                    [CSRName] NVARCHAR(200) NOT NULL
                );");

            EnsureTable(conn, logger, "dbo.DataProcessing", @"
                CREATE TABLE [dbo].[DataProcessing] (
                    [DataProcessingID]   INT           IDENTITY(1,1) PRIMARY KEY,
                    [DataProcessingName] NVARCHAR(200) NOT NULL
                );");

            EnsureTable(conn, logger, "dbo.Sales", @"
                CREATE TABLE [dbo].[Sales] (
                    [SalesID]   INT           IDENTITY(1,1) PRIMARY KEY,
                    [SalesName] NVARCHAR(200) NOT NULL
                );");
        }

        private static void EnsureJobComponentsTable(SqlConnection conn, ILogger logger)
        {
            if (TableExists(conn, "dbo.JobComponents"))
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE [dbo].[JobComponents] (
                    [ComponentID]      INT           IDENTITY(1,1) PRIMARY KEY,
                    [JobNumber]        INT           NOT NULL,
                    [ComponentName]    NVARCHAR(200) NOT NULL,
                    [FacingDirection]  NVARCHAR(50)  NULL,
                    [ComponentOrder]   INT           NOT NULL DEFAULT 1,
                    [Type]             NVARCHAR(10)  NOT NULL DEFAULT 'Print'
                );
                CREATE INDEX [IX_JobComponents_JobNumber] ON [dbo].[JobComponents]([JobNumber]);";
            cmd.ExecuteNonQuery();
            logger.LogInformation("Created table dbo.JobComponents.");
        }

        private static void EnsureJobsNewColumns(SqlConnection conn, ILogger logger)
        {
            EnsureColumn(conn, logger, "Jobs", "RushJob",                        "BIT NOT NULL DEFAULT 0");
            EnsureColumn(conn, logger, "Jobs", "JobSpeed",                       "NVARCHAR(50) NOT NULL DEFAULT 'Standard'");
            EnsureColumn(conn, logger, "Jobs", "PrintComponent5Name",            "NVARCHAR(200) NULL");
            EnsureColumn(conn, logger, "Jobs", "PrintComponent5FacingDirection", "NVARCHAR(50) NULL");
            EnsureColumn(conn, logger, "Jobs", "MatchComponent1FacingDirection", "NVARCHAR(50) NULL");
            EnsureColumn(conn, logger, "Jobs", "MatchComponent2FacingDirection", "NVARCHAR(50) NULL");
            EnsureColumn(conn, logger, "Jobs", "MatchComponent3FacingDirection", "NVARCHAR(50) NULL");
            EnsureColumn(conn, logger, "Jobs", "MatchComponent4FacingDirection", "NVARCHAR(50) NULL");
            EnsureColumn(conn, logger, "Jobs", "MatchComponent5FacingDirection", "NVARCHAR(50) NULL");

            BackfillJobSpeed(conn, logger);
        }

        private static void EnsureTasksSchema(SqlConnection conn, ILogger logger)
        {
            if (!TableExists(conn, "dbo.Tasks"))
            {
                logger.LogWarning("Skipping Tasks bootstrap: dbo.Tasks does not exist.");
                return;
            }

            EnsureTaskIdColumn(conn, logger);
            EnsureColumn(conn, logger, "Tasks", "TaskNumber",         "INT NULL");
            EnsureColumn(conn, logger, "Tasks", "Stage",              "NVARCHAR(100) NULL");
            EnsureColumn(conn, logger, "Tasks", "AssignedTo",         "NVARCHAR(200) NULL");
            EnsureColumn(conn, logger, "Tasks", "Assignee2",          "NVARCHAR(200) NULL");
            EnsureColumn(conn, logger, "Tasks", "Dependencies",       "NVARCHAR(200) NULL");
            EnsureColumn(conn, logger, "Tasks", "CompletedDate",      "DATETIME2 NULL");
            EnsureColumn(conn, logger, "Tasks", "Notes",              "NVARCHAR(MAX) NULL");
            EnsureColumn(conn, logger, "Tasks", "OffsetFromStartDate","INT NULL");
            EnsureColumn(conn, logger, "Tasks", "OffsetFromMailDate", "INT NULL");

            BackfillTaskStatus(conn, logger);
            BackfillTaskNumbers(conn, logger);
        }

        private static void EnsureTaskIdColumn(SqlConnection conn, ILogger logger)
        {
            if (ColumnExists(conn, "Tasks", "TaskId"))
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                ALTER TABLE [dbo].[Tasks] ADD [TaskId] UNIQUEIDENTIFIER NULL;
                UPDATE [dbo].[Tasks] SET [TaskId] = NEWID() WHERE [TaskId] IS NULL;
                ALTER TABLE [dbo].[Tasks] ALTER COLUMN [TaskId] UNIQUEIDENTIFIER NOT NULL;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c
                        ON c.default_object_id = dc.object_id
                    INNER JOIN sys.tables t
                        ON t.object_id = c.object_id
                    INNER JOIN sys.schemas s
                        ON s.schema_id = t.schema_id
                    WHERE s.name = 'dbo'
                      AND t.name = 'Tasks'
                      AND c.name = 'TaskId'
                )
                BEGIN
                    ALTER TABLE [dbo].[Tasks]
                    ADD CONSTRAINT [DF_Tasks_TaskId] DEFAULT NEWID() FOR [TaskId];
                END";
            cmd.ExecuteNonQuery();
            logger.LogInformation("Added TaskId GUID key to dbo.Tasks.");
        }

        private static void BackfillTaskStatus(SqlConnection conn, ILogger logger)
        {
            if (!ColumnExists(conn, "Tasks", "Status"))
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE [dbo].[Tasks]
                SET [Status] = 'Scheduled'
                WHERE [Status] IS NULL
                   OR LTRIM(RTRIM([Status])) = '';";
            var rows = cmd.ExecuteNonQuery();
            if (rows > 0)
            {
                logger.LogInformation("Backfilled Status for {Count} dbo.Tasks rows.", rows);
            }
        }

        private static void BackfillJobSpeed(SqlConnection conn, ILogger logger)
        {
            if (!ColumnExists(conn, "Jobs", "JobSpeed"))
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE [dbo].[Jobs]
                SET [JobSpeed] = CASE
                    WHEN [RushJob] = 1 THEN 'Rush'
                    ELSE 'Standard'
                END
                WHERE [JobSpeed] IS NULL
                   OR LTRIM(RTRIM([JobSpeed])) = '';";

            var rows = cmd.ExecuteNonQuery();
            if (rows > 0)
            {
                logger.LogInformation("Backfilled JobSpeed for {Count} dbo.Jobs rows.", rows);
            }
        }

        private static void BackfillTaskNumbers(SqlConnection conn, ILogger logger)
        {
            if (!ColumnExists(conn, "Tasks", "TaskId") || !ColumnExists(conn, "Tasks", "TaskNumber"))
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                ;WITH ExistingMax AS (
                    SELECT [JobNumber], ISNULL(MAX([TaskNumber]), 0) AS [MaxTaskNumber]
                    FROM [dbo].[Tasks]
                    GROUP BY [JobNumber]
                ),
                Missing AS (
                    SELECT
                        t.[TaskId],
                        t.[JobNumber],
                        ROW_NUMBER() OVER (
                            PARTITION BY t.[JobNumber]
                            ORDER BY COALESCE(t.[DueDate], CONVERT(datetime2, '9999-12-31')),
                                     COALESCE(t.[TaskName], ''),
                                     t.[TaskId]
                        ) AS [RowNum]
                    FROM [dbo].[Tasks] t
                    WHERE t.[TaskNumber] IS NULL
                )
                UPDATE t
                SET [TaskNumber] = CASE
                    WHEN e.[MaxTaskNumber] = 0 THEN m.[RowNum]
                    ELSE e.[MaxTaskNumber] + m.[RowNum]
                END
                FROM [dbo].[Tasks] t
                INNER JOIN Missing m
                    ON m.[TaskId] = t.[TaskId]
                INNER JOIN ExistingMax e
                    ON e.[JobNumber] = m.[JobNumber];";
            var rows = cmd.ExecuteNonQuery();
            if (rows > 0)
            {
                logger.LogInformation("Backfilled TaskNumber for {Count} dbo.Tasks rows.", rows);
            }
        }

        private static void EnsureTable(SqlConnection conn, ILogger logger, string fullTableName, string createSql)
        {
            if (TableExists(conn, fullTableName))
            {
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = createSql;
            cmd.ExecuteNonQuery();
            logger.LogInformation("Created table {TableName}.", fullTableName);
        }

        private static void EnsureColumn(SqlConnection conn, ILogger logger, string tableName, string columnName, string columnDef)
        {
            if (!AllowedTableNames.Contains(tableName))
            {
                logger.LogWarning("EnsureColumn called with unrecognized table name '{TableName}'. Skipping.", tableName);
                return;
            }

            if (ColumnExists(conn, tableName, columnName))
            {
                return;
            }

            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE [dbo].[{tableName}] ADD [{columnName}] {columnDef};";
            alterCmd.ExecuteNonQuery();
            logger.LogInformation("Added column {Column} to dbo.{Table}.", columnName, tableName);
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

        private static bool ColumnExists(SqlConnection conn, string tableName, string columnName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = @TableName
                  AND COLUMN_NAME = @ColumnName;";
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
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
    }
}
