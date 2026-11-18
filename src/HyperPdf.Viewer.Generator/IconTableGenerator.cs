// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.CodeAnalysis;

namespace HyperPdf.Viewer.Generator;

/// <summary>
/// Makes the viewer's icons from the SVG files the project lists as additional files. Each file is one icon: its name is the
/// member of <c>ChromeIcon</c>, its <c>title</c> the member's summary, the <c>data-tone</c> of its root the icon's tint, and its paths
/// and shapes the outline, on a 24 by 24 grid. An icon is added, changed or removed by adding, editing or deleting a file. A file
/// that cannot be read stops the build with the file's name and what is wrong.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class IconTableGenerator : IIncrementalGenerator
{
    /// <summary>The extension of the files that are icons.</summary>
    private const string Extension = ".svg";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var icons = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellation) => IconReader.Read(Path.GetFileNameWithoutExtension(file.Path), file.GetText(cancellation)?.ToString() ?? string.Empty))
            .WithTrackingName("Icons")
            .Collect();
        context.RegisterSourceOutput(icons, static (output, all) => output.AddSource("ChromeIcons.g.cs", IconEmitter.Emit(all)));
    }
}
