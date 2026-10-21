#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;

// Rebuilds every checked-in notices file (licenses/notices/*.md) from the restored NuGet graph, so a package bump never
// needs a hand edit. The app's notices cover every supported Tesseract runtime, so the app is restored once per runtime into
// its own assets file. With --push, a changed result is committed to the current branch, pushed, and CI is started again for
// that branch, because a push made with the workflow token does not start CI by itself.
// Usage: dotnet run --file scripts/RefreshThirdPartyNotices.cs -- [--push]
const int TimeoutMinutes = 20;

const string AppProject = "src/HyperPdf.App/HyperPdf.App.csproj";

const string CommitMessage = "chore(deps): refresh third-party notices";

const string CiWorkflow = "ci.yml";

string[] tesseractRuntimes = ["linux-x64", "linux-arm64", "osx-arm64", "win-x64", "win-arm64"];

string[] libraryProjects = ["src/HyperPdfLibrary/HyperPdfLibrary.csproj", "src/HyperPdfLibrary.Render.Skia/HyperPdfLibrary.Render.Skia.csproj"];

var root = ScriptProcess.Root();

var push = args.Contains("--push");

var scratch = Path.Combine(Path.GetTempPath(), $"hyperpdf-notices-{Environment.ProcessId}");

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(TimeoutMinutes));

var token = cancellation.Token;

try
{
    var status = await GenerateAsync(AppProject, "HyperPDF", tesseractRuntimes);
    foreach (var project in libraryProjects)
    {
        status = status == 0 ? await GenerateAsync(project, Path.GetFileNameWithoutExtension(project), [string.Empty]) : status;
    }

    return status == 0 && push ? await PushAsync() : status;
}
finally
{
    if (Directory.Exists(scratch))
    {
        Directory.Delete(scratch, true);
    }
}

// Restores a project once per runtime, keeps a copy of each assets file, then writes the project's notices from the copies.
// The assets file stays in its default place: moving it with a global MSBuildProjectExtensionsPath would also move every
// referenced project's file to the same path.
async Task<int> GenerateAsync(string project, string name, string[] runtimes)
{
    var projectPath = Path.Combine(root, project);
    var assetsFile = (await ScriptProcess.CaptureAsync(ScriptProcess.Dotnet, ["msbuild", projectPath, "-getProperty:ProjectAssetsFile", "-nologo"], root, token)).Trim();
    var assets = new List<string>();
    foreach (var runtime in runtimes)
    {
        var restore = new List<string> { "restore", projectPath, "-v", "q", "--nologo" };
        if (runtime.Length > 0)
        {
            restore.Add($"-p:TesseractRuntimeIdentifier={runtime}");
        }

        var restored = await ScriptProcess.RunAsync(ScriptProcess.Dotnet, restore, root, token);
        if (restored != 0)
        {
            return restored;
        }

        var copy = Path.Combine(scratch, Path.GetFileNameWithoutExtension(project), runtime, "project.assets.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(assetsFile, copy, true);
        assets.Add(copy);
    }

    var components = (await ScriptProcess.CaptureAsync(ScriptProcess.Dotnet, ["msbuild", projectPath, "-getProperty:NoticesComponents", "-nologo"], root, token)).Trim();
    var generate = new List<string>
    {
        "run", "--file", Path.Combine(root, "scripts", "GenerateThirdPartyNotices.cs"), "--",
        "--out", Path.Combine(root, "licenses", "notices", $"{Path.GetFileNameWithoutExtension(project)}.md"),
        "--licences", Path.Combine(root, "licenses"),
        "--project-licence", Path.Combine(root, "LICENSE"),
        "--project-name", name,
    };
    foreach (var file in assets)
    {
        generate.Add("--assets");
        generate.Add(file);
    }

    foreach (var id in components.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        generate.Add("--component");
        generate.Add(id);
    }

    return await ScriptProcess.RunAsync(ScriptProcess.Dotnet, generate, root, token);
}

// Commits and pushes the notices when they changed, then starts CI for the branch.
async Task<int> PushAsync()
{
    var changed = (await ScriptProcess.CaptureAsync("git", ["status", "--porcelain", "--", "licenses/notices"], root, token)).Trim();
    if (changed.Length == 0)
    {
        Console.WriteLine("The notices are already current.");
        return 0;
    }

    var branch = (await ScriptProcess.CaptureAsync("git", ["rev-parse", "--abbrev-ref", "HEAD"], root, token)).Trim();
    string[][] steps =
    [
        ["config", "user.name", "github-actions[bot]"],
        ["config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com"],
        ["add", "licenses/notices"],
        ["commit", "-m", CommitMessage],
        ["push", "origin", $"HEAD:{branch}"],
    ];
    foreach (var step in steps)
    {
        var code = await ScriptProcess.RunAsync("git", step, root, token);
        if (code != 0)
        {
            return code;
        }
    }

    return await ScriptProcess.RunAsync("gh", ["workflow", "run", CiWorkflow, "--ref", branch], root, token);
}
