#!/usr/bin/env -S dotnet run --file
#:include KnownNameTableBuilder.cs
#:include ../src/HyperPdfLibrary/Objects/KnownNameHash.cs
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Usage: dotnet run --file src/tools/KnownNameTableGenerator.cs -- [repository root]
using HyperPdf.Scripts;

const string InputPath = "src/HyperPdfLibrary/Objects/KnownName.cs";

const string OutputPath = "src/HyperPdfLibrary/Objects/KnownNameSpellings.cs";

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

var source = await File.ReadAllTextAsync(Path.Combine(root, InputPath));

await File.WriteAllTextAsync(Path.Combine(root, OutputPath), KnownNameTableBuilder.Emit(source));

Console.WriteLine($"Wrote {OutputPath}.");
