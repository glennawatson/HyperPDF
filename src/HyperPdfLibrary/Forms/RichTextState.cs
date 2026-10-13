// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Forms;

/// <summary>The styles in force and the paragraph being built while rich text is read.</summary>
[DebuggerDisplay("RichTextState: {_runs.Count} runs")]
internal sealed class RichTextState
{
    /// <summary>The styles of the open elements, innermost on top.</summary>
    private readonly Stack<RichStyle> _styles = new();

    /// <summary>The runs of the paragraph being built.</summary>
    private List<RichRun> _runs = [];

    /// <summary>The alignment of the paragraph being built.</summary>
    private int _alignment = RichStyle.NoAlignment;

    /// <summary>Whether the paragraph was started by an element, so it counts even without text.</summary>
    private bool _isOpen;

    /// <summary>Whether the next character would follow a space or start a line, so a space is dropped.</summary>
    private bool _atSpace = true;

    /// <summary>Initializes a new instance of the <see cref="RichTextState"/> class.</summary>
    /// <param name="root">The style the text starts in.</param>
    internal RichTextState(RichStyle root) => _styles.Push(root);

    /// <summary>Gets the style of the innermost open element.</summary>
    internal RichStyle Style => _styles.Peek();

    /// <summary>Opens an element's style.</summary>
    /// <param name="style">The style.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Push(RichStyle style) => _styles.Push(style);

    /// <summary>Closes an element.</summary>
    /// <param name="isParagraph">Whether it was a paragraph element, which ends the paragraph.</param>
    /// <param name="paragraphs">Receives a finished paragraph.</param>
    internal void EndElement(bool isParagraph, List<RichParagraph> paragraphs)
    {
        if (_styles.Count > 1)
        {
            _ = _styles.Pop();
        }

        if (isParagraph)
        {
            EndParagraph(paragraphs);
        }
    }

    /// <summary>Starts a paragraph, finishing the one before it.</summary>
    /// <param name="alignment">The paragraph's alignment.</param>
    /// <param name="paragraphs">Receives the finished paragraph.</param>
    internal void StartParagraph(int alignment, List<RichParagraph> paragraphs)
    {
        EndParagraph(paragraphs);
        _alignment = alignment;
        _isOpen = true;
    }

    /// <summary>Finishes the paragraph being built, if it has text or was opened by an element.</summary>
    /// <param name="paragraphs">Receives the paragraph.</param>
    internal void EndParagraph(List<RichParagraph> paragraphs)
    {
        TrimTrailingSpace();
        if (_runs.Count > 0 || _isOpen)
        {
            paragraphs.Add(new(_runs, _alignment));
        }

        _runs = [];
        _alignment = RichStyle.NoAlignment;
        _isOpen = false;
        _atSpace = true;
    }

    /// <summary>Adds a line break (<c>br</c>) to the paragraph.</summary>
    internal void AddLineBreak()
    {
        Append("\n", Style);
        _atSpace = true;
    }

    /// <summary>Adds text in the current style; white space collapses into one space unless the style keeps spaces.</summary>
    /// <param name="value">The text.</param>
    internal void AddText(string value)
    {
        var style = Style;
        var text = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var space = c is ' ' or '\t' or '\r' or '\n';
            if (space && _atSpace && !style.KeepSpaces)
            {
                continue;
            }

            _ = text.Append(space ? ' ' : c);
            _atSpace = space;
        }

        if (text.Length > 0)
        {
            Append(text.ToString(), style);
        }
    }

    /// <summary>Drops the space that collapsed white space leaves at the end of a paragraph.</summary>
    private void TrimTrailingSpace()
    {
        if (_runs.Count == 0 || _runs[^1].Style.KeepSpaces || !_runs[^1].Text.EndsWith(' '))
        {
            return;
        }

        var last = _runs[^1];
        var trimmed = last.Text.TrimEnd(' ');
        if (trimmed.Length == 0)
        {
            _runs.RemoveAt(_runs.Count - 1);
            return;
        }

        _runs[^1] = last with { Text = trimmed };
    }

    /// <summary>Adds text to the paragraph, joining it to the run before when the style is the same.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">The style.</param>
    private void Append(string text, RichStyle style)
    {
        if (_runs.Count == 0)
        {
            _alignment = _alignment == RichStyle.NoAlignment ? style.Alignment : _alignment;
        }

        if (_runs.Count > 0 && _runs[^1].Style.Equals(style))
        {
            _runs[^1] = new(_runs[^1].Text + text, style);
            return;
        }

        _runs.Add(new(text, style));
    }
}
