# MacBundle

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
- `ApplicationIcon` (`string`) — icon source file. `.icns` is copied directly; on macOS, other image formats are converted to `.icns`.

The generated bundle uses:

- `AppName` from `MacOSBundleName` or `AssemblyName`
- `AppCopyright` from `Copyright`
- `AppIdentifier` from `MacOSBundleIdentifier`, git remote, or app name
- `AppSpokenName` from app name
