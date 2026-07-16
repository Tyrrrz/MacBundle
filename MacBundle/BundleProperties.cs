using System;
using System.Linq;
using MacBundle.Utils;
using PowerKit;
using PowerKit.Extensions;

namespace MacBundle;

public partial class BundleProperties
{
    public required string Identifier { get; init; }

    public required string Name
    {
        get;
        init
        {
            if (value.Length > 15)
            {
                throw new InvalidOperationException(
                    $"Bundle name '{value}' exceeds the 15-character limit."
                );
            }

            field = value;
        }
    }

    public required string DisplayName { get; init; }

    public required string SpokenName { get; init; }

    public required string ExecutableName { get; init; }

    public required string? Copyright { get; init; }

    public required string Version { get; init; }

    public required string ShortVersion { get; init; }

    public override string ToString() =>
        $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
              <dict>
                <key>CFBundlePackageType</key>
                <string>APPL</string>

                <key>CFBundleIdentifier</key>
                <string>{{Xml.Escape(Identifier)}}</string>

                <key>CFBundleName</key>
                <string>{{Xml.Escape(Name)}}</string>

                <key>CFBundleDisplayName</key>
                <string>{{Xml.Escape(DisplayName)}}</string>

                <key>CFBundleSpokenName</key>
                <string>{{Xml.Escape(SpokenName)}}</string>

                <key>CFBundleExecutable</key>
                <string>{{Xml.Escape(ExecutableName)}}</string>

                <key>NSHumanReadableCopyright</key>
                <string>{{Xml.Escape(Copyright)}}</string>

                <key>CFBundleVersion</key>
                <string>{{Xml.Escape(Version)}}</string>

                <key>CFBundleShortVersionString</key>
                <string>{{Xml.Escape(ShortVersion)}}</string>

                <key>CFBundleIconFile</key>
                <string>{{Xml.Escape(IconName)}}</string>

                <key>CFBundleIconName</key>
                <string>{{Xml.Escape(IconName)}}</string>

                <key>NSHighResolutionCapable</key>
                <true />
              </dict>
            </plist>
            """;
}

public partial class BundleProperties
{
    public const string IconName = "AppIcon";

    public static string? TryGetIdentifierFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var uri = Ssh.TryParse(url);
        if (uri is null && !Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            return null;
        }

        var host = string.Join(
            '.',
            uri.Host
                // Replace github.com/gitlab.com hosts with github.io/gitlab.io
                // for consistency with Pages domains.
                .Replace("github.com", "github.io")
                .Replace("gitlab.com", "gitlab.io")
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .AsEnumerable()
                .Reverse()
        );

        // Cut the ".git" suffix and replace slashes with dots
        var path = uri.AbsolutePath.TrimStart('/').SubstringUntilLast(".git").Replace('/', '.');

        return host + "." + path;
    }

    public static string? TrySanitizeVersion(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
            return null;

        var version = versionText
            .Trim()
            // Trim leading 'v' or 'V' characters
            .TrimStart('v', 'V')
            // Cut the pre-release suffix
            .SubstringUntil("-")
            .Pipe(s => System.Version.TryParse(s, out var parsed) ? parsed : null);

        return version?.ToString(3);
    }
}
