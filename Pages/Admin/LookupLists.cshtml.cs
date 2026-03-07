using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace JobEntryApp.Pages.Admin
{
    public class LookupListsModel : PageModel
    {
        private readonly IConfiguration _config;

        public LookupListsModel(IConfiguration config)
        {
            _config = config;
        }

        public record LookupItem(int Id, string Name, string? CustomerName = null);

        public List<LookupItem> Customers { get; set; } = new();
        public List<LookupItem> SubAccounts { get; set; } = new();
        public List<LookupItem> CsrList { get; set; } = new();
        public List<LookupItem> DataProcessingList { get; set; } = new();
        public List<LookupItem> SalesList { get; set; } = new();

        [TempData] public string? StatusMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public void OnGet() => LoadAll();

        // ── Customers ──────────────────────────────────────────
        public IActionResult OnPostAddCustomer(string name)
        {
            Execute("INSERT INTO dbo.Customers (CustomerName) VALUES (@Name);", name);
            StatusMessage = $"Customer '{name}' added.";
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteCustomer(int id)
        {
            ExecuteById("DELETE FROM dbo.Customers WHERE CustomerID = @Id;", id);
            StatusMessage = "Customer deleted.";
            return RedirectToPage();
        }

        // ── Sub Accounts ────────────────────────────────────────
        public IActionResult OnPostAddSubAccount(string customerName, string subAccountName)
        {
            Execute(
                "INSERT INTO dbo.SubAccounts (CustomerName, SubAccountName) VALUES (@Name, @Sub);",
                customerName, subAccountName);
            StatusMessage = $"Sub-account '{subAccountName}' added under '{customerName}'.";
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteSubAccount(int id)
        {
            ExecuteById("DELETE FROM dbo.SubAccounts WHERE SubAccountID = @Id;", id);
            StatusMessage = "Sub-account deleted.";
            return RedirectToPage();
        }

        // ── CSR ─────────────────────────────────────────────────
        public IActionResult OnPostAddCsr(string name)
        {
            Execute("INSERT INTO dbo.CSR (CSRName) VALUES (@Name);", name);
            StatusMessage = $"CSR '{name}' added.";
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteCsr(int id)
        {
            ExecuteById("DELETE FROM dbo.CSR WHERE CSRID = @Id;", id);
            StatusMessage = "CSR deleted.";
            return RedirectToPage();
        }

        // ── Data Processing ────────────────────────────────────
        public IActionResult OnPostAddDataProcessing(string name)
        {
            Execute("INSERT INTO dbo.DataProcessing (DataProcessingName) VALUES (@Name);", name);
            StatusMessage = $"Data Processing '{name}' added.";
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteDataProcessing(int id)
        {
            ExecuteById("DELETE FROM dbo.DataProcessing WHERE DataProcessingID = @Id;", id);
            StatusMessage = "Data Processing entry deleted.";
            return RedirectToPage();
        }

        // ── Sales ───────────────────────────────────────────────
        public IActionResult OnPostAddSales(string name)
        {
            Execute("INSERT INTO dbo.Sales (SalesName) VALUES (@Name);", name);
            StatusMessage = $"Sales '{name}' added.";
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteSales(int id)
        {
            ExecuteById("DELETE FROM dbo.Sales WHERE SalesID = @Id;", id);
            StatusMessage = "Sales entry deleted.";
            return RedirectToPage();
        }

        // ── Helpers ─────────────────────────────────────────────
        private void LoadAll()
        {
            var cs = _config.GetConnectionString("JobEntryDb");
            if (string.IsNullOrWhiteSpace(cs)) return;

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();

                Customers = ReadItems(conn,
                    "SELECT CustomerID, CustomerName FROM dbo.Customers ORDER BY CustomerName;",
                    r => new LookupItem(r.GetInt32(0), r.GetString(1)));

                SubAccounts = ReadItems(conn,
                    "SELECT SubAccountID, SubAccountName, CustomerName FROM dbo.SubAccounts ORDER BY CustomerName, SubAccountName;",
                    r => new LookupItem(r.GetInt32(0), r.GetString(1), r.GetString(2)));

                CsrList = ReadItems(conn,
                    "SELECT CSRID, CSRName FROM dbo.CSR ORDER BY CSRName;",
                    r => new LookupItem(r.GetInt32(0), r.GetString(1)));

                DataProcessingList = ReadItems(conn,
                    "SELECT DataProcessingID, DataProcessingName FROM dbo.DataProcessing ORDER BY DataProcessingName;",
                    r => new LookupItem(r.GetInt32(0), r.GetString(1)));

                SalesList = ReadItems(conn,
                    "SELECT SalesID, SalesName FROM dbo.Sales ORDER BY SalesName;",
                    r => new LookupItem(r.GetInt32(0), r.GetString(1)));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Could not load lookup lists: " + ex.Message;
            }
        }

        private static List<LookupItem> ReadItems(SqlConnection conn, string sql, Func<SqlDataReader, LookupItem> map)
        {
            var list = new List<LookupItem>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(map(reader));
            return list;
        }

        private void Execute(string sql, string name, string? extra = null)
        {
            var cs = _config.GetConnectionString("JobEntryDb");
            if (string.IsNullOrWhiteSpace(cs)) return;

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@Name", name);
                if (extra is not null)
                    cmd.Parameters.AddWithValue("@Sub", extra);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Database error: " + ex.Message;
            }
        }

        private void ExecuteById(string sql, int id)
        {
            var cs = _config.GetConnectionString("JobEntryDb");
            if (string.IsNullOrWhiteSpace(cs)) return;

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Database error: " + ex.Message;
            }
        }
    }
}
