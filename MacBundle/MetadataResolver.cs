using System;
using System.Linq;

namespace MacBundle;

public static class MetadataResolver
{
    public static string ResolveAppIdentifier(
        string? configuredIdentifier,
        string appName,
        string? gitRemoteUrl
    )
    {
        if (!string.IsNullOrWhiteSpace(configuredIdentifier))
            return configuredIdentifier.Trim();

        var derivedIdentifier = TryDeriveIdentifierFromGitRemote(gitRemoteUrl);
        if (!string.IsNullOrWhiteSpace(derivedIdentifier))
            return derivedIdentifier;

        return SanitizeIdentifierSegment(appName);
    }

    public static string? TryDeriveIdentifierFromGitRemote(string? gitRemoteUrl)
    {
        if (string.IsNullOrWhiteSpace(gitRemoteUrl))
            return null;

        var url = gitRemoteUrl.Trim();
        string? host = null;
        string? path = null;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri != null)
        {
            host = uri.Host;
            path = uri.AbsolutePath.Trim('/');
        }
        else
        {
            var atIndex = url.IndexOf('@');
            var colonIndex = url.IndexOf(':');
            if (atIndex >= 0 && colonIndex > atIndex)
            {
                host = url.Substring(atIndex + 1, colonIndex - atIndex - 1);
                path = url.Substring(colonIndex + 1).Trim('/');
            }
        }

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(path))
            return null;

        var hostValue = host;
        var pathValue = path;

        if (pathValue.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            pathValue = pathValue.Substring(0, pathValue.Length - 4);

        var hostPrefix = string.Equals(hostValue, "github.com", StringComparison.OrdinalIgnoreCase)
            ? "io.github"
            : string.Join(
                ".",
                Enumerable
                    .Reverse(hostValue.Split(['.'], StringSplitOptions.RemoveEmptyEntries))
                    .Select(SanitizeIdentifierSegmentLower)
            );

        var pathPrefix = string.Join(
            ".",
            pathValue
                .Split(['/'], StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeIdentifierSegment)
        );

        if (string.IsNullOrWhiteSpace(pathPrefix))
            return null;

        return hostPrefix + "." + pathPrefix;
    }

    public static string ResolveVersion(
        string? version,
        string? assemblyVersion,
        string? fileVersion,
        string fallback
    )
    {
        var raw =
            !string.IsNullOrWhiteSpace(version) ? version
            : !string.IsNullOrWhiteSpace(assemblyVersion) ? assemblyVersion
            : !string.IsNullOrWhiteSpace(fileVersion) ? fileVersion
            : null;

        if (raw is null)
            return fallback;

        // Strip pre-release and build metadata suffixes (e.g. "-rc.1+git.abc"),
        // then take up to 3 period-separated numeric components.
        var corePart = raw.Split(['-', '+'], 2)[0];
        var parts = corePart
            .Split(['.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 0 && p.All(char.IsDigit))
            .Take(3)
            .ToArray();

        return parts.Length > 0 ? string.Join(".", parts) : fallback;
    }

    public static string ResolveShortVersion(string fullVersion)
    {
        // CFBundleShortVersionString must be exactly three period-separated non-negative integers.
        // Strip any pre-release or build metadata suffixes (e.g. "-rc.1+git.abc").
        var corePart = fullVersion.Split(['-', '+'], 2)[0];
        var parts = corePart
            .Split(['.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 0 && p.All(char.IsDigit))
            .Take(3)
            .ToArray();
        var components = new string[3];
        for (var i = 0; i < 3; i++)
            components[i] = i < parts.Length ? parts[i] : "0";

        return string.Join(".", components);
    }

    private static string SanitizeIdentifierSegmentLower(string value) =>
        SanitizeIdentifierSegment(value).ToLowerInvariant();

    private static string SanitizeIdentifierSegment(string value)
    {
        var normalized = new string(
            value.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray()
        );

        return string.IsNullOrWhiteSpace(normalized) ? "app" : normalized;
    }
}
