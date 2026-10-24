// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Engine;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures choosing a font for text that no installed font covers, which is what typing a rare script into a free text box asks for
/// on every edit. It uses the fonts installed on the machine, so the numbers depend on how many there are.
/// </summary>
public class HyperPdfTextBoxFallbackBenchmarks
{
    /// <summary>A code point that is not assigned, so no installed font has a glyph for it.</summary>
    private const string Text = "͸";

    /// <summary>The size of the text.</summary>
    private const float FontSize = 12F;

    /// <summary>The installed fonts.</summary>
    private FontCatalog _catalog = null!;

    /// <summary>The engine's font state.</summary>
    private TextBoxFontState _state = null!;

    /// <summary>The format of the text.</summary>
    private TextFormat _format = null!;

    /// <summary>Scans the installed fonts once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _catalog = FontCatalog.Scan(FontCatalog.SystemDirectories());
        _state = new(new(() => _catalog));
        _format = new("Helvetica", FontSize, 0);
    }

    /// <summary>Chooses a font for the uncovered text.</summary>
    /// <returns>The font chosen.</returns>
    [Benchmark]
    public object ResolveUncovered() => TextBoxFonts.Resolve(_state, _catalog, _format, Text)!;
}
