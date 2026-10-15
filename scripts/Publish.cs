#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;

const int Usage = 2;

if (args.Length == 0 || args[0] == "--help")
{
    Console.WriteLine("dotnet run --file scripts/Publish.cs -- <runtime identifier> [dotnet publish arguments]");
    return args.Length == 0 ? Usage : 0;
}

var root = ScriptProcess.Root();

var rid = args[0];

if (rid is not ("linux-x64" or "linux-arm64" or "win-x64" or "win-arm64" or "osx-x64" or "osx-arm64"))
{
    throw new ArgumentException("Unsupported runtime identifier.");
}

var output = Path.Combine(root, "artifacts", rid);

var symbols = Path.Combine(root, "artifacts", "symbols", rid);

if (Directory.Exists(output))
{
    Directory.Delete(output, true);
}

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

string[] arguments = [
    "publish",
    Path.Combine(root, "src", "HyperPdf.App", "HyperPdf.App.csproj"),
    "-c",
    "Release",
    "-f",
    "net11.0",
    "-r",
    rid,
    "-o",
    output,
    ..args[1..]];

var status = await ScriptProcess.RunAsync(ScriptProcess.Dotnet, arguments, root, cancellation.Token);

if (status != 0)
{
    return status;
}

_ = Directory.CreateDirectory(symbols);

foreach (var file in Directory.EnumerateFiles(output))
{
    if (Path.GetExtension(file) is ".dbg" or ".pdb")
    {
        File.Move(file, Path.Combine(symbols, Path.GetFileName(file)), true);
    }
}

foreach (var directory in Directory.EnumerateDirectories(output, "*.dSYM"))
{
    var target = Path.Combine(symbols, Path.GetFileName(directory));
    if (Directory.Exists(target))
    {
        Directory.Delete(target, true);
    }

    Directory.Move(directory, target);
}

Console.WriteLine($"Published {rid} to {output}");

return 0;
