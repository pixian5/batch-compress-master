using System;
using System.Collections.Generic;
using System.IO;

namespace BatchCompress.Avalonia.Core.Services;

// GPT-5, 2026-08-05: Centralized cross-platform filter for filesystem metadata that should never become
// a compression or extraction job. The check examines every path segment so files inside metadata folders
// are rejected even when supplied manually or through a TXT list.
public static class SystemMetadataFileFilter
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows Explorer and recycle-bin metadata.
        "desktop.ini", "thumbs.db", "ehthumbs.db", "ehthumbs_vista.db", "$recycle.bin",
        "recycler", "system volume information",

        // macOS Finder, Spotlight, Time Machine and AppleDouble metadata.
        ".ds_store", ".appledouble", ".lsoverride", "icon\r", ".spotlight-v100", ".trashes",
        ".fseventsd", ".documentrevisions-v100", ".temporaryitems", ".volumeicon.icns",
        ".com.apple.timemachine.donotpresent",

        // Linux and desktop-environment metadata.
        ".directory", ".trash", ".gvfs", "lost+found"
    };

    /// <summary>
    /// Returns true when a path is an operating-system metadata file/directory or lies below one.
    /// </summary>
    public static bool ShouldSkip(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (ReservedNames.Contains(segment) ||
                segment.StartsWith("._", StringComparison.Ordinal) ||
                segment.StartsWith(".trash-", StringComparison.OrdinalIgnoreCase) ||
                segment.StartsWith(".nfs", StringComparison.Ordinal) ||
                segment.StartsWith(".~lock.", StringComparison.Ordinal) ||
                segment.StartsWith("~$", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
