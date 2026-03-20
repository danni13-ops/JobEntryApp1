namespace JobEntryApp.Infrastructure
{
    public static class JobFolderService
    {
        public static IReadOnlyList<string> StandardSubfolders { get; } =
        [
            "DATA",
            "ART",
            "REPORTS",
            "SIGNOFFS",
            "POSTAL",
            "CONTROL CARDS",
            "PO& INSTRUCTIONS"
        ];

        public static string BuildJobFolderPath(string basePath, string? customer, string? subAccount, int jobNumber, string? jobName)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new InvalidOperationException("Job folder base path is not configured.");
            }

            var customerFolder = NormalizeSegment(customer, "UNKNOWN CUSTOMER");
            var jobFolderName = NormalizeSegment($"{jobNumber} {jobName}".Trim(), jobNumber.ToString());

            var path = Path.Combine(basePath, customerFolder);
            if (!string.IsNullOrWhiteSpace(subAccount))
            {
                path = Path.Combine(path, NormalizeSegment(subAccount, "GENERAL"));
            }

            return Path.Combine(path, jobFolderName);
        }

        public static string EnsureJobFolderTree(string basePath, string? customer, string? subAccount, int jobNumber, string? jobName)
        {
            var root = Path.GetPathRoot(basePath);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                throw new InvalidOperationException($"Job folder root '{root}' is not available.");
            }

            Directory.CreateDirectory(basePath);
            var jobFolderPath = BuildJobFolderPath(basePath, customer, subAccount, jobNumber, jobName);
            Directory.CreateDirectory(jobFolderPath);

            foreach (var subfolder in StandardSubfolders)
            {
                Directory.CreateDirectory(Path.Combine(jobFolderPath, subfolder));
            }

            return jobFolderPath;
        }

        private static string NormalizeSegment(string? value, string fallback)
        {
            var cleaned = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                cleaned = cleaned.Replace(invalidChar, '-');
            }

            cleaned = cleaned.Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }
    }
}
