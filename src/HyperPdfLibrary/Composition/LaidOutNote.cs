// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A footnote's text laid out and ready to draw at the foot of its page.</summary>
[DebuggerDisplay("LaidOutNote: {Label}, {Height} pt")]
internal sealed class LaidOutNote
{
    /// <summary>Initializes a new instance of the <see cref="LaidOutNote"/> class.</summary>
    /// <param name="label">The note's number as written.</param>
    /// <param name="paragraphs">The note's paragraphs, laid out.</param>
    internal LaidOutNote(string label, List<LaidOutParagraph> paragraphs)
    {
        Label = label;
        Paragraphs = paragraphs;
        var height = 0F;
        foreach (var paragraph in paragraphs)
        {
            height += paragraph.LinesHeight + paragraph.Style.SpaceAfter;
        }

        Height = height;
    }

    /// <summary>Gets the note's number as written.</summary>
    internal string Label { get; }

    /// <summary>Gets the note's paragraphs.</summary>
    internal List<LaidOutParagraph> Paragraphs { get; }

    /// <summary>Gets the height of the note with the space after its paragraphs.</summary>
    internal float Height { get; }
}
