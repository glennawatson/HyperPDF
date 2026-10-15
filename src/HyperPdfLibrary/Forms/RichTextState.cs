// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>The styles in force and the paragraph being built while rich text is read.</summary>
[DebuggerDisplay("RichTextState: {_depth} styles")]
internal ref struct RichTextState
{
    /// <summary>The styles of the open elements, innermost last; a pooled array.</summary>
    private RichStyle[]? _styles;

    /// <summary>The number of open styles.</summary>
    private int _depth;

    /// <summary>The place of the first run of the paragraph being built.</summary>
    private int _firstRun;

    /// <summary>The alignment of the paragraph being built.</summary>
    private int _alignment;

    /// <summary>Whether the paragraph was started by an element, so it counts even without text.</summary>
    private bool _isOpen;

    /// <summary>Whether the next character would follow a space or start a line, so a space is dropped.</summary>
    private bool _atSpace;

    /// <summary>Initializes a new instance of the <see cref="RichTextState"/> struct.</summary>
    /// <param name="root">The style the text starts in.</param>
    internal RichTextState(RichStyle root)
    {
        _alignment = RichStyle.NoAlignment;
        _atSpace = true;
        Push(root);
    }

    /// <summary>Gets the style of the innermost open element.</summary>
    internal readonly RichStyle Style => _styles![_depth - 1];

    /// <summary>Opens an element's style.</summary>
    /// <param name="style">The style.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Push(RichStyle style)
    {
        PooledArrays.EnsureRoom(ref _styles, _depth, clear: false)[_depth] = style;
        _depth++;
    }

    /// <summary>Closes an element.</summary>
    /// <param name="isParagraph">Whether it was a paragraph element, which ends the paragraph.</param>
    /// <param name="text">Receives a finished paragraph.</param>
    internal void EndElement(bool isParagraph, ref RichText text)
    {
        if (_depth > 1)
        {
            _depth--;
        }

        if (isParagraph)
        {
            EndParagraph(ref text);
        }
    }

    /// <summary>Starts a paragraph, finishing the one before it.</summary>
    /// <param name="alignment">The paragraph's alignment.</param>
    /// <param name="text">Receives the finished paragraph.</param>
    internal void StartParagraph(int alignment, ref RichText text)
    {
        EndParagraph(ref text);
        _alignment = alignment;
        _isOpen = true;
    }

    /// <summary>Finishes the paragraph being built, if it has text or was opened by an element.</summary>
    /// <param name="text">Receives the paragraph.</param>
    internal void EndParagraph(ref RichText text)
    {
        TrimTrailingSpace(ref text);
        var runs = text.RunCount - _firstRun;
        if (runs > 0 || _isOpen)
        {
            text.AddParagraph(new(_firstRun, runs, _alignment));
        }

        _firstRun = text.RunCount;
        _alignment = RichStyle.NoAlignment;
        _isOpen = false;
        _atSpace = true;
    }

    /// <summary>Adds a line break (<c>br</c>) to the paragraph.</summary>
    /// <param name="text">Receives the line break.</param>
    internal void AddLineBreak(ref RichText text)
    {
        var space = text.GetCharSpace(1);
        space[0] = '\n';
        text.CommitChars(1);
        Append(1, Style, ref text);
        _atSpace = true;
    }

    /// <summary>Adds text in the current style; white space collapses into one space unless the style keeps spaces.</summary>
    /// <param name="value">The text.</param>
    /// <param name="text">Receives the text.</param>
    internal void AddText(ReadOnlySpan<char> value, ref RichText text)
    {
        var style = Style;
        var space = text.GetCharSpace(value.Length);
        var written = 0;
        foreach (var c in value)
        {
            var isSpace = c is ' ' or '\t' or '\r' or '\n';
            if (isSpace && _atSpace && !style.KeepSpaces)
            {
                continue;
            }

            space[written] = isSpace ? ' ' : c;
            written++;
            _atSpace = isSpace;
        }

        if (written == 0)
        {
            return;
        }

        text.CommitChars(written);
        Append(written, style, ref text);
    }

    /// <summary>Gives the style stack back.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Dispose() => PooledArrays.Return(ref _styles, _depth, clear: false);

    /// <summary>Drops the space that collapsed white space leaves at the end of a paragraph.</summary>
    /// <param name="text">The text being built.</param>
    private readonly void TrimTrailingSpace(ref RichText text)
    {
        if (text.RunCount == _firstRun)
        {
            return;
        }

        var last = text.GetRun(text.RunCount - 1);
        if (last.Style.KeepSpaces)
        {
            return;
        }

        var shown = text.GetText(last);
        var trimmed = shown.TrimEnd(' ').Length;
        if (trimmed == shown.Length)
        {
            return;
        }

        if (trimmed == 0)
        {
            text.RemoveLastRun();
            return;
        }

        text.TrimLastRun(shown.Length - trimmed);
    }

    /// <summary>Adds characters just written to the paragraph, joining them to the run before when the style is the same.</summary>
    /// <param name="count">The number of characters just committed.</param>
    /// <param name="style">The style.</param>
    /// <param name="text">The text being built.</param>
    private void Append(int count, RichStyle style, ref RichText text)
    {
        if (text.RunCount == _firstRun)
        {
            _alignment = _alignment == RichStyle.NoAlignment ? style.Alignment : _alignment;
            text.AddRun(new(text.CharCount - count, count, style));
            return;
        }

        var last = text.GetRun(text.RunCount - 1);
        if (last.Style.Equals(style))
        {
            text.SetRun(text.RunCount - 1, last with { Length = last.Length + count });
            return;
        }

        text.AddRun(new(text.CharCount - count, count, style));
    }
}
