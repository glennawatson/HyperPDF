// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Annotation editing and saving.</summary>
public sealed partial class PdfiumDocument
{
    /// <summary>Saves only the changes after the original bytes, keeping digital signatures valid.</summary>
    private const int SaveIncremental = 1;

    /// <summary>Saves a complete, compact file.</summary>
    private const int SaveFull = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The annotations of each page read so far, dropped when the page is edited.</summary>
    private readonly Dictionary<int, PageAnnotation[]> _annotationCache = [];

    /// <summary>The number of edits since opening or the last save.</summary>
    private int _unsavedChanges;

    /// <summary>
    /// 1 once an edit has left work for saving (a reply to link, a removed annotation to leave out, or entries PDFium
    /// cannot write), so saving finishes the annotations.
    /// </summary>
    private int _pendingSaveWork;

    /// <summary>The author recorded on new annotations.</summary>
    private string _author = Environment.UserName;

    /// <inheritdoc/>
    public bool HasUnsavedChanges => Volatile.Read(ref _unsavedChanges) != 0;

    /// <inheritdoc/>
    public string Author
    {
        get => Volatile.Read(ref _author);
        set => Volatile.Write(ref _author, string.IsNullOrWhiteSpace(value) ? Environment.UserName : value.Trim());
    }

    /// <inheritdoc/>
    public void GetAnnotations(int pageIndex, List<PageAnnotation> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        if (_annotationCache.TryGetValue(pageIndex, out var cached))
        {
            output.AddRange(cached);
            return;
        }

        if (IsDisposed || GetPage(pageIndex) is not { } page)
        {
            return;
        }

        var start = output.Count;
        PdfiumAnnotations.Read(page, output);
        _annotationCache[pageIndex] = [.. CollectionsMarshal.AsSpan(output)[start..]];
    }

