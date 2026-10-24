// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdf.Benchmarks;

/// <summary>Takes the shaping plan of a font for Latin text with no feature settings, as every shaped run does, once the plan has been made.</summary>
public class HyperPdfShapePlanBenchmarks
{
    /// <summary>The font's layout tables.</summary>
    private OtFontLayout _layout = null!;

    /// <summary>The script of the text.</summary>
    private ShapingScript _script = null!;

    /// <summary>Reads the bundled sans font and makes the plan once.</summary>
    /// <exception cref="InvalidOperationException">The bundled font is missing or cannot be read.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var data = BundledFaces.Decode(BundledFamily.Sans, false, false) ?? throw new InvalidOperationException("The bundled font is missing.");
        if (!SfntFont.TryRead(data, 0, out var font, out var refusal))
        {
            throw new InvalidOperationException(refusal);
        }

        _layout = new(font);
        _script = UnicodeScripts.Of('a');
        _ = _layout.PlanFor(_script, 0, false, false, null);
    }

    /// <summary>Takes the plan from the cache.</summary>
    /// <returns>Whether a plan was returned.</returns>
    [Benchmark]
    public bool CachedPlan() => _layout.PlanFor(_script, 0, false, false, null) is not null;
}
