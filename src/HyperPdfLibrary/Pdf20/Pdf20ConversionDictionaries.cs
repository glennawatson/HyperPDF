// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Runs the deprecated-feature fixes over every dictionary of the document, then tells the user what changed and what
/// stops the copy. The fixes move or remove what carries no content; the features whose removal would lose content are
/// counted and refused. A changed dictionary is copied, never changed in place.
/// </summary>
internal static class Pdf20ConversionDictionaries
{
    /// <summary>Applies the fixes.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(Pdf20ConversionSession session)
    {
        var tally = new Pdf20ConversionTally(session.Names);
        _ = PdfAConversionRewriter.Rewrite(session.Store, tally, Fix, session.CancellationToken);
        Pdf20ConversionDictionaryReport.Report(session, tally);
    }

    /// <summary>Fixes one dictionary.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>A changed copy, or <see langword="null"/> to leave the dictionary.</returns>
    private static PdfDictionary? Fix(Pdf20ConversionTally tally, PdfDictionary dictionary)
    {
        PdfDictionary? copy = null;
        Pdf20ConversionResourceFixes.Fix(tally, dictionary, ref copy);
        Pdf20ConversionGraphicsFixes.Fix(tally, dictionary, ref copy);
        Pdf20ConversionFileFixes.Fix(tally, dictionary, ref copy);
        Pdf20ConversionMediaFixes.Fix(tally, dictionary, ref copy);
        return copy;
    }
}
