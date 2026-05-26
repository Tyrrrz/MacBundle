using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace MacBundle;

public sealed class MacBundleGeneratorOptions
{
    public string ProjectDirectory { get; set; } = null!;

    public string OutputDirectory { get; set; } = null!;

    public string AssemblyName { get; set; } = null!;

    public string? MacOSBundleName { get; set; }

    public string? Copyright { get; set; }

    public string? MacOSBundleIdentifier { get; set; }

    public string? ApplicationIcon { get; set; }

    public string? Version { get; set; }

    public string? AssemblyVersion { get; set; }

    public string? FileVersion { get; set; }
}

public static class MacBundleGenerator
{
    public static bool Generate(
        MacBundleGeneratorOptions options,
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
        var appSpokenName = appName;
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
                appSpokenName,
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
        string appSpokenName,
        string appIdentifier,
        string appIconName,
        string? appCopyright,
        string? version,
        string? assemblyVersion,
        string? fileVersion
    )
    {
        var fullVersion = MetadataResolver.ResolveVersion(version, assemblyVersion, fileVersion, "1.0.0");
        var shortVersion = MetadataResolver.ResolveShortVersion(fullVersion);

        return string.Format(
            CultureInfo.InvariantCulture,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
                + "<plist version=\"1.0\">\n"
                + "  <dict>\n"
                + "    <key>CFBundleDisplayName</key>\n"
                + "    <string>{0}</string>\n"
                + "    <key>CFBundleName</key>\n"
                + "    <string>{1}</string>\n"
                + "    <key>CFBundleExecutable</key>\n"
                + "    <string>{2}</string>\n"
                + "    <key>NSHumanReadableCopyright</key>\n"
                + "    <string>{3}</string>\n"
                + "    <key>CFBundleIdentifier</key>\n"
                + "    <string>{4}</string>\n"
                + "    <key>CFBundleSpokenName</key>\n"
                + "    <string>{5}</string>\n"
                + "    <key>CFBundleIconFile</key>\n"
                + "    <string>{6}</string>\n"
                + "    <key>CFBundleIconName</key>\n"
                + "    <string>{7}</string>\n"
                + "    <key>CFBundleVersion</key>\n"
                + "    <string>{8}</string>\n"
                + "    <key>CFBundleShortVersionString</key>\n"
                + "    <string>{9}</string>\n"
                + "    <key>NSHighResolutionCapable</key>\n"
                + "    <true />\n"
                + "    <key>CFBundlePackageType</key>\n"
                + "    <string>APPL</string>\n"
                + "  </dict>\n"
                + "</plist>\n",
            Escape(appName),
            Escape(appName),
            Escape(appName),
            Escape(appCopyright),
            Escape(appIdentifier),
            Escape(appSpokenName),
            Escape(appIconName),
            Escape(appIconName),
            Escape(fullVersion),
            Escape(shortVersion)
        );
    }

    private static string Escape(string? value) => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;

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

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            logWarning?.Invoke("Skipping icon conversion to .icns because host OS is not macOS.");
            return;
        }

        var iconSetDirectory = Path.Combine(
            Path.GetTempPath(),
            "macbundle-iconset-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(iconSetDirectory);

        try
        {
            var sizes = new[] { 16, 32, 128, 256, 512 };
            foreach (var size in sizes)
            {
                var oneX = Path.Combine(iconSetDirectory, $"icon_{size}x{size}.png");
                var twoX = Path.Combine(iconSetDirectory, $"icon_{size}x{size}@2x.png");

                if (
                    !TryRunProcess(
                        "sips",
                        $"-z {size} {size} \"{fullApplicationIconPath}\" --out \"{oneX}\"",
                        logWarning
                    )
                )
                {
                    return;
                }

                var retinaSize = size * 2;
                if (
                    !TryRunProcess(
                        "sips",
                        $"-z {retinaSize} {retinaSize} \"{fullApplicationIconPath}\" --out \"{twoX}\"",
                        logWarning
                    )
                )
                {
                    return;
                }
            }

            TryRunProcess("iconutil", $"-c icns \"{iconSetDirectory}\" -o \"{targetIcnsPath}\"", logWarning);
        }
        finally
        {
            if (Directory.Exists(iconSetDirectory))
                Directory.Delete(iconSetDirectory, true);
        }
    }

    private static bool TryRunProcess(string fileName, string arguments, Action<string>? logWarning)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            process.WaitForExit();

            if (process.ExitCode == 0)
                return true;

            var error = process.StandardError.ReadToEnd();
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Command '{0} {1}' failed: {2}",
                    fileName,
                    arguments,
                    error
                )
            );
            return false;
        }
        catch (Exception ex)
        {
            logWarning?.Invoke(ex.Message);
            return false;
        }
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
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "config --get remote.origin.url",
                WorkingDirectory = projectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(3000))
                return null;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            return output;
        }
        catch
        {
            return null;
        }
    }
}

public static class MetadataResolver
{
    public static string ResolveAppIdentifier(
        string? configuredIdentifier,
        string appName,
        string? gitRemoteUrl
    )
    {
        if (!string.IsNullOrWhiteSpace(configuredIdentifier))
            return configuredIdentifier!.Trim();

        var derivedIdentifier = TryDeriveIdentifierFromGitRemote(gitRemoteUrl);
        if (!string.IsNullOrWhiteSpace(derivedIdentifier))
            return derivedIdentifier!;

        return SanitizeIdentifierSegment(appName);
    }

    public static string? TryDeriveIdentifierFromGitRemote(string? gitRemoteUrl)
    {
        if (string.IsNullOrWhiteSpace(gitRemoteUrl))
            return null;

        var url = gitRemoteUrl!.Trim();
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

        var hostValue = host!;
        var pathValue = path!;

        if (pathValue.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            pathValue = pathValue.Substring(0, pathValue.Length - 4);

        var hostPrefix = string.Equals(hostValue, "github.com", StringComparison.OrdinalIgnoreCase)
            ? "io.github"
            : string.Join(
                ".",
                Enumerable
                    .Reverse(hostValue.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(SanitizeIdentifierSegmentLower)
            );

        var pathPrefix = string.Join(
            ".",
            pathValue
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
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
        if (!string.IsNullOrWhiteSpace(version))
            return version!;

        if (!string.IsNullOrWhiteSpace(assemblyVersion))
            return assemblyVersion!;

        if (!string.IsNullOrWhiteSpace(fileVersion))
            return fileVersion!;

        return fallback;
    }

    public static string ResolveShortVersion(string fullVersion)
    {
        var parts = fullVersion.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3)
            return fullVersion;

        return string.Join(".", parts.Take(3));
    }

    private static string SanitizeIdentifierSegmentLower(string value) =>
        SanitizeIdentifierSegment(value).ToLowerInvariant();

    private static string SanitizeIdentifierSegment(string value)
    {
        var normalized = new string(
            value
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                .ToArray()
        );

        return string.IsNullOrWhiteSpace(normalized) ? "app" : normalized;
    }
}
