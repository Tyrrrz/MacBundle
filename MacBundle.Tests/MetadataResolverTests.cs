using FluentAssertions;

namespace MacBundle.Tests;

public class MetadataResolverSpecs
{
    [Fact]
    public void I_can_resolve_an_identifier_from_a_github_ssh_remote()
    {
        // Arrange
        const string remote = "git@github.com:Tyrrrz/YoutubeDownloader.git";

        // Act
        var identifier = MetadataResolver.TryDeriveIdentifierFromGitRemote(
            remote
        );

        // Assert
        identifier.Should().Be("io.github.Tyrrrz.YoutubeDownloader");
    }

    [Fact]
    public void I_can_resolve_an_identifier_from_a_github_https_remote()
    {
        // Arrange
        const string remote = "https://github.com/Tyrrrz/YoutubeDownloader.git";

        // Act
        var identifier = MetadataResolver.TryDeriveIdentifierFromGitRemote(
            remote
        );

        // Assert
        identifier.Should().Be("io.github.Tyrrrz.YoutubeDownloader");
    }

    [Fact]
    public void I_fallback_to_the_app_name_when_the_remote_is_missing()
    {
        // Arrange
        const string appName = "YoutubeDownloader";

        // Act
        var identifier = MetadataResolver.ResolveAppIdentifier(null, appName, null);

        // Assert
        identifier.Should().Be(appName);
    }
}
