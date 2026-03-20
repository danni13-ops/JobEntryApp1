using JobEntryApp.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace JobEntryApp.Pages
{
	public class TaskTemplatesModel : PageModel
	{
		private readonly IConfiguration _config;

		public TaskTemplatesModel(IConfiguration config)
		{
			_config = config;
		}

		public List<TaskTemplate> TaskTemplates { get; set; } = new();

		public void OnGet()
		{
			var cs = _config.GetConnectionString("JobEntryDb");
			if (string.IsNullOrWhiteSpace(cs))
			{
				TaskTemplates = TaskTemplateDefaults.All
					.Select(t => new TaskTemplate
					{
						SortOrder = t.SortOrder,
						TaskName = t.TaskName,
						Stage = t.Stage,
						OffsetDays = t.DaysOffset,
						AnchorType = t.AnchorType
					})
					.ToList();
				return;
			}

			using var conn = new SqlConnection(cs);
			conn.Open();

			TaskTemplates = TaskTemplateRepository.Load(conn)
				.Select(t => new TaskTemplate
				{
					SortOrder = t.SortOrder,
					TaskName = t.TaskName,
					Stage = t.Stage,
					OffsetDays = t.DaysOffset,
					AnchorType = t.AnchorType
				})
				.ToList();
		}
	}

	public class TaskTemplate
	{
		public int SortOrder { get; set; }
		public string TaskName { get; set; } = string.Empty;
		public string Stage { get; set; } = string.Empty;
		public int OffsetDays { get; set; }
		public string AnchorType { get; set; } = "StartDate";
	}
}
