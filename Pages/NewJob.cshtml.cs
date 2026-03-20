using JobEntryApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

namespace JobEntryApp.Pages
{
	public class NewJobModel : PageModel
	{
		private readonly IConfiguration _config;

		public NewJobModel(IConfiguration config)
		{
			_config = config;
		}

		[BindProperty]
		public JobInputModel Job { get; set; } = new();

		public string? StatusMessage { get; set; }

		public List<SelectListItem> CustomerList { get; set; } = new();
		public List<SelectListItem> SubAccountList { get; set; } = new();
		public List<SelectListItem> CsrList { get; set; } = new();
		public List<SelectListItem> DataProcessingList { get; set; } = new();
		public List<SelectListItem> SalesList { get; set; } = new();

		private void LoadDropdowns()
		{
			CustomerList = new List<SelectListItem>
			{
				new SelectListItem { Text = "RWT", Value = "RWT" },
				new SelectListItem { Text = "Edgemark", Value = "Edgemark" },
				new SelectListItem { Text = "3 Creative", Value = "3 Creative" },
				new SelectListItem { Text = "KT Productions", Value = "KT Productions" },
				new SelectListItem { Text = "Mission Wired", Value = "Mission Wired" },
				new SelectListItem { Text = "Eberle", Value = "Eberle" },
				new SelectListItem { Text = "Milestone Marketing", Value = "Milestone Marketing" },
				new SelectListItem { Text = "ATA", Value = "ATA" },
				new SelectListItem { Text = "Kael", Value = "Kael" },
				new SelectListItem { Text = "Production Solutions", Value = "Production Solutions" },
				new SelectListItem { Text = "Merkle", Value = "Merkle" },
				new SelectListItem { Text = "MBI", Value = "MBI" },
				new SelectListItem { Text = "Concord Dired", Value = "Concord Dired" },
				new SelectListItem { Text = "Duestch", Value = "Duestch" },
				new SelectListItem { Text = "Color Source", Value = "Color Source" },
				new SelectListItem { Text = "Tension", Value = "Tension" },
				new SelectListItem { Text = "Suarez", Value = "Suarez" },
				new SelectListItem { Text = "TFP", Value = "TFP" }
			};

			SubAccountList = new List<SelectListItem>
			{
				new SelectListItem { Text = "Humane", Value = "Humane" },
				new SelectListItem { Text = "HSLF", Value = "HSLF" },
				new SelectListItem { Text = "AFP", Value = "AFP" },
				new SelectListItem { Text = "PETA", Value = "PETA" },
				new SelectListItem { Text = "NACOP", Value = "NACOP" },
				new SelectListItem { Text = "PPFA", Value = "PPFA" },
				new SelectListItem { Text = "PPFA Acq", Value = "PPFA Acq" },
				new SelectListItem { Text = "UNHCR", Value = "UNHCR" },
				new SelectListItem { Text = "UCS", Value = "UCS" },
				new SelectListItem { Text = "NYPL", Value = "NYPL" },
				new SelectListItem { Text = "Madre", Value = "Madre" },
				new SelectListItem { Text = "Mercy Corp", Value = "Mercy Corp" },
				new SelectListItem { Text = "DNC", Value = "DNC" },
				new SelectListItem { Text = "WWII", Value = "WWII" },
				new SelectListItem { Text = "Obama Renewal/Foundation", Value = "Obama Renewal/Foundation" },
				new SelectListItem { Text = "Victory Fund", Value = "Victory Fund" },
				new SelectListItem { Text = "Medicare", Value = "Medicare" },
				new SelectListItem { Text = "Auto", Value = "Auto" },
				new SelectListItem { Text = "HHV", Value = "HHV" },
				new SelectListItem { Text = "ACI", Value = "ACI" },
				new SelectListItem { Text = "CSPF", Value = "CSPF" },
				new SelectListItem { Text = "Million Voices", Value = "Million Voices" },
				new SelectListItem { Text = "YC", Value = "YC" },
				new SelectListItem { Text = "The Heritage Foundation", Value = "The Heritage Foundation" },
				new SelectListItem { Text = "WFW", Value = "WFW" },
				new SelectListItem { Text = "AMC", Value = "AMC" },
				new SelectListItem { Text = "Kennedy Center", Value = "Kennedy Center" },
				new SelectListItem { Text = "NAS", Value = "NAS" },
				new SelectListItem { Text = "NWF", Value = "NWF" },
				new SelectListItem { Text = "NJH", Value = "NJH" },
				new SelectListItem { Text = "PIH", Value = "PIH" },
				new SelectListItem { Text = "BFAS", Value = "BFAS" },
				new SelectListItem { Text = "CVT", Value = "CVT" },
				new SelectListItem { Text = "HWFA", Value = "HWFA" },
				new SelectListItem { Text = "WEAVE", Value = "WEAVE" },
				new SelectListItem { Text = "TNM", Value = "TNM" },
				new SelectListItem { Text = "CARE", Value = "CARE" },
				new SelectListItem { Text = "WHHA", Value = "WHHA" },
				new SelectListItem { Text = "Green Peace", Value = "Green Peace" },
				new SelectListItem { Text = "NMAAHC", Value = "NMAAHC" },
				new SelectListItem { Text = "PCS", Value = "PCS" },
				new SelectListItem { Text = "Danbury Mint", Value = "Danbury Mint" },
				new SelectListItem { Text = "Easton Press", Value = "Easton Press" },
				new SelectListItem { Text = "TMM", Value = "TMM" },
				new SelectListItem { Text = "SLSN", Value = "SLSN" },
				new SelectListItem { Text = "NKH", Value = "NKH" },
				new SelectListItem { Text = "St. Jude", Value = "St. Jude" },
				new SelectListItem { Text = "Thrift Book", Value = "Thrift Book" },
				new SelectListItem { Text = "Sheriff Deputy", Value = "Sheriff Deputy" },
				new SelectListItem { Text = "Camden County", Value = "Camden County" },
				new SelectListItem { Text = "Geico", Value = "Geico" },
				new SelectListItem { Text = "Gen21 Heater", Value = "Gen21 Heater" },
				new SelectListItem { Text = "Mist Free Humidifier", Value = "Mist Free Humidifier" },
				new SelectListItem { Text = "BioSpeed Clean SM", Value = "BioSpeed Clean SM" },
				new SelectListItem { Text = "Total Relief Heating Pad", Value = "Total Relief Heating Pad" },
				new SelectListItem { Text = "Oxileaf", Value = "Oxileaf" },
				new SelectListItem { Text = "Climater", Value = "Climater" },
				new SelectListItem { Text = "America Needs Fatima", Value = "America Needs Fatima" },
				new SelectListItem { Text = "IFAW", Value = "IFAW" },
				new SelectListItem { Text = "PNC", Value = "PNC" },
				new SelectListItem { Text = "NFF", Value = "NFF" }
			};

			CsrList = new List<SelectListItem>
			{
				new SelectListItem { Text = "JESSE THOMAS", Value = "JESSE THOMAS" },
				new SelectListItem { Text = "BETH WORLEY", Value = "BETH WORLEY" },
				new SelectListItem { Text = "MONICA MANERCHIA", Value = "MONICA MANERCHIA" },
				new SelectListItem { Text = "LYDIA BROWN", Value = "LYDIA BROWN" },
				new SelectListItem { Text = "DANIELLE SANTONE", Value = "DANIELLE SANTONE" },
				new SelectListItem { Text = "ERIN SULLIVAN", Value = "ERIN SULLIVAN" },
				new SelectListItem { Text = "LINDA JOHNSON", Value = "LINDA JOHNSON" }
			};

			DataProcessingList = new List<SelectListItem>
			{
				new SelectListItem { Text = "CY YOST", Value = "CY YOST" },
				new SelectListItem { Text = "LOU HIERONIMUS", Value = "LOU HIERONIMUS" },
				new SelectListItem { Text = "JERRY WEIR", Value = "JERRY WEIR" },
				new SelectListItem { Text = "TOM HESS", Value = "TOM HESS" },
				new SelectListItem { Text = "KELLY WICKER", Value = "KELLY WICKER" },
				new SelectListItem { Text = "NICK SANTONE", Value = "NICK SANTONE" },
				new SelectListItem { Text = "LARRY BIDDLECOME", Value = "LARRY BIDDLECOME" },
				new SelectListItem { Text = "RICHARD KIMBLE", Value = "RICHARD KIMBLE" }
			};

			SalesList = new List<SelectListItem>
			{
				new SelectListItem { Text = "DAN DOBBIN", Value = "DAN DOBBIN" },
				new SelectListItem { Text = "DANIELLE SANTONE", Value = "DANIELLE SANTONE" },
				new SelectListItem { Text = "ERIN SULLIVAN", Value = "ERIN SULLIVAN" },
				new SelectListItem { Text = "PAT MAGEE", Value = "PAT MAGEE" },
				new SelectListItem { Text = "BRIAN DUDLEY", Value = "BRIAN DUDLEY" }
			};
		}

