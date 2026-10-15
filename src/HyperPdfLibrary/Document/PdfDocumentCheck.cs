// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Checks document structure and content.</summary>
public static class PdfDocumentCheck
{
    /// <summary>The objects or pages checked between yields in <see cref="CheckAsync(PdfDocument, PdfCheckOptions, CancellationToken)"/>.</summary>
    private const int CheckBatch = 64;

    /// <summary>
    /// Gets a value indicating whether reading has repaired damage in the file so far: a rebuilt cross-reference table, trailer,
    /// catalog or page tree, a wrong stream length, a bad page box, truncated stream data or a malformed font entry. Faults
    /// inside streams and fonts show once they are read, so ask again after rendering or call <see cref="Check(PdfDocument, PdfCheckOptions)"/>.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>True when opening the document repaired its structure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WasRepaired(PdfDocument document) => StoreRepairs.HasRepairs(document.Objects);

    /// <summary>
    /// Opens bytes with the check's recovery switches and checks them. A file that cannot be opened without recovery gives
    /// one fault instead of an exception.
    /// </summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="options">The checks and open switches.</param>
    /// <returns>Each fault with its object number and offset.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfCheckReport Check(byte[] bytes, PdfCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            using var document = PdfDocumentReader.OpenWith(bytes, PdfDocumentCheck.OpenOptionsFor(options));
            return PdfDocumentCheck.Check(document, options);
        }
        catch (PdfException e)
        {
            return PdfDocumentCheck.Unreadable(e);
        }
    }

    /// <summary>Checks the whole document: reads every object, decodes every stream, parses every page's content and checks the page tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The checks to run. The recovery switches apply only to the static check methods.</param>
    /// <returns>Each fault with its object number and offset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static PdfCheckReport Check(PdfDocument document, PdfCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return PdfDocumentChecker.Rent(document, options, CancellationToken.None).Run();
    }

    /// <summary>Reads a file with real asynchronous I/O, then opens and checks it with the check's recovery switches.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The checks and open switches.</param>
    /// <param name="cancellationToken">Stops the read and the check.</param>
    /// <returns>Each fault with its object number and offset.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfCheckReport> CheckAsync(string path, PdfCheckOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = PdfDocumentReader.OpenWith(bytes, PdfDocumentCheck.OpenOptionsFor(options) with { CancellationToken = cancellationToken });
            return await PdfDocumentCheck.CheckAsync(document, options, cancellationToken).ConfigureAwait(false);
        }
        catch (PdfException e)
        {
            return PdfDocumentCheck.Unreadable(e);
        }
    }

    /// <summary>
    /// Checks the whole document without holding a thread for the whole run: the work happens in batches, and the check
    /// yields between them and stops when the token is cancelled.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The checks to run. The recovery switches apply only to the static check methods.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>Each fault with its object number and offset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfCheckReport> CheckAsync(PdfDocument document, PdfCheckOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var checker = PdfDocumentChecker.Rent(document, options, cancellationToken);
        await Task.Yield();
        for (var number = 1; number < checker.ObjectLimit; number++)
        {
            checker.CheckObject(number);
            await PdfDocumentCheck.YieldEvery(number, cancellationToken).ConfigureAwait(false);
        }

        checker.CheckStructure();
        for (var index = 0; index < checker.PageCount; index++)
        {
            checker.CheckContent(index);
            await PdfDocumentCheck.YieldEvery(index, cancellationToken).ConfigureAwait(false);
        }

        return checker.Finish();
    }

    /// <summary>Gets the distinct repairs reported so far, in the order they were first met.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A copy of the repairs, each with its object number and offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDiagnostic[] GetRepairs(PdfDocument document) => StoreRepairs.GetRepairs(document.Objects);

    /// <summary>Maps a check's switches to open options.</summary>
    /// <param name="options">The check options.</param>
    /// <returns>The open options.</returns>
    private static PdfOpenOptions OpenOptionsFor(PdfCheckOptions options) => new() { Recovery = options.Recovery, IgnoreXrefStreams = options.IgnoreXrefStreams, };

    /// <summary>Makes the report for a file that could not be opened.</summary>
    /// <param name="error">The error.</param>
    /// <returns>A report with the one fault.</returns>
    private static PdfCheckReport Unreadable(PdfException error) => new([new(PdfDiagnosticCode.BadStructure, error.Message, 0, -1)], 0, 0, 0);

    /// <summary>Yields to the scheduler after each batch of steps.</summary>
    /// <param name="step">The step just finished.</param>
    /// <param name="cancellationToken">The token that stops the check.</param>
    /// <returns>A task that completes when the next batch may start.</returns>
    private static async ValueTask YieldEvery(int step, CancellationToken cancellationToken)
    {
        if (step % PdfDocumentCheck.CheckBatch != 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
    }
}
