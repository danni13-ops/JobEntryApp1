using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace JobEntryApp.Pages
{
    public class BulkUploadModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<BulkUploadModel> _logger;

        public BulkUploadModel(IConfiguration config, ILogger<BulkUploadModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        [BindProperty]
        [Required]
        public string SourceFilePath { get; set; } = @"C:\Users\DanielleSantone\Downloads\Unconfirmed 56836.xls";

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? LastResultJson { get; set; }

        public ImportResult? LastResult { get; set; }

        public void OnGet()
        {
            if (!string.IsNullOrWhiteSpace(LastResultJson))
            {
                try
                {
                    LastResult = System.Text.Json.JsonSerializer.Deserialize<ImportResult>(LastResultJson);
                }
                catch
                {
                    LastResult = null;
                }
            }
        }

        public IActionResult OnPostImportLegacy()
        {
            if (string.IsNullOrWhiteSpace(SourceFilePath) || !System.IO.File.Exists(SourceFilePath))
            {
                ModelState.AddModelError(nameof(SourceFilePath), "File not found.");
                return Page();
            }

            try
            {
                var rows = ReadLegacyRowsFromXls(SourceFilePath);
                var result = ImportRows(rows);
                LastResultJson = System.Text.Json.JsonSerializer.Serialize(result);
                StatusMessage = $"Import completed. Jobs inserted: {result.JobsInserted}, updated: {result.JobsUpdated}, Mail rows inserted: {result.MailInserted}, updated: {result.MailUpdated}, Task rows created: {result.TaskRowsCreated}.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk import failed for file {FilePath}", SourceFilePath);
                ModelState.AddModelError(string.Empty, "Import failed: " + ex.Message);
                return Page();
            }
        }

        private ImportResult ImportRows(List<LegacyImportRow> rows)
        {
            var result = new ImportResult();
            result.TotalRowsRead = rows.Count;

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            var templates = LoadTaskTemplates(conn);

            foreach (var row in rows)
            {
                if (row.JobNumber <= 0 || string.IsNullOrWhiteSpace(row.JobName) || string.IsNullOrWhiteSpace(row.Customer))
                {
                    result.RowsSkippedInvalid++;
                    continue;
                }

                using var tx = conn.BeginTransaction();
                try
                {
                    var upsertResult = UpsertJob(conn, tx, row);
                    if (upsertResult == UpsertType.Inserted)
                    {
                        result.JobsInserted++;
                    }
                    else
                    {
                        result.JobsUpdated++;
                    }

                    var mailUpsert = UpsertMailChart(conn, tx, row);
                    if (mailUpsert == UpsertType.Inserted)
                    {
                        result.MailInserted++;
                    }
                    else
                    {
                        result.MailUpdated++;
                    }

                    if (row.MailDate.HasValue && row.MailDate.Value.Date >= DateTime.Today)
                    {
                        var tasksAdded = EnsureTasksForJob(conn, tx, row, templates);
                        result.TaskRowsCreated += tasksAdded;
                    }
                    else
                    {
                        result.TaskCreationSkippedJobs++;
                    }

                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    result.RowsFailed++;
                    _logger.LogWarning(ex, "Failed importing row JobNumber={JobNumber}", row.JobNumber);
                }
            }

            return result;
        }

        private static UpsertType UpsertJob(SqlConnection conn, SqlTransaction tx, LegacyImportRow row)
        {
            using var existsCmd = conn.CreateCommand();
            existsCmd.Transaction = tx;
            existsCmd.CommandText = "SELECT COUNT(*) FROM dbo.Jobs WHERE JobNumber = @JobNumber;";
            existsCmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
            var exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;

            if (exists)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = @"
                    UPDATE dbo.Jobs
                    SET JobName = @JobName,
                        MailDate = @MailDate,
                        Customer = @Customer,
                        Csr = @Csr,
                        Quantity = @Quantity,
                        StartDate = @StartDate,
                        Status = CASE WHEN Status IS NULL OR LTRIM(RTRIM(Status)) = '' THEN 'New' ELSE Status END
                    WHERE JobNumber = @JobNumber;";
                BindCommonJobParams(updateCmd, row);
                updateCmd.ExecuteNonQuery();
                return UpsertType.Updated;
            }

            using (var insertCmd = conn.CreateCommand())
            {
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
                    INSERT INTO dbo.Jobs (
                        JobNumber, JobName, MailDate, Customer, Csr, Quantity, StartDate, Status, [Print], TwoWayMatch
                    ) VALUES (
                        @JobNumber, @JobName, @MailDate, @Customer, @Csr, @Quantity, @StartDate, @Status, @Print, @TwoWayMatch
                    );";

                insertCmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
                insertCmd.Parameters.AddWithValue("@JobName", row.JobName);
                insertCmd.Parameters.AddWithValue("@MailDate", row.MailDate.HasValue ? row.MailDate.Value : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Customer", row.Customer);
                insertCmd.Parameters.AddWithValue("@Csr", (object?)row.Csr ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Quantity", row.EstQuantity);
                insertCmd.Parameters.AddWithValue("@StartDate", row.StartDate.HasValue ? row.StartDate.Value : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Status", "New");
                insertCmd.Parameters.AddWithValue("@Print", false);
                insertCmd.Parameters.AddWithValue("@TwoWayMatch", false);
                insertCmd.ExecuteNonQuery();
            }

            return UpsertType.Inserted;
        }

        private static void BindCommonJobParams(SqlCommand cmd, LegacyImportRow row)
        {
            cmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
            cmd.Parameters.AddWithValue("@JobName", row.JobName);
            cmd.Parameters.AddWithValue("@MailDate", row.MailDate.HasValue ? row.MailDate.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Customer", row.Customer);
            cmd.Parameters.AddWithValue("@Csr", (object?)row.Csr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Quantity", row.EstQuantity);
            cmd.Parameters.AddWithValue("@StartDate", row.StartDate.HasValue ? row.StartDate.Value : (object)DBNull.Value);
        }

        private static UpsertType UpsertMailChart(SqlConnection conn, SqlTransaction tx, LegacyImportRow row)
        {
            const int blankKit = 0;

            using var existsCmd = conn.CreateCommand();
            existsCmd.Transaction = tx;
            existsCmd.CommandText = "SELECT COUNT(*) FROM dbo.MailChart WHERE JobNumber = @JobNumber AND Kit = @Kit;";
            existsCmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
            existsCmd.Parameters.AddWithValue("@Kit", blankKit);
            var exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;

            if (exists)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = @"
                    UPDATE dbo.MailChart
                    SET Customer = @Customer,
                        JobName = @JobName,
                        AE = @AE,
                        MailDate = @MailDate,
                        Quantity = @Quantity
                    WHERE JobNumber = @JobNumber
                      AND Kit = @Kit;";
                updateCmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
                updateCmd.Parameters.AddWithValue("@Kit", blankKit);
                updateCmd.Parameters.AddWithValue("@Customer", row.Customer);
                updateCmd.Parameters.AddWithValue("@JobName", row.JobName);
                updateCmd.Parameters.AddWithValue("@AE", (object?)row.Csr ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@MailDate", row.MailDate.HasValue ? row.MailDate.Value : (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@Quantity", row.EstQuantity);
                updateCmd.ExecuteNonQuery();
                return UpsertType.Updated;
            }

            using (var insertCmd = conn.CreateCommand())
            {
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
                    INSERT INTO dbo.MailChart (
                        JobNumber, Kit, Customer, JobName, AE, MailDate, Quantity
                    ) VALUES (
                        @JobNumber, @Kit, @Customer, @JobName, @AE, @MailDate, @Quantity
                    );";
                insertCmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
                insertCmd.Parameters.AddWithValue("@Kit", blankKit);
                insertCmd.Parameters.AddWithValue("@Customer", row.Customer);
                insertCmd.Parameters.AddWithValue("@JobName", row.JobName);
                insertCmd.Parameters.AddWithValue("@AE", (object?)row.Csr ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@MailDate", row.MailDate.HasValue ? row.MailDate.Value : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Quantity", row.EstQuantity);
                insertCmd.ExecuteNonQuery();
            }

            return UpsertType.Inserted;
        }

        private static int EnsureTasksForJob(SqlConnection conn, SqlTransaction tx, LegacyImportRow row, IReadOnlyList<TaskTemplateRow> templates)
        {
            using var existsCmd = conn.CreateCommand();
            existsCmd.Transaction = tx;
            existsCmd.CommandText = "SELECT COUNT(*) FROM dbo.Tasks WHERE JobNumber = @JobNumber;";
            existsCmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
            if (Convert.ToInt32(existsCmd.ExecuteScalar()) > 0)
            {
                return 0;
            }

            var created = 0;
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
                    "CSR" => row.Csr ?? "CSR",
                    "DP" => "DP",
                    null => "Unassigned",
                    _ => template.DefaultAssignee
                };

                DateTime? dueDate = null;
                if (template.DaysOffset.HasValue && row.StartDate.HasValue)
                {
                    dueDate = row.StartDate.Value.AddDays(template.DaysOffset.Value);
                }

                insertCmd.Parameters.AddWithValue("@JobNumber", row.JobNumber);
                insertCmd.Parameters.AddWithValue("@TaskNumber", template.TaskNumber);
                insertCmd.Parameters.AddWithValue("@TaskName", template.TaskName);
                insertCmd.Parameters.AddWithValue("@Stage", (object?)template.Stage ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@AssignedTo", assignedTo);
                insertCmd.Parameters.AddWithValue("@Assignee2", DBNull.Value);
                insertCmd.Parameters.AddWithValue("@DueDate", dueDate.HasValue ? dueDate.Value : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@OffsetFromStartDate", (object?)template.DaysOffset ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@OffsetFromMailDate", DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Dependencies", (object?)template.Dependencies ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Status", "Scheduled");
                insertCmd.ExecuteNonQuery();
                created++;
            }

            return created;
        }

        private static List<TaskTemplateRow> LoadTaskTemplates(SqlConnection conn)
        {
            var hasIsActiveColumn = HasTaskTemplateColumn(conn, "IsActive");
            var templates = ReadTaskTemplates(conn, activeOnly: hasIsActiveColumn);

            if (hasIsActiveColumn && templates.Count == 0)
            {
                templates = ReadTaskTemplates(conn, activeOnly: false);
            }

            if (templates.Count == 0)
            {
                templates = GetBuiltInDefaultTemplates();
            }

            return templates;
        }

        private static List<TaskTemplateRow> ReadTaskTemplates(SqlConnection conn, bool activeOnly)
        {
            var templates = new List<TaskTemplateRow>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT TaskNumber, TaskName, Stage, DefaultAssignee, DaysOffset, Dependencies
                FROM dbo.TaskTemplates
                {(activeOnly ? "WHERE IsActive = 1" : string.Empty)}
                ORDER BY TaskNumber;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                templates.Add(new TaskTemplateRow
                {
                    TaskNumber = reader.GetInt32(0),
                    TaskName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Stage = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DefaultAssignee = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DaysOffset = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                    Dependencies = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }

            return templates;
        }

        private static bool HasTaskTemplateColumn(SqlConnection conn, string columnName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'TaskTemplates'
                  AND COLUMN_NAME = @ColumnName;";
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static List<TaskTemplateRow> GetBuiltInDefaultTemplates()
        {
            return
            [
                new TaskTemplateRow { TaskNumber = 1, TaskName = "Job Intake", Stage = "Intake", DefaultAssignee = "CSR", DaysOffset = 0, Dependencies = null },
                new TaskTemplateRow { TaskNumber = 2, TaskName = "Data Received", Stage = "Data", DefaultAssignee = "DP", DaysOffset = 0, Dependencies = "1" },
                new TaskTemplateRow { TaskNumber = 3, TaskName = "Data Review", Stage = "Data", DefaultAssignee = "DP", DaysOffset = 1, Dependencies = "2" },
                new TaskTemplateRow { TaskNumber = 4, TaskName = "Counts Approved", Stage = "Data", DefaultAssignee = "CSR", DaysOffset = 2, Dependencies = "3" },
                new TaskTemplateRow { TaskNumber = 5, TaskName = "Art Proof Ready", Stage = "Prepress", DefaultAssignee = "CSR", DaysOffset = 3, Dependencies = "4" },
                new TaskTemplateRow { TaskNumber = 6, TaskName = "Signoffs Due", Stage = "Prepress", DefaultAssignee = "CSR", DaysOffset = 5, Dependencies = "5" },
                new TaskTemplateRow { TaskNumber = 7, TaskName = "Signoffs Approved", Stage = "Prepress", DefaultAssignee = "CSR", DaysOffset = 7, Dependencies = "6" },
                new TaskTemplateRow { TaskNumber = 8, TaskName = "Merge/Purge", Stage = "Data", DefaultAssignee = "DP", DaysOffset = 8, Dependencies = "4" },
                new TaskTemplateRow { TaskNumber = 9, TaskName = "Print Files Ready", Stage = "Prepress", DefaultAssignee = "DP", DaysOffset = 9, Dependencies = "8" },
                new TaskTemplateRow { TaskNumber = 10, TaskName = "Print Production Start", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 10, Dependencies = "9" },
                new TaskTemplateRow { TaskNumber = 11, TaskName = "Print Quality Check", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 11, Dependencies = "10" },
                new TaskTemplateRow { TaskNumber = 12, TaskName = "Lettershop Setup", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 12, Dependencies = "11" },
                new TaskTemplateRow { TaskNumber = 13, TaskName = "Personalization", Stage = "Production", DefaultAssignee = "DP", DaysOffset = 13, Dependencies = "12" },
                new TaskTemplateRow { TaskNumber = 14, TaskName = "Insert Setup", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 14, Dependencies = "12" },
                new TaskTemplateRow { TaskNumber = 15, TaskName = "Match Setup", Stage = "Production", DefaultAssignee = "DP", DaysOffset = 15, Dependencies = "13" },
                new TaskTemplateRow { TaskNumber = 16, TaskName = "Addressing", Stage = "Production", DefaultAssignee = "DP", DaysOffset = 16, Dependencies = "15" },
                new TaskTemplateRow { TaskNumber = 17, TaskName = "Postal Prep", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 17, Dependencies = "16" },
                new TaskTemplateRow { TaskNumber = 18, TaskName = "Postal Docs Complete", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 18, Dependencies = "17" },
                new TaskTemplateRow { TaskNumber = 19, TaskName = "Production Out", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 19, Dependencies = "18" },
                new TaskTemplateRow { TaskNumber = 20, TaskName = "Final QC", Stage = "Production", DefaultAssignee = "CSR", DaysOffset = 20, Dependencies = "19" },
                new TaskTemplateRow { TaskNumber = 21, TaskName = "Truck/Style Confirmed", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 21, Dependencies = "20" },
                new TaskTemplateRow { TaskNumber = 22, TaskName = "Mail Date", Stage = "Postal", DefaultAssignee = "CSR", DaysOffset = 22, Dependencies = "21" }
            ];
        }

        private static List<LegacyImportRow> ReadLegacyRowsFromXls(string filePath)
        {
            var result = new List<LegacyImportRow>();

            dynamic? excelApp = null;
            dynamic? workbooks = null;
            dynamic? workbook = null;
            dynamic? worksheet = null;
            dynamic? usedRange = null;

            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw new PlatformNotSupportedException("Excel COM automation is only supported on Windows.");

                var excelType = Type.GetTypeFromProgID("Excel.Application")
                    ?? throw new InvalidOperationException("Excel COM is not available.");
                excelApp = Activator.CreateInstance(excelType);
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                workbooks = excelApp.Workbooks;
                workbook = workbooks.Open(filePath);
                worksheet = workbook.Worksheets.Item(1);
                usedRange = worksheet.UsedRange;

                var rowCount = (int)usedRange.Rows.Count;
                for (var row = 2; row <= rowCount; row++)
                {
                    var customer = ReadCellText(worksheet, row, 1);
                    var jobNumberText = ReadCellText(worksheet, row, 2);
                    var jobName = ReadCellText(worksheet, row, 3);
                    var csr = ReadCellText(worksheet, row, 4);
                    var mailDateText = ReadCellText(worksheet, row, 5);
                    var qtyText = ReadCellText(worksheet, row, 6);
                    var startDateText = ReadCellText(worksheet, row, 10); // Data Due -> StartDate

                    if (!int.TryParse(jobNumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int jobNumber))
                    {
                        continue;
                    }

                    var mailDate = ParseDate(mailDateText);
                    var startDate = ParseDate(startDateText);
                    var quantity = ParseIntegerQuantity(qtyText);

                    result.Add(new LegacyImportRow
                    {
                        Customer = customer ?? string.Empty,
                        JobNumber = jobNumber,
                        JobName = jobName ?? string.Empty,
                        Csr = csr,
                        MailDate = mailDate,
                        EstQuantity = quantity,
                        StartDate = startDate
                    });
                }
            }
            finally
            {
                try
                {
                    workbook?.Close(false);
                    excelApp?.Quit();
                }
                catch
                {
                    // ignore COM cleanup errors
                }

                ReleaseComObject(usedRange);
                ReleaseComObject(worksheet);
                ReleaseComObject(workbook);
                ReleaseComObject(workbooks);
                ReleaseComObject(excelApp);

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return result;
        }

        private static string? ReadCellText(dynamic worksheet, int row, int column)
        {
            var cell = worksheet?.Cells?.Item(row, column);
            var raw = cell is null ? null : cell.Text;
            var text = raw is null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static void ReleaseComObject(object? obj)
        {
            if (obj is null)
            {
                return;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                Marshal.FinalReleaseComObject(obj);
            }
            catch
            {
                // ignore release errors
            }
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                return dt.Date;
            }

            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
            {
                return dt.Date;
            }

            return null;
        }

        private static int ParseIntegerQuantity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var cleaned = value.Replace(",", string.Empty).Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            {
                if (dec < 0)
                {
                    return 0;
                }

                if (dec > int.MaxValue)
                {
                    return int.MaxValue;
                }

                return (int)Math.Round(dec, MidpointRounding.AwayFromZero);
            }

            return 0;
        }

        public class ImportResult
        {
            public int TotalRowsRead { get; set; }
            public int RowsSkippedInvalid { get; set; }
            public int RowsFailed { get; set; }
            public int JobsInserted { get; set; }
            public int JobsUpdated { get; set; }
            public int MailInserted { get; set; }
            public int MailUpdated { get; set; }
            public int TaskRowsCreated { get; set; }
            public int TaskCreationSkippedJobs { get; set; }
        }

        private class LegacyImportRow
        {
            public string Customer { get; set; } = string.Empty;
            public int JobNumber { get; set; }
            public string JobName { get; set; } = string.Empty;
            public string? Csr { get; set; }
            public DateTime? MailDate { get; set; }
            public int EstQuantity { get; set; }
            public DateTime? StartDate { get; set; }
        }

        private class TaskTemplateRow
        {
            public int TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string? Stage { get; set; }
            public string? DefaultAssignee { get; set; }
            public int? DaysOffset { get; set; }
            public string? Dependencies { get; set; }
        }

        private enum UpsertType
        {
            Inserted = 1,
            Updated = 2
        }
    }
}
