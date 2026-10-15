// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The state one decode shares between segments: the pixels it may still decode or composite, scratch bitmaps reused
/// for each symbol and each refined instance, and the idle segment, bitmap, symbol and settings objects a decode
/// builds, kept so the next decode with the same scratch cache builds none. Hostile data can ask for huge symbols or
/// endless instances; charging every step against a fixed budget bounds the time spent.
/// </summary>
[DebuggerDisplay("Jbig2Workspace: {Remaining} left")]
internal sealed class Jbig2Workspace : IDisposable
{
    /// <summary>The decoder's explicitly owned scratch cache.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The dynamic Huffman tables whose storage is in use by this decode.</summary>
    private readonly List<Jbig2HuffmanTable> _activeHuffman = [];

    /// <summary>The scratch bitmap for symbols being decoded, or <see langword="null"/> before first use.</summary>
    private Jbig2Bitmap? _symbol;

    /// <summary>The scratch bitmap for refined text region instances, or <see langword="null"/> before first use.</summary>
    private Jbig2Bitmap? _refinement;

    /// <summary>The state of the global segments, or <see langword="null"/> before first use.</summary>
    private Jbig2DecodeState? _globalState;

    /// <summary>The state of the page's segments, or <see langword="null"/> before first use.</summary>
    private Jbig2DecodeState? _pageState;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Workspace"/> class with <see cref="Jbig2Limits.MaxWork"/>.</summary>
    internal Jbig2Workspace()
        : this(new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Jbig2Workspace"/> class with an explicit cache.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    internal Jbig2Workspace(ScratchPools scratchPools)
    {
        _scratchPools = scratchPools;
        Remaining = Jbig2Limits.MaxWork;
    }

    /// <summary>Gets the work left.</summary>
    internal long Remaining { get; private set; }

    /// <summary>Gets the decoder's scratch cache.</summary>
    internal ScratchPools ScratchPools => _scratchPools;

    /// <summary>Gets the idle segments.</summary>
    internal Jbig2Recycler<Jbig2Segment> Segments { get; } = new();

    /// <summary>Gets the idle bitmaps, without their buffers.</summary>
    internal Jbig2Recycler<Jbig2Bitmap> Bitmaps { get; } = new();

    /// <summary>Gets the idle symbol stores, without their buffers.</summary>
    internal Jbig2Recycler<Jbig2SymbolStore> Stores { get; } = new();

    /// <summary>Gets the idle symbol sets.</summary>
    internal Jbig2Recycler<Jbig2SymbolSet> Sets { get; } = new();

    /// <summary>Gets the idle symbol dictionaries, without their buffers.</summary>
    internal Jbig2Recycler<Jbig2SymbolDictionary> Dictionaries { get; } = new();

    /// <summary>Gets the idle pattern dictionaries.</summary>
    internal Jbig2Recycler<Jbig2PatternDictionary> Patterns { get; } = new();

    /// <summary>Gets the parameters of the halftone region being decoded.</summary>
    internal Jbig2HalftoneSettings HalftoneSettings { get; } = new();

    /// <summary>Gets the idle dynamic Huffman table objects.</summary>
    internal Jbig2Recycler<Jbig2HuffmanTable> HuffmanTables { get; } = new();

    /// <summary>Gets the temporary lines of a custom Huffman table.</summary>
    internal Jbig2HuffmanTable.LineList HuffmanLines { get; } = new();

    /// <summary>Gets the parameters of the text region being read.</summary>
    internal Jbig2TextRegionSettings TextSettings { get; } = new();

    /// <summary>Gets the parameters of the aggregate symbols of the symbol dictionary being decoded.</summary>
    internal Jbig2TextRegionSettings AggregateSettings { get; } = new();

    /// <summary>Gets the parameters of the symbol dictionary being read.</summary>
    internal Jbig2SymbolDictionarySettings DictionarySettings { get; } = new();

    /// <summary>Returns the scratch bitmaps' buffers to the pool.</summary>
    public void Dispose()
    {
        _symbol?.Release();
        _refinement?.Release();
        if (_activeHuffman.Count == 0)
        {
            return;
        }

        TextSettings.Reset();
        AggregateSettings.Reset();
        foreach (var table in _activeHuffman)
        {
            table.Release(_scratchPools);
            HuffmanTables.Give(table);
        }

        _activeHuffman.Clear();
    }

    /// <summary>Takes an idle workspace of a scratch cache, or makes one, with a full work budget.</summary>
    /// <param name="scratchPools">The cache that owns the idle workspaces.</param>
    /// <returns>The workspace; give it back with <see cref="Return"/>.</returns>
    internal static Jbig2Workspace Rent(ScratchPools scratchPools)
    {
        var workspace = scratchPools.Objects<Jbig2Workspace>().Take() ?? new(scratchPools);
        workspace.Remaining = Jbig2Limits.MaxWork;
        return workspace;
    }

    /// <summary>Keeps a dynamic Huffman table alive until the decode returns its workspace.</summary>
    /// <param name="table">The table whose arrays belong to this workspace.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void KeepHuffmanTable(Jbig2HuffmanTable table) => _activeHuffman.Add(table);

    /// <summary>Gives back a workspace after a decode: its buffers return to the cache and it stays idle for the next decode, unless the cache has closed.</summary>
    internal void Return()
    {
        Dispose();

        // A closed cache keeps nothing; the idle objects are then left to the collector.
        _ = _scratchPools.Objects<Jbig2Workspace>().Offer(this);
    }

    /// <summary>Spends work.</summary>
    /// <param name="amount">The pixels about to be decoded or composited; at least one is always charged.</param>
    /// <returns><see langword="false"/> when the budget is spent.</returns>
    internal bool TryCharge(long amount)
    {
        Remaining -= Math.Max(amount, 1);
        return Remaining >= 0;
    }

    /// <summary>Starts the state of the global segments.</summary>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The state, which the caller disposes.</returns>
    internal Jbig2DecodeState BeginGlobals(CancellationToken cancellationToken)
    {
        var state = _globalState ??= new(null, this, null, cancellationToken);
        state.Begin(null, null, cancellationToken);
        return state;
    }

    /// <summary>Starts the state of the page's segments.</summary>
    /// <param name="page">The page bitmap.</param>
    /// <param name="globals">The global state, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The state, which the caller disposes.</returns>
    internal Jbig2DecodeState BeginPage(Jbig2Bitmap page, Jbig2DecodeState? globals, CancellationToken cancellationToken)
    {
        var state = _pageState ??= new(page, this, globals, cancellationToken);
        state.Begin(page, globals, cancellationToken);
        return state;
    }

    /// <summary>Gets a white bitmap, reusing an idle one's object. Dispose it to give the object and its buffer back.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is not valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Jbig2Bitmap? RentBitmap(int width, int height) => Jbig2Bitmap.Rent(width, height, this);

    /// <summary>Gets the white scratch bitmap for a symbol, sized for it. It is valid until the next call.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is not valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Jbig2Bitmap? Symbol(int width, int height) => Reuse(ref _symbol, width, height);

    /// <summary>Gets the white scratch bitmap for a refined instance, sized for it. It is valid until the next call.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is not valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Jbig2Bitmap? Refinement(int width, int height) => Reuse(ref _refinement, width, height);

    /// <summary>Resizes a scratch bitmap, creating it on first use.</summary>
    /// <param name="scratch">The scratch bitmap.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is not valid.</returns>
    private Jbig2Bitmap? Reuse(ref Jbig2Bitmap? scratch, int width, int height)
    {
        if (scratch is null)
        {
            scratch = Jbig2Bitmap.Create(width, height, _scratchPools);
            return scratch;
        }

        return scratch.TryReshape(width, height) ? scratch : null;
    }
}
