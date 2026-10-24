#!/usr/bin/env -S dotnet run --file
#:include StandardsRuleTableBuilder.cs
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Usage: dotnet run --file src/tools/StandardsRuleTableGenerator.cs -- <folder of veraPDF PDF_UA profile XML files> [repository root]
using System.Text;
using HyperPdf.Scripts;

const string OutputPath = "src/HyperPdfLibrary/Accessibility/Data/StandardsRules.tsv";

if (args.Length < 1)
{
    Console.Error.WriteLine("Pass the folder that holds the veraPDF profile XML files.");
    return 1;
}

var root = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();

var table = StandardsRuleTableBuilder.Build(args[0]);

await File.WriteAllTextAsync(Path.Combine(root, OutputPath), table, new UTF8Encoding(false));

Console.WriteLine($"Wrote {OutputPath}.");

return 0;
