// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Interchange;

namespace HyperPdfLibrary.Tests.Interchange;

/// <summary>Reads hand-written XFDF files, including hostile and damaged ones.</summary>
public sealed class XfdfReadTests
{
    /// <summary>A small limit that a generated file is longer than.</summary>
    private const long SmallLimit = 4096;

    /// <summary>The allocation, in bytes, a rejected billion-laughs document must stay far below.</summary>
    private const long AllocationCeiling = 1 << 22;

    /// <summary>The number of copies of a repeated element in the oversized file.</summary>
    private const int Repeats = 2000;

    /// <summary>The depth of the nesting bomb.</summary>
    private const int BombDepth = 200;

    /// <summary>The number of annotations in the sample.</summary>
    private const int SampleAnnotations = 2;

    /// <summary>The number of annotations in the shapes sample: the unknown element is left out.</summary>
    private const int ShapeAnnotations = 6;

    /// <summary>The year of the sample dates.</summary>
    private const int SampleYear = 2026;

    /// <summary>The numbers in the sample highlight's quadrilateral.</summary>
    private const int QuadNumbers = 8;

    /// <summary>The strokes of the sample ink annotation.</summary>
    private const int Strokes = 2;

    /// <summary>The numbers in the first stroke.</summary>
    private const int FirstStrokeNumbers = 6;

    /// <summary>The numbers in the dash pattern.</summary>
    private const int DashNumbers = 2;

    /// <summary>The numbers in the polygon's vertices.</summary>
    private const int VertexNumbers = 6;

    /// <summary>The cloud intensity of the sample circle.</summary>
    private const float SampleIntensity = 2;

    /// <summary>The opacity of the sample highlight.</summary>
    private const float HalfOpacity = 0.5F;

    /// <summary>Gets a file with fields, a highlight and a text note with a reply.</summary>
    private static ReadOnlySpan<byte> Sample => """
        <?xml version="1.0" encoding="UTF-8"?>
        <xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve">
          <f href="form.pdf"/>
          <ids original="0123456789ABCDEF0123456789ABCDEF" modified="FEDCBA9876543210FEDCBA9876543210"/>
          <fields>
            <field name="Address">
              <field name="Street"><value>Main St</value></field>
              <field name="City"><value>Perth</value></field>
            </field>
            <field name="Notes">
              <value>one</value>
              <value-richtext><body xmlns="http://www.w3.org/1999/xhtml"><p>one <b>bold</b></p></body></value-richtext>
            </field>
          </fields>
          <annots>
            <highlight page="0" rect="72,690,300,700" color="#FFFF00" flags="print" name="h1" title="Ann" subject="Look"
                       date="D:20260102030405+10'00'" creationdate="D:20260101000000Z" opacity="0.5"
                       coords="72,700,300,700,72,690,300,690">
              <contents>Check this</contents>
              <popup page="0" rect="310,600,400,700" open="yes"/>
            </highlight>
            <text page="0" rect="100,100,120,120" icon="Comment" inreplyto="h1" replyType="R" state="Accepted" statemodel="Review" name="t1">
              <contents-richtext><body xmlns="http://www.w3.org/1999/xhtml"><p>Agreed</p></body></contents-richtext>
            </text>
          </annots>
        </xfdf>
        """u8;

    /// <summary>Gets a file with ink, a polygon, a line, an attachment, free text, a circle and an element the library does not know.</summary>
    private static ReadOnlySpan<byte> Shapes => """
        <xfdf xmlns="http://ns.adobe.com/xfdf/"><annots>
          <ink page="1" rect="0,0,50,50" width="2" style="dash" dashes="3,2">
            <inklist><gesture>1,2;3,4;5,6</gesture><gesture>7,8;9,10</gesture></inklist>
          </ink>
          <polygon page="1" rect="0,0,50,50" interior-color="#00FF00"><vertices>0,0;10,0;10,10</vertices></polygon>
          <line page="1" rect="0,0,50,50" start="1,2" end="30,40" head="OpenArrow" tail="None"/>
          <fileattachment page="1" rect="0,0,20,20" icon="PushPin" file="a.txt" description="note">
            <data mode="raw" encoding="hex" length="3">48 69 21</data>
          </fileattachment>
          <freetext page="1" rect="0,0,100,40" justification="1">
            <defaultappearance>/Helv 12 Tf 0 g</defaultappearance><defaultstyle>font: 12pt Helvetica</defaultstyle><contents>Hi</contents>
          </freetext>
          <circle page="1" rect="0,0,10,10" intensity="2" style="solid"/>
          <unknownthing page="1"/>
        </annots></xfdf>
        """u8;

