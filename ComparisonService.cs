namespace NTFSReport;

public sealed class ComparisonService
{
    public ComparisonResult Compare(ScanResult left, ScanResult right)
    {
        var result = new ComparisonResult { Left = left, Right = right };

        // Build relative-path dictionaries
        var leftMap = left.AllFolders.ToDictionary(
            f => f.RelativePath,
            f => f,
            StringComparer.OrdinalIgnoreCase);

        var rightMap = right.AllFolders.ToDictionary(
            f => f.RelativePath,
            f => f,
            StringComparer.OrdinalIgnoreCase);

        var allKeys = new HashSet<string>(leftMap.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(rightMap.Keys);

        foreach (var key in allKeys)
        {
            leftMap.TryGetValue(key, out var leftNode);
            rightMap.TryGetValue(key, out var rightNode);

            DiffStatus status;
            if (leftNode == null) status = DiffStatus.RightOnly;
            else if (rightNode == null) status = DiffStatus.LeftOnly;
            else if (PermissionsMatch(leftNode, rightNode)) status = DiffStatus.Same;
            else status = DiffStatus.Changed;

            result.Diffs.Add(new FolderDiff
            {
                RelativePath = key,
                Status = status,
                Left = leftNode,
                Right = rightNode
            });

            switch (status)
            {
                case DiffStatus.Same:      result.SameCount++;      break;
                case DiffStatus.Changed:   result.ChangedCount++;   break;
                case DiffStatus.LeftOnly:  result.LeftOnlyCount++;  break;
                case DiffStatus.RightOnly: result.RightOnlyCount++; break;
            }
        }

        result.Diffs.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static bool PermissionsMatch(FolderNode left, FolderNode right)
    {
        if (left.Permissions.Count != right.Permissions.Count) return false;

        var leftSet = new HashSet<string>(left.Permissions.Select(PermKey));
        var rightSet = new HashSet<string>(right.Permissions.Select(PermKey));
        return leftSet.SetEquals(rightSet);
    }

    private static string PermKey(PermissionEntry p) =>
        $"{p.Identity}|{p.AccessType}|{p.Rights}|{p.IsInherited}";
}
