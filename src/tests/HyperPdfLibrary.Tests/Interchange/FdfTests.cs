// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Interchange;

namespace HyperPdfLibrary.Tests.Interchange;

/// <summary>Reads, writes and imports FDF files.</summary>
public sealed class FdfTests
{
    /// <summary>A small limit that the sample is longer than.</summary>
    private const long SmallLimit = 32;

    /// <summary>The start of a hexadecimal string that holds UTF-16BE text: its byte order mark.</summary>
    private const string ByteOrderMark = "<FEFF";

    /// <summary>A string written in PDFDocEncoding: the é of "café" is the one byte 0xE9, written as an octal escape.</summary>
    private const string PdfDocCafe = @"(caf\351)";

    /// <summary>The fields the three-field file applies.</summary>
    private const int AppliedFields = 3;

    /// <summary>The field flags of the sample city field.</summary>
    private const uint CityFlags = 4096;

    /// <summary>The flags the sample city field sets.</summary>
    private const uint CitySetFlags = 1;

    /// <summary>The flags the sample city field clears.</summary>
    private const uint CityClearFlags = 2;

    /// <summary>The length of the FDF header, <c>%FDF-1.2</c>.</summary>
    private const int HeaderLength = 8;

    /// <summary>Gets an FDF file with three known fields and one unknown.</summary>
    private static ReadOnlySpan<byte> ThreeFields => """
        %FDF-1.2
        1 0 obj
        << /FDF << /F (form.pdf) /Fields [
            << /T (Notes) /V (hello) >>
            << /T (Agree) /V /Yes >>
            << /T (Colour) /V (r) >>
            << /T (NoSuchField) /V (ignored) >> ] >> >>
        endobj
        trailer
        << /Root 1 0 R >>
        %%EOF
        """u8;

    /// <summary>Gets a file with names, kids, hex strings, scripts, an identifier and a status.</summary>
    private static ReadOnlySpan<byte> Details => """
        %FDF-1.2
        1 0 obj
        << /FDF << /F (a.pdf) /Status (Done) /Encoding /Shift-JIS /ID [<0102030405060708090A0B0C0D0E0F10> <AABBCCDDEEFF00112233445566778899>]
           /Fields [ << /T (Address) /Kids [ << /T (Street) /V <FEFF00630061006600E9> >> << /T (City) /V (caf\351) /Ff 4096 /SetFf 1 /ClrFf 2 >> ] >>
                     << /T (Tags) /V [(a) (b)] >> << /T (Rich) /V (x) /RV (<body><p>x</p></body>) >> ]
           /JavaScript << /Before (before();) /After (after();) /Doc [(init) (var a = 1;)] >> >> >>
        endobj
        trailer
        << /Root 1 0 R >>
        %%EOF
        """u8;

    /// <summary>Three fields set text, a check box and a choice; an unknown name is ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesFieldsAndIgnoresUnknownNames()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);

        var result = PdfInterchange.ImportFdf(document, ThreeFields.ToArray());

