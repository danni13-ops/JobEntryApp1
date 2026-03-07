using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobEntryApp.Pages
{
	public class TaskTemplatesModel : PageModel
	{
		public IActionResult OnGet()
		{
			// Redirect users away from this page since Task Templates
			// should not be visible in the UI.
			return RedirectToPage("/Index");
		}
	}
}