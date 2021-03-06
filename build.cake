#tool "GitVersion.CommandLine"
///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutionPath = File("./src/Cake.ViewCompiler.sln");
var solution = ParseSolution(solutionPath);
var projects = solution.Projects;
var projectPaths = projects.Select(p => p.Path.GetDirectory());
var testAssemblies = projects.Where(p => p.Name.Contains("Test")).Select(p => p.Path.GetDirectory() + "/bin/" + configuration + "/" + p.Name + ".dll");
var artifacts = "./dist/";
var testResultsPath = MakeAbsolute(Directory(artifacts + "./test-results"));
GitVersion versionInfo = null;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(() =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
	versionInfo = GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true
    });
	Information("Building for version {0}", versionInfo.FullSemVer);
});

Teardown(() =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in projectPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
    }
    Information("Cleaning common files...");
    CleanDirectory(artifacts);
});

Task("Restore")
    .Does(() =>
{
    // Restore all NuGet packages.
    Information("Restoring solution...");
    NuGetRestore(solutionPath);
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
	Information("Building solution...");
	MSBuild(solutionPath, settings =>
		settings.SetPlatformTarget(PlatformTarget.MSIL)
			.WithProperty("TreatWarningsAsErrors","true")
			.SetVerbosity(Verbosity.Quiet)
			.WithTarget("Build")
			.SetConfiguration(configuration));
});

Task("Copy-Files")
    .IsDependentOn("Build")
    .Does(() =>
{
    CreateDirectory(artifacts + "build");
	foreach (var project in projects) {
		CreateDirectory(artifacts + "build/" + project.Name);
		var files = GetFiles(project.Path.GetDirectory() +"/bin/" +configuration +"/" + "*.dll");
		CopyFiles(files, artifacts + "build/" + project.Name);
	}
});

Task("NuGet")
    .IsDependentOn("Copy-Files")
    .Does(() => {
		CreateDirectory(artifacts + "package/");
        Information("Building NuGet package");
        var nuspecFiles = GetFiles("./**/*.nuspec");
        NuGetPack(nuspecFiles, new NuGetPackSettings() {
			Version = versionInfo.NuGetVersionV2,
            Files = new [] {
                new NuSpecContent { Source = "Cake.ViewCompiler/Cake.ViewCompiler.dll", Target = "lib/net45"}
            },
            BasePath = artifacts + "build",
            OutputDirectory = artifacts + "package"
        });
        //MoveFiles("./**/*DocCreator.*.nupkg", artifacts + "/package/");
    });

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("NuGet");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
