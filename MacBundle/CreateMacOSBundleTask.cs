using Microsoft.Build.Framework;

namespace MacBundle;

public class CreateMacOSBundleTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public required string ProjectDirectory { get; set; }

    [Required]
    public required string OutputDirectory { get; set; }

    [Required]
    public required string AssemblyName { get; set; }

    public string? Product { get; set; }

    public string? MacOSBundleIdentifier { get; set; }

    public string? MacOSBundleName { get; set; }

    public string? MacOSBundleDisplayName { get; set; }

    public string? MacOSBundleSpokenName { get; set; }

    public string? MacOSBundleVersion { get; set; }

    public string? MacOSBundleShortVersion { get; set; }

    public string? MacOSBundleIcon { get; set; }

    public string? ApplicationIcon { get; set; }

    public string? MacOSBundleCopyright { get; set; }

    public string? Copyright { get; set; }

    public string? Version { get; set; }

    public string? AssemblyVersion { get; set; }

    public string? FileVersion { get; set; }

    public override bool Execute()
    {
        var gitRemoteUrl = CommandRunner.TryGetStandardOutput(
            "git",
            ["config", "--get", "remote.origin.url"],
            ProjectDirectory,
            System.TimeSpan.FromSeconds(3)
        );

        var identifier = MetadataResolver.ResolveAppIdentifier(
            MacOSBundleIdentifier,
            AssemblyName,
            gitRemoteUrl
        );
        var name =
            !string.IsNullOrWhiteSpace(MacOSBundleName) ? MacOSBundleName
            : !string.IsNullOrWhiteSpace(Product) ? Product
            : AssemblyName;
        var displayName = !string.IsNullOrWhiteSpace(MacOSBundleDisplayName)
            ? MacOSBundleDisplayName
            : name;
        var spokenName = !string.IsNullOrWhiteSpace(MacOSBundleSpokenName)
            ? MacOSBundleSpokenName
            : displayName;
        var version = !string.IsNullOrWhiteSpace(MacOSBundleVersion)
            ? MacOSBundleVersion
            : MetadataResolver.ResolveVersion(Version, AssemblyVersion, FileVersion, "1.0.0");
        var shortVersion = !string.IsNullOrWhiteSpace(MacOSBundleShortVersion)
            ? MacOSBundleShortVersion
            : MetadataResolver.ResolveShortVersion(version);
        var iconSourcePath = !string.IsNullOrWhiteSpace(MacOSBundleIcon)
            ? MacOSBundleIcon
            : ApplicationIcon;
        var copyright = !string.IsNullOrWhiteSpace(MacOSBundleCopyright)
            ? MacOSBundleCopyright
            : Copyright;

        var options = new MacBundleProperties
        {
            ProjectDirectory = ProjectDirectory,
            OutputDirectory = OutputDirectory,
            ExecutableName = AssemblyName,
            Identifier = identifier,
            Name = name,
            DisplayName = displayName,
            SpokenName = spokenName,
            Version = version,
            ShortVersion = shortVersion,
            IconSourcePath = iconSourcePath,
            Copyright = copyright,
        };

        return MacBundleGenerator.Generate(
            options,
            message => Log.LogMessage(MessageImportance.Low, "{0}", message),
            warning => Log.LogWarning("{0}", warning)
        );
    }
}
