// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Output;

/// <summary>Rewrites a file just saved as a PDF 2.0 file, so editing leaves current-standard files behind by default.</summary>
internal static class Pdf20Resave
{
    /// <summary>The ending of the temporary file the PDF 2.0 form is written to.</summary>
    private const string TemporarySuffix = ".pdf20";

    /// <summary>Replaces a saved file with the open document's PDF 2.0 form, when the engine can make one that keeps everything the file has.</summary>
    /// <param name="upgrade">The open document's writer and what the person agreed may be dropped.</param>
    /// <param name="path">The file just saved, which the open document's edits are all in.</param>
    /// <returns>
    /// A cold signal giving whether the file now is a PDF 2.0 file, and when it is not, the plain-words reason; a refusal leaves the
    /// saved file exactly as it was. Unsubscribing stops converting and writing.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<Pdf20ResaveResult> Try(Pdf20Upgrade upgrade, string path) =>
        CopyFile.WriteSignal(path, TemporarySuffix, stream => upgrade.Writer.WritePdf20Copy(stream, upgrade.Options), static result => result.Converted)
            .Select(static result => new Pdf20ResaveResult(result.Converted, ReasonOf(result)))
            .Catch<Pdf20ResaveResult, Exception>(Refused);

    /// <summary>Gets why a PDF 2.0 form was not made.</summary>
    /// <param name="result">What the conversion did.</param>
    /// <returns>The plain-words reason, or <see langword="null"/> when the file was converted or no reason was given.</returns>
    private static string? ReasonOf(Pdf20CopyResult result) =>
        result.Converted || result.Problems.Length == 0 ? null : result.Problems[0].Description;

    /// <summary>Turns an expected failure into a refusal with no reason, and passes on any other.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal giving the refusal, or failing with the same error.</returns>
    private static IObservable<Pdf20ResaveResult> Refused(Exception error) =>
        error is IOException or UnauthorizedAccessException or DocumentOpenException or InvalidOperationException or InvalidDataException
            ? Signal.Return<Pdf20ResaveResult>(new(false, null))
            : Signal.Throw<Pdf20ResaveResult>(error);
}
