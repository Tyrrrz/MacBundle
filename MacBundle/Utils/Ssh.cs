using System;

namespace MacBundle.Utils;

internal static class Ssh
{
    private static Uri? TryParseUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "ssh" ? uri : null;

    private static Uri? TryParseScp(string scp)
    {
        var atIndex = scp.IndexOf('@');
        var colonIndex = scp.IndexOf(':');

        if (colonIndex < 0)
        {
            return null;
        }

        var username = atIndex >= 0 ? scp[..atIndex] : null;
        var host = atIndex >= 0 ? scp[(atIndex + 1)..colonIndex] : scp[..colonIndex];
        var path = scp[(colonIndex + 1)..];

        return new UriBuilder
        {
            Scheme = "ssh",
            Host = host,
            Path = path,
            UserName = username ?? string.Empty,
        }.Uri;
    }

    public static Uri? TryParse(string urlOrScp) => TryParseUrl(urlOrScp) ?? TryParseScp(urlOrScp);
}
