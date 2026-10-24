// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Layout;

/// <summary>Reads a page: finds its lines of text, reads each with the best of the given models, and reports the words.</summary>
public static class PageReader
{
    /// <summary>The share of a typical letter's height a line window is at least as tall as.</summary>
    private const double MinimumWindowHeights = 1.5;

    /// <summary>The shortest a line may be to be worth reading, in pixels.</summary>
    private const int MinimumLineHeight = 4;

    /// <summary>The brightest grey level.</summary>
    private const byte White = byte.MaxValue;

    /// <summary>Reads the words on a page.</summary>
    /// <param name="models">The models to read with; the one most sure of a line wins it.</param>
    /// <param name="grey">The pixels of the page, one byte each.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="request">The page resolution, the words' destination and the working memory.</param>
    public static void Read(IReadOnlyList<LstmModel> models, ReadOnlySpan<byte> grey, int width, int height, WordRequest request)
    {
        ArgumentNullException.ThrowIfNull(models);
        var scratch = request.Scratch;
        var mask = InkMask.Build(grey, width, height, scratch);
        var components = scratch.Components;
        components.Clear();
        RunLabeller.Label(mask, components, scratch);
        var typical = TextLineFinder.TypicalHeight(components, scratch);
        var lines = scratch.Lines;
        lines.Clear();
        TextLineFinder.Find(components, lines, scratch);
        var minimumHeight = (int)(MinimumWindowHeights * typical);
        foreach (var line in lines)
        {
            var window = LineWindow.For(line, minimumHeight, width, height);
            if (window.Height >= MinimumLineHeight)
            {
                ReadLine(models, grey, width, new(window, mask), request);
            }
        }
    }

    /// <summary>Reads one line with every model and keeps the most confident reading.</summary>
    /// <param name="models">The models.</param>
    /// <param name="grey">The pixels of the page.</param>
    /// <param name="pageWidth">The page width.</param>
    /// <param name="placement">Where the line is on the page.</param>
    /// <param name="request">The page resolution, the words' destination and the working memory.</param>
    private static void ReadLine(IReadOnlyList<LstmModel> models, ReadOnlySpan<byte> grey, int pageWidth, WordPlacement placement, WordRequest request)
    {
        var scratch = request.Scratch;
        var window = placement.Window;
        var pixels = scratch.RentBytes(window.Pixels);
        var line = pixels.AsSpan(0, window.Pixels);
        window.CopyFrom(grey, pageWidth, line);
        if (!placement.Ink.InkIsDark)
        {
            Invert(line);
        }

        LstmModel? bestModel = null;
        LineReading best = default;
        var firstList = true;
        for (var i = 0; i < models.Count; i++)
        {
            var model = models[i];

            // Two lists take turns: the one holding the best reading so far is left alone while the next model reads.
            var reading = LineReader.Read(model, line, window, firstList ? scratch.CandidateCharacters : scratch.BestCharacters, scratch);
            if (bestModel is not null && reading.Confidence <= best.Confidence)
            {
                continue;
            }

            best = reading;
            bestModel = model;
            firstList = !firstList;
        }

        scratch.Return(pixels);
        if (bestModel is not null)
        {
            WordBuilder.Add(bestModel, best, placement, request);
        }
    }

    /// <summary>Swaps light and dark.</summary>
    /// <param name="pixels">The pixels, changed in place.</param>
    private static void Invert(Span<byte> pixels)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(White - pixels[i]);
        }
    }
}
