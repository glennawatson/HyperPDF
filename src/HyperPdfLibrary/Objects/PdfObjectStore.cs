// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// The indirect objects of a document, parsed on first use and cached. A cached object is read without a lock; parsing a
/// new one takes the store's lock. Safe to call from any thread.
/// </summary>
[DebuggerDisplay("PdfObjectStore: {Size} objects")]
public sealed class PdfObjectStore : IDisposable
{
    /// <summary>The file.</summary>
    private readonly PdfByteSource _source;

    /// <summary>The whole file when it is held in one array, which objects are parsed from in place; else <see langword="null"/>.</summary>
    private readonly byte[]? _array;

    /// <summary>Whether disposing the store disposes <see cref="_source"/>.</summary>
    private readonly bool _ownsSource;

    /// <summary>Guards parsing, the object stream cache and edits. Re-entrant, as parsing one object can need another.</summary>
    private readonly Lock _gate = new();

    /// <summary>The most recently used decoded object streams, by object number; others are decoded again when needed.</summary>
    private ObjectStreamCache? _objectStreams;

    /// <summary>Scratch buffers used if this store has no document-open context.</summary>
    private ScratchPools? _scratchPools;

    /// <summary>The embedding state shared by sinks writing this document.</summary>
    private PdfFontEmbeddingState? _fontEmbedding;

    /// <summary>Where each object lives.</summary>
    private XrefTable _xref;

    /// <summary>The parsed objects, by number: a dictionary, array, stream, or boxed value.</summary>
    private object?[] _cache;

    /// <summary>Whether a damaged table has already been rebuilt.</summary>
    private bool _repairTried;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>The next unused object number.</summary>
    private int _nextNumber;

    /// <summary>The number of indirect object parses nested in this store, guarded by <see cref="_gate"/>.</summary>
    private int _parseDepth;

    /// <summary>The offsets of the objects in the original file, sorted; made on first use.</summary>
    private long[]? _sortedOffsets;

    /// <summary>The prefetch marks for dictionaries parsed by this store.</summary>
    private ConditionalWeakTable<PdfDictionary, PdfPrefetchMark>? _prefetchMarks;

    /// <summary>Fallback lookup tables for name trees owned by this store.</summary>
    private ConditionalWeakTable<PdfDictionary, Dictionary<byte[], PdfValue>>? _nameTreeTables;

    /// <summary>1 once redactions were applied.</summary>
    private int _requiresCompactSave;

    /// <summary>The changed and added objects by number, guarded by the lock; made on first use.</summary>
    private Dictionary<int, PdfValue>? _edits;

    /// <summary>The deleted object numbers, guarded by the lock; made on first use.</summary>
    private HashSet<int>? _deleted;

    /// <summary>The generations of freed objects, guarded by the lock; made on first use.</summary>
    private Dictionary<int, int>? _freedGenerations;

    /// <summary>The open transaction, guarded by the lock. The caller owns and disposes it, not the store.</summary>
    private PdfEditTransaction? _transaction;

    /// <summary>The undo and redo stacks, created on first use.</summary>
    private PdfEditHistory? _history;

    /// <summary>Called after a transaction ends or an edit is undone or redone, so readers drop cached pages.</summary>
    private IObjectsChangedListener? _changed;

    /// <summary>Completed save graphs by encryption and reachability options, guarded by the store lock.</summary>
    private PdfObjectGraph?[]? _saveGraphs;

    /// <summary>Initializes a new instance of the <see cref="PdfObjectStore"/> class.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether disposing the store disposes the file.</param>
    /// <param name="xref">Where each object lives.</param>
    internal PdfObjectStore(PdfByteSource source, bool ownsSource, XrefTable xref)
    {
        _source = source;
        _array = source.WholeArray;
        _ownsSource = ownsSource;
        _xref = xref;
        _cache = StoreCache.Rent(xref.Size);
    }

    /// <summary>Gets the document's name table.</summary>
    public PdfNameTable Names { get; } = new();

    /// <summary>Gets the security handler, or <see langword="null"/> when the document is not encrypted.</summary>
    public PdfSecurityHandler? Security { get; internal set; }

    /// <summary>Gets the trailer dictionary.</summary>
    public PdfDictionary Trailer => _xref.Trailer!;

    /// <summary>Gets the document catalog.</summary>
    public PdfDictionary Catalog { get; internal set; } = null!;

    /// <summary>Gets the header version, for example "1.7".</summary>
    public string Version { get; internal set; } = string.Empty;

