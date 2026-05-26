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
}
