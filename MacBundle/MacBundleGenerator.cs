using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using CliWrap;
using CliWrap.Buffered;

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
                <string>{{Escape(appName)}}</string>
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
                    !CommandRunner.TryRun(
                        "sips",
                        new[]
                        {
                            "-z",
                            size.ToString(CultureInfo.InvariantCulture),
                            size.ToString(CultureInfo.InvariantCulture),
                            fullApplicationIconPath,
                            "--out",
                            oneX
                        },
                        logWarning
                    )
                )
                {
                    return;
                }

                var retinaSize = size * 2;
                if (
                    !CommandRunner.TryRun(
                        "sips",
                        new[]
                        {
                            "-z",
                            retinaSize.ToString(CultureInfo.InvariantCulture),
                            retinaSize.ToString(CultureInfo.InvariantCulture),
                            fullApplicationIconPath,
                            "--out",
                            twoX
                        },
                        logWarning
                    )
                )
                {
                    return;
                }
            }

            CommandRunner.TryRun(
                "iconutil",
                new[] { "-c", "icns", iconSetDirectory, "-o", targetIcnsPath },
                logWarning
            );
        }
        finally
        {
            if (Directory.Exists(iconSetDirectory))
                Directory.Delete(iconSetDirectory, true);
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
        return CommandRunner.TryGetStandardOutput(
            "git",
            new[] { "config", "--get", "remote.origin.url" },
            projectDirectory,
            TimeSpan.FromSeconds(3)
        );
    }
}

internal static class CommandRunner
{
    public static bool TryRun(string fileName, IReadOnlyList<string> arguments, Action<string>? logWarning)
    {
        try
        {
            var result = Cli
                .Wrap(fileName)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync()
                .GetAwaiter()
                .GetResult();

            if (result.ExitCode == 0)
                return true;

            var error = result.StandardError.Trim();
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Command '{0} {1}' failed: {2}",
                    fileName,
                    string.Join(" ", arguments),
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

    public static string? TryGetStandardOutput(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout
    )
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(timeout);

            var result = Cli
                .Wrap(fileName)
                .WithArguments(arguments)
                .WithWorkingDirectory(workingDirectory)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationTokenSource.Token)
                .GetAwaiter()
                .GetResult();

            var output = result.StandardOutput.Trim();
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
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
