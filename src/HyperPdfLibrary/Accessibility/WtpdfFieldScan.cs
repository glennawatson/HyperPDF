// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks that a text field's rich text value matches its plain value (WTPDF 1.0, clause 8.10.3.3).</summary>
internal static class WtpdfFieldScan
{
    /// <summary>The most parents followed when looking for an inherited field entry, so a damaged cycle ends.</summary>
    private const int MaxDepth = 32;

    /// <summary>Reports a text field that has an <c>RV</c> but no <c>V</c>, or an <c>RV</c> that differs from its <c>V</c>.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="widget">The widget annotation, which may be merged with its field.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void Check(PdfNameTable names, PdfDictionary widget, List<PdfWtpdfFinding> findings)
    {
        var richValue = names.Intern("RV"u8);
        if (!IsTextField(widget) || Inherited(widget, richValue) is not { Kind: PdfKind.String } rich)
        {
            return;
        }

        var value = Inherited(widget, KnownName.V);
        if (value is { Kind: PdfKind.String } plain && rich.AsStringBytes().SequenceEqual(plain.AsStringBytes()))
        {
            return;
        }

        findings.Add(new(PdfWtpdfProfile.Reuse, "8.10.3.3", "A text field with an RV entry needs a V entry with the same text.", 0));
    }

    /// <summary>Determines whether a widget belongs to a text field.</summary>
    /// <param name="widget">The widget.</param>
    /// <returns><see langword="true"/> for a text field.</returns>
    private static bool IsTextField(PdfDictionary widget) =>
        Inherited(widget, KnownName.FT) is { Kind: PdfKind.Name } type && type.AsName().ToKnownName() == KnownName.Tx;

    /// <summary>Finds a field entry on the widget or the nearest parent that has it.</summary>
    /// <param name="widget">The widget.</param>
    /// <param name="key">The entry's key.</param>
    /// <returns>The value, or <see langword="null"/> when no one in the chain has it.</returns>
    private static PdfValue? Inherited(PdfDictionary widget, PdfName key)
    {
        var current = widget;
        for (var depth = 0; current is not null && depth < MaxDepth; depth++)
        {
            if (!current.Get(key).IsNull)
            {
                return current.Get(key);
            }

            current = current.GetDictionary(KnownName.Parent);
        }

        return null;
    }
}
