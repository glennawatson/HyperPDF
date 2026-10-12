// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks that <see cref="PdfAConverter"/> fixes what PDF/A forbids and refuses, with reasons, what it cannot fix.</summary>
public sealed class PdfAConverterTests
{
    /// <summary>The offset of the device class in an ICC header.</summary>
    private const int ClassOffset = 12;

    /// <summary>The offset of the colour space in an ICC header.</summary>
    private const int SpaceOffset = 16;

    /// <summary>The offset of the major version in an ICC header.</summary>
    private const int VersionOffset = 8;

    /// <summary>The ICC major version PDF/A-1 needs.</summary>
    private const int IccVersion2 = 2;

    /// <summary>The length of an ICC signature.</summary>
    private const int SignatureLength = 4;

    /// <summary>The components of the sRGB profile.</summary>
    private const int RgbComponents = 3;

    /// <summary>Gets the levels the conversion targets in these tests.</summary>
    /// <returns>The levels.</returns>
    public static IEnumerable<PdfALevel> Levels() => [PdfALevel.PdfA1B, PdfALevel.PdfA2B, PdfALevel.PdfA2U, PdfALevel.PdfA3B, PdfALevel.PdfA4];

    /// <summary>Non-embedded Helvetica is replaced by an embedded subset of the matching open font with a ToUnicode map.</summary>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Levels))]
    public async Task NonEmbeddedFontIsEmbedded(PdfALevel level, CancellationToken cancellationToken)
    {
        var (result, output) = await ConvertAsync(ConversionTestPdf.HelveticaText(), level, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var converted = PdfDocumentReader.Open(output, null);
        var font = PdfDocumentPages.GetPage(converted, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(converted.Objects.Names.Intern("F1"u8))!;
        await Assert.That(font.GetDictionary(KnownName.FontDescriptor)!.GetStream(KnownName.FontFile2)).IsNotNull();
        await Assert.That(font.GetStream(KnownName.ToUnicode)).IsNotNull();
        await Assert.That(PdfAValidator.Validate(converted)!.Passed).IsTrue();
        await Assert.That(Array.Exists(result.Changes, static change => change.Description.Contains("Helvetica", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A font that only draws invisible text, as an OCR layer does, is exempt and left as it is.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvisibleTextFontIsLeftAlone(CancellationToken cancellationToken)
    {
        var (result, output) = await ConvertAsync(ConversionTestPdf.InvisibleText(), PdfALevel.PdfA2B, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var converted = PdfDocumentReader.Open(output, null);
        var font = PdfDocumentPages.GetPage(converted, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(converted.Objects.Names.Intern("F1"u8))!;
        await Assert.That(font.ContainsKey(KnownName.FontDescriptor)).IsFalse();
    }

    /// <summary>JavaScript and launch actions are removed; the link stays.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForbiddenActionsAreRemoved(CancellationToken cancellationToken)
    {
        var (result, output) = await ConvertAsync(ConversionTestPdf.Scripts(), PdfALevel.PdfA2B, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var converted = PdfDocumentReader.Open(output, null);
        await Assert.That(converted.Catalog.ContainsKey(KnownName.OpenAction)).IsFalse();
        var link = PdfDocumentPages.GetPage(converted, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;
        await Assert.That(link.IsName(KnownName.Subtype, KnownName.Link)).IsTrue();
        await Assert.That(link.ContainsKey(KnownName.A)).IsFalse();
        await Assert.That(Array.Exists(result.Changes, static change => change.Clause.Length > 0 && change.Description.Contains("actions", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A file without an output intent gets an sRGB one with a version 2 display profile.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingOutputIntentIsAdded(CancellationToken cancellationToken)
    {
        var (result, output) = await ConvertAsync(ConversionTestPdf.Scripts(), PdfALevel.PdfA1B, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var converted = PdfDocumentReader.Open(output, null);
        var intent = converted.Catalog.GetArray(converted.Objects.Names.Intern("OutputIntents"u8))!.GetDictionary(0)!;
        var profile = intent.GetStream(converted.Objects.Names.Intern("DestOutputProfile"u8))!;
        var bytes = profile.DecodeToArray(cancellationToken);
        await Assert.That(converted.Objects.Names.GetString(intent.GetName(KnownName.S))).IsEqualTo("GTS_PDFA1");
        await Assert.That(profile.Dictionary.GetInt32(KnownName.N)).IsEqualTo(RgbComponents);
        await Assert.That(Encoding.ASCII.GetString(bytes, ClassOffset, SignatureLength)).IsEqualTo("mntr");
        await Assert.That(Encoding.ASCII.GetString(bytes, SpaceOffset, SignatureLength)).IsEqualTo("RGB ");
        await Assert.That((int)bytes[VersionOffset]).IsEqualTo(IccVersion2);
    }

    /// <summary>PDF/A-4 keeps descriptive metadata in XMP only, so /Info goes and its title moves to XMP.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf4MovesInfoToXmp(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(ConversionTestPdf.Scripts(), null);
        PdfDocumentMetadataEditing.SetMetadata(source, new() { Title = "Report" });
        await using var stream = new MemoryStream();

        var result = await PdfAConverter.SaveAsPdfAAsync(source, PdfALevel.PdfA4, stream, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var converted = PdfDocumentReader.Open(stream.ToArray(), null);
        await Assert.That(converted.Objects.Trailer.ContainsKey(KnownName.Info)).IsFalse();
        await Assert.That(PdfDocumentMetadata.GetXmp(converted)!.Title).IsEqualTo("Report");
        await Assert.That(converted.Objects.Version).IsEqualTo("2.0");
    }

    /// <summary>Level A needs tags, which an untagged file lacks, so it is refused and nothing is written.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedFileIsRefusedAtLevelA(CancellationToken cancellationToken)
    {
        var (result, output) = await ConvertAsync(ConversionTestPdf.Scripts(), PdfALevel.PdfA2A, cancellationToken);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(output).IsEmpty();
        await Assert.That(Array.Exists(result.Problems, static problem => problem.Description.Contains("tagged", StringComparison.Ordinal) && problem.Clause.Length > 0)).IsTrue();
    }

    /// <summary>Device CMYK without a CMYK profile cannot be described by an sRGB intent, so it is refused.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykWithoutProfileIsRefused(CancellationToken cancellationToken)
    {
        var (result, output) = await ConvertAsync(ConversionTestPdf.Cmyk(), PdfALevel.PdfA2B, cancellationToken);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(output).IsEmpty();
        await Assert.That(Array.Exists(result.Problems, static problem => problem.Description.Contains("CMYK", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A signed file is refused, as converting it would break the signature.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedFileIsRefused(CancellationToken cancellationToken)
    {
        var (result, output) = await ConvertAsync(ConversionTestPdf.Signed(), PdfALevel.PdfA2B, cancellationToken);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(output).IsEmpty();
        await Assert.That(result.Problems[0].Description).Contains("signed");
    }

    /// <summary>The document converted stays as it was: the conversion works on a copy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SourceDocumentDoesNotChange()
    {
        using var source = PdfDocumentReader.Open(ConversionTestPdf.Scripts(), null);
        await using var stream = new MemoryStream();

        var result = PdfAConverter.SaveAsPdfA(source, PdfALevel.PdfA2B, stream);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        await Assert.That(stream.Length).IsGreaterThan(0);
        await Assert.That(source.Catalog.ContainsKey(KnownName.OpenAction)).IsTrue();
        await Assert.That(source.Catalog.ContainsKey(KnownName.Metadata)).IsFalse();
        await Assert.That(StoreEditing.HasEdits(source.Objects)).IsFalse();
    }

    /// <summary>A cancelled token stops the conversion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationStopsTheConversion()
    {
        using var source = PdfDocumentReader.Open(ConversionTestPdf.Scripts(), null);
        await using var stream = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(() => PdfAConverter.SaveAsPdfA(source, PdfALevel.PdfA2B, stream, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Converts a file and returns the outcome and the written bytes.</summary>
    /// <param name="file">The source file.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome and the bytes written, empty when refused.</returns>
    private static async Task<ConversionOutcome> ConvertAsync(byte[] file, PdfALevel level, CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(file, null);
        await using var stream = new MemoryStream();
        var result = await PdfAConverter.SaveAsPdfAAsync(source, level, stream, cancellationToken);
        return new(result, stream.ToArray());
    }

    /// <summary>Lists a refused conversion's problems, for assertion messages.</summary>
    /// <param name="result">The outcome.</param>
    /// <returns>The problems.</returns>
    private static string Describe(PdfAConversionResult result) => string.Join("; ", result.Problems.Select(static problem => $"{problem.Clause} {problem.Description}"));

    /// <summary>A conversion's outcome and output.</summary>
    /// <param name="Result">The outcome.</param>
    /// <param name="Output">The bytes written.</param>
    private sealed record ConversionOutcome(PdfAConversionResult Result, byte[] Output);
}
