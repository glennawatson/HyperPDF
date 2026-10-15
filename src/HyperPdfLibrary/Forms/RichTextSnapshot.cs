// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>Owns parsed field text and its exact source bytes for repeated appearance generation.</summary>
internal sealed class RichTextSnapshot
{
    /// <summary>The source XML.</summary>
    private readonly byte[] _source;

    /// <summary>The default style source.</summary>
    private readonly byte[] _style;

    /// <summary>The parsed characters.</summary>
    private readonly char[] _characters;

    /// <summary>The parsed runs.</summary>
    private readonly RichRun[] _runs;

    /// <summary>The parsed paragraphs.</summary>
    private readonly RichParagraph[] _paragraphs;

    /// <summary>Initializes a new instance of the <see cref="RichTextSnapshot"/> class.</summary>
    /// <param name="source">The source XML.</param>
    /// <param name="style">The default style source.</param>
    /// <param name="text">The parsed text.</param>
    internal RichTextSnapshot(ReadOnlySpan<byte> source, ReadOnlySpan<byte> style, ref RichText text)
    {
        _source = source.ToArray();
        _style = style.ToArray();
        _characters = new char[text.CharCount];
        _runs = new RichRun[text.RunCount];
        _paragraphs = new RichParagraph[text.ParagraphCount];
        for (var i = 0; i < _runs.Length; i++)
        {
            var run = text.GetRun(i);
            _runs[i] = run;
            text.GetText(run).CopyTo(_characters.AsSpan(run.Start));
        }

        for (var i = 0; i < _paragraphs.Length; i++)
        {
            _paragraphs[i] = text.GetParagraph(i);
        }
    }

    /// <summary>Checks the exact source and copies matching parsed text into the caller's buffers.</summary>
    /// <param name="source">The source XML.</param>
    /// <param name="style">The default style source.</param>
    /// <param name="text">Receives the parsed text.</param>
    /// <returns>Whether the sources match.</returns>
    internal bool TryCopy(ReadOnlySpan<byte> source, ReadOnlySpan<byte> style, ref RichText text)
    {
        if (!source.SequenceEqual(_source) || !style.SequenceEqual(_style))
        {
            return false;
        }

        _characters.CopyTo(text.GetCharSpace(_characters.Length));
        text.CommitChars(_characters.Length);
        foreach (var run in _runs)
        {
            text.AddRun(run);
        }

        foreach (var paragraph in _paragraphs)
        {
            text.AddParagraph(paragraph);
        }

        return true;
    }
}
