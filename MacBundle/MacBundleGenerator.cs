using System;
using System.Globalization;
using System.IO;
using System.Security;

namespace MacBundle;

public static class MacBundleGenerator
{
    public static bool Generate(
        MacBundleProperties options,
        Action<string>? logMessage = null,
        Action<string>? logWarning = null
    )
    {
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        if (!Directory.Exists(outputDirectory))
        {
            logMessage?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Skipping bundle generation: output directory '{0}' does not exist.",
                    outputDirectory
                )
            );
            return true;
        }

        if (options.Name.Length > 15)
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Bundle name '{0}' exceeds the 15-character limit. "
                        + "Set the <MacOSBundleName> property to a shorter name.",
                    options.Name
                )
            );

        var bundleDirectory = Path.Combine(outputDirectory, options.Name + ".app");
        var contentsDirectory = Path.Combine(bundleDirectory, "Contents");
        var executableDirectory = Path.Combine(contentsDirectory, "MacOS");
        var resourcesDirectory = Path.Combine(contentsDirectory, "Resources");

        if (Directory.Exists(bundleDirectory))
            Directory.Delete(bundleDirectory, true);

        Directory.CreateDirectory(executableDirectory);
        Directory.CreateDirectory(resourcesDirectory);

        foreach (var sourcePath in Directory.GetFileSystemEntries(outputDirectory))
        {
            if (
                Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar)
                == Path.GetFullPath(bundleDirectory).TrimEnd(Path.DirectorySeparatorChar)
            )
            {
                continue;
            }

            var destinationPath = Path.Combine(executableDirectory, Path.GetFileName(sourcePath));
            CopyFileSystemEntry(sourcePath, destinationPath);
        }

        var appIconName = "AppIcon";
        var appIconPath = Path.Combine(resourcesDirectory, appIconName + ".icns");
        if (!string.IsNullOrWhiteSpace(options.IconSourcePath))
        {
            TryCreateIcnsIcon(
                options.ProjectDirectory,
                options.IconSourcePath,
                appIconPath,
                logWarning
            );
        }

        File.WriteAllText(
            Path.Combine(contentsDirectory, "Info.plist"),
            GenerateInfoPlist(
                options.Identifier,
                options.Name,
                options.DisplayName,
                options.SpokenName,
                options.Version,
                options.ShortVersion,
                appIconName,
                options.Copyright,
                options.ExecutableName
            )
        );

        logMessage?.Invoke(
            string.Format(
                CultureInfo.InvariantCulture,
                "Generated macOS bundle at '{0}'.",
                bundleDirectory
            )
        );

        return true;
    }

    private static string GenerateInfoPlist(
        string identifier,
        string name,
        string displayName,
        string spokenName,
        string version,
        string shortVersion,
        string iconName,
        string? copyright,
        string executableName
    )
    {
        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
              <dict>
                <key>CFBundlePackageType</key>
                <string>APPL</string>

                <key>CFBundleIdentifier</key>
                <string>{{Escape(identifier)}}</string>

                <key>CFBundleName</key>
                <string>{{Escape(name)}}</string>

                <key>CFBundleDisplayName</key>
                <string>{{Escape(displayName)}}</string>

                <key>CFBundleSpokenName</key>
                <string>{{Escape(spokenName)}}</string>

                <key>CFBundleExecutable</key>
                <string>{{Escape(executableName)}}</string>

                <key>CFBundleVersion</key>
                <string>{{Escape(version)}}</string>

                <key>CFBundleShortVersionString</key>
                <string>{{Escape(shortVersion)}}</string>

                <key>NSHumanReadableCopyright</key>
                <string>{{Escape(copyright)}}</string>

                <key>CFBundleIconFile</key>
                <string>{{Escape(iconName)}}</string>

                <key>CFBundleIconName</key>
                <string>{{Escape(iconName)}}</string>

                <key>NSHighResolutionCapable</key>
                <true />
              </dict>
            </plist>
            """;
    }

    private static string Escape(string? value) =>
        SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;

    private static void TryCreateIcnsIcon(
        string projectDirectory,
        string applicationIconPath,
        string targetIcnsPath,
        Action<string>? logWarning
    )
    {
        var fullApplicationIconPath = Path.IsPathRooted(applicationIconPath)
            ? applicationIconPath
            : Path.Combine(projectDirectory, applicationIconPath);

        if (!File.Exists(fullApplicationIconPath))
        {
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Icon '{0}' does not exist.",
                    fullApplicationIconPath
                )
            );
            return;
        }

        if (
            string.Equals(
                Path.GetExtension(fullApplicationIconPath),
                ".icns",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            File.Copy(fullApplicationIconPath, targetIcnsPath, true);
            return;
        }

        IcnsWriter.TryCreateFromIco(fullApplicationIconPath, targetIcnsPath, logWarning);
    }

    private static void CopyFileSystemEntry(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
        {
            Directory.CreateDirectory(destinationPath);
            foreach (var nestedSourcePath in Directory.GetFileSystemEntries(sourcePath))
            {
                var nestedDestinationPath = Path.Combine(
                    destinationPath,
                    Path.GetFileName(nestedSourcePath)
                );
                CopyFileSystemEntry(nestedSourcePath, nestedDestinationPath);
            }

            return;
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        File.Copy(sourcePath, destinationPath, true);
    }
}
