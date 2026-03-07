using System.ComponentModel.DataAnnotations;

namespace JobEntryApp.Models
{
    public class JobModel
    {
        public int JobNumber { get; set; }

        [Required(ErrorMessage = "Job Name is required.")]
        public string JobName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mail Date is required.")]
        public DateTime? MailDate { get; set; }

        [Required(ErrorMessage = "Customer is required.")]
        public string Customer { get; set; } = string.Empty;

        public string? KitName { get; set; }
        public string? SubAccount { get; set; }
        public string? Csr { get; set; }
        public string? DataProcessing { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int? Quantity { get; set; }

        public string? PostageStyle { get; set; }
        public string? PostageClass { get; set; }
        public string? Sales { get; set; }
        public string? Commingler { get; set; }
        public DateTime? StartDate { get; set; }
        public string Status { get; set; } = "New";

        public bool Print { get; set; }
        public bool TwoWayMatch { get; set; }

        public int? MatchWayCount { get; set; }
        public string? MatchComponent1 { get; set; }
        public string? MatchComponent2 { get; set; }
        public string? MatchComponent3 { get; set; }
        public string? MatchComponent4 { get; set; }
        public string? MatchComponent5 { get; set; }

        public int? PrintPieceCount { get; set; }
        public string? PrintComponent1Name { get; set; }
        public string? PrintComponent1FacingDirection { get; set; }
        public string? PrintComponent2Name { get; set; }
        public string? PrintComponent2FacingDirection { get; set; }
        public string? PrintComponent3Name { get; set; }
        public string? PrintComponent3FacingDirection { get; set; }
        public string? PrintComponent4Name { get; set; }
        public string? PrintComponent4FacingDirection { get; set; }
        public string? PrintComponent5Name { get; set; }
        public string? PrintComponent5FacingDirection { get; set; }

        public string? MatchComponent1FacingDirection { get; set; }
        public string? MatchComponent2FacingDirection { get; set; }
        public string? MatchComponent3FacingDirection { get; set; }
        public string? MatchComponent4FacingDirection { get; set; }
        public string? MatchComponent5FacingDirection { get; set; }

        public bool RushJob { get; set; }
    }
}
