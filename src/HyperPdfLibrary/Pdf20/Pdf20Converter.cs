// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Saves a copy of a document as PDF 2.0 (ISO 32000-2). The conversion works on a private copy, so the document passed in
/// never changes. The copy has a <c>%PDF-2.0</c> header and a trailer /ID, and it carries no feature PDF 2.0 deprecates:
/// <list type="bullet">
/// <item><description>
/// Moved: the document information (14.3.3) goes to XMP, with custom keys in the <c>pdfx</c> namespace and the XMP value
/// winning when the two differ; blend mode arrays and /Compatible (11.3.5) become one mode; operating system file names
/// (7.11.3) move to /F.
/// </description></item>
/// <item><description>
/// Dropped, because they carry no content: XFA of a form that has AcroForm fields, /NeedsRendering false and
/// /NeedAppearances (12.7.3, 7.7.2; field appearances are drawn first), procedure sets (14.2), font, image and form names
/// (9.6.2.1, 8.9.5.1, 8.10.2), /CharSet and /CIDSet (9.8), alternate presentations (7.7.4), Web Capture data (14.10),
/// prepress viewer preferences (12.2), the tag suspects flag (14.7.1), identity transfer functions (8.4.5), launch
/// parameters per platform (12.6.4.6) and the legal attestation counts (12.8.7).
/// </description></item>
/// <item><description>
/// Refused, because dropping them would change what the file shows, plays or prints: signed files (12.8), encrypted files
/// (7.6), XFA-only forms and /NeedsRendering true (7.7.2, Annex K), movie, sound and trap network annotations and the
/// sound and movie actions (12.5.6, 12.6.4), OPI (14.11.7), preseparated pages (14.11.4) and transfer functions that
/// change colour (8.4.5). So are files that claim PDF/A-1, 2 or 3 or a PDF/X edition older than X-6, whose headers must
/// be 1.x.
/// </description></item>
/// </list>
/// Every move, drop and refusal is listed in the result with its clause. Nothing is written when the copy is refused.
/// </summary>
public static class Pdf20Converter
{
    /// <summary>Saves a copy of a document as PDF 2.0.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">Receives the file when the copy is made; nothing is written otherwise.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Pdf20ConversionResult SaveAsPdf20(PdfDocument document, Stream destination) => SaveAsPdf20(document, destination, CancellationToken.None);

    /// <summary>Saves a copy of a document as PDF 2.0.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">Receives the file when the copy is made; nothing is written otherwise.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Pdf20ConversionResult SaveAsPdf20(PdfDocument document, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        var output = Convert(document, Pdf20ConversionOptions.Default, cancellationToken);
        if (output.File is { } file)
        {
            destination.Write(file);
        }

