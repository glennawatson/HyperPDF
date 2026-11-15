#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;

// Rebuilds every checked-in notices file (src/licenses/notices/*.md) from the restored NuGet graph, so a package bump never
// needs a hand edit. With --push, a changed result is committed and pushed to the current branch. The workflow uses a
// repository's GitHub App so the push can update main and start CI.
// Usage: dotnet run --file src/tools/RefreshThirdPartyNotices.cs -- [--push]
const int TimeoutMinutes = 20;

// The three heads share one notices file, so its packages are the union of what each head restores. The Linux head names the data components.
string[] appProjects = ["src/HyperPdf.Head.Linux/HyperPdf.Head.Linux.csproj", "src/HyperPdf.Head.Windows/HyperPdf.Head.Windows.csproj", "src/HyperPdf.Head.MacOS/HyperPdf.Head.MacOS.csproj"];

const string AppNotices = "HyperPdf";

const string CommitMessage = "chore(deps): refresh third-party notices";

string[] libraryProjects = ["src/HyperPdfLibrary/HyperPdfLibrary.csproj", "src/HyperPdfLibrary.Render.Skia/HyperPdfLibrary.Render.Skia.csproj"];

var root = ScriptProcess.Root();

var push = args.Contains("--push");

var scratch = Path.Combine(Path.GetTempPath(), $"hyperpdf-notices-{Environment.ProcessId}");

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(TimeoutMinutes));

var token = cancellation.Token;

try
{
    var status = await GenerateAsync(appProjects, AppNotices, "HyperPDF");
    foreach (var project in libraryProjects)
    {
        var name = Path.GetFileNameWithoutExtension(project);
        status = status == 0 ? await GenerateAsync([project], name, name) : status;
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

// Restores the projects, keeps a copy of each assets file, then writes the notices of all of them into one file. The assets files
// stay in their default places: moving them with a global MSBuildProjectExtensionsPath would also move every referenced
// project's file to the same path. The first project names the data components.
async Task<int> GenerateAsync(string[] projects, string file, string name)
{
    var assets = new List<string>();
    foreach (var project in projects)
    {
        var projectPath = Path.Combine(root, project);
        var restored = await ScriptProcess.RunAsync(ScriptProcess.Dotnet, ["restore", projectPath, "-v", "q", "--nologo"], root, token);
        if (restored != 0)
        {
            return restored;
        }

        var assetsFile = (await ScriptProcess.CaptureAsync(ScriptProcess.Dotnet, ["msbuild", projectPath, "-getProperty:ProjectAssetsFile", "-nologo"], root, token)).Trim();
        var copy = Path.Combine(scratch, Path.GetFileNameWithoutExtension(project), "project.assets.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(assetsFile, copy, true);
        assets.Add(copy);
    }

    var components = (await ScriptProcess.CaptureAsync(ScriptProcess.Dotnet, ["msbuild", Path.Combine(root, projects[0]), "-getProperty:NoticesComponents", "-nologo"], root, token)).Trim();
    var generate = new List<string>
    {
        "run", "--file", Path.Combine(root, "src", "tools", "GenerateThirdPartyNotices.cs"), "--",
        "--out", Path.Combine(root, "src", "licenses", "notices", $"{file}.md"),
        "--licences", Path.Combine(root, "src", "licenses"),
        "--project-licence", Path.Combine(root, "LICENSE"),
        "--project-name", name,
    };
    foreach (var assetsFile in assets)
    {
        generate.Add("--assets");
        generate.Add(assetsFile);
    }

    foreach (var id in components.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        generate.Add("--component");
        generate.Add(id);
    }

    return await ScriptProcess.RunAsync(ScriptProcess.Dotnet, generate, root, token);
}

// Commits and pushes the notices when they changed.
async Task<int> PushAsync()
{
    var changed = (await ScriptProcess.CaptureAsync("git", ["status", "--porcelain", "--", "src/licenses/notices"], root, token)).Trim();
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
        ["add", "src/licenses/notices"],
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

    return 0;
}
