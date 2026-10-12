// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The graphics state of one content stream read by the transparency search. Each stream starts from the default state.</summary>
[DebuggerDisplay("PdfAPaintState: depth {Depth}, {Saved.Count} saved")]
internal sealed class PdfAPaintState
{
    /// <summary>Initializes a new instance of the <see cref="PdfAPaintState"/> class.</summary>
    /// <param name="resources">The resources the stream uses.</param>
    /// <param name="depth">The nesting depth of the stream.</param>
    internal PdfAPaintState(PdfDictionary resources, int depth)
    {
        Resources = resources;
        Depth = depth;
    }

    /// <summary>Gets the resources the stream uses.</summary>
    internal PdfDictionary Resources { get; }

    /// <summary>Gets the nesting depth of the stream.</summary>
    internal int Depth { get; }

    /// <summary>Gets or sets the state in force.</summary>
    internal PdfAPaint Current { get; set; }

    /// <summary>Gets the states saved by <c>q</c>.</summary>
    internal Stack<PdfAPaint> Saved { get; } = [];
}
