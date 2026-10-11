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
/// content. Each fault carries the object number and, where there is one, the offset.
/// </summary>
[DebuggerDisplay("PdfDocumentChecker: {_faults.Count} faults")]
internal sealed class PdfDocumentChecker
{
    /// <summary>The faults found by the steps, in order.</summary>
    private readonly List<PdfDiagnostic> _faults = [];

    /// <summary>The document being checked.</summary>
    private readonly PdfDocument _document;

    /// <summary>The checks to run.</summary>
    private readonly PdfCheckOptions _options;

    /// <summary>The context that receives the streams' diagnostics and the cancellation token.</summary>
    private readonly PdfOpenContext _context;

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
    internal PdfDocumentChecker(PdfDocument document, PdfCheckOptions options, CancellationToken cancellationToken)
    {
        _document = document;
        _options = options;
        _context = new(_faults.Add, cancellationToken);
    }

    /// <summary>Gets one more than the highest object number.</summary>
    internal int ObjectLimit => _document.Objects.Size;

    /// <summary>Gets the number of pages.</summary>
    internal int PageCount => _document.PageCount;

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
        new PageTreeChecker(_document.Objects, _faults).Check();
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
        var report = new List<PdfDiagnostic>();
        var seen = new HashSet<FaultKey>();
        foreach (var fault in StoreRepairs.GetDiagnostics(_document.Objects))
        {
            AddOnce(fault, seen, report);
        }

        foreach (var fault in _faults)
        {
            AddOnce(fault, seen, report);
        }

        return new(report, _objects, _streams, _pages);
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
