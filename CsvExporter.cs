using System.Text;

namespace NTFSReport;

public static class CsvExporter
{
    public static void Export(ScanResult result, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Path,FolderDepth,BrokenInheritance,AccessDenied,Identity,AccessType,Rights,RightsDecoded,IsInherited,InheritanceFlags,PropagationFlags");

        foreach (var folder in result.AllFolders)
        {
            if (folder.AccessDenied)
            {
                sb.Append(CsvField(folder.Path)).Append(',');
                sb.Append(folder.Depth).Append(',');
                sb.Append(folder.InheritanceBroken).Append(',');
                sb.Append("TRUE").Append(',');
                sb.AppendLine(",,,,,");
                continue;
            }

            if (folder.Permissions.Count == 0)
            {
                sb.Append(CsvField(folder.Path)).Append(',');
                sb.Append(folder.Depth).Append(',');
                sb.Append(folder.InheritanceBroken).Append(',');
                sb.Append("FALSE").Append(',');
                sb.AppendLine(",,,,,");
                continue;
            }

            foreach (var perm in folder.Permissions)
            {
                sb.Append(CsvField(folder.Path)).Append(',');
                sb.Append(folder.Depth).Append(',');
                sb.Append(folder.InheritanceBroken).Append(',');
                sb.Append("FALSE").Append(',');
                sb.Append(CsvField(perm.Identity)).Append(',');
                sb.Append(CsvField(perm.AccessType)).Append(',');
                sb.Append(CsvField(perm.Rights)).Append(',');
                sb.Append(CsvField(perm.RightsDecoded)).Append(',');
                sb.Append(perm.IsInherited).Append(',');
                sb.Append(CsvField(perm.InheritanceFlags)).Append(',');
                sb.AppendLine(CsvField(perm.PropagationFlags));
            }
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportComparison(ComparisonResult comparison, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RelativePath,Status,Identity,AccessType,Rights,IsInherited,PresentInLeft,PresentInRight");

        foreach (var diff in comparison.Diffs)
        {
            var node = diff.Left ?? diff.Right;
            if (node == null) continue;

            bool inLeft  = diff.Left  != null;
            bool inRight = diff.Right != null;

            if (diff.Status == DiffStatus.Same && node.Permissions.Count == 0)
            {
                sb.AppendLine($"{CsvField(diff.RelativePath)},{diff.Status},,,,,{inLeft},{inRight}");
                continue;
            }

            // Union all permissions from both sides
            var allPerms = new List<(PermissionEntry Perm, bool IsLeft)>();
            if (diff.Left != null)  allPerms.AddRange(diff.Left.Permissions.Select(p  => (p, true)));
            if (diff.Right != null) allPerms.AddRange(diff.Right.Permissions.Select(p => (p, false)));

            foreach (var (perm, isLeft) in allPerms)
            {
                sb.Append(CsvField(diff.RelativePath)).Append(',');
                sb.Append(diff.Status).Append(',');
                sb.Append(CsvField(perm.Identity)).Append(',');
                sb.Append(CsvField(perm.AccessType)).Append(',');
                sb.Append(CsvField(perm.Rights)).Append(',');
                sb.Append(perm.IsInherited).Append(',');
                sb.Append(isLeft ? "TRUE" : "").Append(',');
                sb.AppendLine(isLeft ? "" : "TRUE");
            }
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return "\"" + value + "\"";
    }
}
