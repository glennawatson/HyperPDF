// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The state of one content stream while the PDF/A content rules read it.</summary>
[DebuggerDisplay("PdfAContentState: {Where}, {Saved.Count} saved")]
internal sealed class PdfAContentState
{
    /// <summary>Initializes a new instance of the <see cref="PdfAContentState"/> class.</summary>
    /// <param name="scope">The resources the stream's names resolve against.</param>
    /// <param name="graphics">The graphics state the stream starts with.</param>
    /// <param name="where">Names the stream in the report.</param>
    internal PdfAContentState(PdfAResourceScope scope, PdfAGraphicsSnapshot graphics, string where)
    {
        Scope = scope;
        Graphics = graphics;
        Where = where;
    }

    /// <summary>Gets the resources the stream's names resolve against.</summary>
    internal PdfAResourceScope Scope { get; }

    /// <summary>Gets the report's name for the stream.</summary>
    internal string Where { get; }

    /// <summary>Gets or sets the current graphics state.</summary>
    internal PdfAGraphicsSnapshot Graphics { get; set; }

    /// <summary>Gets the states saved by <c>q</c>; the count is the nesting depth.</summary>
    internal List<PdfAGraphicsSnapshot> Saved { get; } = [];
}
