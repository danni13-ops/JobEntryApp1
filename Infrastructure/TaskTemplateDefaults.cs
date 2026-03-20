namespace JobEntryApp.Infrastructure
{
    public sealed record TaskTemplateDefault(
        int SortOrder,
        string TaskName,
        string Stage,
        string? AssignedTo,
        string? AssignedTo2,
        int DaysOffset,
        string AnchorType,
        bool? IsCritical,
        string? JobSpeed);

    public static class TaskTemplateDefaults
    {
        public static IReadOnlyList<TaskTemplateDefault> All { get; } =
        [
            new(1, "Open Job Jacket, Send out info, Assigned DP, Job Scheduled", "Initiate", null, null, -19, "StartDate", null, null),
            new(2, "Data and Instructions Received *** START DATE FOR PROJECT*** Send Job Specs to DP & Send client schedule", "Initiate", null, null, -15, "StartDate", null, null),
            new(3, "Receive, Send and get approval on Counts", "Prep", null, null, -13, "StartDate", null, null),
            new(4, "Send Postage Request and Order Stamps", "Prep", null, null, -13, "StartDate", null, null),
            new(5, "Create Inventory Grid and Update inventory log w/Job #", "Prep", null, null, -12, "StartDate", null, null),
            new(6, "Signoffs Received, proofed, revisions handled, PDF approved", "Prep", null, null, -11, "StartDate", null, null),
            new(7, "Create Laser, Fold, Inkjet, Stamping and Inserting Control Cards", "Prep", null, null, -11, "StartDate", null, null),
            new(8, "Request live form, proof 2x, check barcode & window & send scanlines", "Prep", null, null, -9, "StartDate", null, null),
            new(9, "Laser Signoffs Approved", "Pre-Production", null, null, -9, "StartDate", null, null),
            new(10, "Make up Packages (ensure form fits into BRE and proper clearance)", "Pre-Production", null, null, -9, "StartDate", null, null),
            new(11, "Inserting Signoffs are sent", "Pre-Production", null, null, -7, "StartDate", null, null),
            new(12, "Ensure postage received and pick up stamps", "Pre-Production", null, null, -9, "StartDate", null, null),
            new(13, "Laser, Inkjet, Affixing and Folding Control Cards to Production", "Production", null, null, -9, "StartDate", null, null),
            new(14, "Insertion Control Cards to Production", "Production", null, null, -9, "StartDate", null, null),
            new(15, "Production Counts", "Production", null, null, -7, "StartDate", null, null),
            new(16, "QC Pulls", "Production", null, null, -7, "StartDate", null, null),
            new(17, "Upload Firebird Files, Create 3602, Send Postage", "Finalize", null, null, -2, "MailDate", null, null),
            new(18, "Send out Lives and Samples", "Finalize", null, null, -2, "MailDate", null, null),
            new(19, "Mail Date", "Finalize", null, null, 0, "MailDate", null, null),
            new(20, "End of Job Inventory", "Finalize", null, null, 2, "MailDate", null, null),
            new(21, "Send Postal Receipt", "Finalize", null, null, 5, "MailDate", null, null),
            new(22, "Invoice Job", "Finalize", null, null, 7, "MailDate", null, null)
        ];
    }
}