        var widgets = new List<PdfFormWidget>();
        HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(document), 0, widgets);
        await Assert.That(result.FieldsApplied).IsEqualTo(AppliedFields);
        await Assert.That(result.FieldsSkipped).IsEqualTo(1);
        await Assert.That(widgets[FormSamples.NotesIndex].Value).IsEqualTo("hello");
        await Assert.That(widgets[FormSamples.AgreeIndex].IsChecked).IsTrue();
        await Assert.That(widgets[FormSamples.ColourIndex].SelectedOption).IsEqualTo(0);
        await Assert.That(widgets[FormSamples.SecretIndex].Value).IsEqualTo("hidden");
    }

    /// <summary>Names, nested kids, UTF-16 and PDFDocEncoding strings, flags, scripts, identifiers and status are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsFileDetails()
    {
        var data = FdfReader.Read(Details.ToArray());

        await Assert.That(data.FileHref).IsEqualTo("a.pdf");
        await Assert.That(data.Status).IsEqualTo("Done");
        await Assert.That(data.Encoding).IsEqualTo("Shift-JIS");
        await Assert.That(data.OriginalId).IsEqualTo("0102030405060708090A0B0C0D0E0F10");
        await Assert.That(data.ModifiedId).IsEqualTo("AABBCCDDEEFF00112233445566778899");
        await Assert.That(data.Fields.Select(static field => field.Name)).IsEquivalentTo(["Address.Street", "Address.City", "Tags", "Rich"]);
        await Assert.That(data.Fields[0].Value).IsEqualTo("café");
        await Assert.That(data.Fields[1].Value).IsEqualTo("café");
        await Assert.That(data.Fields[1].Flags).IsEqualTo(CityFlags);
        await Assert.That(data.Fields[1].SetFlags).IsEqualTo(CitySetFlags);
        await Assert.That(data.Fields[1].ClearFlags).IsEqualTo(CityClearFlags);
        await Assert.That(data.Fields[2].Values).IsEquivalentTo(["a", "b"]);
        await Assert.That(data.Fields[3].RichText).IsEqualTo("<body><p>x</p></body>");
        await Assert.That(data.JavaScript!.Before).IsEqualTo("before();");
        await Assert.That(data.JavaScript.After).IsEqualTo("after();");
        await Assert.That(data.JavaScript.Document).IsEquivalentTo(["init", "var a = 1;"]);
    }

    /// <summary>Written FDF reads back to the same data, and has the FDF header and a root trailer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrittenFileReadsBack()
    {
        var data = FdfReader.Read(Details.ToArray());

        var bytes = FdfWriter.Write(data);
        var again = FdfReader.Read(bytes);

        await Assert.That(Encoding.Latin1.GetString(bytes, 0, HeaderLength)).IsEqualTo("%FDF-1.2");
        await Assert.That(Encoding.Latin1.GetString(bytes)).Contains("trailer");
        await Assert.That(Encoding.UTF8.GetString(XfdfWriter.Write(again))).IsEqualTo(Encoding.UTF8.GetString(XfdfWriter.Write(data)));
        await Assert.That(again.JavaScript!.Document).IsEquivalentTo(["init", "var a = 1;"]);
        await Assert.That(again.Status).IsEqualTo("Done");
        await Assert.That(again.Fields[1].Flags).IsEqualTo(CityFlags);
    }

    /// <summary>A file that is not FDF, has no FDF dictionary or is too long is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadFilesAreRefused()
    {
        var noDictionary = "%FDF-1.2\n1 0 obj\n<< /Other 1 >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF"u8;
        var notFdf = "%PDF-1.7\n"u8.ToArray();
        var missing = noDictionary.ToArray();
        var three = ThreeFields.ToArray();
        var options = new PdfInterchangeOptions(SmallLimit);

        await Assert.That(() => FdfReader.Read(notFdf)).Throws<PdfException>();
        await Assert.That(() => FdfReader.Read(missing)).Throws<PdfException>();
        await Assert.That(() => FdfReader.Read(three, options)).Throws<PdfException>();
        await Assert.That(static () => FdfReader.Read([])).Throws<PdfException>();
    }

    /// <summary>
    /// Text that fits PDFDocEncoding is written as PDFDocEncoding and text that does not as UTF-16BE with its byte order mark
    /// (ISO 32000-2 §7.9.2.2), and both read back unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextEncodingsWriteAndReadBack()
    {
        const string latin = "café";
        const string wide = "Ωmega 日本";
        var data = new PdfInterchangeData();
        data.Fields.Add(new("Wide", [wide]));
        data.Fields.Add(new("Latin", [latin]));

        var bytes = FdfWriter.Write(data);
        var read = FdfReader.Read(bytes);
        var text = Encoding.Latin1.GetString(bytes);

        await Assert.That(read.Fields.Single(static field => field.Name == "Wide").Value).IsEqualTo(wide);
        await Assert.That(read.Fields.Single(static field => field.Name == "Latin").Value).IsEqualTo(latin);
        await Assert.That(CountOf(text, ByteOrderMark)).IsEqualTo(1).Because("only the text outside PDFDocEncoding needs a byte order mark");
        await Assert.That(text).Contains(PdfDocCafe);
    }

    /// <summary>A field tree nested far deeper than the limit is read within the limit, without overflowing the stack.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExcessiveNestingIsBounded()
    {
        const int levels = 2000;
        var builder = new StringBuilder("%FDF-1.2\n1 0 obj\n<< /FDF << /Fields [");
        for (var i = 0; i < levels; i++)
        {
            _ = builder.Append("<< /T (n) /Kids [");
        }

        _ = builder.Append("<< /T (leaf) /V (x) >>");
        for (var i = 0; i < levels; i++)
        {
            _ = builder.Append("] >>");
        }

        _ = builder.Append("] >> >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF");
        var bytes = Encoding.Latin1.GetBytes(builder.ToString());

        var deepest = 0;
        try
        {
            deepest = FdfReader.Read(bytes).Fields.Select(static field => field.Name.Count(static character => character == '.') + 1).DefaultIfEmpty(0).Max();
        }
        catch (PdfException)
        {
            // Refusing the file is as safe as reading it to the limit.
        }

        await Assert.That(deepest).IsLessThanOrEqualTo(PdfLimits.MaxNesting + 1);
    }

    /// <summary>A field tree that refers to itself is read once, not forever.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclicKidsAreBounded()
    {
        var cycle = "%FDF-1.2\n1 0 obj\n<< /FDF << /Fields [2 0 R] >> >>\nendobj\n2 0 obj\n<< /T (a) /V (x) /Kids [2 0 R] >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF"u8.ToArray();

        var data = FdfReader.Read(cycle);

        await Assert.That(data.Fields.Count).IsEqualTo(1);
    }

    /// <summary>Counts the non-overlapping occurrences of a text.</summary>
    /// <param name="text">The text searched.</param>
    /// <param name="value">The text sought.</param>
    /// <returns>The count.</returns>
    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
