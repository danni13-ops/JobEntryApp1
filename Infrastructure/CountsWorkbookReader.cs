using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JobEntryApp.Infrastructure
{
    public static class CountsWorkbookReader
    {
        private static readonly Regex CellReferenceRegex = new(@"([A-Z]+)(\d+)", RegexOptions.Compiled);

        public static bool TryRead(string path, out CountWorkbookData workbook)
        {
            workbook = new CountWorkbookData
            {
                SourceFileName = Path.GetFileName(path)
            };

            try
            {
                workbook = Read(path);
                return true;
            }
            catch
            {
                workbook = new CountWorkbookData
                {
                    SourceFileName = Path.GetFileName(path)
                };
                return false;
            }
        }

        public static CountWorkbookData Read(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            var workbookXml = LoadXml(archive, "xl/workbook.xml");
            var workbookRelationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            var sharedStrings = LoadSharedStrings(archive);

            XNamespace spreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            var outputSheet = workbookXml
                .Descendants(spreadsheetNs + "sheet")
                .FirstOrDefault(sheet => string.Equals((string?)sheet.Attribute("name"), "Output", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The workbook does not contain an Output sheet.");

            var relationId = (string?)outputSheet.Attribute(relationshipNs + "id")
                ?? throw new InvalidOperationException("The Output sheet relationship is missing.");

            var target = workbookRelationships
                .Descendants(packageNs + "Relationship")
                .FirstOrDefault(r => string.Equals((string?)r.Attribute("Id"), relationId, StringComparison.Ordinal))
                ?.Attribute("Target")
                ?.Value
                ?? throw new InvalidOperationException("The Output sheet target is missing.");

            var normalizedTarget = target.Replace('\\', '/').TrimStart('/');
            if (!normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTarget = $"xl/{normalizedTarget}";
            }

            var sheetXml = LoadXml(archive, normalizedTarget);
            var kits = new List<KitCount>();
            int? total = null;

            foreach (var row in sheetXml.Descendants(spreadsheetNs + "row"))
            {
                string? kitText = null;
                string? quantityText = null;

                foreach (var cell in row.Elements(spreadsheetNs + "c"))
                {
                    var reference = (string?)cell.Attribute("r");
                    if (string.IsNullOrWhiteSpace(reference))
                    {
                        continue;
                    }

                    var match = CellReferenceRegex.Match(reference);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var column = match.Groups[1].Value;
                    var value = GetCellValue(cell, sharedStrings, spreadsheetNs);
                    if (string.Equals(column, "G", StringComparison.OrdinalIgnoreCase))
                    {
                        kitText = value;
                    }
                    else if (string.Equals(column, "H", StringComparison.OrdinalIgnoreCase))
                    {
                        quantityText = value;
                    }
                }

                if (!TryParseWholeNumber(quantityText, out var quantity) || quantity <= 0)
                {
                    continue;
                }

                if (TryParseWholeNumber(kitText, out var kitNumber) && kitNumber > 0)
                {
                    kits.Add(new KitCount(kitNumber, quantity));
                }
                else
                {
                    total = quantity;
                }
            }

            return new CountWorkbookData
            {
                SourceFileName = Path.GetFileName(path),
                Kits = kits
                    .GroupBy(k => k.Kit)
                    .Select(g => new KitCount(g.Key, g.Sum(x => x.Quantity)))
                    .OrderBy(k => k.Kit)
                    .ToList(),
                TotalQuantity = total ?? kits.Sum(k => k.Quantity)
            };
        }

        private static XDocument LoadXml(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName)
                ?? throw new InvalidOperationException($"Workbook entry '{entryName}' was not found.");

            using var stream = entry.Open();
            return XDocument.Load(stream);
        }

        private static List<string> LoadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
            {
                return new List<string>();
            }

            XNamespace spreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            using var stream = entry.Open();
            var sharedStringsXml = XDocument.Load(stream);
            return sharedStringsXml
                .Descendants(spreadsheetNs + "si")
                .Select(si => string.Concat(si.Descendants(spreadsheetNs + "t").Select(t => t.Value)))
                .ToList();
        }

        private static string? GetCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace spreadsheetNs)
        {
            var cellType = (string?)cell.Attribute("t");
            if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cell.Descendants(spreadsheetNs + "t").Select(t => t.Value));
            }

            var rawValue = cell.Element(spreadsheetNs + "v")?.Value;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex)
                && sharedStringIndex >= 0
                && sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex];
            }

            return rawValue;
        }

        private static bool TryParseWholeNumber(string? value, out int number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return true;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalNumber))
            {
                number = decimal.ToInt32(decimal.Truncate(decimalNumber));
                return true;
            }

            return false;
        }
    }

    public sealed class CountWorkbookData
    {
        public string SourceFileName { get; init; } = string.Empty;
        public int TotalQuantity { get; init; }
        public List<KitCount> Kits { get; init; } = new();
    }

    public readonly record struct KitCount(int Kit, int Quantity);
}
