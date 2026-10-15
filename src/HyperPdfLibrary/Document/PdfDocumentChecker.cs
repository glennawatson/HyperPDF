// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Runs a whole-document check in steps, so a caller can pause between them: one object, the page tree, or one page's
/// content. Each fault carries the object number and, where there is one, the offset. The document keeps the checker of
/// the last check that ended, so checks run one after another make it once.
/// </summary>
[DebuggerDisplay("PdfDocumentChecker: {_faults.Count} faults")]
internal sealed class PdfDocumentChecker
{
    /// <summary>The faults found by the steps, in order.</summary>
    private readonly List<PdfDiagnostic> _faults = [];

    /// <summary>The document being checked.</summary>
    private readonly PdfDocument _document;

    /// <summary>The callback that adds a diagnostic to <see cref="_faults"/>.</summary>
    private readonly Action<PdfDiagnostic> _sink;

    /// <summary>The checks to run.</summary>
    private PdfCheckOptions _options;

    /// <summary>The context that receives the streams' diagnostics and the cancellation token.</summary>
    private PdfOpenContext _context;

    /// <summary>The context made for a check that cannot be cancelled, kept for the next one.</summary>
    private PdfOpenContext? _quietContext;

    /// <summary>The objects read.</summary>
    private int _objects;

    /// <summary>The streams decoded.</summary>
    private int _streams;

    /// <summary>The pages checked.</summary>
    private int _pages;

    /// <summary>Initializes a new instance of the <see cref = "PdfDocumentChecker"/> class.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "options">The checks to run.</param>
    /// <param name = "cancellationToken">The token that stops the check.</param>
    private PdfDocumentChecker(PdfDocument document, PdfCheckOptions options, CancellationToken cancellationToken)
    {
        _document = document;
        _sink = _faults.Add;
        _options = options;
        _context = CreateContext(cancellationToken);
    }

    /// <summary>Gets one more than the highest object number.</summary>
    internal int ObjectLimit => _document.Objects.Size;

    /// <summary>Gets the number of pages.</summary>
    internal int PageCount => _document.PageCount;

