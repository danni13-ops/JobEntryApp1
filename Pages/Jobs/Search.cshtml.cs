using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JobEntryApp.Pages.Jobs
{
    public class SearchModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public int? JobNumber { get; set; }

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (JobNumber.HasValue)
            {
                if (!IsValidJobNumber())
                {
                    ErrorMessage = "Please enter a valid job number.";
                    return Page();
                }
                return RedirectToPage("/Jobs/Details", new { jobNumber = JobNumber.Value });
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            if (!IsValidJobNumber())
            {
                ErrorMessage = "Please enter a valid job number.";
                return Page();
            }
            return RedirectToPage("/Jobs/Details", new { jobNumber = JobNumber!.Value });
        }

        private bool IsValidJobNumber() => JobNumber.HasValue && JobNumber.Value > 0;
    }
}
