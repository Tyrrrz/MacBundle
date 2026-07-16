using System;
using System.Collections.Generic;
using System.IO;

namespace MacBundle.Utils;

internal partial class GitConfiguration(string? remoteOriginUrl)
{
    public string? RemoteOriginUrl { get; } = remoteOriginUrl;
}

internal partial class GitConfiguration
{
    private static string? TryGetGitDirectoryPath(string workingDirectoryPath)
    {
        var directoryInfo = new DirectoryInfo(workingDirectoryPath);

        while (directoryInfo is not null)
        {
            var gitDirectoryPath = Path.Combine(directoryInfo.FullName, ".git");

            if (Directory.Exists(gitDirectoryPath))
                return gitDirectoryPath;

            directoryInfo = directoryInfo.Parent;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> GetConfig(string configFilePath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

                map[configKey] = line[(separatorIndex + 1)..].Trim();
            }
        }

        return map;
    }

    public static GitConfiguration? TryResolve(string? workingDirectoryPath = null)
    {
        var gitDirectoryPath = TryGetGitDirectoryPath(
            workingDirectoryPath ?? Directory.GetCurrentDirectory()
        );

        if (gitDirectoryPath is null)
            return null;

        var configFilePath = Path.Combine(gitDirectoryPath, "config");
        if (!File.Exists(configFilePath))
            return null;

        var config = GetConfig(configFilePath);

        return new GitConfiguration(config.GetValueOrDefault("remote.origin.url"));
    }
}
