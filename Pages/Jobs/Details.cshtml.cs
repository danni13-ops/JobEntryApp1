using JobEntryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

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

        public IActionResult OnGet()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

            using var conn = new SqlConnection(cs);
            using SqlCommand cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT JobNumber, JobName, MailDate, Customer, SubAccount, Csr, DataProcessing,
                       Quantity, PostageStyle, PostageClass, Sales, StartDate, Status,
                       [Print], TwoWayMatch,
                       MatchWayCount, MatchComponent1, MatchComponent2, MatchComponent3, MatchComponent4, MatchComponent5,
                       PrintPieceCount, PrintComponent1Name, PrintComponent1FacingDirection,
                       PrintComponent2Name, PrintComponent2FacingDirection,
                       PrintComponent3Name, PrintComponent3FacingDirection,
                       PrintComponent4Name, PrintComponent4FacingDirection
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
                MatchWayCount = reader.IsDBNull(15) ? (int?)null : reader.GetInt32(15),
                MatchComponent1 = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                MatchComponent2 = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                MatchComponent3 = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                MatchComponent4 = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),
                MatchComponent5 = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                PrintPieceCount = reader.IsDBNull(21) ? (int?)null : reader.GetInt32(21),
                PrintComponent1Name = reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                PrintComponent1FacingDirection = reader.IsDBNull(23) ? string.Empty : reader.GetString(23),
                PrintComponent2Name = reader.IsDBNull(24) ? string.Empty : reader.GetString(24),
                PrintComponent2FacingDirection = reader.IsDBNull(25) ? string.Empty : reader.GetString(25),
                PrintComponent3Name = reader.IsDBNull(26) ? string.Empty : reader.GetString(26),
                PrintComponent3FacingDirection = reader.IsDBNull(27) ? string.Empty : reader.GetString(27),
                PrintComponent4Name = reader.IsDBNull(28) ? string.Empty : reader.GetString(28),
                PrintComponent4FacingDirection = reader.IsDBNull(29) ? string.Empty : reader.GetString(29)
            };

            return Page();
        }
    }
}