    /// <inheritdoc/>
    public int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddMarkup(page, kind, lines, color, contents, Author) : -1);
    }

    /// <inheritdoc/>
    public int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddInk(page, points, strokeLengths, color, width, kind, Author) : -1);
    }

    /// <inheritdoc/>
    public int AddNote(int pageIndex, PagePoint location, string contents, uint color)
    {
        ArgumentNullException.ThrowIfNull(contents);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddNote(page, location, contents, color, Author) : -1);
    }

    /// <inheritdoc/>
    public int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddText(_fonts, page, location, text, (fontSize, color), kind, Author) : -1);
    }

    /// <inheritdoc/>
    public int AddShape(int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddShape(page, kind, start, end, color, width, Author) : -1);
    }

    /// <inheritdoc/>
    public int AddStamp(int pageIndex, PagePoint location, string label, uint color)
    {
        ArgumentNullException.ThrowIfNull(label);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && _fonts.Get(false) is { } font ? PdfiumAnnotations.AddStamp(_fonts.Document, font, page, location, label, color, Author) : -1);
    }

    /// <inheritdoc/>
    public int AddImageStamp(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height * BgraBytes)
        {
            return -1;
        }

        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddImageStamp(_handle, page, bounds, pixels, width, height, Author) : -1);
    }

    /// <inheritdoc/>
    public int AddPolygon(int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Pending(Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddPolygon(page, kind, vertices, color, width, Author) : -1));
    }

    /// <inheritdoc/>
    public int AddCallout(int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var scope = PdfiumLibrary.EnterScope();
        var index = EditablePage(pageIndex) is { } page && _fonts.Get(false) is { } font
            ? PdfiumAnnotations.AddCallout(_fonts.Document, page, (target, location), new(font, fontSize, color), text, Author)
            : -1;
        return Pending(Changed(pageIndex, index));
    }

    /// <inheritdoc/>
    public bool SetBounds(int pageIndex, int index, PageRect bounds)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Pending(Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetBounds(page, index, bounds)));
    }

    /// <inheritdoc/>
    public bool SetLineWidth(int pageIndex, int index, float width)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetLineWidth(page, index, width));
    }

    /// <inheritdoc/>
    public bool SetFontSize(int pageIndex, int index, float fontSize)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Pending(Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetFontSize(_fonts, page, index, fontSize)));
    }

    /// <inheritdoc/>
    public bool SetRemoved(int pageIndex, int index, bool removed)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Pending(Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetRemoved(page, index, removed)));
    }

    /// <inheritdoc/>
    public void GetReplies(int pageIndex, int index, List<AnnotationReply> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        if (EditablePage(pageIndex) is { } page)
        {
            PdfiumAnnotations.ReadReplies(page, index, output);
        }
    }

    /// <inheritdoc/>
    public int AddReply(int pageIndex, int index, string contents, ReviewState state)
    {
        ArgumentNullException.ThrowIfNull(contents);
        using var scope = PdfiumLibrary.EnterScope();
        var reply = EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddReply(page, index, contents, state, Author) : -1;
        return Pending(Changed(pageIndex, reply));
    }

    /// <inheritdoc/>
    public bool SetColor(int pageIndex, int index, uint color)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetColor(page, index, color));
    }

    /// <inheritdoc/>
    public bool SetContents(int pageIndex, int index, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetContents(page, index, contents));
    }

    /// <inheritdoc/>
    public bool Remove(int pageIndex, int index)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && NativeMethods.FPDFPage_RemoveAnnot(page.Handle, index) != 0);
    }

    /// <inheritdoc/>
    public bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed)
        {
            return false;
        }

        var flags = NativeMethods.FPDF_GetSignatureCount(_handle) > 0 ? SaveIncremental : SaveFull;
        if (Volatile.Read(ref _pendingSaveWork) == 0)
        {
            if (!WriteDocument(_handle, destination, flags))
            {
                return false;
            }
        }
        else
        {
            // Replies name the comment they answer, removed annotations are kept hidden, and some entries are kept as
            // text until saved; finish them with standard PDF on the way out. The file is written into a pooled buffer
            // that the update is read from, so the file is never copied, and the update is appended after it.
            using var buffer = new PooledWriteStream();
            if (!WriteDocument(_handle, buffer, flags))
            {
                return false;
            }

            WriteFinished(buffer.Written, destination);
        }

        Volatile.Write(ref _unsavedChanges, 0);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> SaveAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        await using var buffer = new PooledWriteStream();
        int version;
        using (var scope = PdfiumLibrary.EnterScope())
        {
            if (IsDisposed)
            {
                return false;
            }

            version = Volatile.Read(ref _unsavedChanges);
            var flags = NativeMethods.FPDF_GetSignatureCount(_handle) > 0 ? SaveIncremental : SaveFull;
            if (!WriteDocument(_handle, buffer, flags))
            {
                return false;
            }

            if (Volatile.Read(ref _pendingSaveWork) != 0 && AnnotationReplyLinks.HasPendingWork(buffer.Written.Span))
            {
                buffer.Write(AnnotationReplyLinks.CreateUpdate(buffer.Written));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        await destination.WriteAsync(buffer.Written, cancellationToken).ConfigureAwait(false);
        _ = Interlocked.CompareExchange(ref _unsavedChanges, 0, version);
        return true;
    }

    /// <summary>Writes a saved file, followed by the update that finishes its annotations when it has any to finish.</summary>
    /// <param name="written">The file as PDFium wrote it.</param>
    /// <param name="destination">The stream.</param>
    private static void WriteFinished(ReadOnlyMemory<byte> written, Stream destination)
    {
        destination.Write(written.Span);
        if (AnnotationReplyLinks.HasPendingWork(written.Span))
        {
            destination.Write(AnnotationReplyLinks.CreateUpdate(written));
        }
    }

    /// <summary>Writes a document into a stream. Callers hold the PDFium lock.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="flags">Incremental or full.</param>
    /// <returns><see langword="true"/> when written.</returns>
    private static unsafe bool WriteDocument(PdfiumDocumentHandle document, Stream destination, int flags)
    {
        var stream = new GCHandle<Stream>(destination);
        try
        {
            var writer = new FileWrite(&WriteBlock, stream);
            return NativeMethods.FPDF_SaveAsCopy(document, &writer, flags) != 0;
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>PDFium's write callback: copies a block into the destination stream.</summary>
    /// <param name="writer">The writer PDFium was given.</param>
    /// <param name="data">The block.</param>
    /// <param name="size">The block size in bytes.</param>
    /// <returns>1 on success, 0 to make PDFium stop.</returns>
    [UnmanagedCallersOnly]
    private static unsafe int WriteBlock(FileWrite* writer, void* data, CULong size)
    {
        try
        {
            ReadOnlySpan<byte> block = new(data, checked((int)size.Value));
            writer->Target.Write(block);
            return 1;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    /// <summary>Gets a page for editing, or <see langword="null"/> when the document is closed.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The page.</returns>
    private PdfiumPage? EditablePage(int pageIndex) => IsDisposed ? null : GetPage(pageIndex);

    /// <summary>Notes that saving has work to finish when an edit succeeded.</summary>
    /// <param name="index">The new annotation index, or -1.</param>
    /// <returns>The index.</returns>
    private int Pending(int index)
    {
        _ = Pending(index >= 0);
        return index;
    }

    /// <summary>Notes that saving has work to finish when an edit succeeded.</summary>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    private bool Pending(bool changed)
    {
        if (changed)
        {
            Volatile.Write(ref _pendingSaveWork, 1);
        }

        return changed;
    }

    /// <summary>Records an edit when an index shows one was made.</summary>
    /// <param name="pageIndex">The edited page.</param>
    /// <param name="index">The new annotation index, or -1.</param>
    /// <returns>The index.</returns>
    private int Changed(int pageIndex, int index)
    {
        _ = Changed(pageIndex, index >= 0);
        return index;
    }

    /// <summary>Records an edit when it succeeded, forgetting the page's cached annotations.</summary>
    /// <param name="pageIndex">The edited page.</param>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    private bool Changed(int pageIndex, bool changed)
    {
        if (changed)
        {
            _ = _annotationCache.Remove(pageIndex);
            _ = Interlocked.Increment(ref _unsavedChanges);
            InvalidateLayerView();
        }

        return changed;
    }
}
