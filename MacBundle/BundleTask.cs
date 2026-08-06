using System;
using System.IO;
using System.Linq;
using MacBundle.Graphics;
using MacBundle.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using PowerKit.Extensions;

namespace MacBundle;

public class BundleTask : Task
{
    [Required]
    public required string? BundleIdentifier { get; init; }

    public string BundleName { get; init; }

    public string? BundleDisplayName { get; init; }

    public string? BundleSpokenName { get; init; }

    public string? BundleCopyright { get; init; }

    public string? BundleVersion { get; init; }

    public string? BundleShortVersion { get; init; }

    public string? BundleIconFilePath { get; init; }

    [Required]
    public required string TargetFilePath { get; init; }

    public string TargetDirectoryPath =>
        Path.GetDirectoryName(TargetFilePath) ?? Directory.GetCurrentDirectory();

    public string TargetBundleDirectoryPath =>
        Path.Combine(TargetDirectoryPath, BundleName + ".app");

    public string TargetBundleContentsDirectoryPath =>
        Path.Combine(TargetBundleDirectoryPath, "Contents");

    public string TargetBundleBinDirectoryPath =>
        Path.Combine(TargetBundleContentsDirectoryPath, "MacOS");

    public string TargetBundleResourcesDirectoryPath =>
        Path.Combine(TargetBundleContentsDirectoryPath, "Resources");

    private static string? TryResolveBundleIdentifierFromGitRemoteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var uri = Ssh.TryParse(url);
        if (uri is null && !Uri.TryCreate(url, UriKind.Absolute, out uri))
            return null;

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

    private void CopyApplicationFiles()
    {
        Log.LogMessage("Copying application files to the bundle...");

        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(TargetDirectoryPath))
        {
            // Skip the bundle itself to avoid infinite recursion
            if (Path.AreEqual(sourcePath, TargetBundleDirectoryPath))
                continue;

            Directory.CreateDirectory(TargetBundleBinDirectoryPath);

            var destinationPath = Path.Combine(
                TargetBundleBinDirectoryPath,
                Path.GetFileName(sourcePath)
            );

            // Source is a directory
            if (Directory.Exists(sourcePath))
            {
                Directory.Copy(sourcePath, destinationPath, true);
            }
            // Source is a file
            else
            {
                var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
                    Directory.CreateDirectory(destinationDirectoryPath);

                File.Copy(sourcePath, destinationPath, true);
            }
        }

        Log.LogMessage("Copied application files to '{0}'.", TargetBundleBinDirectoryPath);
    }

    private void CopyIconFile()
    {
        Log.LogMessage("Copying icon file to the bundle...");

        if (string.IsNullOrWhiteSpace(BundleIconFilePath))
        {
            Log.LogMessage("No icon file specified.");
            return;
        }

        Directory.CreateDirectory(TargetBundleResourcesDirectoryPath);

        var iconDestinationFilePath = Path.Combine(
            TargetBundleResourcesDirectoryPath,
            Path.GetFileName(Path.ChangeExtension(BundleIconFilePath, ".icns"))
        );

        // Direct copy
        if (
            string.Equals(
                Path.GetExtension(BundleIconFilePath),
                ".icns",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            File.Copy(BundleIconFilePath, iconDestinationFilePath, true);
        }
        // Conversion from ICO
        else if (
            string.Equals(
                Path.GetExtension(BundleIconFilePath),
                ".ico",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            using var sourceStream = File.OpenRead(BundleIconFilePath);
            using var destinationStream = File.Create(iconDestinationFilePath);

            var icon = Icon.LoadIco(sourceStream);
            icon.SaveIcns(destinationStream);
        }
        // Unknown format
        else
        {
            throw new InvalidOperationException(
                $"Unsupported icon file format: '{BundleIconFilePath}'."
            );
        }

        Log.LogMessage("Copied the icon file to '{0}'.", iconDestinationFilePath);
    }

    private void CreateManifestFile()
    {
        Log.LogMessage("Creating the manifest file...");

        Directory.CreateDirectory(TargetBundleContentsDirectoryPath);

        var manifestFilePath = Path.Combine(TargetBundleContentsDirectoryPath, "Info.plist");

        var properties = new BundleProperties
        {
            Identifier =
                BundleIdentifier
                ?? Git.TryGetRemoteOriginUrl()?.Pipe(TryResolveBundleIdentifierFromGitRemoteUrl)
                ?? BundleName,
            Name = BundleName,
            DisplayName = BundleDisplayName ?? BundleName,
            SpokenName = BundleSpokenName ?? BundleDisplayName ?? BundleName,
            ExecutableName = Path.GetFileNameWithoutExtension(TargetFilePath),
            Copyright = BundleCopyright,
            Version = BundleVersion ?? "1.0.0",
            ShortVersion = BundleShortVersion ?? BundleVersion ?? "1.0.0",
            IconName = !string.IsNullOrWhiteSpace(BundleIconFilePath)
                ? Path.GetFileName(Path.ChangeExtension(BundleIconFilePath, ".icns"))
                : null,
        };

        File.WriteAllText(manifestFilePath, properties.ToString());

        Log.LogMessage("Created the manifest file at '{0}'.", manifestFilePath);
    }

    public override bool Execute()
    {
        Log.LogMessage("Bundle identifier: '{0}'", BundleIdentifier);
        Log.LogMessage("Bundle name: '{0}'", BundleName);
        Log.LogMessage("Bundle display name: '{0}'", BundleDisplayName);
        Log.LogMessage("Bundle spoken name: '{0}'", BundleSpokenName);
        Log.LogMessage("Bundle copyright: '{0}'", BundleCopyright);
        Log.LogMessage("Bundle version: '{0}'", BundleVersion);
        Log.LogMessage("Bundle short version: '{0}'", BundleShortVersion);
        Log.LogMessage("Bundle icon: '{0}'", BundleIconFilePath);
        Log.LogMessage("Target: '{0}'", TargetFilePath);

        Directory.Reset(TargetBundleDirectoryPath);

        CopyApplicationFiles();
        CopyIconFile();
        CreateManifestFile();

        Log.LogMessage("Bundle successfully created.");
        return true;
    }
}
