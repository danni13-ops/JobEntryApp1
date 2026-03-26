global using Microsoft.Data.SqlClient;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace JobEntryApp.Pages.Postage
{
	public class IndexModel : PageModel
	{
		private readonly IConfiguration _config;

		public List<PostageJob> StampJobs { get; set; } = new();
		public List<PostageJob> PermitJobs { get; set; } = new();

		public IndexModel(IConfiguration config)
		{
			_config = config;
		}

		public void OnGet()
		{
			using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
			conn.Open();

			SqlCommand cmd = new SqlCommand(@"
                SELECT JobNumber, Customer, JobName, MailDate, Quantity, PostageStyle
                FROM MailChart
                WHERE MailDate >= GETDATE()", conn);

			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				var job = new PostageJob
				{
					JobNumber = reader.GetInt32(0),
					Customer = reader.GetString(1),
					JobName = reader.GetString(2),
					MailDate = reader.GetDateTime(3),
					Quantity = reader.GetInt32(4)
				};

				var style = reader.GetString(5);

				if (style == "Stamp")
					StampJobs.Add(job);

				if (style == "Permit" || style == "Indicia")
					PermitJobs.Add(job);
			}
		}

		public class PostageJob
		{
			public int JobNumber { get; set; }

			public string Customer { get; set; } = string.Empty;

			public string JobName { get; set; } = string.Empty;

			public DateTime MailDate { get; set; }

			public int Quantity { get; set; }
		}
	}
}