		public void OnGet()
		{
			LoadDropdowns();
			Job = new JobInputModel
			{
				JobNumber = GetNextJobNumber(),
                JobSpeed = JobSchedules.Standard
				// All other fields remain blank/default on first load.
			};
			ModelState.Clear();
		}

		public IActionResult OnPost()
		{
			LoadDropdowns();
            NormalizeScheduleFields();
			NormalizeConditionalFields();
			ValidateConditionalFields();

			if (!ModelState.IsValid)
				return Page();

			// Calculate StartDate BEFORE saving
			Job.StartDate = CalculateStartDate(Job);

			try
			{
				SaveJobWithTasks(Job);
			}
			catch (Exception ex)
			{
				ModelState.AddModelError(string.Empty, $"Could not save job: {ex.Message}");
				return Page();
			}

			StatusMessage = "Job saved successfully.";

			return RedirectToPage("/Jobs/Index");
		}

        private void NormalizeScheduleFields()
        {
            Job.JobSpeed = JobSchedules.Normalize(Job.JobSpeed);
            Job.RushJob = JobSchedules.IsExpedited(Job.JobSpeed);
        }

		private void NormalizeConditionalFields()
		{
			if (!Job.Print)
			{
				Job.PrintPieceCount = null;
				ModelState.Remove($"{nameof(Job)}.{nameof(JobInputModel.PrintPieceCount)}");
				for (var i = 1; i <= 4; i++)
				{
					ClearPrintComponent(i);
				}
			}
			else
			{
				var printComponentCount = Math.Clamp(Job.PrintPieceCount ?? 1, 1, 4);
				for (var i = printComponentCount + 1; i <= 4; i++)
				{
					ClearPrintComponent(i);
				}
			}

			if (!Job.TwoWayMatch)
			{
				Job.MatchWayCount = null;
				ModelState.Remove($"{nameof(Job)}.{nameof(JobInputModel.MatchWayCount)}");
				for (var i = 1; i <= 5; i++)
				{
					ClearMatchComponent(i);
				}
			}
			else
			{
				var matchComponentCount = Math.Clamp(Job.MatchWayCount ?? 2, 2, 5);
				for (var i = matchComponentCount + 1; i <= 5; i++)
				{
					ClearMatchComponent(i);
				}
			}
		}

