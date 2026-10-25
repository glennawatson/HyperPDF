// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Layout;

/// <summary>Splits what a model read from a line into words and places each on the page.</summary>
public static class WordBuilder
{
    /// <summary>The number of the space in a model's character set.</summary>
    private const int SpaceId = 0;

    /// <summary>The scale from the 0 to 1 the model gives to the 0 to 100 words are reported with.</summary>
    private const float PercentScale = 100F;

    /// <summary>Adds the words of a line.</summary>
    /// <param name="model">The model that read the line.</param>
    /// <param name="reading">What the model read.</param>
    /// <param name="placement">Where the line was cut from, and the ink it holds.</param>
    /// <param name="request">The page resolution, the words' destination and the working memory.</param>
    public static void Add(LstmModel model, LineReading reading, WordPlacement placement, WordRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        var word = request.Scratch.Word;
        word.Clear();
        foreach (var character in reading.Characters)
        {
            if (character.Id == SpaceId)
            {
                Flush(word, reading, placement, request);
                continue;
            }

            word.Add(character, model.Characters[character.Id]);
        }

        Flush(word, reading, placement, request);
    }

    /// <summary>Adds the word read so far, if there is one, and starts the next.</summary>
    /// <param name="word">The word so far.</param>
    /// <param name="reading">What the model read.</param>
    /// <param name="placement">Where the line was cut from.</param>
    /// <param name="request">The page resolution and the words' destination.</param>
    private static void Flush(WordInProgress word, LineReading reading, WordPlacement placement, WordRequest request)
    {
        if (word.Count == 0)
        {
            return;
        }

        var window = placement.Window;
        var left = window.Left + (int)(word.FirstStep * reading.PixelsPerStep);
        var right = window.Left + (int)Math.Ceiling((word.LastStep + 1) * reading.PixelsPerStep) - 1;
        var area = new TextLine(Math.Max(left, window.Left), window.Top, Math.Min(right, window.Left + window.Width - 1), window.Top + window.Height - 1);
        var box = placement.Ink.TryGetExtent(area, out var ink) ? ink : area;
        var scale = request.PixelsPerPoint;
        var bounds = PageRect.FromEdges(box.Left / scale, box.Top / scale, (box.Right + 1) / scale, (box.Bottom + 1) / scale);
        request.Output.Add(new(word.Text.ToString(), bounds, word.Confidence * PercentScale));
        word.Clear();
    }
}