        return output.Result;
    }

    /// <summary>
    /// Saves a copy of a document as PDF 2.0. The file's bytes are loaded first with asynchronous I/O, and the copy is
    /// written to the destination the same way.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">Receives the file when the copy is made; nothing is written otherwise.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Pdf20ConversionResult> SaveAsPdf20Async(PdfDocument document, Stream destination, CancellationToken cancellationToken) =>
        SaveAsPdf20Async(document, destination, Pdf20ConversionOptions.Default, cancellationToken);

    /// <summary>
    /// Saves a copy of a document as PDF 2.0, dropping the signatures and the protection when the options say so. The
    /// file's bytes are loaded first with asynchronous I/O, and the copy is written to the destination the same way.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">Receives the file when the copy is made; nothing is written otherwise.</param>
    /// <param name="options">What the copy may drop.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<Pdf20ConversionResult> SaveAsPdf20Async(PdfDocument document, Stream destination, Pdf20ConversionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        var source = document.Objects.Source;
        if (source.NeedsPrefetch)
        {
            await source.PrefetchAsync(0, source.Length, cancellationToken).ConfigureAwait(false);
        }

        var output = Convert(document, options, cancellationToken);
        if (output.File is { } file)
        {
            await destination.WriteAsync(file, cancellationToken).ConfigureAwait(false);
        }

        return output.Result;
    }

    /// <summary>Converts a document, returning the file only when it passed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">What the copy may drop.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome and, when converted, the file.</returns>
    private static Pdf20ConversionOutput Convert(PdfDocument document, in Pdf20ConversionOptions options, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        cancellationToken.ThrowIfCancellationRequested();
        if (Pdf20ConversionSource.Check(document, options) is { } refused)
        {
            return refused;
        }

        try
        {
            // The working copy shares the file and carries the unsaved edits; it is the copy's own, so the fixes may change it.
            using var working = PdfDocumentOptimizing.OpenWorkingCopy(document);
            var session = new Pdf20ConversionSession(working, cancellationToken);
            DropForOptions(session, options);
            Fix(session);
            return session.HasProblems ? Stopped(session) : Write(session, document.PageCount, options.RemoveEncryption);
        }
        catch (PdfException)
        {
            return Refused("7.5", "The document could not be read completely, so it cannot be copied.");
        }
    }

    /// <summary>Writes the fixed working copy and checks the file.</summary>
    /// <param name="session">The conversion, with no problems.</param>
    /// <param name="pageCount">The pages the source has.</param>
    /// <param name="removeEncryption">Whether the copy is written with no protection.</param>
    /// <returns>The outcome and the file, when it passed.</returns>
    private static Pdf20ConversionOutput Write(Pdf20ConversionSession session, int pageCount, bool removeEncryption)
    {
        var file = PdfCompactWriter.SaveUnchecked(session.Store, new(true, removeEncryption));
        if (IsPdf20Copy(file, pageCount))
        {
            return new(new(session.GetChanges(), []), file);
        }

        session.Refuse("7.5", "The finished copy did not pass HyperPDF's own check, so it was not saved.");
        return Stopped(session);
    }

    /// <summary>Builds the outcome of a conversion that stopped.</summary>
    /// <param name="session">The conversion.</param>
    /// <returns>The outcome, with no file.</returns>
    private static Pdf20ConversionOutput Stopped(Pdf20ConversionSession session) => new(new(session.GetChanges(), session.GetProblems()), null);

    /// <summary>Removes the signatures the options allow to be removed, and says so in the result.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="options">What the copy may drop.</param>
    private static void DropForOptions(Pdf20ConversionSession session, in Pdf20ConversionOptions options)
    {
        if (options.RemoveSignatures && PdfDocumentSignatureEditing.RemoveSignatures(session.Document) > 0)
        {
            session.Changed("12.8.1", "The signatures were removed, because a changed file no longer matches what was signed.");
        }

        if (options.RemoveEncryption && session.Store.Security is not null)
        {
            session.Changed("7.6.1", "The protection was removed, because PDF 2.0 deprecates the kind the file used.");
        }
    }

    /// <summary>Runs every fix on the working copy, in order.</summary>
    /// <param name="session">The conversion.</param>
    private static void Fix(Pdf20ConversionSession session)
    {
        Pdf20ConversionVersion.Apply(session);
        Pdf20ConversionForms.DrawAppearances(session);
        Pdf20ConversionCatalog.Apply(session);
        Pdf20ConversionDictionaries.Apply(session);
        Pdf20ConversionContent.Apply(session);
        Pdf20ConversionInfo.Apply(session);
    }

    /// <summary>Opens the written file again and checks its header, its page count and that its document information holds at most the two dates.</summary>
    /// <param name="file">The written file.</param>
    /// <param name="pageCount">The pages the source has.</param>
    /// <returns><see langword="true"/> when the file passes.</returns>
    private static bool IsPdf20Copy(byte[] file, int pageCount)
    {
        using var written = PdfDocumentReader.Open(file, null);
        var info = StoreReading.Resolve(written.Objects, written.Objects.Trailer.GetRaw(KnownName.Info)).AsDictionary();
        return file.AsSpan().StartsWith("%PDF-2.0"u8)
            && string.Equals(written.Objects.Version, Pdf20ConversionVersion.Version, StringComparison.Ordinal)
            && written.PageCount == pageCount
            && (info is null || Pdf20InfoMapping.OnlyDates(info));
    }

    /// <summary>Builds a refusal with one problem.</summary>
    /// <param name="clause">The clause.</param>
    /// <param name="description">The reason, in plain words.</param>
    /// <returns>The outcome.</returns>
    private static Pdf20ConversionOutput Refused(string clause, string description) =>
        new(new([], [new(Pdf20ConversionSession.Specification, clause, description)]), null);
}
