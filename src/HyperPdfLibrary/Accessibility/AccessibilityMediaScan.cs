// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks the media clip data ISO 14289-1 clause 7.18.6.2 requires: the optional <c>CT</c> and <c>Alt</c> keys (ISO 32000-1 Table 274) are present.</summary>
internal static class AccessibilityMediaScan
{
    /// <summary>Reports a screen annotation whose rendition's media clip data lacks <c>/CT</c> or <c>/Alt</c>.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the finding.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    internal static void Check(PdfNameTable names, PdfDictionary annotation, int? part, AccessibilityFindings findings, int pageIndex)
    {
        if (part != PdfUaClaim.Part1
            || annotation.GetName(KnownName.Subtype).ToKnownName() != KnownName.Screen
            || annotation.GetDictionary(KnownName.A)?.GetDictionary(KnownName.R)?.GetDictionary(KnownName.C) is not { } clip)
        {
            return;
        }

        // A media clip section holds its data in /D; media clip data is the clip itself.
        var data = clip.GetName(KnownName.S) == names.Intern("MCS"u8) ? clip.GetDictionary(KnownName.D) : clip;
        if (data is not null && (data.Get(KnownName.CT).IsNull || data.GetArray(KnownName.Alt) is not { Count: > 0 }))
        {
            AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.MediaClipNoAlternative, pageIndex);
        }
    }
}
