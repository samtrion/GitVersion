using Cake.Common.Tools.DotNet.Publish;
using Common.Utilities;

namespace Build.Tasks;

[TaskName(nameof(PackagePrepare))]
[TaskDescription("Prepares for packaging")]
[IsDependentOn(typeof(ValidateVersion))]
public class PackagePrepare : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        PackPrepareNative(context);

        var sourceDir = Paths.Native.Combine(PlatformFamily.Windows.ToString()).Combine("win-x64");
        var sourceFiles = context.GetFiles(sourceDir + "/*.*");

        var portableDir = Paths.ArtifactsBinPortable.Combine("tools");
        context.EnsureDirectoryExists(portableDir);

        sourceFiles += context.GetFiles("./build/nuspec/*.ps1") + context.GetFiles("./build/nuspec/*.txt");
        context.CopyFiles(sourceFiles, portableDir);
    }

    private static void PackPrepareNative(BuildContext context)
    {
        // publish single file for all native runtimes (self contained)
        var platform = context.Platform;
        var runtimes = context.NativeRuntimes[platform];

        foreach (var runtime in runtimes)
        {
            var outputPath = PackPrepareNative(context, runtime);

            // testing windows and macos artifacts, the linux is tested with docker
            if (platform == PlatformFamily.Linux) continue;

            if (context.IsRunningOnAmd64() && runtime.EndsWith("x64") || context.IsRunningOnArm64() && runtime.EndsWith("arm64"))
            {
                context.Information("Validating native lib:");
                var nativeExe = outputPath.CombineWithFilePath(context.IsOnWindows ? "gitversion.exe" : "gitversion");
                context.ValidateOutput(nativeExe.FullPath, "/showvariable FullSemver", context.Version?.GitVersion?.FullSemVer);
            }
        }
    }

    private static DirectoryPath PackPrepareNative(BuildContext context, string runtime)
    {
        var platform = context.Platform;
        var outputPath = Paths.Native.Combine(platform.ToString().ToLower()).Combine(runtime);

        var settings = new DotNetPublishSettings
        {
            Framework = $"net{Constants.VersionLatest}",
            Runtime = runtime,
            NoRestore = false,
            Configuration = context.MsBuildConfiguration,
            OutputDirectory = outputPath,
            MSBuildSettings = context.MsBuildSettings,
            IncludeNativeLibrariesForSelfExtract = true,
            PublishSingleFile = true,
            SelfContained = true
        };

        context.DotNetPublish("./src/GitVersion.App/GitVersion.App.csproj", settings);

        return outputPath;
    }
}
