using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using PowerKit;

namespace MacBundle;

public class BundleProperties
{
    public required string Identifier { get; init; }

    public required string? Name
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

    public required string? DisplayName { get; init; }

    public required string? SpokenName { get; init; }

    public required string? ExecutableName { get; init; }

    public required string? Copyright { get; init; }

    public required string? Version
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

    public required string? ShortVersion
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

    public required string? IconName { get; init; }

    public override string ToString()
    {
        var keyValuePairs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CFBundlePackageType"] = "APPL",
            ["CFBundleIdentifier"] = Identifier,
            ["CFBundleName"] = Name,
            ["CFBundleDisplayName"] = DisplayName,
            ["CFBundleSpokenName"] = SpokenName,
            ["CFBundleExecutable"] = ExecutableName,
            ["NSHumanReadableCopyright"] = Copyright,
            ["CFBundleVersion"] = Version,
            ["CFBundleShortVersionString"] = ShortVersion,
            ["CFBundleIconFile"] = IconName,
            ["CFBundleIconName"] = IconName,
            ["NSHighResolutionCapable"] = "true",
        };

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType(
                "plist",
                "-//Apple//DTD PLIST 1.0//EN",
                "http://www.apple.com/DTDs/PropertyList-1.0.dtd",
                null
            ),
            new XElement(
                "plist",
                new XAttribute("version", "1.0"),
                new XElement(
                    "dict",
                    keyValuePairs
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                        .Select(kvp =>
                            new object[]
                            {
                                new XElement("key", kvp.Key),
                                new XElement("string", kvp.Value),
                            }
                        )
                        .SelectMany(x => x)
                )
            )
        );

        return doc.ToString();
    }
}