    /// <summary>Gets a billion-laughs document.</summary>
    private static ReadOnlySpan<byte> Laughs => """
        <?xml version="1.0"?>
        <!DOCTYPE xfdf [
          <!ENTITY a "ha">
          <!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">
          <!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">
          <!ENTITY d "&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;">
          <!ENTITY e "&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;">
        ]>
        <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields><field name="x"><value>&e;</value></field></fields></xfdf>
        """u8;

    /// <summary>The file, fields, annotations and attributes are all read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsFieldsAnnotationsAndAttributes()
    {
        var data = XfdfReader.Read(Sample.ToArray());

        await Assert.That(data.FileHref).IsEqualTo("form.pdf");
        await Assert.That(data.OriginalId).IsEqualTo("0123456789ABCDEF0123456789ABCDEF");
        await Assert.That(data.ModifiedId).IsEqualTo("FEDCBA9876543210FEDCBA9876543210");
        await Assert.That(data.Fields.Select(static field => field.Name)).IsEquivalentTo(["Address.Street", "Address.City", "Notes"]);
        await Assert.That(data.Fields[0].Value).IsEqualTo("Main St");
        await Assert.That(data.Fields[2].RichText).Contains("<b>bold</b>");
        await Assert.That(data.Annotations.Count).IsEqualTo(SampleAnnotations);

        var highlight = data.Annotations[0];
        await Assert.That(highlight.Subtype).IsEqualTo("Highlight");
        await Assert.That(InterchangeValues.FormatRect(highlight.Rect!.Value)).IsEqualTo("72,690,300,700");
        await Assert.That(highlight.Color).IsEqualTo(0xFFFF00U);
        await Assert.That(highlight.Flags).IsEqualTo(PdfAnnotationFlags.Print);
        await Assert.That(highlight.Name).IsEqualTo("h1");
        await Assert.That(highlight.Title).IsEqualTo("Ann");
        await Assert.That(highlight.Subject).IsEqualTo("Look");
        await Assert.That(highlight.Contents).IsEqualTo("Check this");
        await Assert.That(highlight.Opacity).IsEqualTo(HalfOpacity);
        await Assert.That(highlight.Date!.Value.Year).IsEqualTo(SampleYear);
        await Assert.That(highlight.Coords.Length).IsEqualTo(QuadNumbers);
        await Assert.That(highlight.Popup!.IsOpen).IsTrue();

        var note = data.Annotations[1];
        await Assert.That(note.Subtype).IsEqualTo("Text");
        await Assert.That(note.InReplyTo).IsEqualTo("h1");
        await Assert.That(note.ReplyType).IsEqualTo("R");
        await Assert.That(note.State).IsEqualTo("Accepted");
        await Assert.That(note.StateModel).IsEqualTo("Review");
        await Assert.That(note.RichContents).Contains("<p>Agreed</p>");
    }

    /// <summary>Ink gestures, polygon vertices, lines and attached files are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsShapesAndAttachments()
    {
        var data = XfdfReader.Read(Shapes.ToArray());

        await Assert.That(data.Annotations.Count).IsEqualTo(ShapeAnnotations);
        await Assert.That(data.Annotations[0].Gestures.Length).IsEqualTo(Strokes);
        await Assert.That(data.Annotations[0].Gestures.Span[0].Length).IsEqualTo(FirstStrokeNumbers);
        await Assert.That(data.Annotations[0].Style).IsEqualTo("D");
        await Assert.That(data.Annotations[0].Dashes.Length).IsEqualTo(DashNumbers);
        await Assert.That(data.Annotations[1].Vertices.Length).IsEqualTo(VertexNumbers);
        await Assert.That(data.Annotations[1].InteriorColor).IsEqualTo(0x00FF00U);
        await Assert.That(InterchangeValues.FormatList(data.Annotations[2].Line.Span, ',')).IsEqualTo("1,2,30,40");
        await Assert.That(data.Annotations[2].Head).IsEqualTo("OpenArrow");
        await Assert.That(Encoding.ASCII.GetString(data.Annotations[3].Attachment!.Data)).IsEqualTo("Hi!");
        await Assert.That(data.Annotations[3].Attachment!.FileName).IsEqualTo("a.txt");
        await Assert.That(data.Annotations[4].DefaultAppearance).IsEqualTo("/Helv 12 Tf 0 g");
        await Assert.That(data.Annotations[4].Justification).IsEqualTo(1);
        await Assert.That(data.Annotations[5].Intensity).IsEqualTo(SampleIntensity);
    }

