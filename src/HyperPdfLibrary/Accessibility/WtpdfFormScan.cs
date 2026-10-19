// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks that a Form tag encloses at most one widget annotation (WTPDF 1.0, clause 8.10.1).</summary>
internal static class WtpdfFormScan
{
    /// <summary>Counts the widgets a Form encloses directly and reports it when there is more than one.</summary>
    /// <param name="form">The Form tag.</param>
    /// <param name="store">The document's object store.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void Check(PdfStructureElement form, PdfObjectStore store, List<PdfWtpdfFinding> findings)
    {
        var widgets = 0;
        foreach (var kid in form.KidSpan)
        {
            if (kid.Kind == PdfStructureKidKind.Object && kid.Object.IsValid
                && StoreReading.Resolve(store, PdfValue.FromReference(kid.Object)).AsDictionary() is { } annotation
                && annotation.GetName(KnownName.Subtype).ToKnownName() == KnownName.Widget)
            {
                widgets++;
            }
        }

        if (widgets > 1)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.10.1", "A Form tag can enclose only one widget annotation. Give each widget its own Form tag.", form.Id.Number));
        }
    }
}
