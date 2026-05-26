namespace MacBundle.Tests;

public class CreateMacOSBundleTaskTests
{
    [Fact]
    public void Should_generate_bundle_with_metadata_and_output_files()
    {
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

            Assert.True(result);

            var bundlePath = Path.Combine(outputPath, "SampleApp.app");
            var plistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
            var executableBundlePath = Path.Combine(bundlePath, "Contents", "MacOS", "SampleApp");
            var libraryBundlePath = Path.Combine(bundlePath, "Contents", "MacOS", "SampleApp.dll");
            var iconBundlePath = Path.Combine(bundlePath, "Contents", "Resources", "AppIcon.icns");

            Assert.True(File.Exists(plistPath));
            Assert.True(File.Exists(executableBundlePath));
            Assert.True(File.Exists(libraryBundlePath));
            Assert.True(File.Exists(iconBundlePath));

            var plist = File.ReadAllText(plistPath);
            Assert.Contains("<string>SampleApp</string>", plist);
            Assert.Contains("<string>1.2.3.4</string>", plist);
            Assert.Contains("<string>1.2.3</string>", plist);
            Assert.Contains("<string>Copyright (C) Test</string>", plist);
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }
}
