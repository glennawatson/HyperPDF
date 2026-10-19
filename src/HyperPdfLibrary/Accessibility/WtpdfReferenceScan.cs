// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks the <c>Ref</c> entries Well-Tagged PDF 1.0 asks for: table of contents items (clause 8.2.5.8) and continued lists (clause 8.2.5.25).</summary>
internal static class WtpdfReferenceScan
{
    /// <summary>Requires each TOCI to point at its target with a <c>Ref</c> on itself or a child.</summary>
    /// <param name="item">The TOCI.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void CheckTocItem(PdfStructureElement item, List<PdfWtpdfFinding> findings)
    {
        if (HasRef(item))
        {
            return;
        }

        foreach (var kid in item.KidSpan)
        {
            if (kid.Element is { } child && HasRef(child))
            {
                return;
            }
        }

        findings.Add(new(PdfWtpdfProfile.Reuse, "8.2.5.8", "A table of contents item needs a Ref entry, on itself or on a Reference inside it, that names the target.", item.Id.Number));
    }

    /// <summary>Requires a footnote or endnote to name the citations that refer to it with a <c>Ref</c> entry (clause 8.2.5.14.1).</summary>
    /// <param name="note">The FENote.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void CheckNote(PdfStructureElement note, List<PdfWtpdfFinding> findings)
    {
        if (!HasRef(note))
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.2.5.14.1", "An FENote needs a Ref entry that names every citation that refers to it.", note.Id.Number));
        }
    }

    /// <summary>Requires a continued list to say so and to carry a <c>Ref</c> to the other parts (reuse level).</summary>
    /// <param name="list">The L tag.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void CheckContinuedList(PdfStructureElement list, List<PdfWtpdfFinding> findings)
    {
        if (list.Attributes.HasContinuedFrom && !HasRef(list))
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.2.5.25", "A list that continues from another list needs a Ref entry that names every part of the list.", list.Id.Number));
        }
    }

    /// <summary>Determines whether a tag has a <c>Ref</c> entry.</summary>
    /// <param name="element">The tag.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    private static bool HasRef(PdfStructureElement element) => !element.Dictionary.Get(KnownName.Ref).IsNull;
}
