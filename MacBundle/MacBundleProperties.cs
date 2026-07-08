namespace MacBundle;

public sealed class MacBundleProperties
{
    public required string ProjectDirectory { get; init; }

    public required string OutputDirectory { get; init; }

    public required string AssemblyName { get; init; }

    public string? MacOSBundleName { get; init; }

    public string? Copyright { get; init; }

    public string? MacOSBundleIdentifier { get; init; }

    public string? ApplicationIcon { get; init; }

    public string? Version { get; init; }

    public string? AssemblyVersion { get; init; }

    public string? FileVersion { get; init; }
}
