namespace JobEntryApp.Models
{
    public class JobComponentModel
    {
        public int ComponentID { get; set; }
        public int JobNumber { get; set; }
        public string ComponentName { get; set; } = string.Empty;
        public string? FacingDirection { get; set; }
        public int ComponentOrder { get; set; }

        /// <summary>"Print" or "Match"</summary>
        public string Type { get; set; } = "Print";
    }
}
