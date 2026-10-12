// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of one content stream while the PDF/A text walk reads it.</summary>
[DebuggerDisplay("PdfATextFrame: mode {Graphics.Mode}, {Saved.Count} saved")]
internal sealed class PdfATextFrame
{
    /// <summary>Initializes a new instance of the <see cref="PdfATextFrame"/> class.</summary>
    /// <param name="resources">The stream's own resources, or null when it has none and names resolve on the page.</param>
    /// <param name="graphics">The font and rendering mode the stream starts with.</param>
    /// <param name="structParents">The /StructParents key the stream's marked-content ids resolve through, or -1.</param>
    internal PdfATextFrame(PdfDictionary? resources, PdfATextGraphics graphics, int structParents)
    {
        Resources = resources;
        Graphics = graphics;
        StructParents = structParents;
    }

    /// <summary>Gets the /StructParents key the stream's marked-content ids resolve through, or -1.</summary>
    internal int StructParents { get; }

    /// <summary>Gets the stream's own resources, searched before the page's.</summary>
    internal PdfDictionary? Resources { get; }

    /// <summary>Gets or sets the current font and rendering mode.</summary>
    internal PdfATextGraphics Graphics { get; set; }

    /// <summary>Gets the states saved by <c>q</c>.</summary>
    internal Stack<PdfATextGraphics> Saved { get; } = [];

    /// <summary>Gets the open marked-content sequences.</summary>
    internal Stack<PdfAMarkState> Marked { get; } = [];

    /// <summary>Gets a value indicating whether the current marked content carries <c>/ActualText</c>.</summary>
    internal bool HasActualText => Marked.TryPeek(out var mark) && mark.ActualText;

    /// <summary>Gets the marked-content id of the current marked content, or -1.</summary>
    internal int Mcid => Marked.TryPeek(out var mark) ? mark.Mcid : -1;
}
