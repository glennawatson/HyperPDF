// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of one content stream as the PDF/A colour walk reads it.</summary>
[DebuggerDisplay("PdfAContentRun: {Saved.Count} saved states")]
internal sealed class PdfAContentRun
{
    /// <summary>Initializes a new instance of the <see cref="PdfAContentRun"/> class.</summary>
    /// <param name="scope">The resources the stream reads.</param>
    /// <param name="initial">The graphics state the stream starts with.</param>
    internal PdfAContentRun(in PdfAColourResourceScope scope, in PdfAColourPaintState initial)
    {
        Scope = scope;
        Initial = initial;
        Paint = initial;
    }

    /// <summary>Gets the resources the stream reads.</summary>
    internal PdfAColourResourceScope Scope { get; }

    /// <summary>Gets the graphics state the stream started with.</summary>
    internal PdfAColourPaintState Initial { get; }

    /// <summary>Gets or sets the current graphics state.</summary>
    internal PdfAColourPaintState Paint { get; set; }

    /// <summary>Gets the states saved with <c>q</c>.</summary>
    internal Stack<PdfAColourPaintState> Saved { get; } = [];

    /// <summary>Gets the position in the stream, for patterns and images painted now.</summary>
    internal PdfAContentFrame Frame => new(Scope, Paint, Initial);
}
