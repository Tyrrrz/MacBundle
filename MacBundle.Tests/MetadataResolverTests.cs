namespace MacBundle.Tests;

public class MetadataResolverTests
{
    [Fact]
    public void Should_resolve_identifier_from_github_ssh_remote()
    {
        var identifier = MetadataResolver.TryDeriveIdentifierFromGitRemote(
            "git@github.com:Tyrrrz/YoutubeDownloader.git"
        );

        Assert.Equal("io.github.Tyrrrz.YoutubeDownloader", identifier);
    }

    [Fact]
    public void Should_resolve_identifier_from_github_https_remote()
    {
        var identifier = MetadataResolver.TryDeriveIdentifierFromGitRemote(
            "https://github.com/Tyrrrz/YoutubeDownloader.git"
        );

        Assert.Equal("io.github.Tyrrrz.YoutubeDownloader", identifier);
    }

    [Fact]
    public void Should_fallback_identifier_to_app_name_when_remote_is_missing()
    {
        var identifier = MetadataResolver.ResolveAppIdentifier(null, "YoutubeDownloader", null);

        Assert.Equal("YoutubeDownloader", identifier);
    }
}
