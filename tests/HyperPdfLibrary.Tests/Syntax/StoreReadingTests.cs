// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests bounded resolution of raw indirect object cycles.</summary>
public sealed class StoreReadingTests
{
    /// <summary>The first object in the reference cycle.</summary>
    private const int FirstReference = 3;

    /// <summary>The other object in the reference cycle.</summary>
    private const int SecondReference = 4;

    /// <summary>The cross-reference size of the cycle file.</summary>
    private const int CycleSize = 5;

    /// <summary>The data length recovered from either circular stream.</summary>
    private const int StreamLength = 3;

    /// <summary>The plain catalog, independent of the hostile objects.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The empty page tree.</summary>
    private const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>The trailer's catalog reference.</summary>
    private const string Root = "/Root 1 0 R";

    /// <summary>A circular reference chain resolves to null and reports its limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Resolve_CircularReferences_ReturnsNull()
    {
        var diagnostics = new List<PdfDiagnostic>();
        using var store = StoreOpening.OpenWith(
            new RawPdf()
                .Object(1, Catalog)
                .Object(FirstReference - 1, Pages)
                .Object(FirstReference, "4 0 R")
                .Object(SecondReference, "3 0 R")
                .Table(CycleSize, Root).ToArray(),
            new() { Diagnostics = diagnostics.Add });

        var value = StoreReading.Resolve(store, PdfValue.FromReference(new(FirstReference, 0)));

        await Assert.That(value.IsNull).IsTrue();
        await Assert.That(diagnostics.Exists(static item => item.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>Circular stream Length references stop re-entrant parsing and leave the store usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetObject_CircularStreamLengths_RestoresParseDepth()
    {
        var diagnostics = new List<PdfDiagnostic>();
        using var store = StoreOpening.OpenWith(
            new RawPdf()
                .Object(1, Catalog)
                .Object(FirstReference - 1, Pages)
                .Object(FirstReference, "<< /Length 4 0 R >>\nstream\nabc\nendstream")
                .Object(SecondReference, "<< /Length 3 0 R >>\nstream\ndef\nendstream")
                .Table(CycleSize, Root).ToArray(),
            new() { Diagnostics = diagnostics.Add });

        var stream = StoreReading.GetObject(store, new(FirstReference, 0)).AsStream();

        await Assert.That(stream!.RawLength).IsEqualTo(StreamLength);
        await Assert.That(store.ParseDepthState).IsEqualTo(0);
        await Assert.That(diagnostics.Exists(static item => item.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>Object-stream headers cannot resolve themselves through a compressed object in the same container.</summary>
    /// <param name="key">The header key referring to the compressed object.</param>
    /// <param name="testToken">Cancels object resolution.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("N")]
    [Arguments("First")]
    [Arguments("Filter")]
    [Arguments("DecodeParms")]
    public async Task GetObject_CircularObjectStreamHeader_ThrowsDomainError(string key, CancellationToken testToken)
    {
        var raw = new RawPdf()
            .Object(1, Catalog)
            .Object(FirstReference - 1, Pages)
            .Stream(FirstReference, $"/Type /ObjStm /N 1 /First 4 /{key} 4 0 R", "4 0 1"u8);
        var xrefOffset = raw.Position;
        using var store = StoreOpening.OpenWith(
            raw.XrefStream(
                CycleSize,
                CycleSize + 1,
                "[0 6]",
                Root,
                [
                    new((byte)XrefEntryType.Free, 0, 0),
                    new((byte)XrefEntryType.InFile, raw.OffsetOf(1), 0),
                    new((byte)XrefEntryType.InFile, raw.OffsetOf(FirstReference - 1), 0),
                    new((byte)XrefEntryType.InFile, raw.OffsetOf(FirstReference), 0),
                    new((byte)XrefEntryType.Compressed, FirstReference, 0),
                    new((byte)XrefEntryType.InFile, xrefOffset, 0),
                ]).ToArray(),
            new() { CancellationToken = testToken });

        await Assert.That(() => StoreReading.GetObject(store, new(SecondReference, 0))).Throws<PdfException>();
        await Assert.That(store.ParseDepthState).IsEqualTo(0);
    }
}