    /// <summary>Gets one more than the highest object number.</summary>
    public int Size => Math.Max(_xref.Size, _nextNumber);

    /// <summary>Gets a value indicating whether the cross-reference table was rebuilt because the file is damaged.</summary>
    public bool WasRepaired => _xref.Repaired;

    /// <summary>Gets a value indicating whether the newest cross-reference section is a stream.</summary>
    public bool UsesXrefStreams => _xref.UsesXrefStreams;

    /// <summary>Gets the offset of the newest cross-reference section, or -1 when the table was rebuilt.</summary>
    public long StartXref => _xref.StartXref;

    /// <summary>Gets the offset of the <c>%PDF-</c> header; offsets in the file are relative to it, as viewers read them.</summary>
    public int HeaderOffset { get; internal set; }

    /// <summary>Gets the file the objects are read from.</summary>
    public PdfByteSource Source => _source;

    /// <summary>Gets a value indicating whether the store has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Gets or sets a value indicating whether the security handler belongs to another store, such as the document a working
    /// copy was made from. A borrowed handler is shared, so disposing this store leaves it open.
    /// </summary>
    internal bool SecurityBorrowed { get; set; }

    /// <summary>Gets or sets whether the first file window names a linearization dictionary.</summary>
    internal bool MightBeLinearized { get; set; }

    /// <summary>Gets the owned array state to store operations.</summary>
    internal byte[]? ArrayState => _array;

    /// <summary>Gets the owned ownsSource state to store operations.</summary>
    internal bool OwnsSourceState => _ownsSource;

    /// <summary>Gets the owned objectStreams state to store operations.</summary>
    internal ObjectStreamCache ObjectStreamsState => Volatile.Read(ref _objectStreams) ?? CreateObjectStreams();

    /// <summary>Gets the object stream cache, or <see langword="null"/> when no object stream was read yet.</summary>
    internal ObjectStreamCache? ObjectStreamsIfCreated => Volatile.Read(ref _objectStreams);

    /// <summary>Gets the document's decoder scratch cache, or this store's fallback cache.</summary>
    internal ScratchPools ScratchPools => Context?.ScratchPools ?? (Volatile.Read(ref _scratchPools) ?? CreateScratchPools());

    /// <summary>Gets font embedding state shared by sinks for this store.</summary>
    internal PdfFontEmbeddingState FontEmbedding
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var existing = Volatile.Read(ref _fontEmbedding);
            if (existing is not null)
            {
                return existing;
            }