		private void ValidateConditionalFields()
		{
			if (Job.Print && (!Job.PrintPieceCount.HasValue || Job.PrintPieceCount.Value < 1 || Job.PrintPieceCount.Value > 4))
			{
				ModelState.AddModelError($"{nameof(Job)}.{nameof(JobInputModel.PrintPieceCount)}", "How many components must be between 1 and 4.");
			}

			if (Job.Print)
			{
				var printComponentCount = Math.Clamp(Job.PrintPieceCount ?? 1, 1, 4);
				for (var i = 1; i <= printComponentCount; i++)
				{
					ValidatePrintComponent(i);
				}
			}

			if (Job.TwoWayMatch && (!Job.MatchWayCount.HasValue || Job.MatchWayCount.Value < 2 || Job.MatchWayCount.Value > 5))
			{
				ModelState.AddModelError($"{nameof(Job)}.{nameof(JobInputModel.MatchWayCount)}", "How many way match must be between 2 and 5.");
			}

			if (Job.TwoWayMatch)
			{
				var matchComponentCount = Math.Clamp(Job.MatchWayCount ?? 2, 2, 5);
				for (var i = 1; i <= matchComponentCount; i++)
				{
					ValidateMatchComponent(i);
				}
			}
		}

		private void ValidatePrintComponent(int index)
		{
			var (name, size) = GetPrintComponent(index);
			if (string.IsNullOrWhiteSpace(name))
			{
				ModelState.AddModelError(GetPrintComponentNameKey(index), $"Component {index} name is required.");
			}

			if (string.IsNullOrWhiteSpace(size))
			{
				ModelState.AddModelError(GetPrintComponentSizeKey(index), $"Component {index} size is required.");
			}
		}

		private void ValidateMatchComponent(int index)
		{
			var (name, facingDirection) = GetMatchComponent(index);
			if (string.IsNullOrWhiteSpace(name))
			{
				ModelState.AddModelError(GetMatchComponentNameKey(index), $"Matching component {index} name is required.");
			}

			if (string.IsNullOrWhiteSpace(facingDirection))
			{
				ModelState.AddModelError(GetMatchComponentFacingDirectionKey(index), $"Matching component {index} facing direction is required.");
			}
		}

		private void ClearPrintComponent(int index)
		{
			SetPrintComponent(index, null, null);
			ModelState.Remove(GetPrintComponentNameKey(index));
			ModelState.Remove(GetPrintComponentSizeKey(index));
		}

		private void ClearMatchComponent(int index)
		{
			SetMatchComponent(index, null, null);
			ModelState.Remove(GetMatchComponentNameKey(index));
			ModelState.Remove(GetMatchComponentFacingDirectionKey(index));
		}

