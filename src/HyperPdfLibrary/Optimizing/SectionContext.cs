// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>What writing a page's elements reads and fills.</summary>
[DebuggerDisplay("SectionContext: {Labels.Length} units")]
internal readonly ref struct SectionContext
{
    /// <summary>Initializes a new instance of the <see cref="SectionContext"/> struct.</summary>
    /// <param name="page">The reference to the page.</param>
    /// <param name="labels">Each unit's label.</param>
    /// <param name="byMcid">Receives the element owning each marked content id.</param>
    internal SectionContext(PdfValue page, ReadOnlySpan<TagLabel> labels, Span<PdfValue> byMcid)
    {
        Page = page;
        Labels = labels;
        ByMcid = byMcid;
    }

    /// <summary>Gets the reference to the page.</summary>
    internal PdfValue Page { get; }

    /// <summary>Gets each unit's label.</summary>
    internal ReadOnlySpan<TagLabel> Labels { get; }

    /// <summary>Gets the span that receives the element owning each marked content id.</summary>
    internal Span<PdfValue> ByMcid { get; }
}
