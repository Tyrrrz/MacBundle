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

    public string? MacOSBundleName { get; set; }

    public string? Copyright { get; set; }

    public string? MacOSBundleIdentifier { get; set; }

    public string? ApplicationIcon { get; set; }

    public string? Version { get; set; }

    public string? AssemblyVersion { get; set; }

    public string? FileVersion { get; set; }

    public override bool Execute()
    {
        var options = new MacBundleProperties
        {
            ProjectDirectory = ProjectDirectory,
            OutputDirectory = OutputDirectory,
            AssemblyName = AssemblyName,
            MacOSBundleName = MacOSBundleName,
            Copyright = Copyright,
            MacOSBundleIdentifier = MacOSBundleIdentifier,
            ApplicationIcon = ApplicationIcon,
            Version = Version,
            AssemblyVersion = AssemblyVersion,
            FileVersion = FileVersion
        };

        return MacBundleGenerator.Generate(
            options,
            message => Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, "{0}", message),
            warning => Log.LogWarning("{0}", warning)
        );
    }
}
