// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Layout;

/// <summary>A word being read off a line.</summary>
[DebuggerDisplay("WordInProgress: {Text}")]
internal sealed class WordInProgress
{
    /// <summary>The sum of the characters' confidences.</summary>
    private float _total;

    /// <summary>Gets the word's characters.</summary>
    internal StringBuilder Text { get; } = new();

    /// <summary>Gets the number of characters.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the first output step of the word.</summary>
    internal int FirstStep { get; private set; }

    /// <summary>Gets the last output step of the word.</summary>
    internal int LastStep { get; private set; }

    /// <summary>Gets the mean confidence of the characters, from 0 to 1.</summary>
    internal float Confidence => Count == 0 ? 0F : _total / Count;

    /// <summary>Adds a character.</summary>
    /// <param name="character">The character read.</param>
    /// <param name="text">The character's text.</param>
    internal void Add(RecognizedCharacter character, string text)
    {
        if (Count == 0)
        {
            FirstStep = character.FirstStep;
        }

        LastStep = character.LastStep;
        _total += character.Confidence;
        Count++;
        _ = Text.Append(text);
    }

    /// <summary>Forgets the word.</summary>
    internal void Clear()
    {
        _ = Text.Clear();
        Count = 0;
        _total = 0F;
    }
}