		private (string? Name, string? Size) GetPrintComponent(int index)
			=> index switch
			{
				1 => (Job.PrintComponent1Name, Job.PrintComponent1FacingDirection),
				2 => (Job.PrintComponent2Name, Job.PrintComponent2FacingDirection),
				3 => (Job.PrintComponent3Name, Job.PrintComponent3FacingDirection),
				4 => (Job.PrintComponent4Name, Job.PrintComponent4FacingDirection),
				_ => (null, null)
			};

		private void SetPrintComponent(int index, string? name, string? size)
		{
			switch (index)
			{
				case 1:
					Job.PrintComponent1Name = name;
					Job.PrintComponent1FacingDirection = size;
					break;
				case 2:
					Job.PrintComponent2Name = name;
					Job.PrintComponent2FacingDirection = size;
					break;
				case 3:
					Job.PrintComponent3Name = name;
					Job.PrintComponent3FacingDirection = size;
					break;
				case 4:
					Job.PrintComponent4Name = name;
					Job.PrintComponent4FacingDirection = size;
					break;
			}
		}

		private (string? Name, string? FacingDirection) GetMatchComponent(int index)
			=> index switch
			{
				1 => (Job.MatchComponent1, Job.MatchComponent1FacingDirection),
				2 => (Job.MatchComponent2, Job.MatchComponent2FacingDirection),
				3 => (Job.MatchComponent3, Job.MatchComponent3FacingDirection),
				4 => (Job.MatchComponent4, Job.MatchComponent4FacingDirection),
				5 => (Job.MatchComponent5, Job.MatchComponent5FacingDirection),
				_ => (null, null)
			};

		private void SetMatchComponent(int index, string? name, string? facingDirection)
		{
			switch (index)
			{
				case 1:
					Job.MatchComponent1 = name;
					Job.MatchComponent1FacingDirection = facingDirection;
					break;
				case 2:
					Job.MatchComponent2 = name;
					Job.MatchComponent2FacingDirection = facingDirection;
					break;
				case 3:
					Job.MatchComponent3 = name;
					Job.MatchComponent3FacingDirection = facingDirection;
					break;
				case 4:
					Job.MatchComponent4 = name;
					Job.MatchComponent4FacingDirection = facingDirection;
					break;
				case 5:
					Job.MatchComponent5 = name;
					Job.MatchComponent5FacingDirection = facingDirection;
					break;
			}
		}

		private static string GetPrintComponentNameKey(int index)
			=> $"{nameof(Job)}.{index switch
			{
				1 => nameof(JobInputModel.PrintComponent1Name),
				2 => nameof(JobInputModel.PrintComponent2Name),
				3 => nameof(JobInputModel.PrintComponent3Name),
				4 => nameof(JobInputModel.PrintComponent4Name),
				_ => throw new ArgumentOutOfRangeException(nameof(index))
			}}";

