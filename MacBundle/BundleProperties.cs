using System;
using PowerKit;

namespace MacBundle;

public class BundleProperties
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

    public required string Copyright { get; init; }

    public required string Version
    {
        get;
        init
        {
            if (!System.Version.TryParse(value, out var version))
            {
                throw new InvalidOperationException(
                    $"Bundle version '{value}' is not a valid version string."
                );
            }

            if (version.Revision >= 0)
            {
                throw new InvalidOperationException(
                    $"Bundle version '{value}' has more than 3 components."
                );
            }

            field = version.ToString();
        }
    }

    public required string ShortVersion
    {
        get;
        init
        {
            if (!System.Version.TryParse(value, out var version))
            {
                throw new InvalidOperationException(
                    $"Bundle version '{value}' is not a valid version string."
                );
            }

            if (version.Revision >= 0)
            {
                throw new InvalidOperationException(
                    $"Bundle version '{value}' has more than 3 components."
                );
            }

            field = version.ToString();
        }
    }

    public required string IconName { get; init; }

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
