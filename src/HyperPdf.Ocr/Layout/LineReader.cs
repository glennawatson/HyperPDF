// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Layout;

/// <summary>Reads one line of text with a model.</summary>
public static class LineReader
{
    /// <summary>The number of the space in a model's character set.</summary>
    private const int SpaceId = 0;

    /// <summary>Reads a line.</summary>
    /// <param name="model">The model.</param>
    /// <param name="pixels">The line's pixels, row after row.</param>
    /// <param name="window">The size of the window the pixels were cut from.</param>
    /// <param name="characters">Receives the characters read, replacing what it held.</param>
    /// <param name="scratch">The working memory.</param>
    /// <returns>What the model read.</returns>
    public static LineReading Read(LstmModel model, ReadOnlySpan<byte> pixels, LineWindow window, List<RecognizedCharacter> characters, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(scratch);
        var prepared = LinePreparer.Prepare(pixels, window.Width, window.Height, model.Shape.Height, scratch);
        var outputs = model.Run(prepared, scratch);
        characters.Clear();
        CtcDecoder.Decode(outputs, model.Coder, model.BlankCode, characters, scratch);
        var preparedWidth = prepared.Width;
        scratch.Return(outputs);
        scratch.Return(prepared);
        var total = 0.0;
        var counted = 0;
        foreach (var character in characters)
        {
            if (character.Id == SpaceId)
            {
                continue;
            }

            total += character.Confidence;
            counted++;
        }

        var pixelsPerStep = (double)model.StepWidth * window.Width / preparedWidth;
        return new(characters, pixelsPerStep, counted == 0 ? 0.0 : total / counted);
    }
}
