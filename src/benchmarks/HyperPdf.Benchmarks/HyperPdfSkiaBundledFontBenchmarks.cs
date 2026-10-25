// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media;
using BenchmarkDotNet.Attributes;
using HyperPdf.Engine;
using HyperPdf.Skia;
using HyperPdf.Skia.Fonts;

namespace HyperPdf.Benchmarks;

/// <summary>Asks the Skia font manager for the fonts the app carries itself: the first request, which decodes and loads the font, and the 200 that follow.</summary>
public class HyperPdfSkiaBundledFontBenchmarks
{
    /// <summary>The requests made by one repeated-lookup call.</summary>
    private const int Calls = 200;

    /// <summary>The family asked for: the app's sans font.</summary>
    private const string Family = "Arimo";

    /// <summary>The font manager, with its sans font already loaded.</summary>
    private readonly FontManagerImpl _warm = new(Options());

    /// <summary>Loads the font once, so the repeated lookups measure the kept typeface.</summary>
    [GlobalSetup]
    public void Setup() => _ = _warm.TryCreateGlyphTypeface(Family, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _);

    /// <summary>Makes a font manager and asks it for the sans font, as the first text in the editor does.</summary>
    /// <returns>Whether the font was found.</returns>
    [Benchmark]
    public bool FirstRequest() => new FontManagerImpl(Options()).TryCreateGlyphTypeface(Family, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _);

    /// <summary>Asks a font manager that already holds the font for it again and again.</summary>
    /// <returns>The number of requests that found the font.</returns>
    [Benchmark]
    public int RepeatedRequests()
    {
        var found = 0;
        for (var i = 0; i < Calls; i++)
        {
            if (_warm.TryCreateGlyphTypeface(Family, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _))
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>Makes the options the app uses.</summary>
    /// <returns>The options.</returns>
    private static SkiaOptions Options() => new() { BundledFamilies = BundledScreenFonts.Families, BundledFontReader = BundledScreenFonts.Read };
}