    /// <summary>A document type declaration is refused, whether or not it holds entities.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesDocumentTypeDeclaration()
    {
        var withEntity = """
            <?xml version="1.0"?>
            <!DOCTYPE xfdf [<!ENTITY secret SYSTEM "file:///etc/hostname">]>
            <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields><field name="a"><value>&secret;</value></field></fields></xfdf>
            """u8;
        var plain = """<!DOCTYPE xfdf><xfdf xmlns="http://ns.adobe.com/xfdf/"/>"""u8;
        var entityBytes = withEntity.ToArray();
        var plainBytes = plain.ToArray();

        await Assert.That(() => XfdfReader.Read(entityBytes)).Throws<PdfException>();
        await Assert.That(() => XfdfReader.Read(plainBytes)).Throws<PdfException>();
    }

    /// <summary>A billion-laughs document fails at its declaration without expanding anything.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BillionLaughsDoesNotExpand()
    {
        var bytes = Laughs.ToArray();

        await Assert.That(() => XfdfReader.Read(bytes)).Throws<PdfException>();
        await Assert.That(AllocationProbe.Minimum(() => IsRejected(bytes))).IsLessThan(AllocationCeiling);
    }

    /// <summary>Unclosed elements, broken markup and the wrong root fail with a library exception.</summary>
    /// <param name="xml">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("""<xfdf xmlns="http://ns.adobe.com/xfdf/"><fields><field name="a"><value>x</value></field>""")]
    [Arguments("""<xfdf xmlns="http://ns.adobe.com/xfdf/"><annots><highlight page="0"></annots></xfdf>""")]
    [Arguments("""<xfdf><fields><field name="a"><value>x</field></fields></xfdf>""")]
    [Arguments("<notxfdf/>")]
    [Arguments("")]
    [Arguments("not xml at all")]
    [Arguments("""<xfdf xmlns="http://ns.adobe.com/xfdf/"><fields><field name="a"><value>x<b/></value></field></fields></xfdf>""")]
    public async Task MalformedDocumentsFailSafely(string xml) =>
        await Assert.That(() => XfdfReader.Read(Encoding.UTF8.GetBytes(xml))).Throws<PdfException>();

    /// <summary>A file longer than the limit is refused, from bytes or from a stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedFileIsRefused()
    {
        var builder = new StringBuilder("""<xfdf xmlns="http://ns.adobe.com/xfdf/"><fields>""");
        for (var i = 0; i < Repeats; i++)
        {
            _ = builder.Append("""<field name="field"><value>some value that takes up room</value></field>""");
        }

        _ = builder.Append("</fields></xfdf>");
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var options = new PdfInterchangeOptions(SmallLimit);
        await using var stream = new MemoryStream(bytes);

        await Assert.That(() => XfdfReader.Read(bytes, options)).Throws<PdfException>();
        await Assert.That(() => XfdfReader.Read(stream, options)).Throws<PdfException>();
        await Assert.That(XfdfReader.Read(bytes).Fields.Count).IsEqualTo(Repeats);
    }

    /// <summary>Fields nested deeper than the limit are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeepNestingIsRefused()
    {
        var builder = new StringBuilder("""<xfdf xmlns="http://ns.adobe.com/xfdf/"><fields>""");
        for (var i = 0; i < BombDepth; i++)
        {
            _ = builder.Append("""<field name="n">""");
        }

        for (var i = 0; i < BombDepth; i++)
        {
            _ = builder.Append("</field>");
        }

        _ = builder.Append("</fields></xfdf>");
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());

        await Assert.That(() => XfdfReader.Read(bytes)).Throws<PdfException>();
    }

    /// <summary>Non-ASCII text, other encodings and a byte order mark are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsUnicodeInOtherEncodings()
    {
        const string text = """<xfdf xmlns="http://ns.adobe.com/xfdf/"><fields><field name="Name"><value>caf&#xE9; 中 &#x1F600;</value></field></fields></xfdf>""";
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
        var utf8 = Encoding.UTF8.GetPreamble().Concat(Utf8(text)).ToArray();

        await Assert.That(XfdfReader.Read(utf16).Fields[0].Value).IsEqualTo("café 中 \U0001F600");
        await Assert.That(XfdfReader.Read(utf8).Fields[0].Value).IsEqualTo("café 中 \U0001F600");
    }

    /// <summary>Encodes text as UTF-8.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Reads a document that the reader must reject.</summary>
    /// <param name="bytes">The document.</param>
    /// <returns>True when the reader threw a library exception.</returns>
    private static bool IsRejected(byte[] bytes)
    {
        try
        {
            _ = XfdfReader.Read(bytes);
            return false;
        }
        catch (PdfException)
        {
            return true;
        }
    }
}
