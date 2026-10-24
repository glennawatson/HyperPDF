// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures name classification and bounded colour-space resource walks.</summary>
public sealed class PdfNameClassificationBenchmarks
{
    /// <summary>The caller's markup-only flattening options.</summary>
    private readonly PdfFlattenOptions _options = new() { Targets = PdfFlattenTargets.MarkupOnly };

    /// <summary>The caller's annotation dictionary.</summary>
    private readonly PdfDictionary _annotation = new(null);

    /// <summary>The document owning the resource dictionary.</summary>
    private PdfDocument _document = null!;

    /// <summary>The validation state owned by this benchmark caller.</summary>
    private PdfAGraphicsWalk _walk = null!;

    /// <summary>The named colour-space resource.</summary>
    private PdfDictionary _resources = null!;

    /// <summary>The colour-space resource name.</summary>
    private PdfName _space;

    /// <summary>A normal Pattern value.</summary>
    private PdfValue _pattern;

    /// <summary>A circular singleton Pattern resource.</summary>
    private PdfValue _circular;

    /// <summary>Creates and warms caller-owned resource state.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        _walk = new(new(_document, PdfALevel.PdfA2B, CancellationToken.None));
        _resources = new(_document.Objects);
        var spaces = new PdfDictionary(_document.Objects);
        _space = _document.Objects.Names.Intern("Space"u8);
        spaces.Set(_space, PdfValue.FromArray(new(_document.Objects, [PdfValue.FromName(KnownName.CalGray), PdfValue.FromInteger(0)])));
        _resources.Set(KnownName.ColorSpace, PdfValue.FromDictionary(spaces));
        _pattern = PdfValue.FromName(KnownName.Pattern);
        var circular = new PdfArray(null, 1);
        _circular = PdfValue.FromArray(circular);
        circular.Add(_circular);
        _annotation.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Highlight));
        _ = StandardBlendMode();
        _ = LegacyBlendMode();
        _ = MarkupSelection();
        _ = PatternName();
        _ = CircularPattern();
        _ = ColorSpaceFamily();
        _ = AllowedAnnotationKind();
        _ = AttachmentMimeType();
        _ = UnknownAttachmentMimeType();
        _ = ReadableContentFilter();
        _ = CompatibleConversionBlend();
        _ = AllowedImageDepth();
    }

    /// <summary>Releases the resource owner.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Recognises a standard separable blend mode.</summary>
    /// <returns>Whether the name is standard.</returns>
    [Benchmark]
    public bool StandardBlendMode() => PdfBlendModeNames.IsStandard(KnownName.ColorBurn);

    /// <summary>Rejects the legacy Compatible name from the PDF 2.0 standard-mode set.</summary>
    /// <returns>Whether the name is standard.</returns>
    [Benchmark]
    public bool LegacyBlendMode() => PdfBlendModeNames.IsStandard(KnownName.Compatible);

    /// <summary>Selects a markup annotation from caller-owned options and dictionary state.</summary>
    /// <returns>The selection action.</returns>
    [Benchmark]
    public int MarkupSelection() => (int)PdfFlattenSelection.Decide(_options, _annotation).Action;

    /// <summary>Accepts an immediately available Pattern name.</summary>
    /// <returns>Whether it is a pattern.</returns>
    [Benchmark]
    public bool PatternName() => PdfAColorSpaces.IsPattern(_pattern, CancellationToken.None);

    /// <summary>Rejects a circular singleton array after bounded traversal.</summary>
    /// <returns>Whether it is a pattern.</returns>
    [Benchmark]
    public bool CircularPattern() => PdfAColorSpaces.IsPattern(_circular, CancellationToken.None);

    /// <summary>Classifies a standard colour-space array through its resource dictionary.</summary>
    /// <returns>The colour-space kind.</returns>
    [Benchmark]
    public int ColorSpaceFamily() => (int)PdfAColorSpaces.FromResource(_walk, _resources, _space, false);

    /// <summary>Classifies a later markup annotation for PDF/A-2.</summary>
    /// <returns>Whether the type is allowed.</returns>
    [Benchmark]
    public bool AllowedAnnotationKind() => PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA2B, "Redact");

    /// <summary>Matches an attachment extension without creating a suffix string.</summary>
    /// <returns>The guessed media type.</returns>
    [Benchmark]
    public string AttachmentMimeType() => PdfAConversionMimeTypes.Guess("report.PdF");

    /// <summary>Rejects an unknown extension without retaining a lookup table.</summary>
    /// <returns>The fallback media type.</returns>
    [Benchmark]
    public string UnknownAttachmentMimeType() => PdfAConversionMimeTypes.Guess("report.unknown");

    /// <summary>Recognises a filter from its parsed name.</summary>
    /// <returns>Whether the content checker reads it.</returns>
    [Benchmark]
    public bool ReadableContentFilter() => PdfAContentScan.IsReadableFilter(KnownName.FlateDecode);

    /// <summary>Preserves Compatible when converting an existing blend-mode entry.</summary>
    /// <returns>Whether conversion recognises the mode.</returns>
    [Benchmark]
    public bool CompatibleConversionBlend() => PdfAConversionResources.IsKnownBlend(PdfValue.FromName(KnownName.Compatible), CancellationToken.None);

    /// <summary>Checks the extra image depth allowed by later PDF/A parts.</summary>
    /// <returns>Whether the depth is permitted.</returns>
    [Benchmark]
    public bool AllowedImageDepth()
    {
        const int WideBits = 16;
        return PdfAConversionResources.IsAllowedBitDepth(WideBits, true);
    }
}
