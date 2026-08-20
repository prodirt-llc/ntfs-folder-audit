using System.Collections.Concurrent;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Channels;

namespace NTFSReport;

public sealed class PermissionScanner
{
    // Default parallelism — overridden by user setting in ScanOptions
    private static readonly int DefaultDegreeOfParallelism =
        Math.Min(16, Math.Max(4, Environment.ProcessorCount * 2));

    private static readonly HashSet<string> SystemFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData",
        "$Recycle.Bin", "System Volume Information", "$WINDOWS.~BT",
        "Recovery", "PerfLogs", "MSOCache"
    };

    public async Task<ScanResult> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.Now;
        var bag = new ConcurrentBag<FolderNode>();

        // Shared atomic counters
        int folderCount = 0, permCount = 0, errorCount = 0;

        // Work queue: each item is (path, parentPath, depth)
        var channel = Channel.CreateUnbounded<(string path, string parent, int depth)>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        // Seed with root
        await channel.Writer.WriteAsync((options.RootPath, "", 0), ct);

        // Track outstanding work items so we know when we're done
        int pending = 1;
        var allDone = new TaskCompletionSource<bool>();

        // Use user-specified thread count if set, otherwise auto-detect
        int dop = options.MaxThreads > 0
            ? options.MaxThreads
            : options.RootPath.StartsWith(@"\\") ? 32 : DefaultDegreeOfParallelism;

        // Spin up workers
        var workers = Enumerable.Range(0, dop).Select(_ => Task.Run(async () =>
        {
            await foreach (var (folderPath, parentPath, depth) in channel.Reader.ReadAllAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var node = ScanSingleFolder(folderPath, parentPath, depth, options,
                        ref permCount, ref errorCount, ct);

                    bag.Add(node);
                    int fc = Interlocked.Increment(ref folderCount);

                    if (fc % 25 == 0)
                    {
                        progress?.Report(new ScanProgress
                        {
                            FolderCount     = fc,
                            PermissionCount = permCount,
                            ErrorCount      = errorCount,
                            CurrentPath     = folderPath
                        });
                    }

                    // Enqueue subdirectories if within depth limit
                    bool atLimit = !options.TopLevelOnly && options.MaxDepth > 0 && depth + 1 >= options.MaxDepth;
                    bool topOnly = options.TopLevelOnly && depth >= 1;

                    if (!atLimit && !topOnly && !node.AccessDenied)
                    {
                        try
                        {
                            var subs = Directory.GetDirectories(folderPath);
                            if (subs.Length > 0)
                            {
                                Interlocked.Add(ref pending, subs.Length);
                                foreach (var sub in subs)
                                {
                                    if (options.ExcludeSystemFolders && depth == 0)
                                    {
                                        var leaf = Path.GetFileName(sub.TrimEnd('\\', '/'));
                                        if (SystemFolderNames.Contains(leaf))
                                        {
                                            Interlocked.Decrement(ref pending);
                                            continue;
                                        }
                                    }
                                    await channel.Writer.WriteAsync((sub, folderPath, depth + 1), ct);
                                }
                            }
                        }
                        catch (UnauthorizedAccessException) { }
                        catch (OperationCanceledException) { throw; }
                        catch { }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { Interlocked.Increment(ref errorCount); }
                finally
                {
                    if (Interlocked.Decrement(ref pending) == 0)
                    {
                        channel.Writer.TryComplete();
                        allDone.TrySetResult(true);
                    }
                }
            }
        }, ct)).ToArray();

        try
        {
            await Task.WhenAny(Task.WhenAll(workers), allDone.Task).WaitAsync(ct);
        }
        catch (OperationCanceledException) { throw; }

        var allFolders = bag.ToList();
        BuildTree(allFolders, options.RootPath);

        // Sort by depth first, then alphabetically by name within each level
        allFolders.Sort((a, b) =>
        {
            int d = a.Depth.CompareTo(b.Depth);
            if (d != 0) return d;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        var result = new ScanResult
        {
            Options                 = options,
            Elapsed                 = DateTime.Now - startTime,
            AllFolders              = allFolders,
            TotalFolders            = folderCount,
            TotalPermissions        = permCount,
            TotalErrors             = errorCount,
            BrokenInheritanceCount  = allFolders.Count(f => f.InheritanceBroken),
            Root                    = allFolders.FirstOrDefault(f => f.Depth == 0)
        };

        return result;
    }

    private static FolderNode ScanSingleFolder(
        string folderPath,
        string parentPath,
        int depth,
        ScanOptions options,
        ref int permCount,
        ref int errorCount,
        CancellationToken ct)
    {
        var node = new FolderNode
        {
            Path       = folderPath,
            Name       = GetFolderName(folderPath),
            ParentPath = parentPath,
            Depth      = depth
        };

        try
        {
            var dirInfo = new DirectoryInfo(folderPath);
            var acl = dirInfo.GetAccessControl(AccessControlSections.Access);
            node.InheritanceBroken = acl.AreAccessRulesProtected && depth > 0;

            var rules = acl.GetAccessRules(true, true, typeof(NTAccount));
            int localPerms = 0;
            foreach (AuthorizationRule rule in rules)
            {
                if (rule is FileSystemAccessRule fsRule)
                {
                    node.Permissions.Add(new PermissionEntry
                    {
                        Identity         = fsRule.IdentityReference.Value,
                        Rights           = fsRule.FileSystemRights.ToString(),
                        RightsDecoded    = DecodeRights(fsRule.FileSystemRights),
                        AccessType       = fsRule.AccessControlType.ToString(),
                        IsInherited      = fsRule.IsInherited,
                        InheritanceFlags = fsRule.InheritanceFlags.ToString(),
                        PropagationFlags = fsRule.PropagationFlags.ToString()
                    });
                    localPerms++;
                }
            }
            Interlocked.Add(ref permCount, localPerms);
        }
        catch (UnauthorizedAccessException)
        {
            node.AccessDenied = true;
            Interlocked.Increment(ref errorCount);
        }
        catch
        {
            node.AccessDenied = true;
            Interlocked.Increment(ref errorCount);
        }

        return node;
    }

    private static void BuildTree(List<FolderNode> folders, string rootPath)
    {
        var map = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in folders)
        {
            map[f.Path] = f;
            // Set relative path for comparison
            if (f.Path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                var rel = f.Path.Substring(rootPath.Length).TrimStart('\\', '/');
                f.RelativePath = rel.ToLowerInvariant();
            }
        }

        foreach (var f in folders)
        {
            if (!string.IsNullOrEmpty(f.ParentPath) && map.TryGetValue(f.ParentPath, out var parent))
                parent.Children.Add(f);
        }

        // Sort children alphabetically at every node
        foreach (var f in folders)
            f.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetFolderName(string path)
    {
        var name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? path : name;
    }

    public static string DecodeRights(FileSystemRights rights)
    {
        int val = (int)rights;
        return val switch
        {
            268435456   => "Full Control (All)",
            -1610612736 => "Read & Execute, Synchronize",
            -536805376  => "Modify, Synchronize",
            1179785     => "Read",
            1180063     => "Read, Write",
            1180095     => "Read, Write, Delete",
            1245631     => "Read & Execute",
            1179817     => "Read, Write Attributes",
            _ when rights == FileSystemRights.FullControl => "Full Control",
            _ when rights.HasFlag(FileSystemRights.FullControl) => "Full Control",
            _ when rights.HasFlag(FileSystemRights.Modify) => "Modify",
            _ when rights.HasFlag(FileSystemRights.ReadAndExecute) => "Read & Execute",
            _ when rights.HasFlag(FileSystemRights.Read) => "Read",
            _ => DecodeRawFlags(val)
        };
    }

    private static string DecodeRawFlags(int val)
    {
        var flags = new List<string>();
        if ((val & 1) != 0) flags.Add("List Directory");
        if ((val & 2) != 0) flags.Add("Create Files");
        if ((val & 4) != 0) flags.Add("Create Directories");
        if ((val & 8) != 0) flags.Add("Read Ext Attributes");
        if ((val & 16) != 0) flags.Add("Write Ext Attributes");
        if ((val & 32) != 0) flags.Add("Traverse");
        if ((val & 64) != 0) flags.Add("Delete Subdirs");
        if ((val & 128) != 0) flags.Add("Read Attributes");
        if ((val & 256) != 0) flags.Add("Write Attributes");
        if ((val & 65536) != 0) flags.Add("Delete");
        if ((val & 131072) != 0) flags.Add("Read Permissions");
        if ((val & 262144) != 0) flags.Add("Change Permissions");
        if ((val & 524288) != 0) flags.Add("Take Ownership");
        if ((val & 1048576) != 0) flags.Add("Synchronize");
        return flags.Count > 0 ? string.Join(", ", flags) : val.ToString();
    }
}
