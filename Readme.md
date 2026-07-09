# MacBundle

<table>
    <tr>
        <td width="99999" align="center">Development of this project is entirely funded by the community. <b><a href="https://tyrrrz.me/donate">Consider donating to support!</a></b></td>
    </tr>
</table>

<p align="center">
    <img src="favicon.png" alt="Icon" />
</p>

Automated macOS bundling for .NET apps.

## Usage

1. Install the `MacBundle` NuGet package.
2. Build or publish your app.
3. If targeting macOS (RID starts with `osx`) — or building on macOS without explicitly setting a RID — MacBundle automatically generates an `.app` bundle next to build/publish output.

## Configuration

- `GenerateMacOSBundle` (`bool`) — explicitly enable/disable bundle generation. By default, this is auto-detected from target/host environment.
- `MacOSBundleName` (`string`) — app bundle name. Defaults to `AssemblyName`.
- `MacOSBundleIdentifier` (`string`) — app identifier. Defaults to:
  1. identifier derived from git remote (for GitHub remotes: `io.github.<Owner>.<Repo>`), then
  2. `MacOSBundleName`/`AssemblyName`.
- `ApplicationIcon` (`string`) — icon source file. `.icns` is copied directly; `.ico` (PNG-encoded entries and some BMP-backed entries) and `.png` are converted to `.icns` in managed code.

The generated bundle uses:

- `AppName` from `MacOSBundleName` or `AssemblyName`
- `AppCopyright` from `Copyright`
- `AppIdentifier` from `MacOSBundleIdentifier`, git remote, or app name
- `AppSpokenName` from app name

## Demo project

`MacBundle.Demo.Gui` demonstrates local testing without packing/publishing the NuGet package first.
