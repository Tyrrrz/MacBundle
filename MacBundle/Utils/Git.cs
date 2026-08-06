using System;
using System.Collections.Generic;
using System.IO;

namespace MacBundle.Utils;

internal static class Git
{
    private static string? TryGetDirectoryPath(string? workingDirectoryPath = null)
    {
        var directory = new DirectoryInfo(workingDirectoryPath ?? Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var gitDirectoryPath = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(gitDirectoryPath))
                return gitDirectoryPath;

            directory = directory.Parent;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string>? TryGetConfig(
        string? workingDirectoryPath = null
    )
    {
        var gitDirectoryPath = TryGetDirectoryPath(workingDirectoryPath);
        if (string.IsNullOrWhiteSpace(gitDirectoryPath))
            return null;

        var configFilePath = Path.Combine(gitDirectoryPath, "config");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var currentSectionName = string.Empty;
        var currentSubsectionName = string.Empty;

        foreach (var rawLine in File.ReadLines(configFilePath))
        {
            var line = rawLine.Trim();

            // Comments
            if (
                string.IsNullOrWhiteSpace(line)
                || line.StartsWith("#", StringComparison.Ordinal)
                || line.StartsWith(";", StringComparison.Ordinal)
            )
            {
                continue;
            }
            // Section header
            else if (
                line.StartsWith("[", StringComparison.Ordinal)
                && line.EndsWith("]", StringComparison.Ordinal)
            )
            {
                var components = line[1..^1]
                    .Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);

                currentSectionName = components[0].Trim();

                currentSubsectionName =
                    components.Length > 1 ? components[1].Trim('"') : string.Empty;
            }
            // Key-value pair
            else
            {
                var separatorIndex = line.IndexOf('=');
                if (separatorIndex < 0 || string.IsNullOrWhiteSpace(currentSectionName))
                    continue;

                var subsectionPrefix = string.IsNullOrWhiteSpace(currentSubsectionName)
                    ? string.Empty
                    : "." + currentSubsectionName;

                var keyName = line[..separatorIndex].Trim();
                var configKey = currentSectionName + subsectionPrefix + "." + keyName;

                result[configKey] = line[(separatorIndex + 1)..].Trim();
            }
        }

        return result;
    }

    public static string? TryGetRemoteOriginUrl(string? workingDirectoryPath = null) =>
        TryGetConfig(workingDirectoryPath)?.GetValueOrDefault("remote.origin.url");
}