            var created = new PdfFontEmbeddingState();
            return Interlocked.CompareExchange(ref _fontEmbedding, created, null) ?? created;
        }
    }

    /// <summary>Gets embedding state without creating it.</summary>
    internal PdfFontEmbeddingState? ExistingFontEmbedding => Volatile.Read(ref _fontEmbedding);

    /// <summary>Gets a reference to the mutable xref state to store operations.</summary>
    internal ref XrefTable XrefState => ref _xref;

    /// <summary>Gets a reference to the mutable cache state to store operations.</summary>
    internal ref object?[] CacheState => ref _cache;

    /// <summary>Gets a reference to the mutable repairTried state to store operations.</summary>
    internal ref bool RepairTriedState => ref _repairTried;

    /// <summary>Gets or sets the cancellation and diagnostics context, or <see langword="null"/> when none was given.</summary>
    internal PdfOpenContext? Context { get; set; }

    /// <summary>Gets the amount added to an offset written in the file to find its position: the header offset, or zero.</summary>
    internal int OffsetBase => _xref.OffsetBase;

    /// <summary>Gets the changed and added objects by number, made on first use; read or changed only while holding the lock.</summary>
    internal Dictionary<int, PdfValue> EditsState => _edits ??= [];

    /// <summary>Gets the changed and added objects, or <see langword="null"/> when the store has none; read only while holding the lock.</summary>
    internal Dictionary<int, PdfValue>? EditsIfCreated => _edits;

    /// <summary>Gets the deleted object numbers, made on first use; read or changed only while holding the lock.</summary>
    internal HashSet<int> DeletedState => _deleted ??= [];

    /// <summary>Gets the deleted object numbers, or <see langword="null"/> when none was deleted; read only while holding the lock.</summary>
    internal HashSet<int>? DeletedIfCreated => _deleted;

    /// <summary>Gets the generations of freed objects, made on first use; read or changed only while holding the lock.</summary>
    internal Dictionary<int, int> FreedGenerationsState => _freedGenerations ??= [];

    /// <summary>Gets the generations of freed objects, or <see langword="null"/> when none was freed; read only while holding the lock.</summary>
    internal Dictionary<int, int>? FreedGenerationsIfCreated => _freedGenerations;

    /// <summary>Gets or sets a value indicating whether a transaction changed a trailer entry, guarded by the lock.</summary>
    internal bool TrailerEditedState { get; set; }

    /// <summary>Gets a reference to the mutable nextNumber state to store operations.</summary>
    internal ref int NextNumberState => ref _nextNumber;

    /// <summary>Gets the parse depth guarded by this store's gate.</summary>
    internal ref int ParseDepthState => ref _parseDepth;

    /// <summary>Gets a reference to the mutable sortedOffsets state to store operations.</summary>
    internal ref long[]? SortedOffsetsState => ref _sortedOffsets;

    /// <summary>Gets the marks for dictionaries loaded ahead from this store.</summary>
    internal ConditionalWeakTable<PdfDictionary, PdfPrefetchMark> PrefetchMarks => LazyInitializer.EnsureInitialized(ref _prefetchMarks);

    /// <summary>Gets fallback name-tree tables retained for this store's lifetime.</summary>
    internal ConditionalWeakTable<PdfDictionary, Dictionary<byte[], PdfValue>> NameTreeTables => LazyInitializer.EnsureInitialized(ref _nameTreeTables);

    /// <summary>Gets the number of decoded object streams held.</summary>
    internal int CachedObjectStreams => Volatile.Read(ref _objectStreams)?.Count ?? 0;

    /// <summary>Gets the decoded bytes the cached object streams hold.</summary>
    internal long CachedObjectStreamBytes => Volatile.Read(ref _objectStreams)?.Bytes ?? 0;

    /// <summary>Gets a reference to the mutable requiresCompactSave state to store operations.</summary>
    internal ref int RequiresCompactSaveState => ref _requiresCompactSave;

    /// <summary>Gets one more than the highest object number in the cross-reference table.</summary>
    internal int XrefSize => _xref.Size;

    /// <summary>Gets a reference to the mutable transaction state to store operations.</summary>
    internal ref PdfEditTransaction? TransactionState => ref _transaction;

    /// <summary>Gets a reference to the mutable history state to store operations.</summary>
    internal ref PdfEditHistory? HistoryState => ref _history;

    /// <summary>Gets a reference to the mutable changed state to store operations.</summary>
    internal ref IObjectsChangedListener? ChangedState => ref _changed;

    /// <summary>Gets the completed save graphs, guarded by the store lock.</summary>
    internal ref PdfObjectGraph?[]? SaveGraphsState => ref _saveGraphs;

    /// <summary>Gets the lock that guards the objects and their edits.</summary>
    internal Lock Gate => _gate;

    /// <summary>
    /// Releases the security handler, the file and the parsed objects. Later reads throw <see cref="ObjectDisposedException"/>;
    /// objects callers already hold stay usable until they are collected.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            // A parse in progress finishes first; cached readers check the disposed state after loading a slot.
            Volatile.Read(ref _objectStreams)?.Clear();
            var cache = _cache;
            Volatile.Write(ref _cache, []);
            StoreCache.Return(cache);
            _saveGraphs = null;
        }

        _ = Interlocked.Exchange(ref _prefetchMarks, null);
        _ = Interlocked.Exchange(ref _nameTreeTables, null);
        _ = Interlocked.Exchange(ref _fontEmbedding, null);

        Context?.CloseScratchPools();
        Volatile.Read(ref _scratchPools)?.Close();

        if (!SecurityBorrowed)
        {
            Security?.Dispose();
        }

        if (_ownsSource)
        {
            _source.Dispose();
        }
    }

    /// <summary>Creates the object stream cache once, when the first object stream is read.</summary>
    /// <returns>The cache.</returns>
    private ObjectStreamCache CreateObjectStreams()
    {
        var created = new ObjectStreamCache();
        return Interlocked.CompareExchange(ref _objectStreams, created, null) ?? created;
    }

    /// <summary>Creates the fallback scratch cache once for a store without an open context.</summary>
    /// <returns>The cache.</returns>
    private ScratchPools CreateScratchPools()
    {
        var created = new ScratchPools();
        var result = Interlocked.CompareExchange(ref _scratchPools, created, null) ?? created;
        if (IsDisposed)
        {
            result.Close();
        }

        return result;
    }
}
