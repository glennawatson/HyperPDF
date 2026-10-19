// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Reads and declares Well-Tagged PDF (PDF Association, WTPDF 1.0). Reading is lenient: any file can be checked. Declaring is
/// strict: a level is written only when the file meets every rule this library can check for it.
/// </summary>
public static class PdfDocumentWtpdf
{
    /// <summary>Checks a document against Well-Tagged PDF.</summary>
    /// <param name="document">The document.</param>
    /// <returns>Which levels the file declares and what falls short.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfWtpdfReport Check(PdfDocument document) => Check(document, CancellationToken.None);

    /// <summary>Checks a document against Well-Tagged PDF.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Which levels the file declares and what falls short.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfWtpdfReport Check(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return WtpdfScan.Check(document, cancellationToken);
    }

    /// <summary>Removes a level's declaration, for example when an edit means the file no longer meets it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="profile">The level to withdraw.</param>
    /// <returns><see langword="true"/> when a declaration was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a known level.</exception>
    /// <exception cref="InvalidOperationException">The metadata cannot be edited safely.</exception>
    public static bool Withdraw(PdfDocument document, PdfWtpdfProfile profile)
    {
        ArgumentNullException.ThrowIfNull(document);
        var removed = PdfDocumentDeclarations.Remove(document, UriOf(profile));
        var removedWithSlash = PdfDocumentDeclarations.Remove(document, SlashUriOf(profile));
        return removed || removedWithSlash;
    }

    /// <summary>Declares a level, after checking that the file meets it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="profile">The level to declare. The reuse and accessibility declarations can both be present.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a known level.</exception>
    /// <exception cref="InvalidOperationException">The file falls short of the level, so the claim would be false, or the metadata cannot take the declaration.</exception>
    public static void Declare(PdfDocument document, PdfWtpdfProfile profile)
    {
        ArgumentNullException.ThrowIfNull(document);
        var uri = UriOf(profile);
        var report = WtpdfScan.Check(document, CancellationToken.None);
        foreach (var finding in report.Findings)
        {
            if (finding.Profile <= profile)
            {
                throw new InvalidOperationException(
                    $"The file does not meet Well-Tagged PDF {profile} ({report.CountFor(profile)} problems), so the declaration was not written. "
                    + $"First problem, clause {finding.Clause}: {finding.Message}");
            }
        }

        PdfDocumentDeclarations.Add(document, new(uri, []));
    }

    /// <summary>Gets the address a level is declared with, which is the spelling veraPDF accepts.</summary>
    /// <param name="profile">The level.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a known level.</exception>
    private static string UriOf(PdfWtpdfProfile profile) => profile switch
    {
        PdfWtpdfProfile.Reuse => WtpdfScan.ReuseUri,
        PdfWtpdfProfile.Accessibility => WtpdfScan.AccessibilityUri,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Not a known Well-Tagged PDF level."),
    };

    /// <summary>Gets the address as the WTPDF text spells it, with a slash before the fragment.</summary>
    /// <param name="profile">The level.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a known level.</exception>
    private static string SlashUriOf(PdfWtpdfProfile profile) => profile switch
    {
        PdfWtpdfProfile.Reuse => WtpdfScan.ReuseUriWithSlash,
        PdfWtpdfProfile.Accessibility => WtpdfScan.AccessibilityUriWithSlash,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Not a known Well-Tagged PDF level."),
    };
}
