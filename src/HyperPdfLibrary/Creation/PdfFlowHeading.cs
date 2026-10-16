// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// A heading. The level is kept in order: a heading is at most one level below the one before it, and the first is level 1.
/// By default <see cref="PdfFlowBlock.KeepWithNext"/> is <see langword="true"/> so the heading stays on the same page as
/// the content that follows it.
/// </summary>
[DebuggerDisplay("PdfFlowHeading: H{Level} {Text}")]
public sealed record PdfFlowHeading : PdfFlowBlock
{
    /// <summary>Initializes a new instance of the <see cref="PdfFlowHeading"/> class.</summary>
    /// <param name="level">The heading level, 1 to 6.</param>
    /// <param name="text">The heading's text.</param>
    public PdfFlowHeading(int level, string text)
    {
        Level = level;
        Text = text;
        KeepWithNext = true;
    }

    /// <summary>Gets the heading level, 1 to 6.</summary>
    public int Level { get; init; }

    /// <summary>Gets the heading's text.</summary>
    public string Text { get; init; }

    /// <summary>Deconstructs the heading into its level and text.</summary>
    /// <param name="level">Receives the heading level.</param>
    /// <param name="text">Receives the heading text.</param>
    public void Deconstruct(out int level, out string text)
    {
        level = Level;
        text = Text;
    }
}
