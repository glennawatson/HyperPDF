// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Changes a container makes to the paragraphs inside it: an extra indent, and for a list item a hanging label.</summary>
/// <param name="LeftIndent">Extra indent added to the paragraph's left indent, in points.</param>
/// <param name="Hang">How far the first line hangs to the left of the indent, in points; zero for no hanging label.</param>
/// <param name="Label">The label at the start of the first line, such as a bullet or a number, or <see langword="null"/> for none.</param>
/// <param name="LabelNode">The structure node the label belongs to, or <see langword="null"/> when the label shares the paragraph's node.</param>
[DebuggerDisplay("ParagraphAdjust: indent {LeftIndent}, label {Label}")]
internal readonly record struct ParagraphAdjust(float LeftIndent, float Hang, string? Label, StructNode? LabelNode)
{
    /// <summary>Gets an adjustment that changes nothing.</summary>
    internal static ParagraphAdjust None => default;
}
