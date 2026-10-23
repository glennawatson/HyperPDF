#!/usr/bin/env -S dotnet run --file
#:include UnicodeTextTableBuilder.cs
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Downloads pinned Unicode Character Database files and writes the text extraction tables.
// Usage: dotnet run --file src/tools/UnicodeTextTableGenerator.cs -- [repository root] [Unicode version]
using HyperPdf.Scripts;

const string DefaultVersion = "18.0.0";

const string OutputPath = "src/HyperPdfLibrary/Text/TextUnicodeData.cs";

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

var version = args.Length > 1 ? args[1] : DefaultVersion;

using var http = new HttpClient();

var derivedBidiClass = await http.GetStringAsync(new Uri($"https://www.unicode.org/Public/{version}/ucd/extracted/DerivedBidiClass.txt"));

var bidiMirroring = await http.GetStringAsync(new Uri($"https://www.unicode.org/Public/{version}/ucd/BidiMirroring.txt"));

var unicodeData = await http.GetStringAsync(new Uri($"https://www.unicode.org/Public/{version}/ucd/UnicodeData.txt"));

await File.WriteAllTextAsync(Path.Combine(root, OutputPath), UnicodeTextTableBuilder.Emit(version, derivedBidiClass, bidiMirroring, unicodeData));

Console.WriteLine($"Wrote {OutputPath} from Unicode {version}.");
