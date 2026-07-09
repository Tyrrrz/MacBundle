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

        var appName = string.IsNullOrWhiteSpace(options.MacOSBundleName)
            ? options.AssemblyName
            : options.MacOSBundleName!;
        var appIdentifier = MetadataResolver.ResolveAppIdentifier(
            options.MacOSBundleIdentifier,
            appName,
            TryGetGitRemoteUrl(options.ProjectDirectory)
        );

        var bundleDirectory = Path.Combine(outputDirectory, appName + ".app");
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
        if (!string.IsNullOrWhiteSpace(options.ApplicationIcon))
        {
            TryCreateIcnsIcon(
                options.ProjectDirectory,
                options.ApplicationIcon!,
                appIconPath,
                logWarning
            );
        }

        File.WriteAllText(
            Path.Combine(contentsDirectory, "Info.plist"),
            GenerateInfoPlist(
                appName,
                options.AssemblyName,
                appName,
                appIdentifier,
                appIconName,
                options.Copyright,
                options.Version,
                options.AssemblyVersion,
                options.FileVersion
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
        string appName,
        string executableName,
        string appSpokenName,
        string appIdentifier,
        string appIconName,
        string? appCopyright,
        string? version,
        string? assemblyVersion,
        string? fileVersion
    )
    {
        var fullVersion = MetadataResolver.ResolveVersion(
            version,
            assemblyVersion,
            fileVersion,
            "1.0.0"
        );
        var shortVersion = MetadataResolver.ResolveShortVersion(fullVersion);

        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
              <dict>
                <key>CFBundleDisplayName</key>
                <string>{{Escape(appName)}}</string>
                <key>CFBundleName</key>
                <string>{{Escape(appName)}}</string>
                <key>CFBundleExecutable</key>
                <string>{{Escape(executableName)}}</string>
                <key>NSHumanReadableCopyright</key>
                <string>{{Escape(appCopyright)}}</string>
                <key>CFBundleIdentifier</key>
                <string>{{Escape(appIdentifier)}}</string>
                <key>CFBundleSpokenName</key>
                <string>{{Escape(appSpokenName)}}</string>
                <key>CFBundleIconFile</key>
                <string>{{Escape(appIconName)}}</string>
                <key>CFBundleIconName</key>
                <string>{{Escape(appIconName)}}</string>
                <key>CFBundleVersion</key>
                <string>{{Escape(fullVersion)}}</string>
                <key>CFBundleShortVersionString</key>
                <string>{{Escape(shortVersion)}}</string>
                <key>NSHighResolutionCapable</key>
                <true />
                <key>CFBundlePackageType</key>
                <string>APPL</string>
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
                    "ApplicationIcon '{0}' does not exist.",
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

    private static string? TryGetGitRemoteUrl(string projectDirectory)
    {
        return CommandRunner.TryGetStandardOutput(
            "git",
            new[] { "config", "--get", "remote.origin.url" },
            projectDirectory,
            TimeSpan.FromSeconds(3)
        );
    }
}