		private static string GetPrintComponentSizeKey(int index)
			=> $"{nameof(Job)}.{index switch
			{
				1 => nameof(JobInputModel.PrintComponent1FacingDirection),
				2 => nameof(JobInputModel.PrintComponent2FacingDirection),
				3 => nameof(JobInputModel.PrintComponent3FacingDirection),
				4 => nameof(JobInputModel.PrintComponent4FacingDirection),
				_ => throw new ArgumentOutOfRangeException(nameof(index))
			}}";

		private static string GetMatchComponentNameKey(int index)
			=> $"{nameof(Job)}.{index switch
			{
				1 => nameof(JobInputModel.MatchComponent1),
				2 => nameof(JobInputModel.MatchComponent2),
				3 => nameof(JobInputModel.MatchComponent3),
				4 => nameof(JobInputModel.MatchComponent4),
				5 => nameof(JobInputModel.MatchComponent5),
				_ => throw new ArgumentOutOfRangeException(nameof(index))
			}}";

		private static string GetMatchComponentFacingDirectionKey(int index)
			=> $"{nameof(Job)}.{index switch
			{
				1 => nameof(JobInputModel.MatchComponent1FacingDirection),
				2 => nameof(JobInputModel.MatchComponent2FacingDirection),
				3 => nameof(JobInputModel.MatchComponent3FacingDirection),
				4 => nameof(JobInputModel.MatchComponent4FacingDirection),
				5 => nameof(JobInputModel.MatchComponent5FacingDirection),
				_ => throw new ArgumentOutOfRangeException(nameof(index))
			}}";

		private int GetNextJobNumber()
		{
			var cs = _config.GetConnectionString("JobEntryDb");

			if (string.IsNullOrWhiteSpace(cs))
				return 1;

			using var conn = new SqlConnection(cs);
			conn.Open();

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT ISNULL(MAX(JobNumber),0) + 1 FROM Jobs";

			var result = cmd.ExecuteScalar();

			return result != null ? Convert.ToInt32(result) : 1;
		}

		private void SaveJobWithTasks(JobInputModel job)
		{
			var cs = _config.GetConnectionString("JobEntryDb");
            var jobFolderPath = GetJobFoldersBasePath();
            string? createdJobFolder = null;

			using var conn = new SqlConnection(cs);
			conn.Open();
			using var tx = conn.BeginTransaction();

			try
			{
				SaveJob(conn, tx, job);
                UpsertMailChart(conn, tx, job);
				GenerateTasks(conn, tx, job);
                createdJobFolder = JobFolderService.EnsureJobFolderTree(jobFolderPath, job.Customer, job.SubAccount, job.JobNumber, job.JobName);
				tx.Commit();
			}
			catch
			{
				tx.Rollback();
                if (!string.IsNullOrWhiteSpace(createdJobFolder))
                {
                    try
                    {
                        Directory.Delete(createdJobFolder, recursive: true);
                    }
                    catch
                    {
                        // Ignore cleanup failures and preserve the original error.
                    }
                }
				throw;
			}
		}

        private string GetJobFoldersBasePath()
            => _config["JobFoldersBasePath"]?.Trim()
                ?? @"P:\Danielle\JOB FOLDERS";

		private static void SaveJob(SqlConnection conn, SqlTransaction tx, JobInputModel job)
		{
			using var cmd = conn.CreateCommand();
			cmd.Transaction = tx;

			cmd.CommandText = @"
                INSERT INTO dbo.Jobs
                (
                    JobNumber,
                    JobName,
                    Customer,
                    SubAccount,
                    Quantity,
                    StartDate,
                    MailDate,
                    PostageClass,
                    PostageStyle,
                    CSR,
                    DataProcessing,
                    Sales,
                    RushJob,
                    JobSpeed,
                    [Print],
                    PrintPieceCount,
                    PrintComponent1Name,
                    PrintComponent1FacingDirection,
                    PrintComponent2Name,
                    PrintComponent2FacingDirection,
                    PrintComponent3Name,
                    PrintComponent3FacingDirection,
                    PrintComponent4Name,
                    PrintComponent4FacingDirection,
                    TwoWayMatch,
                    MatchWayCount,
                    MatchComponent1,
                    MatchComponent1FacingDirection,
                    MatchComponent2,
                    MatchComponent2FacingDirection,
                    MatchComponent3,
                    MatchComponent3FacingDirection,
                    MatchComponent4,
                    MatchComponent4FacingDirection,
                    MatchComponent5,
                    MatchComponent5FacingDirection,
                    Status
                )
                VALUES
                (
                    @JobNumber,
                    @JobName,
                    @Customer,
                    @SubAccount,
                    @Quantity,
                    @StartDate,
                    @MailDate,
                    @PostageClass,
                    @PostageStyle,
                    @Csr,
                    @DataProcessing,
                    @Sales,
                    @RushJob,
                    @JobSpeed,
                    @Print,
                    @PrintPieceCount,
                    @PrintComponent1Name,
                    @PrintComponent1FacingDirection,
                    @PrintComponent2Name,
                    @PrintComponent2FacingDirection,
                    @PrintComponent3Name,
                    @PrintComponent3FacingDirection,
                    @PrintComponent4Name,
                    @PrintComponent4FacingDirection,
                    @TwoWayMatch,
                    @MatchWayCount,
                    @MatchComponent1,
                    @MatchComponent1FacingDirection,
                    @MatchComponent2,
                    @MatchComponent2FacingDirection,
                    @MatchComponent3,
                    @MatchComponent3FacingDirection,
                    @MatchComponent4,
                    @MatchComponent4FacingDirection,
                    @MatchComponent5,
                    @MatchComponent5FacingDirection,
                    @Status
                )";

			cmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
			cmd.Parameters.AddWithValue("@JobName", job.JobName ?? "");
			cmd.Parameters.AddWithValue("@Customer", job.Customer ?? "");
			cmd.Parameters.AddWithValue("@SubAccount", job.SubAccount ?? "");
			cmd.Parameters.AddWithValue("@Quantity", (object?)job.Quantity ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@StartDate", job.StartDate ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@MailDate", job.MailDate ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@PostageClass", job.PostageClass ?? "");
			cmd.Parameters.AddWithValue("@PostageStyle", job.PostageStyle ?? "");
			cmd.Parameters.AddWithValue("@Csr", job.Csr ?? "");
			cmd.Parameters.AddWithValue("@DataProcessing", job.DataProcessing ?? "");
			cmd.Parameters.AddWithValue("@Sales", job.Sales ?? "");
            cmd.Parameters.AddWithValue("@RushJob", JobSchedules.IsExpedited(job.JobSpeed));
            cmd.Parameters.AddWithValue("@JobSpeed", JobSchedules.Normalize(job.JobSpeed));
			cmd.Parameters.AddWithValue("@Print", job.Print);
			cmd.Parameters.AddWithValue("@PrintPieceCount", (object?)job.PrintPieceCount ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent1Name", (object?)job.PrintComponent1Name ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent1FacingDirection", (object?)job.PrintComponent1FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent2Name", (object?)job.PrintComponent2Name ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent2FacingDirection", (object?)job.PrintComponent2FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent3Name", (object?)job.PrintComponent3Name ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent3FacingDirection", (object?)job.PrintComponent3FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent4Name", (object?)job.PrintComponent4Name ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@PrintComponent4FacingDirection", (object?)job.PrintComponent4FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@TwoWayMatch", job.TwoWayMatch);
			cmd.Parameters.AddWithValue("@MatchWayCount", (object?)job.MatchWayCount ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent1", (object?)job.MatchComponent1 ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent1FacingDirection", (object?)job.MatchComponent1FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent2", (object?)job.MatchComponent2 ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent2FacingDirection", (object?)job.MatchComponent2FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent3", (object?)job.MatchComponent3 ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent3FacingDirection", (object?)job.MatchComponent3FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent4", (object?)job.MatchComponent4 ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent4FacingDirection", (object?)job.MatchComponent4FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent5", (object?)job.MatchComponent5 ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@MatchComponent5FacingDirection", (object?)job.MatchComponent5FacingDirection ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@Status", "New");

			cmd.ExecuteNonQuery();
		}

        private static void UpsertMailChart(SqlConnection conn, SqlTransaction tx, JobInputModel job)
        {
            var mailChartTable = DatabaseSchema.FindTable(conn, tx, "MailChart", "mailchart");
            if (mailChartTable is null)
            {
                return;
            }

            var columns = DatabaseSchema.GetColumns(conn, mailChartTable, tx);
            var jobNumberColumn = DatabaseSchema.FindColumn(columns, "JobNumber", "job_number");
            if (jobNumberColumn is null)
            {
                return;
            }

            var kitColumn = DatabaseSchema.FindColumn(columns, "Kit", "kit");
            var customerColumn = DatabaseSchema.FindColumn(columns, "Customer", "customer");
            var jobNameColumn = DatabaseSchema.FindColumn(columns, "JobName", "job_name");
            var classColumn = DatabaseSchema.FindColumn(columns, "Class", "class");
            var aeColumn = DatabaseSchema.FindColumn(columns, "AE", "ae");
            var mailDateColumn = DatabaseSchema.FindColumn(columns, "MailDate", "mail_date");
            var quantityColumn = DatabaseSchema.FindColumn(columns, "Quantity", "quantity");
            var styleTruckColumn = DatabaseSchema.FindColumn(columns, "StyleTruck", "style_truck");
            var comminglerColumn = DatabaseSchema.FindColumn(columns, "Commingler", "commingler");

            var keyWhere = new List<string> { $"[{jobNumberColumn}] = @LookupJobNumber" };

            using var existsCmd = conn.CreateCommand();
            existsCmd.Transaction = tx;
            existsCmd.Parameters.AddWithValue("@LookupJobNumber", job.JobNumber);

            if (!string.IsNullOrWhiteSpace(kitColumn))
            {
                keyWhere.Add($"[{kitColumn}] = @LookupKit");
                existsCmd.Parameters.AddWithValue("@LookupKit", 0);
            }

            existsCmd.CommandText = $@"
                SELECT COUNT(*)
                FROM {mailChartTable.SqlName}
                WHERE {string.Join(" AND ", keyWhere)};";

            var exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;

            var values = new List<(string Column, string Parameter, object Value)>();
            AddInsertValue(values, jobNumberColumn, "@JobNumber", job.JobNumber);
            AddInsertValue(values, kitColumn, "@Kit", 0);
            AddInsertValue(values, customerColumn, "@Customer", job.Customer ?? string.Empty);
            AddInsertValue(values, jobNameColumn, "@JobName", job.JobName ?? string.Empty);
            AddInsertValue(values, classColumn, "@Class", job.PostageClass ?? string.Empty);
            AddInsertValue(values, aeColumn, "@AE", job.Csr ?? string.Empty);
            AddInsertValue(values, mailDateColumn, job.MailDate.HasValue ? "@MailDate" : "@MailDateNull", job.MailDate.HasValue ? job.MailDate.Value : DBNull.Value);
            AddInsertValue(values, quantityColumn, "@Quantity", job.Quantity ?? 0);
            AddInsertValue(values, styleTruckColumn, "@StyleTruck", string.Empty);
            AddInsertValue(values, comminglerColumn, "@Commingler", string.Empty);

            if (exists)
            {
                var updateValues = values
                    .Where(v => !string.Equals(v.Column, jobNumberColumn, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(v.Column, kitColumn, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (updateValues.Count == 0)
                {
                    return;
                }

                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = $@"
                    UPDATE {mailChartTable.SqlName}
                    SET {string.Join(", ", updateValues.Select(v => $"[{v.Column}] = {v.Parameter}"))}
                    WHERE {string.Join(" AND ", keyWhere)};";

                updateCmd.Parameters.AddWithValue("@LookupJobNumber", job.JobNumber);
                if (!string.IsNullOrWhiteSpace(kitColumn))
                {
                    updateCmd.Parameters.AddWithValue("@LookupKit", 0);
                }

                foreach (var value in updateValues)
                {
                    updateCmd.Parameters.AddWithValue(value.Parameter, value.Value);
                }

                updateCmd.ExecuteNonQuery();
                return;
            }

            using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;
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

            insertCmd.ExecuteNonQuery();
        }

		public class JobInputModel
		{
			public int JobNumber { get; set; }

			public string? Customer { get; set; }

			public string? JobName { get; set; }

			public string? SubAccount { get; set; }

			public int? Quantity { get; set; }

			public DateTime? StartDate { get; set; }

			public DateTime? MailDate { get; set; }

			public string? PostageClass { get; set; }

			public string? PostageStyle { get; set; }

			public string? Csr { get; set; }

			public string? DataProcessing { get; set; }

			public string? Sales { get; set; }

			public bool RushJob { get; set; }

			public bool Print { get; set; }

			public int? PrintPieceCount { get; set; }

			public string? PrintComponent1Name { get; set; }

			public string? PrintComponent1FacingDirection { get; set; }

			public string? PrintComponent2Name { get; set; }

			public string? PrintComponent2FacingDirection { get; set; }

			public string? PrintComponent3Name { get; set; }

			public string? PrintComponent3FacingDirection { get; set; }

			public string? PrintComponent4Name { get; set; }

			public string? PrintComponent4FacingDirection { get; set; }

			public bool TwoWayMatch { get; set; }

			public int? MatchWayCount { get; set; }

			public string? MatchComponent1 { get; set; }

			public string? MatchComponent1FacingDirection { get; set; }

			public string? MatchComponent2 { get; set; }

			public string? MatchComponent2FacingDirection { get; set; }

			public string? MatchComponent3 { get; set; }

			public string? MatchComponent3FacingDirection { get; set; }

			public string? MatchComponent4 { get; set; }

			public string? MatchComponent4FacingDirection { get; set; }

			public string? MatchComponent5 { get; set; }

			public string? MatchComponent5FacingDirection { get; set; }

            [Display(Name = "Schedule")]
			public string JobSpeed { get; set; } = JobSchedules.Standard;
		}

		private DateTime CalculateStartDate(JobInputModel job)
		{
			// If user provided StartDate, keep it as the explicit override.
			if (job.StartDate.HasValue)
				return job.StartDate.Value;

			int daysBack = JobSchedules.GetBusinessDaysBack(job.JobSpeed);

			// Fallback if MailDate missing (safety)
			if (!job.MailDate.HasValue)
				return DateTime.Today;

			return BusinessDaysBack(job.MailDate.Value, daysBack);
		}

		private DateTime BusinessDaysBack(DateTime date, int days)
		{
			int addedDays = 0;

			while (addedDays < days)
			{
				date = date.AddDays(-1);

				if (date.DayOfWeek != DayOfWeek.Saturday &&
					date.DayOfWeek != DayOfWeek.Sunday)
				{
					addedDays++;
				}
			}

			return date;
		}

        private static void GenerateTasks(SqlConnection conn, SqlTransaction tx, JobInputModel job)
        {
            var tasksTable = DatabaseSchema.FindTable(conn, tx, "Tasks", "tasks")
                ?? throw new InvalidOperationException("Could not find a Tasks table.");
            var taskColumns = DatabaseSchema.GetColumns(conn, tasksTable, tx);
            var jobNumberColumn = DatabaseSchema.FindColumn(taskColumns, "JobNumber", "job_number");
            var taskNumberColumn = DatabaseSchema.FindColumn(taskColumns, "TaskNumber", "task_number");
            var taskNameColumn = DatabaseSchema.FindColumn(taskColumns, "TaskName", "task_name");
            var stageColumn = DatabaseSchema.FindColumn(taskColumns, "Stage", "stage");
            var assignedToColumn = DatabaseSchema.FindColumn(taskColumns, "AssignedTo", "assigned_to");
            var assignee2Column = DatabaseSchema.FindColumn(taskColumns, "Assignee2", "assigned_to_2");
            var dueDateColumn = DatabaseSchema.FindColumn(taskColumns, "DueDate", "due_date");
            var statusColumn = DatabaseSchema.FindColumn(taskColumns, "Status", "status");
            var offsetFromStartDateColumn = DatabaseSchema.FindColumn(taskColumns, "OffsetFromStartDate", "offset_from_start_date");
            var offsetFromMailDateColumn = DatabaseSchema.FindColumn(taskColumns, "OffsetFromMailDate", "offset_from_mail_date");
            var createdAtColumn = DatabaseSchema.FindColumn(taskColumns, "CreatedAt", "created_at");

            if (jobNumberColumn is null || taskNameColumn is null)
            {
                throw new InvalidOperationException("The Tasks table is missing required job/task columns.");
            }

            var templates = TaskTemplateRepository.Load(conn, tx).ToList();

            foreach (var t in templates)
            {
                var dueDate = ResolveDueDate(t, job);
                var offsetFromStartDate = string.Equals(t.AnchorType, "MailDate", StringComparison.OrdinalIgnoreCase)
                    ? (object)DBNull.Value
                    : t.DaysOffset;
                var offsetFromMailDate = string.Equals(t.AnchorType, "MailDate", StringComparison.OrdinalIgnoreCase)
                    ? t.DaysOffset
                    : (object)DBNull.Value;

                var insertValues = new List<(string Column, string Parameter, object Value)>();
                AddInsertValue(insertValues, jobNumberColumn, "@JobNumber", job.JobNumber);
                AddInsertValue(insertValues, taskNumberColumn, "@TaskNumber", t.SortOrder);
                AddInsertValue(insertValues, taskNameColumn, "@TaskName", t.TaskName);
                AddInsertValue(insertValues, stageColumn, "@Stage", string.IsNullOrWhiteSpace(t.Stage) ? DBNull.Value : t.Stage);
                AddInsertValue(insertValues, assignedToColumn, "@AssignedTo", (object?)ResolveAssignedTo(t.AssignedTo, job) ?? DBNull.Value);
                AddInsertValue(insertValues, assignee2Column, "@AssignedTo2", (object?)ResolveAssignedTo(t.AssignedTo2, job) ?? DBNull.Value);
                AddInsertValue(insertValues, dueDateColumn, "@DueDate", dueDate.HasValue ? dueDate.Value : DBNull.Value);
                AddInsertValue(insertValues, offsetFromStartDateColumn, "@OffsetFromStartDate", offsetFromStartDate);
                AddInsertValue(insertValues, offsetFromMailDateColumn, "@OffsetFromMailDate", offsetFromMailDate);
                AddInsertValue(insertValues, statusColumn, "@Status", "Scheduled");
                AddInsertValue(insertValues, createdAtColumn, "@CreatedAt", DateTime.UtcNow);

                using var insert = conn.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = $@"
                    INSERT INTO {tasksTable.SqlName}
                    (
                        {string.Join(", ", insertValues.Select(v => $"[{v.Column}]"))}
                    )
                    VALUES
                    (
                        {string.Join(", ", insertValues.Select(v => v.Parameter))}
                    )";

                foreach (var value in insertValues)
                {
                    insert.Parameters.AddWithValue(value.Parameter, value.Value);
                }

                insert.ExecuteNonQuery();
            }
        }

        private static void AddInsertValue(List<(string Column, string Parameter, object Value)> values, string? column, string parameter, object value)
        {
            if (!string.IsNullOrWhiteSpace(column))
            {
                values.Add((column, parameter, value));
            }
        }

        private static DateTime BusinessDaysFrom(DateTime date, int offset)
        {
            int direction = offset >= 0 ? 1 : -1;
            int days = Math.Abs(offset);

            int added = 0;

            while (added < days)
            {
                date = date.AddDays(direction);

                if (date.DayOfWeek != DayOfWeek.Saturday &&
                    date.DayOfWeek != DayOfWeek.Sunday)
                {
                    added++;
                }
            }

            return date;
        }

        private static DateTime? ResolveDueDate(TaskTemplateDefault template, JobInputModel job)
        {
            if (string.Equals(template.AnchorType, "MailDate", StringComparison.OrdinalIgnoreCase))
            {
                if (job.MailDate.HasValue)
                {
                    return BusinessDaysFrom(job.MailDate.Value, template.DaysOffset);
                }

                return null;
            }

            if (job.StartDate.HasValue)
            {
                return BusinessDaysFrom(job.StartDate.Value, template.DaysOffset);
            }

            if (job.MailDate.HasValue)
            {
                return BusinessDaysFrom(job.MailDate.Value, template.DaysOffset);
            }

            return null;
        }

        private static string? ResolveAssignedTo(string? templateAssignee, JobInputModel job)
            => templateAssignee switch
            {
                "CSR" => string.IsNullOrWhiteSpace(job.Csr) ? null : job.Csr,
                "DP" => string.IsNullOrWhiteSpace(job.DataProcessing) ? null : job.DataProcessing,
                null => null,
                _ => templateAssignee
            };
	}
}
