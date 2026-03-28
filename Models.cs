using System.Text.Json.Serialization;

namespace NTFSReport;

public sealed class PermissionEntry
{
    [JsonPropertyName("i")] public string Identity { get; set; } = "";
    [JsonPropertyName("r")] public string Rights { get; set; } = "";
    [JsonPropertyName("d")] public string RightsDecoded { get; set; } = "";
    [JsonPropertyName("a")] public string AccessType { get; set; } = "Allow";
    [JsonPropertyName("h")] public bool IsInherited { get; set; }
    [JsonPropertyName("f")] public string InheritanceFlags { get; set; } = "";
    [JsonPropertyName("pf")] public string PropagationFlags { get; set; } = "";
}

public sealed class FolderNode
{
    [JsonPropertyName("p")]  public string Path { get; set; } = "";
    [JsonPropertyName("n")]  public string Name { get; set; } = "";
    [JsonPropertyName("pr")] public string ParentPath { get; set; } = "";
    [JsonPropertyName("d")]  public int Depth { get; set; }
    [JsonPropertyName("bi")] public bool InheritanceBroken { get; set; }
    [JsonPropertyName("ad")] public bool AccessDenied { get; set; }
    [JsonPropertyName("pm")] public List<PermissionEntry> Permissions { get; set; } = new();

    [JsonIgnore] public List<FolderNode> Children { get; set; } = new();
    [JsonIgnore] public string RelativePath { get; set; } = "";
}

public sealed class ScanOptions
{
    public string RootPath { get; set; } = "";
    public int MaxDepth { get; set; } = 5;
    public bool ExcludeSystemFolders { get; set; }
    public bool TopLevelOnly { get; set; }
    /// <summary>0 = auto-detect based on path type</summary>
    public int MaxThreads { get; set; } = 0;
}

public sealed class ScanProgress
{
    public int FolderCount { get; set; }
    public int PermissionCount { get; set; }
    public int ErrorCount { get; set; }
    public string CurrentPath { get; set; } = "";
}

public sealed class ScanResult
{
    public ScanOptions Options { get; set; } = new();
    public FolderNode? Root { get; set; }
    public List<FolderNode> AllFolders { get; set; } = new();
    public int TotalFolders { get; set; }
    public int TotalPermissions { get; set; }
    public int TotalErrors { get; set; }
    public int BrokenInheritanceCount { get; set; }
    public TimeSpan Elapsed { get; set; }
    public DateTime ScanDate { get; set; } = DateTime.Now;
}

public enum DiffStatus { Same, Changed, LeftOnly, RightOnly }

public sealed class FolderDiff
{
    public string RelativePath { get; set; } = "";
    public DiffStatus Status { get; set; }
    public FolderNode? Left { get; set; }
    public FolderNode? Right { get; set; }
}

public sealed class ComparisonResult
{
    public ScanResult Left { get; set; } = new();
    public ScanResult Right { get; set; } = new();
    public List<FolderDiff> Diffs { get; set; } = new();
    public int SameCount { get; set; }
    public int ChangedCount { get; set; }
    public int LeftOnlyCount { get; set; }
    public int RightOnlyCount { get; set; }
}
