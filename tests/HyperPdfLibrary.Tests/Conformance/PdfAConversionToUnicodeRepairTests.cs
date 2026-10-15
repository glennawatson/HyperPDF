// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks the repair of ToUnicode maps whose ranges cross a 256-code block.</summary>
public sealed class PdfAConversionToUnicodeRepairTests
{
    /// <summary>A map with one range that crosses the block between codes 0x00FF and 0x0100.</summary>
    private static readonly byte[] CrossingMap =
        """
        /CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /Test def /CMapType 2 def
        1 begincodespacerange <0000> <FFFF> endcodespacerange 1 beginbfrange <00FE> <0101> <0041> endbfrange
        endcmap CMapName currentdict /CMap defineresource pop end end
        """u8.ToArray();

    /// <summary>A map held directly in the font dictionary is replaced by a stream object the dictionary refers to.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DirectMapBecomesAnIndirectObject(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(ConversionTestPdf.HelveticaText(), null);
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, 0);
        var fonts = page.Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!;
        var fontId = fonts.GetValueAt(0).AsReference();
        var copy = StoreReading.Resolve(store, fonts.GetValueAt(0)).AsDictionary()!.Clone();
        copy.Set(KnownName.ToUnicode, PdfValue.FromStream(new(new PdfDictionary(store, 0), CrossingMap)));
        StoreEditing.Replace(store, fontId, PdfValue.FromDictionary(copy));
        var session = new PdfAConversionSession(document, PdfALevel.PdfA2B, [], cancellationToken);

        PdfAConversionToUnicodeRepair.Apply(session);

        var repaired = StoreReading.Resolve(store, PdfValue.FromReference(fontId)).AsDictionary()!;
        await Assert.That(repaired.GetRaw(KnownName.ToUnicode).IsReference).IsTrue();
        await Assert.That(repaired.GetStream(KnownName.ToUnicode)).IsNotNull();
    }
}
