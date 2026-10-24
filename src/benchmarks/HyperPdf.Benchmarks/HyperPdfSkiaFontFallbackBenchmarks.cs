// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Avalonia.Media;
using BenchmarkDotNet.Attributes;
using HyperPdf.Skia.Fonts;

namespace HyperPdf.Benchmarks;

/// <summary>Asks the Skia font manager for a fallback font for 200 characters of mixed scripts, which Avalonia does for every character a text run's font lacks.</summary>
public class HyperPdfSkiaFontFallbackBenchmarks
{
    /// <summary>The calls made by one benchmark call.</summary>
    private const int Calls = 200;

    /// <summary>The characters asked for.</summary>
    private static readonly int[] Codepoints = [0x4E2D, 0x2603, 0x0627, 0x0E01, 0x3042, 0x0905, 0x1F600, 0x00E9];

    /// <summary>The font manager.</summary>
    private readonly FontManagerImpl _fonts = new();

    /// <summary>Asks for a fallback typeface for each character in turn.</summary>
    /// <returns>The number of characters a font was found for.</returns>
    [Benchmark]
    public int MatchCharacters()
    {
        var found = 0;
        for (var i = 0; i < Calls; i++)
        {
            if (!_fonts.TryMatchCharacter(Codepoints[i % Codepoints.Length], FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, "Inter", CultureInfo.InvariantCulture, out var typeface))
            {
                continue;
            }

            found++;
            typeface.Dispose();
        }

        return found;
    }
}
