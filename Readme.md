# MacBundle

[![Status](https://img.shields.io/badge/status-active-47c219.svg)](https://github.com/Tyrrrz/.github/blob/prime/docs/project-status.md)
[![Made in Ukraine](https://img.shields.io/badge/made_in-ukraine-ffd700.svg?labelColor=0057b7)](https://tyrrrz.me/ukraine)
[![Build](https://img.shields.io/github/actions/workflow/status/Tyrrrz/MacBundle/main.yml?branch=prime)](https://github.com/Tyrrrz/MacBundle/actions)
[![Version](https://img.shields.io/nuget/v/MacBundle.svg)](https://nuget.org/packages/MacBundle)
[![Downloads](https://img.shields.io/nuget/dt/MacBundle.svg)](https://nuget.org/packages/MacBundle)
[![Discord](https://img.shields.io/discord/869237470565392384?label=discord)](https://discord.gg/2SUWKFnHSm)
[![Fuck Russia](https://img.shields.io/badge/fuck-russia-e4181c.svg?labelColor=000000)](https://twitter.com/tyrrrz/status/1495972128977571848)

<table>
    <tr>
        <td width="99999" align="center">Development of this project is entirely funded by the community. <b><a href="https://tyrrrz.me/donate">Consider donating to support!</a></b></td>
    </tr>
</table>

<p align="center">
    <img src="favicon.png" alt="Icon" />
</p>

**MacBundle** is an MSBuild extension that automatically generates a macOS `.app` bundle for your .NET application.

## Terms of use<sup>[[?]](https://github.com/Tyrrrz/.github/blob/prime/docs/why-so-political.md)</sup>

By using this project or its source code, for any purpose and in any shape or form, you grant your **implicit agreement** to all the following statements:

- You **condemn Russia and its military aggression against Ukraine**
- You **recognize that Russia is an occupant that unlawfully invaded a sovereign state**
- You **support Ukraine's territorial integrity, including its claims over temporarily occupied territories of Crimea and Donbas**
- You **reject false narratives perpetuated by Russian state propaganda**

To learn more about the war and how you can help, [click here](https://tyrrrz.me/ukraine). Glory to Ukraine! 🇺🇦

## Install

- 📦 [NuGet](https://nuget.org/packages/MacBundle): `dotnet add package MacBundle`

## Usage

Simply install the **MacBundle** package as private dependency in your project to integrate it into the build process:

```xml
<ItemGroup>
  <PackageReference Include="MacBundle" PrivateAssets="all" />
</ItemGroup>
```

The application bundle will be generated automatically in the output directory when building or publishing the project:

```diff
  MyApp
  ├── bin
  │   └── Release
  │       └── net11.0
+ │           ├── MyApp.app
+ │           │  └── Contents
+ │           │      ├── MacOS
+ │           │      │   ├── MyApp
+ │           │      │   ├── MyApp.dll
+ │           │      │   └── (...)
+ │           │      ├── Resources
+ │           │      │   └── AppIcon.icns
+ │           │      └── Info.plist
  │           └── (...)
  ├── MyApp.csproj
  └── (...)
```

### Customizing behavior

#### Explicitly enable or disable bundling

By default, **MacBundle** only generates the `.app` bundle when it's relevant — i.e., when the build is targeting the macOS runtime or when the build is running on a macOS host without a specified runtime.
You can override this behavior by explicitly setting the `<GenerateMacOSBundle>` project property:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net11.0</TargetFramework>
    <!-- ... -->

    <!-- Always generate the macOS app bundle -->
    <GenerateMacOSBundle>true</GenerateMacOSBundle>
  </PropertyGroup>

  <!-- ... -->

</Project>
```

#### Configure metadata

To customize the generated bundle's metadata, you can set the following project properties:

- `<MacOSBundleName>` — bundle name, limited to 15 characters. Maps to the [`CFBundleName`](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundlename) key in the `Info.plist` file. Defaults to the value of `<Product>` or `<AssemblyName>`.
- `<MacOSBundleDisplayName>` — bundle name, not limited in length. Maps to the [`CFBundleDisplayName`](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundledisplayname) key in the `Info.plist` file. Defaults to the value of `<MacOSBundleName>`.
- `<MacOSBundleSpokenName>` — bundle name used by accessibility tools. Maps to the [`CFBundleSpokenName`](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundlespokenname) key in the `Info.plist` file. Defaults to the value of `<MacOSBundleName>`.
- `<MacOSBundleIdentifier>` — unique string used to uniquely identify the bundle within the system, typically in reverse domain name format. Maps to the [`CFBundleIdentifier`](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundleidentifier) key in the `Info.plist` file. Resolved from the configured git remote, if available, or defaults to the value of `<MacOSBundleName>`.
- `<MacOSBundleVersion>` — bundle version string, consisting of 1 to 3 period-separated numbers, used internally by the system. Maps to the [`CFBundleVersion`](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundleversion) key in the `Info.plist` file. Defaults to the value of `<Version>`, `<AssemblyVersion>`, `<FileVersion>`, or `1.0.0`.
- `<MacOSBundleShortVersion>` — bundle version string, consisting of 1 to 3 period-separated numbers, shown to the user. Maps to the [`CFBundleShortVersionString`](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundleshortversionstring) key in the `Info.plist` file. Defaults to the value of `<MacOSBundleVersion>`.
- `<MacOSBundleIcon>` — path to the icon file. You can provide an `.icns` file, which will be used as is — or an `.ico` file, which will be automatically converted to `.icns`. Defaults to the value of `<ApplicationIcon>`.
