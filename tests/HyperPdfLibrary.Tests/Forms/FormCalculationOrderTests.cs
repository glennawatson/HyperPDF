// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests that <see cref="PdfFormOrder.GetCalculationOrder(PdfForm, List{string})"/> lists each name once, in first-seen order, for short and long orders.</summary>
public sealed class FormCalculationOrderTests
{
    /// <summary>The object number of the first field.</summary>
    private const int FirstField = 4;

    /// <summary>The distinct names the long form has.</summary>
    private const int DistinctNames = 40;

    /// <summary>The fields of the long form, each name used by two of them.</summary>
    private const int LongFields = 80;

    /// <summary>The fields of the short form.</summary>
    private const int ShortFields = 6;

    /// <summary>The distinct names the short form has.</summary>
    private const int ShortNames = 3;

    /// <summary>Verifies a long order, whose repeats are found with a table, lists each name once in first-seen order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongOrderListsEachNameOnce()
    {
        var names = Read(LongFields, DistinctNames);

        await Assert.That(names.Count).IsEqualTo(DistinctNames);
        await Assert.That(names.ToArray()).IsEquivalentTo(Expected(DistinctNames));
    }

    /// <summary>Verifies a short order, whose repeats are found by scanning, lists each name once in first-seen order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortOrderListsEachNameOnce()
    {
        var names = Read(ShortFields, ShortNames);

        await Assert.That(names.ToArray()).IsEquivalentTo(Expected(ShortNames));
    }

    /// <summary>Verifies names already in the output are kept and do not hide the same names from the order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamesAlreadyInTheOutputAreKept()
    {
        using var document = PdfDocumentReader.Open(Build(LongFields, DistinctNames), null);
        List<string> names = ["Field0"];

        PdfFormOrder.GetCalculationOrder(PdfDocumentForms.GetForm(document), names);

        await Assert.That(names.Count).IsEqualTo(DistinctNames + 1);
    }

    /// <summary>Gets the names the order lists.</summary>
    /// <param name="fields">The fields.</param>
    /// <param name="distinct">The distinct names.</param>
    /// <returns>The names.</returns>
    private static List<string> Read(int fields, int distinct)
    {
        using var document = PdfDocumentReader.Open(Build(fields, distinct), null);
        List<string> names = [];
        PdfFormOrder.GetCalculationOrder(PdfDocumentForms.GetForm(document), names);
        return names;
    }

    /// <summary>Gets the names a form with that many distinct names has, in order.</summary>
    /// <param name="distinct">The distinct names.</param>
    /// <returns>The names.</returns>
    private static string[] Expected(int distinct) =>
        [.. Enumerable.Range(0, distinct).Select(static i => string.Create(CultureInfo.InvariantCulture, $"Field{i}"))];

    /// <summary>Builds a form whose fields share names in turn: field <c>i</c> is named for <c>i</c> modulo the distinct names.</summary>
    /// <param name="fields">The fields.</param>
    /// <param name="distinct">The distinct names.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(int fields, int distinct)
    {
        var references = new StringBuilder();
        for (var i = 0; i < fields; i++)
        {
            _ = references.Append(CultureInfo.InvariantCulture, $"{FirstField + i} 0 R ");
        }

        List<string> objects =
        [
            $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{references}] /CO [{references}] /DA (/Helv 0 Tf 0 g) >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [{references}] >>",
        ];
        for (var i = 0; i < fields; i++)
        {
            objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field{i % distinct}) /Rect [0 0 10 10] /P 3 0 R >>"));
        }

        return MiniPdf.Build([.. objects]);
    }
}
