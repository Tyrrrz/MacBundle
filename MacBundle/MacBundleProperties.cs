namespace MacBundle;

public class MacBundleProperties
{
    public required string ProjectDirectory { get; init; }

    public required string OutputDirectory { get; init; }

    public required string ExecutableName { get; init; }

    public required string Identifier { get; init; }

    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string SpokenName { get; init; }

    public required string Version { get; init; }

    public required string ShortVersion { get; init; }

    public string? IconSourcePath { get; init; }

    public string? Copyright { get; init; }
}
