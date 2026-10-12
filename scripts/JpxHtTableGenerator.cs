#!/usr/bin/env -S dotnet run --file
#:include JpxHtTableBuilder.cs
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Builds the HT cleanup pass lookup tables from the CxtVLC rows of ITU-T T.814 Annex C and writes JpxHtTables.cs.
// Usage: dotnet run --file scripts/JpxHtTableGenerator.cs -- [repository root]
using HyperPdf.Scripts;

const string DataPath = "src/HyperPdfLibrary/Graphics/Images/Jpx/Data/T814CxtVlc.txt";

const string OutputPath = "src/HyperPdfLibrary/Graphics/Images/Jpx/JpxHtTables.cs";

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

var rows = JpxHtTableBuilder.ParseRows(await File.ReadAllTextAsync(Path.Combine(root, DataPath)));

await File.WriteAllTextAsync(Path.Combine(root, OutputPath), JpxHtTableBuilder.Emit(rows));

Console.WriteLine($"Wrote {OutputPath} from {rows[0].Length} and {rows[1].Length} rows.");