    /// <summary>Takes the document's spare checker, or makes one, ready for a check.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "options">The checks to run.</param>
    /// <param name = "cancellationToken">The token that stops the check.</param>
    /// <returns>The checker, which only the caller uses until its report is built.</returns>
    internal static PdfDocumentChecker Rent(PdfDocument document, PdfCheckOptions options, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref document.State.Checker, null) is not { } checker)
        {
            return new(document, options, cancellationToken);
        }

        checker._options = options;
        checker._context = checker.CreateContext(cancellationToken);
        return checker;
    }

    /// <summary>Runs every step.</summary>
    /// <returns>The report.</returns>
    internal PdfCheckReport Run()
    {
        for (var number = 1; number < ObjectLimit; number++)
        {
            CheckObject(number);
        }

        CheckStructure();
        for (var index = 0; index < PageCount; index++)
        {
            CheckContent(index);
        }

        return Finish();
    }

    /// <summary>Reads one object and, for a stream, decodes it.</summary>
    /// <param name = "number">The object number.</param>
    internal void CheckObject(int number)
    {
        _context.ThrowIfCancelled();
        var objects = _document.Objects;
        if (StoreRepairs.GetEntryType(objects, number) == XrefEntryType.Free)
        {
            return;
        }

        _objects++;
        if (_options.DecodeStreams && StoreReading.GetObject(objects, new(number, 0)).AsStream() is { } stream)
        {
            CheckStream(stream, number);
        }
    }

    /// <summary>Checks the page tree and the pages' boxes.</summary>
    internal void CheckStructure()
    {
        _context.ThrowIfCancelled();
        var pageTree = new PageTreeChecker(_document.Objects, _faults);
        pageTree.Check();
        CheckLinearization();
    }

    /// <summary>Parses one page's content.</summary>
    /// <param name = "index">The zero based page index.</param>
    internal void CheckContent(int index)
    {
        _context.ThrowIfCancelled();
        _pages++;
        if (!_options.ParseContent)
        {
            return;
        }

        var page = PdfDocumentPages.GetPage(_document, index);
        var content = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref content, _context.CancellationToken);
            ContentChecker.Scan(content.WrittenSpan, _document.Objects.Names, page.Id.Number, _faults);
        }
        catch (Exception e) when (e is InvalidDataException or PdfException)
        {
            _faults.Add(new(PdfDiagnosticCode.BadContentStream, "The page's content cannot be read.", page.Id.Number, -1));
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Builds the report: the repairs made while reading, then the faults the steps found, each once.</summary>
    /// <returns>The report.</returns>
    internal PdfCheckReport Finish()
    {
        var repairs = StoreRepairs.GetDiagnostics(_document.Objects);
        IReadOnlyList<PdfDiagnostic> faults = [];
        if (repairs.Length != 0 || _faults.Count != 0)
        {
            faults = Merge(repairs);
        }

        var result = new PdfCheckReport(faults, _objects, _streams, _pages);

        // The checker is ready for the next check of the document.
        _faults.Clear();
        _objects = 0;
        _streams = 0;
        _pages = 0;
        Volatile.Write(ref _document.State.Checker, this);
        return result;
    }

    /// <summary>Adds a fault unless the same fault is already in the report.</summary>
    /// <param name = "fault">The fault.</param>
    /// <param name = "seen">The faults added.</param>
    /// <param name = "report">The report being built.</param>
    private static void AddOnce(in PdfDiagnostic fault, HashSet<FaultKey> seen, List<PdfDiagnostic> report)
    {
        if (seen.Add(new(fault.Code, fault.ObjectNumber, fault.Message)))
        {
            report.Add(fault);
        }
    }

    /// <summary>Lists the repairs made while reading, then the faults the steps found, each once.</summary>
    /// <param name = "repairs">The repairs the document logged.</param>
    /// <returns>The faults.</returns>
    private List<PdfDiagnostic> Merge(PdfDiagnostic[] repairs)
    {
        var report = new List<PdfDiagnostic>();
        var seen = new HashSet<FaultKey>();
        foreach (var fault in repairs)
        {
            AddOnce(fault, seen, report);
        }

        foreach (var fault in _faults)
        {
            AddOnce(fault, seen, report);
        }

        return report;
    }

    /// <summary>Gets the context that collects a check's diagnostics.</summary>
    /// <param name = "cancellationToken">The token that stops the check.</param>
    /// <returns>The context; one that cannot be cancelled is kept for the next check.</returns>
    private PdfOpenContext CreateContext(CancellationToken cancellationToken) =>
        cancellationToken.CanBeCanceled ? new(_sink, _document.DrawingServices, cancellationToken) : _quietContext ??= new(_sink, _document.DrawingServices, cancellationToken);

    /// <summary>Reports a declared linearization header or page hint table that cannot be validated.</summary>
    private void CheckLinearization()
    {
        if (!PdfDocumentLinearization.IsDeclared(_document))
        {
            return;
        }

        var info = PdfDocumentLinearization.GetInfo(_document);
        if (info is null)
        {
            _faults.Add(new(PdfDiagnosticCode.BadLinearization, "The linearization header does not match this file.", 0, 0));
            return;
        }

        try
        {
            if (info.Hints.IsDefaultOrEmpty || PdfLinearizationHints.GetPageRanges(_document, _context.CancellationToken) is not { } ranges)
            {
                _faults.Add(new(PdfDiagnosticCode.BadLinearization, "The linearization page hints are missing or damaged.", 0, info.Hints.IsDefaultOrEmpty ? 0 : info.Hints[0].Offset));
            }
            else if (!PdfLinearizationValidity.TryCheck(_document, info, ranges, _context.CancellationToken, out var reason, out var offset))
            {
                _faults.Add(new(PdfDiagnosticCode.BadLinearization, reason, 0, offset));
            }
        }
        catch (Exception error) when (error is PdfException or InvalidDataException or OverflowException)
        {
            _faults.Add(new(PdfDiagnosticCode.BadLinearization, "The linearization page hints cannot be decoded.", 0, info.Hints[0].Offset));
        }
    }

    /// <summary>Decodes a stream and reports damage; a JPEG without an end marker is truncated.</summary>
    /// <param name = "stream">The stream.</param>
    /// <param name = "number">The stream's object number.</param>
    private void CheckStream(PdfStream stream, int number)
    {
        _streams++;
        var buffer = default(PooledBuffer);
        try
        {
            var codec = PdfStreamDecoder.Decode(stream, _context, ref buffer, _context.CancellationToken);
            if (codec == PdfImageCodec.Jpeg && !JpegMarkers.EndsWithEndOfImage(buffer.WrittenSpan))
            {
                _faults.Add(new(PdfDiagnosticCode.TruncatedStream, "A JPEG image ends without its end marker; the part that decoded is used.", number, -1));
            }
        }
        catch (Exception e) when (e is InvalidDataException or PdfException)
        {
            _faults.Add(new(PdfDiagnosticCode.TruncatedStream, "A stream cannot be decoded.", number, -1));
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>The identity of a fault for removing repeats; the offset is left out because two steps may know it differently.</summary>
    /// <param name = "Code">The fault.</param>
    /// <param name = "ObjectNumber">The object number.</param>
    /// <param name = "Message">The description.</param>
    private readonly record struct FaultKey(PdfDiagnosticCode Code, int ObjectNumber, string Message);
}
