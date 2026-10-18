// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// The cancellation token, recovery switches, diagnostics sink and repair log of one open document. Every open has a
/// context: it keeps each distinct report (up to a cap) so the document can say whether it was repaired.
/// </summary>
/// <param name="sink">The diagnostics callback, or <see langword="null"/>.</param>
/// <param name="drawingServices">The drawing services selected for this document, or <see langword="null"/> to make empty ones when first used.</param>
/// <param name="recovery">Whether damaged objects may be recovered.</param>
/// <param name="ignoreXrefStreams">Whether cross-reference streams are ignored.</param>
/// <param name="maxDecompressedBytes">The validated stream-filter output limit.</param>
/// <param name="token">The cancellation token.</param>
[DebuggerDisplay("PdfOpenContext: {HasRepairs} repairs")]
internal sealed class PdfOpenContext(
    Action<PdfDiagnostic>? sink,
    PdfDrawingServices? drawingServices,
    bool recovery,
    bool ignoreXrefStreams,
    int maxDecompressedBytes,
    CancellationToken token)
{
    /// <summary>The most distinct reports kept; later ones still reach the sink.</summary>
    private const int MaxLogged = 1024;

    /// <summary>The diagnostics callback.</summary>
    private readonly Action<PdfDiagnostic>? _sink = sink;

    /// <summary>The operation token while opening, then the document-lifetime token before publication.</summary>
    private CancellationToken _cancellationToken = token;

    /// <summary>Guards the log; made with the first report, so a document that reports nothing never allocates it.</summary>
    private Lock? _gate;

    /// <summary>The drawing services; made on first use when none was selected.</summary>
    private PdfDrawingServices? _drawingServices = drawingServices;

    /// <summary>The distinct reports, in the order they were first made.</summary>
    private List<PdfDiagnostic>? _log;

    /// <summary>1 once a report of damage has been logged.</summary>
    private int _hasRepairs;

    /// <summary>The document-owned decoder cache, created on first use.</summary>
    private ScratchPools? _scratchPools;

    /// <summary>1 once document close has ended scratch retention.</summary>
    private int _scratchClosed;

    /// <summary>Initializes a new instance of the <see cref="PdfOpenContext"/> class.</summary>
    /// <param name="sink">The diagnostics callback.</param>
    /// <param name="drawingServices">The drawing services.</param>
    /// <param name="token">The cancellation token.</param>
    internal PdfOpenContext(Action<PdfDiagnostic>? sink, PdfDrawingServices? drawingServices, CancellationToken token)
        : this(sink, drawingServices, true, false, PdfLimits.MaxDecodedLength, token)
    {
    }

    /// <summary>Gets a value indicating whether recovery from a damaged structure is allowed.</summary>
    internal bool Recovery => recovery;

    /// <summary>Gets a value indicating whether cross-reference streams are ignored.</summary>
    internal bool IgnoreXrefStreams => ignoreXrefStreams;

    /// <summary>Gets the maximum output bytes allowed for each stream filter.</summary>
    internal int MaxDecompressedBytes => maxDecompressedBytes;

    /// <summary>Gets a value indicating whether any report of repaired damage has been logged.</summary>
    internal bool HasRepairs => Volatile.Read(ref _hasRepairs) != 0;

    /// <summary>Gets the token passed to this document-open or check operation.</summary>
    internal CancellationToken CancellationToken => _cancellationToken;

    /// <summary>Gets the drawing services selected for this open.</summary>
    internal PdfDrawingServices DrawingServices => Volatile.Read(ref _drawingServices) ?? CreateDrawingServices();

    /// <summary>Gets this document's scratch buffers or the cache explicitly shared by its drawing services.</summary>
    internal ScratchPools ScratchPools => Volatile.Read(ref _drawingServices)?.SharedScratchPools ?? Volatile.Read(ref _scratchPools) ?? CreateScratchPools();

    /// <summary>Creates a context for options.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfOpenContext Create(PdfOpenOptions options) =>
        PdfOpenContext.Create(options, options.CancellationToken);

    /// <summary>Creates a context for options with a token that replaces the options' token.</summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The context.</returns>
    internal static PdfOpenContext Create(PdfOpenOptions options, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxDecompressedBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaxDecompressedBytes, PdfLimits.MaxDecodedLength);
        return new(options.Diagnostics, options.DrawingServices, options.Recovery, options.IgnoreXrefStreams, options.MaxDecompressedBytes, cancellationToken);
    }

    /// <summary>Throws when the open was cancelled.</summary>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowIfCancelled(PdfOpenContext? context) => context?.ThrowIfCancelled();

    /// <summary>Logs a diagnostic and passes it to the sink.</summary>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="code">What happened.</param>
    /// <param name="message">A constant description.</param>
    /// <param name="objectNumber">The object involved, or 0.</param>
    /// <param name="offset">The offset involved, or -1.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Report(PdfOpenContext? context, PdfDiagnosticCode code, string message, int objectNumber, long offset) =>
        context?.Add(new(code, message, objectNumber, offset));

    /// <summary>Retains the document token after opening finishes and before the document is published.</summary>
    /// <param name="documentCancellationToken">Cancels later document operations.</param>
    internal void CompleteOpen(CancellationToken documentCancellationToken) => _cancellationToken = documentCancellationToken;

    /// <summary>Releases this document's idle scratch buffers, leaving an explicitly shared cache alone.</summary>
    internal void CloseScratchPools()
    {
        if (Volatile.Read(ref _drawingServices)?.SharedScratchPools is not null)
        {
            return;
        }

        // A full fence, not a volatile write: this thread's read of the pools must not move before the flag is visible, or
        // it and a thread creating the pools can each miss the other, leaving a pool nothing closes.
        _ = Interlocked.Exchange(ref _scratchClosed, 1);
        Volatile.Read(ref _scratchPools)?.Close();
    }

    /// <summary>Gets a copy of the logged reports.</summary>
    /// <returns>The reports.</returns>
    internal PdfDiagnostic[] Snapshot()
    {
        if (Volatile.Read(ref _gate) is not { } gate)
        {
            return [];
        }

        lock (gate)
        {
            return _log is null ? [] : [.. _log];
        }
    }

    /// <summary>Throws when the open was cancelled.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfCancelled() => _cancellationToken.ThrowIfCancellationRequested();

    /// <summary>Logs a diagnostic when it is new, then passes it to the sink.</summary>
    /// <param name="diagnostic">The diagnostic.</param>
    private void Add(PdfDiagnostic diagnostic)
    {
        var gate = Volatile.Read(ref _gate) ?? CreateGate();
        lock (gate)
        {
            // A document that reports damage reports a handful of distinct problems, so a scan of the log finds repeats
            // without a second table; the cap keeps the scan short for a file that reports more.
            _log ??= [];
            if (_log.Count < MaxLogged && !_log.Contains(diagnostic))
            {
                _log.Add(diagnostic);
            }
        }

        if (diagnostic.Code.IsRepair)
        {
            Volatile.Write(ref _hasRepairs, 1);
        }

        _sink?.Invoke(diagnostic);
    }

    /// <summary>Creates the log's lock once.</summary>
    /// <returns>The lock.</returns>
    private Lock CreateGate()
    {
        var created = new Lock();
        return Interlocked.CompareExchange(ref _gate, created, null) ?? created;
    }

    /// <summary>Creates empty drawing services once, for a document opened without any.</summary>
    /// <returns>The services.</returns>
    private PdfDrawingServices CreateDrawingServices()
    {
        var created = new PdfDrawingServices(new());
        return Interlocked.CompareExchange(ref _drawingServices, created, null) ?? created;
    }

    /// <summary>Creates the document's cache once, closing it if disposal won the race.</summary>
    /// <returns>The cache.</returns>
    private ScratchPools CreateScratchPools()
    {
        var created = new ScratchPools();
        var existing = Interlocked.CompareExchange(ref _scratchPools, created, null);
        var result = existing ?? created;
        if (Volatile.Read(ref _scratchClosed) != 0)
        {
            result.Close();
        }

        return result;
    }
}
