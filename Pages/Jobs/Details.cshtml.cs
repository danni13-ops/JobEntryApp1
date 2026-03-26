using JobEntryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SqlClient;

namespace JobEntryApp.Pages.Jobs
{
    public class DetailsModel : PageModel
    {
        private readonly IConfiguration _config;

        public DetailsModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty(SupportsGet = true)]
        public int JobNumber { get; set; }

        public JobModel? Job { get; set; }

        public IActionResult OnGet(int jobNumber)
        {
            if (jobNumber <= 0)
                return RedirectToPage("/Jobs/Index");

            try
            {
                var cs = _config.GetConnectionString("JobEntryDb")
                    ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

                using var conn = new SqlConnection(cs);
                using SqlCommand cmd = conn.CreateCommand();

                cmd.CommandText = @"
                SELECT JobNumber, JobName, MailDate, Customer, SubAccount, Csr, DataProcessing,
                       Quantity, PostageStyle, PostageClass, Sales, StartDate, Status,
                       [Print], TwoWayMatch, RushJob,
                       MatchWayCount,
                       MatchComponent1, MatchComponent2, MatchComponent3, MatchComponent4, MatchComponent5,
                       MatchComponent1FacingDirection, MatchComponent2FacingDirection,
                       MatchComponent3FacingDirection, MatchComponent4FacingDirection, MatchComponent5FacingDirection,
                       PrintPieceCount,
                       PrintComponent1Name, PrintComponent1FacingDirection,
                       PrintComponent2Name, PrintComponent2FacingDirection,
                       PrintComponent3Name, PrintComponent3FacingDirection,
                       PrintComponent4Name, PrintComponent4FacingDirection,
                       PrintComponent5Name, PrintComponent5FacingDirection
                FROM dbo.Jobs
                WHERE JobNumber = @jobNumber;";
                cmd.Parameters.AddWithValue("@jobNumber", JobNumber);

                conn.Open();
                using SqlDataReader reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    return NotFound();
                }

                Job = new JobModel
                {
                    JobNumber = reader.GetInt32(0),
                    JobName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    MailDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                    Customer = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    SubAccount = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Csr = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    DataProcessing = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Quantity = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    PostageStyle = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    PostageClass = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    Sales = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    StartDate = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                    Status = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    Print = !reader.IsDBNull(13) && reader.GetBoolean(13),
                    TwoWayMatch = !reader.IsDBNull(14) && reader.GetBoolean(14),
                    RushJob = !reader.IsDBNull(15) && reader.GetBoolean(15),
                    MatchWayCount = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                    MatchComponent1 = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                    MatchComponent2 = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                    MatchComponent3 = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),
                    MatchComponent4 = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                    MatchComponent5 = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                    MatchComponent1FacingDirection = reader.IsDBNull(22) ? null : reader.GetString(22),
                    MatchComponent2FacingDirection = reader.IsDBNull(23) ? null : reader.GetString(23),
                    MatchComponent3FacingDirection = reader.IsDBNull(24) ? null : reader.GetString(24),
                    MatchComponent4FacingDirection = reader.IsDBNull(25) ? null : reader.GetString(25),
                    MatchComponent5FacingDirection = reader.IsDBNull(26) ? null : reader.GetString(26),
                    PrintPieceCount = reader.IsDBNull(27) ? (int?)null : reader.GetInt32(27),
                    PrintComponent1Name = reader.IsDBNull(28) ? string.Empty : reader.GetString(28),
                    PrintComponent1FacingDirection = reader.IsDBNull(29) ? string.Empty : reader.GetString(29),
                    PrintComponent2Name = reader.IsDBNull(30) ? string.Empty : reader.GetString(30),
                    PrintComponent2FacingDirection = reader.IsDBNull(31) ? string.Empty : reader.GetString(31),
                    PrintComponent3Name = reader.IsDBNull(32) ? string.Empty : reader.GetString(32),
                    PrintComponent3FacingDirection = reader.IsDBNull(33) ? string.Empty : reader.GetString(33),
                    PrintComponent4Name = reader.IsDBNull(34) ? string.Empty : reader.GetString(34),
                    PrintComponent4FacingDirection = reader.IsDBNull(35) ? string.Empty : reader.GetString(35),
                    PrintComponent5Name = reader.IsDBNull(36) ? string.Empty : reader.GetString(36),
                    PrintComponent5FacingDirection = reader.IsDBNull(37) ? string.Empty : reader.GetString(37)
                };
            }
            catch
            {
                return RedirectToPage("/Jobs/Index");
            }

            return Page();
        }
    }
}
