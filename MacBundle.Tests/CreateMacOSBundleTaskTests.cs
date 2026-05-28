using FluentAssertions;
using System.Xml.Linq;

namespace MacBundle.Tests;

public class MacBundleGeneratorSpecs
{
    [Fact]
    public void I_can_generate_a_bundle_with_metadata_and_output_files()
    {
        // Arrange
        var rootPath = Path.Combine(Path.GetTempPath(), "macbundle-tests-" + Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(rootPath, "project");
        var outputPath = Path.Combine(rootPath, "output");
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(outputPath);

        try
        {
            var executablePath = Path.Combine(outputPath, "SampleApp");
            var libraryPath = Path.Combine(outputPath, "SampleApp.dll");
            var iconPath = Path.Combine(projectPath, "app.icns");

            File.WriteAllText(executablePath, "#!/bin/sh");
            File.WriteAllText(libraryPath, "binary-content");
            File.WriteAllText(iconPath, "icns-data");

            // Act
            var result = MacBundleGenerator.Generate(
                new MacBundleGeneratorOptions
                {
                    ProjectDirectory = projectPath,
                    OutputDirectory = outputPath,
                    AssemblyName = "SampleApp",
                    Copyright = "Copyright (C) Test",
                    Version = "1.2.3.4",
                    ApplicationIcon = "app.icns"
                }
            );

            // Assert
            result.Should().BeTrue();

            var bundlePath = Path.Combine(outputPath, "SampleApp.app");
            var plistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
            var executableBundlePath = Path.Combine(bundlePath, "Contents", "MacOS", "SampleApp");
            var libraryBundlePath = Path.Combine(bundlePath, "Contents", "MacOS", "SampleApp.dll");
            var iconBundlePath = Path.Combine(bundlePath, "Contents", "Resources", "AppIcon.icns");

            File.Exists(plistPath).Should().BeTrue();
            File.Exists(executableBundlePath).Should().BeTrue();
            File.Exists(libraryBundlePath).Should().BeTrue();
            File.Exists(iconBundlePath).Should().BeTrue();

            var plist = File.ReadAllText(plistPath);
            var doc = XDocument.Parse(plist);
            var valuesByKey = new Dictionary<string, string>();
            var elements = doc.Root!.Element("dict")!.Elements().ToArray();
            for (var i = 0; i < elements.Length - 1; i++)
            {
                if (elements[i].Name.LocalName != "key")
                    continue;

                valuesByKey[elements[i].Value] = elements[i + 1].Value;
            }

            valuesByKey["CFBundleDisplayName"].Should().Be("SampleApp");
            valuesByKey["CFBundleVersion"].Should().Be("1.2.3.4");
            valuesByKey["CFBundleShortVersionString"].Should().Be("1.2.3");
            valuesByKey["NSHumanReadableCopyright"].Should().Be("Copyright (C) Test");
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public void I_can_generate_an_icns_file_from_a_png_icon()
    {
        // Arrange
        var rootPath = Path.Combine(Path.GetTempPath(), "macbundle-tests-" + Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(rootPath, "project");
        var outputPath = Path.Combine(rootPath, "output");
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(outputPath);

        try
        {
            var executablePath = Path.Combine(outputPath, "SampleApp");
            var pngIconPath = Path.Combine(projectPath, "app.png");

            File.WriteAllText(executablePath, "#!/bin/sh");
            File.WriteAllBytes(
                pngIconPath,
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/aS8AAAAASUVORK5CYII="
                )
            );

            // Act
            var result = MacBundleGenerator.Generate(
                new MacBundleGeneratorOptions
                {
                    ProjectDirectory = projectPath,
                    OutputDirectory = outputPath,
                    AssemblyName = "SampleApp",
                    ApplicationIcon = "app.png"
                }
            );

            // Assert
            result.Should().BeTrue();

            var iconBundlePath = Path.Combine(
                outputPath,
                "SampleApp.app",
                "Contents",
                "Resources",
                "AppIcon.icns"
            );
            File.Exists(iconBundlePath).Should().BeTrue();

            var iconBytes = File.ReadAllBytes(iconBundlePath);
            AssertIcnsPayload(iconBytes).Should().Equal(
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/aS8AAAAASUVORK5CYII="
                )
            );
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public void I_can_generate_an_icns_file_from_an_ico_icon()
    {
        // Arrange
        var rootPath = Path.Combine(Path.GetTempPath(), "macbundle-tests-" + Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(rootPath, "project");
        var outputPath = Path.Combine(rootPath, "output");
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(outputPath);

        try
        {
            var executablePath = Path.Combine(outputPath, "SampleApp");
            var icoIconPath = Path.Combine(projectPath, "app.ico");
            var pngData = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/aS8AAAAASUVORK5CYII="
            );

            File.WriteAllText(executablePath, "#!/bin/sh");
            File.WriteAllBytes(icoIconPath, CreateIcoFromPng(pngData));

            // Act
            var result = MacBundleGenerator.Generate(
                new MacBundleGeneratorOptions
                {
                    ProjectDirectory = projectPath,
                    OutputDirectory = outputPath,
                    AssemblyName = "SampleApp",
                    ApplicationIcon = "app.ico"
                }
            );

            // Assert
            result.Should().BeTrue();

            var iconBundlePath = Path.Combine(
                outputPath,
                "SampleApp.app",
                "Contents",
                "Resources",
                "AppIcon.icns"
            );
            File.Exists(iconBundlePath).Should().BeTrue();

            var iconBytes = File.ReadAllBytes(iconBundlePath);
            AssertIcnsPayload(iconBytes).Should().Equal(pngData);
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }

    private static byte[] CreateIcoFromPng(byte[] pngData)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);

        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)pngData.Length);
        writer.Write((uint)(6 + 16));

        writer.Write(pngData);
        writer.Flush();

        return stream.ToArray();
    }

    private static byte[] AssertIcnsPayload(byte[] icnsData)
    {
        icnsData.Take(4).Should().Equal(new byte[] { (byte)'i', (byte)'c', (byte)'n', (byte)'s' });
        icnsData.Skip(8).Take(4).Should().Equal(new byte[] { (byte)'i', (byte)'c', (byte)'1', (byte)'0' });

        var totalLength = ReadUInt32BigEndian(icnsData, 4);
        var chunkLength = ReadUInt32BigEndian(icnsData, 12);

        totalLength.Should().Be((uint)icnsData.Length);
        chunkLength.Should().Be((uint)(icnsData.Length - 8));

        return icnsData.Skip(16).ToArray();
    }

    private static uint ReadUInt32BigEndian(byte[] data, int offset) =>
        (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
}
