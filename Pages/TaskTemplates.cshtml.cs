using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace JobEntryApp.Pages
{
	public class TaskTemplatesModel : PageModel
	{
		public List<TaskTemplate> TaskTemplates { get; set; } = new();

		public void OnGet()
		{
			TaskTemplates = new List<TaskTemplate>
			{
				new TaskTemplate
				{
					TaskName = "Job Scheduled",
					Stage = "Initiate",
					OffsetDays = -21
				},
				new TaskTemplate
				{
					TaskName = "Counts Approved",
					Stage = "Prep",
					OffsetDays = -14
				},
				new TaskTemplate
				{
					TaskName = "Data Received",
					Stage = "Prep",
					OffsetDays = -10
				},
				new TaskTemplate
				{
					TaskName = "Production Start",
					Stage = "Production",
					OffsetDays = -2
				},
				new TaskTemplate
				{
					TaskName = "Mail Date",
					Stage = "Finalize",
					OffsetDays = 0
				}
			};
		}
	}

	public class TaskTemplate
	{
		public string TaskName { get; set; } = string.Empty;
		public string Stage { get; set; } = string.Empty;
		public int OffsetDays { get; set; }
	}
}