namespace ClaudeCosts.Core.Discovery;

/// <summary>
/// Locates the directories that contain Claude Code usage transcripts
/// (<c>projects/**/*.jsonl</c>), following the same resolution order as ccusage.
/// </summary>
public static class ClaudeDataLocator
{
    /// <summary>
    /// Returns every existing <c>projects</c> directory to scan. Honours
    /// <c>CLAUDE_CONFIG_DIR</c> (one or more paths separated by <c>,</c> / <c>;</c>),
    /// then falls back to <c>~/.claude</c> and <c>~/.config/claude</c>.
    /// </summary>
    public static IReadOnlyList<string> GetProjectRoots()
    {
        var roots = new List<string>();

        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(configDir))
        {
            foreach (var part in configDir.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AddIfExists(roots, Path.Combine(part, "projects"));
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            AddIfExists(roots, Path.Combine(home, ".claude", "projects"));
            AddIfExists(roots, Path.Combine(home, ".config", "claude", "projects"));
        }

        return roots;
    }

    /// <summary>Enumerates every <c>*.jsonl</c> transcript under the located roots.</summary>
    public static IEnumerable<string> EnumerateTranscripts()
    {
        foreach (var root in GetProjectRoots())
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories);
            }
            catch
            {
                continue; // unreadable root — skip
            }

            foreach (var file in files)
                yield return file;
        }
    }

    private static void AddIfExists(List<string> roots, string path)
    {
        if (Directory.Exists(path) &&
            !roots.Any(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase)))
        {
            roots.Add(path);
        }
    }
}
