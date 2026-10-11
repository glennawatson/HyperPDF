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
/// <param name="drawingServices">The drawing services selected for this document.</param>
/// <param name="token">The cancellation token.</param>
[DebuggerDisplay("PdfOpenContext: {HasRepairs} repairs")]
internal sealed class PdfOpenContext(Action<PdfDiagnostic>? sink, PdfDrawingServices drawingServices, CancellationToken token)
{
    /// <summary>The most distinct reports kept; later ones still reach the sink.</summary>
    private const int MaxLogged = 1024;

    /// <summary>The diagnostics callback.</summary>
    private readonly Action<PdfDiagnostic>? _sink = sink;

    /// <summary>Guards the log.</summary>
    private readonly Lock _gate = new();

    /// <summary>The distinct reports, in the order they were first made.</summary>
    private List<PdfDiagnostic>? _log;

    /// <summary>The reports already kept, so a stream decoded again does not repeat itself.</summary>
    private HashSet<PdfDiagnostic>? _seen;

    /// <summary>1 once a report of damage has been logged.</summary>
    private int _hasRepairs;

    /// <summary>Gets a value indicating whether recovery from a damaged structure is allowed.</summary>
    internal bool Recovery { get; init; } = true;

    /// <summary>Gets a value indicating whether cross-reference streams are ignored.</summary>
    internal bool IgnoreXrefStreams { get; init; }

    /// <summary>Gets a value indicating whether any report of repaired damage has been logged.</summary>
    internal bool HasRepairs => Volatile.Read(ref _hasRepairs) != 0;

    /// <summary>Gets the token passed to this document-open or check operation.</summary>
    internal CancellationToken CancellationToken => token;

    /// <summary>Gets the drawing services selected for this open.</summary>
    internal PdfDrawingServices DrawingServices => drawingServices;

    /// <summary>Gets this document's scratch buffers or the cache explicitly shared by its drawing services.</summary>
    internal ScratchPools ScratchPools { get; } = drawingServices.SharedScratchPools ?? new();

    /// <summary>Gets whether this document owns its scratch cache.</summary>
    internal bool OwnsScratchPools { get; } = drawingServices.SharedScratchPools is null;

    /// <summary>Creates a context for options.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The context.</returns>
    internal static PdfOpenContext Create(PdfOpenOptions options) =>
        new(options.Diagnostics, options.DrawingServices ?? new(new()), options.CancellationToken) { Recovery = options.Recovery, IgnoreXrefStreams = options.IgnoreXrefStreams };

    /// <summary>Releases this document's idle scratch buffers, leaving an explicitly shared cache alone.</summary>
    internal void CloseScratchPools()
    {
        if (OwnsScratchPools)
        {
            ScratchPools.Close();
        }
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

    /// <summary>Gets a copy of the logged reports.</summary>
    /// <returns>The reports.</returns>
    internal PdfDiagnostic[] Snapshot()
    {
        lock (_gate)
        {
            return _log is null ? [] : [.. _log];
        }
    }

    /// <summary>Throws when the open was cancelled.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfCancelled() => token.ThrowIfCancellationRequested();

    /// <summary>Logs a diagnostic when it is new, then passes it to the sink.</summary>
    /// <param name="diagnostic">The diagnostic.</param>
    private void Add(PdfDiagnostic diagnostic)
    {
        lock (_gate)
        {
            _log ??= [];
            _seen ??= [];
            if (_log.Count < MaxLogged && _seen.Add(diagnostic))
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
}
