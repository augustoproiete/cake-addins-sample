#addin "nuget:?package=Cake.MinVer&version=1.0.1"
#addin "nuget:?package=Cake.Args&version=1.0.0"

var target       = ArgumentOrDefault<string>("target") ?? "pack";
var buildVersion = MinVer(s => s.WithTagPrefix("v").WithDefaultPreReleasePhase("preview"));

Task("clean")
    .Does(() =>
{
    CleanDirectory("./build/artifacts");
    CleanDirectories("./src/**/bin");
    CleanDirectories("./src/**/obj");
    CleanDirectories("./test/**/bin");
    CleanDirectories("./test/**/obj");
});

Task("restore")
    .IsDependentOn("clean")
    .Does(() =>
{
    DotNetCoreRestore("./cake-addins-sample.sln", new DotNetCoreRestoreSettings
    {
        LockedMode = true,
    });
});

Task("build")
    .IsDependentOn("restore")
    .Does(() =>
{
    DotNetCoreBuild("./cake-addins-sample.sln", new DotNetCoreBuildSettings
    {
        Configuration = "Debug",
        NoRestore = true,
        NoIncremental = false,
        ArgumentCustomization = args =>
            args.AppendQuoted($"-p:Version={buildVersion.Version}")
                .AppendQuoted($"-p:AssemblyVersion={buildVersion.FileVersion}")
                .AppendQuoted($"-p:FileVersion={buildVersion.FileVersion}")
                .AppendQuoted($"-p:ContinuousIntegrationBuild=true")
    });

    DotNetCoreBuild("./cake-addins-sample.sln", new DotNetCoreBuildSettings
    {
        Configuration = "Release",
        NoRestore = true,
        NoIncremental = false,
        ArgumentCustomization = args =>
            args.AppendQuoted($"-p:Version={buildVersion.Version}")
                .AppendQuoted($"-p:AssemblyVersion={buildVersion.FileVersion}")
                .AppendQuoted($"-p:FileVersion={buildVersion.FileVersion}")
                .AppendQuoted($"-p:ContinuousIntegrationBuild=true")
    });
});

Task("test")
    .IsDependentOn("build")
    .Does(() =>
{
    var settings = new DotNetCoreTestSettings
    {
        Configuration = "Release",
        NoRestore = true,
        NoBuild = true,
    };

    var projectFiles = GetFiles("./test/**/*.csproj");
    foreach (var file in projectFiles)
    {
        DotNetCoreTest(file.FullPath, settings);
    }
});

Task("pack")
    .IsDependentOn("test")
    .Does(() =>
{
    var releaseNotes = $"https://github.com/augustoproiete/cake-addins-sample/releases/tag/v{buildVersion.Version}";

    DotNetCorePack("./src/Cake.Addins.Sample/Cake.Addins.Sample.csproj", new DotNetCorePackSettings
    {
        Configuration = "Release",
        NoRestore = true,
        NoBuild = true,
        OutputDirectory = "./build/artifacts",
        ArgumentCustomization = args =>
            args.AppendQuoted($"-p:Version={buildVersion.Version}")
                .AppendQuoted($"-p:PackageReleaseNotes={releaseNotes}")
    });
});

Task("publish")
    .IsDependentOn("pack")
    .Does(context =>
{
    var url =  context.EnvironmentVariable("NUGET_URL");
    if (string.IsNullOrWhiteSpace(url))
    {
        context.Information("No NuGet URL specified. Skipping publishing of NuGet packages");
        return;
    }

    var apiKey =  context.EnvironmentVariable("NUGET_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        context.Information("No NuGet API key specified. Skipping publishing of NuGet packages");
        return;
    }

    var nugetPushSettings = new DotNetCoreNuGetPushSettings
    {
        Source = url,
        ApiKey = apiKey,
    };

    foreach (var nugetPackageFile in GetFiles("./build/artifacts/*.nupkg"))
    {
        DotNetCoreNuGetPush(nugetPackageFile.FullPath, nugetPushSettings);
    }
});

RunTarget(target);
