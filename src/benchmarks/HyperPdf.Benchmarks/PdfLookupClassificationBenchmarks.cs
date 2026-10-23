// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Interchange;

namespace HyperPdf.Benchmarks;

/// <summary>Measures exact PDF operators, predefined collections, CSS and XFDF name dispatch.</summary>
public class PdfLookupClassificationBenchmarks
{
    /// <summary>The PDF/A part based on PDF 2.0.</summary>
    private const int Pdf20Part = 4;

    /// <summary>The mutable annotation owned by this benchmark instance.</summary>
    private readonly PdfInterchangeAnnotation _annotation = new("Text");

    /// <summary>Warms fixed dispatch and its generated static delegates.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _ = SyntaxOperands();
        _ = StandardOperator();
        _ = NonOperator();
        _ = PredefinedCollection();
        _ = UnknownCollection();
        _ = MixedCaseStyle();
        _ = UnknownStyle();
        _ = NamedColor();
        _ = XfdfElement();
        _ = XfdfSubtype();
        _ = XfdfAttribute();
        _ = UnknownXfdfAttribute();
    }

    /// <summary>Classifies a three-byte marked-content operator.</summary>
    /// <returns>The operand classification.</returns>
    [Benchmark]
    public int SyntaxOperands() => (int)PdfASyntaxOperators.LinkOf("BDC"u8);

    /// <summary>Recognises an exact standard operator.</summary>
    /// <returns>Whether the operator is standard.</returns>
    [Benchmark]
    public bool StandardOperator() => PdfASyntaxOperators.IsStandard("SCN"u8);

    /// <summary>Rejects an operator prefix containing a null byte.</summary>
    /// <returns>Whether the word is standard.</returns>
    [Benchmark]
    public bool NonOperator() => PdfASyntaxOperators.IsStandard("Tj\0"u8);

    /// <summary>Finds a Korean collection with its PDF 2.0 ordering.</summary>
    /// <returns>The supplement.</returns>
    [Benchmark]
    public int PredefinedCollection() => PdfAPredefinedCMaps.Collection("UniKS-UTF16-H", Pdf20Part)?.Supplement ?? -1;

    /// <summary>Rejects a differently cased predefined CMap name.</summary>
    /// <returns>Whether the name is predefined.</returns>
    [Benchmark]
    public bool UnknownCollection() => PdfAPredefinedCMaps.IsPredefined("UniJIS-UCS2-h");

    /// <summary>Applies mixed-case CSS declarations from character spans.</summary>
    /// <returns>The RGB colour.</returns>
    [Benchmark]
    public int MixedCaseStyle() => RichStyleReader.Apply("COLOR:ORANGE;FONT-SIZE:18pt;FONT-WEIGHT:bold", RichStyle.None).Color;

    /// <summary>Ignores unsupported CSS declarations.</summary>
    /// <returns>The unchanged colour.</returns>
    [Benchmark]
    public int UnknownStyle() => RichStyleReader.Apply("unknown:ignored;color:not-a-color", RichStyle.None).Color;

    /// <summary>Matches a mixed-case named CSS colour.</summary>
    /// <returns>The RGB colour.</returns>
    [Benchmark]
    public int NamedColor() => RichStyleReader.TryReadColor("FuChSiA", out var color) ? color : -1;

    /// <summary>Writes the element of a case-sensitive PDF subtype.</summary>
    /// <returns>The element name.</returns>
    [Benchmark]
    public string XfdfElement() => XfdfNames.TryGetElement("FileAttachment", out var element) ? element : string.Empty;

    /// <summary>Reads a mixed-case annotation element.</summary>
    /// <returns>The PDF subtype.</returns>
    [Benchmark]
    public string XfdfSubtype() => XfdfNames.TryGetSubtype("FiLeAtTaChMeNt", out var subtype) ? subtype : string.Empty;

    /// <summary>Applies a mixed-case attribute to the instance-owned annotation.</summary>
    /// <returns>Whether the attribute is supported.</returns>
    [Benchmark]
    public bool XfdfAttribute() => XfdfAttributes.Apply(_annotation, "TiTlE", "Title");

    /// <summary>Rejects an unsupported attribute without changing the annotation.</summary>
    /// <returns>Whether the attribute is supported.</returns>
    [Benchmark]
    public bool UnknownXfdfAttribute() => XfdfAttributes.Apply(_annotation, "titlex", "Title");
}
