// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>What one tagging pass over a document builds up page by page: the section of each tagged page and the parent tree entries.</summary>
[DebuggerDisplay("TaggerState: {SectionCount} sections, {Figures} figures")]
internal sealed class TaggerState : IDisposable
{
    /// <summary>The largest glyph-index buffer retained between tagging operations.</summary>
    private const int MaxKeptGlyphs = 4096;

    /// <summary>Idle states with no document attached.</summary>
    private readonly ReusableSlots<TaggerState> _idle;

    /// <summary>The section elements, rented from the pool.</summary>
    private PdfValue[]? _sections;

    /// <summary>The parent tree's number tree entries, rented from the pool.</summary>
    private PdfValue[]? _parentTree;

    /// <summary>The number of parent tree entries.</summary>
    private int _parentCount;

    /// <summary>Whether the state has been returned to the pool.</summary>
    private int _returned;

    /// <summary>Initializes a new instance of the <see cref="TaggerState"/> class.</summary>
    /// <param name="idle">The owning service's tagging pool.</param>
    private TaggerState(ReusableSlots<TaggerState> idle) => _idle = idle;

    /// <summary>Gets the working copy.</summary>
    internal PdfDocument Document { get; private set; } = null!;

    /// <summary>Gets what the layout inference may use: the document's repeating margins and the host's hooks.</summary>
    internal PdfLayoutInference Inference { get; private set; }

    /// <summary>Gets the glyph-index scratch list owned by this tagging operation.</summary>
    internal List<int> Scratch { get; } = [];

    /// <summary>Gets the document or registered service's compression owner.</summary>
    internal ScratchPools ScratchPools { get; private set; } = null!;

    /// <summary>Gets the token that stops the pass.</summary>
    internal CancellationToken CancellationToken { get; private set; }

    /// <summary>Gets the reserved document element.</summary>
    internal PdfObjectId DocumentElement { get; private set; }

    /// <summary>Gets or sets the figures created.</summary>
    internal int Figures { get; set; }

    /// <summary>Gets or sets the next page's parent tree key.</summary>
    internal int NextKey { get; set; }

    /// <summary>Gets the number of tagged pages.</summary>
    internal int SectionCount { get; private set; }

    /// <summary>Gets the section elements of the tagged pages.</summary>
    internal ReadOnlySpan<PdfValue> Sections => _sections is null ? [] : _sections.AsSpan(0, SectionCount);

    /// <summary>Gets the parent tree's number tree entries: key, then the page's array of elements by marked content id.</summary>
    internal ReadOnlySpan<PdfValue> ParentTree => _parentTree is null ? [] : _parentTree.AsSpan(0, _parentCount);

    /// <summary>Gives the rented arrays back.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        PooledArrays.Return(ref _sections, SectionCount, true);
        PooledArrays.Return(ref _parentTree, _parentCount, true);
        SectionCount = 0;
        _parentCount = 0;
        NextKey = 0;
        Figures = 0;
        Document = null!;
        Inference = default;
        ScratchPools = null!;
        CancellationToken = default;
        Scratch.Clear();
        if (Scratch.Capacity > MaxKeptGlyphs)
        {
            Scratch.Capacity = 0;
        }

        _ = _idle.Offer(this);
    }

    /// <summary>Leases empty state for a tagging pass.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="inference">What the layout inference may use.</param>
    /// <param name="scratchPools">The compression owner.</param>
    /// <param name="cancellationToken">Stops the pass between pages.</param>
    /// <returns>The state, which the caller disposes.</returns>
    internal static TaggerState Rent(PdfDocument document, PdfLayoutInference inference, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        var element = StoreEditing.Add(document.Objects, PdfValue.Null);
        var idle = document.DrawingServices.Optimization.Taggers;
        var state = idle.Take() ?? new(idle);
        state.Document = document;
        state.Inference = inference;
        state.ScratchPools = scratchPools;
        state.CancellationToken = cancellationToken;
        state.DocumentElement = element;
        Volatile.Write(ref state._returned, 0);
        return state;
    }

    /// <summary>Adds a tagged page's section element.</summary>
    /// <param name="section">The reference to the section.</param>
    internal void AddSection(PdfValue section)
    {
        PooledArrays.EnsureRoom(ref _sections, SectionCount, true)[SectionCount] = section;
        SectionCount++;
    }

    /// <summary>Adds a number tree entry to the parent tree.</summary>
    /// <param name="value">The key or the value.</param>
    internal void AddParentEntry(PdfValue value)
    {
        PooledArrays.EnsureRoom(ref _parentTree, _parentCount, true)[_parentCount] = value;
        _parentCount++;
    }
}
