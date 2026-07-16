using System;
using System.IO;
using MacBundle.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using PowerKit.Extensions;

namespace MacBundle;

public class BundleTask : Task
{
    [Required]
    public required string? BundleIdentifier { get; set; }

    [Required]
    public required string BundleName { get; set; }

    [Required]
    public required string BundleDisplayName { get; set; }

    [Required]
    public required string BundleSpokenName { get; set; }

    [Required]
    public required string? BundleCopyright { get; set; }

    [Required]
    public required string BundleVersion { get; set; }

    [Required]
    public required string BundleShortVersion { get; set; }

    [Required]
    public required string? BundleIconFilePath { get; set; }

    [Required]
    public required string TargetFilePath { get; set; }

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

    private void InitializeDirectories()
    {
        Log.LogMessage("Creating bundle directories...");

        Directory.Reset(TargetBundleDirectoryPath);
        Directory.CreateDirectory(TargetBundleBinDirectoryPath);
        Directory.CreateDirectory(TargetBundleResourcesDirectoryPath);

        Log.LogMessage("Created bundle directories at '{0}'.", TargetBundleDirectoryPath);
    }

    private void CopyBinaryFiles()
    {
        Log.LogMessage("Copying binary files to bundle...");

        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(TargetDirectoryPath))
        {
            if (
                Path.GetFullPath(sourcePath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                == Path.GetFullPath(TargetBundleDirectoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            )
            {
                continue;
            }

            var destinationPath = Path.Combine(
                TargetBundleBinDirectoryPath,
                Path.GetFileName(sourcePath)
            );

            if (Directory.Exists(sourcePath))
            {
                Directory.Copy(sourcePath, destinationPath, true);
            }
            else
            {
                var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
                    Directory.CreateDirectory(destinationDirectoryPath);

                File.Copy(sourcePath, destinationPath, true);
            }
        }

        Log.LogMessage("Copied binary files to bundle at '{0}'.", TargetBundleBinDirectoryPath);
    }

    private void CopyIconFile()
    {
        Log.LogMessage("Copying icon file to bundle...");

        if (string.IsNullOrWhiteSpace(BundleIconFilePath))
        {
            Log.LogMessage("No bundle icon file specified.");
            return;
        }

        var iconDestinationFilePath = Path.Combine(
            TargetBundleResourcesDirectoryPath,
            BundleProperties.IconName + ".icns"
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
            Log.LogMessage("Provided icon file is in the ICNS format.");
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
            Log.LogMessage("Provided icon file is in the ICO format.");

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

        Log.LogMessage("Copied icon file to bundle at '{0}'.", iconDestinationFilePath);
    }

    private void CreateManifestFile()
    {
        Log.LogMessage("Creating manifest file...");

        var manifestFilePath = Path.Combine(TargetBundleContentsDirectoryPath, "Info.plist");

        var properties = new BundleProperties
        {
            Identifier =
                BundleIdentifier
                ?? BundleProperties.TryGetIdentifierFromUrl(
                    GitConfiguration.TryResolve(TargetDirectoryPath)?.RemoteOriginUrl
                )
                ?? BundleName,
            Name = BundleName,
            DisplayName = BundleDisplayName,
            SpokenName = BundleSpokenName,
            ExecutableName = Path.GetFileNameWithoutExtension(TargetFilePath),
            Copyright = BundleCopyright,
            Version = BundleVersion,
            ShortVersion = BundleShortVersion,
        };

        File.WriteAllText(manifestFilePath, properties.ToString());

        Log.LogMessage("Created manifest file at '{0}'.", manifestFilePath);
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

        InitializeDirectories();
        CopyBinaryFiles();
        CopyIconFile();
        CreateManifestFile();

        Log.LogMessage("Bundle successfully created.");
        return true;
    }
}
