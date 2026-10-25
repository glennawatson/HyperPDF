#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:include LocalisationResources.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Scripts;

// Usage: dotnet run --file src/tools/GenerateLocalisation.cs -- [--require-complete]
const int UsageError = 2;

if (args is not ([] or ["--require-complete"] or ["-RequireComplete"]))
{
    Console.Error.WriteLine("Usage: dotnet run --file src/tools/GenerateLocalisation.cs -- [--require-complete]");
    return UsageError;
}

var repository = Repository();

var count = LocalisationResources.Generate(
    Path.Combine(repository, "docs", "localisation"),
    Path.Combine(repository, "src", "HyperPdf.Localisation"),
    args.Length != 0);

Console.WriteLine($"Generated {count} localisation resource keys.");

return 0;

// The source path keeps the tool independent of its caller's working directory.
static string Repository([CallerFilePath] string source = "") =>
    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
