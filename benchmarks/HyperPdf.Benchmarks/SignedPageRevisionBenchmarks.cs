// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdf.Benchmarks;

/// <summary>Measures comparison of a signed page with later raw incremental revisions.</summary>
public class SignedPageRevisionBenchmarks
{
    /// <summary>The current revision.</summary>
    private PdfDocument _document = null!;

    /// <summary>The objects of the signed revision.</summary>
    private PdfObjectStore _signed = null!;

    /// <summary>The position after the signed bytes.</summary>
    private long _signedEnd;

    /// <summary>Gets or sets whether the later revision shadows the signed page.</summary>
    [Params(false, true)]
    public bool ShadowPage { get; set; }

    /// <summary>Prepares the same signed bytes for unchanged and shadowed comparisons.</summary>
    [GlobalSetup]
    public void Setup()
    {
        using var certificate = PdfSigning.CreateCertificate("Revision benchmark", null, false, TimeProvider.System);
        var signed = SignedPageRevisions.Signed(certificate, string.Empty);
        var file = ShadowPage ? SignedPageRevisions.Shadow(signed, SignedPageRevisions.Annotation) : signed;
        _signedEnd = signed.Length;
        _document = PdfDocumentReader.Open(file, null);
        _signed = StoreRevisions.OpenRevision(_document.Objects, _signedEnd);
        _ = PdfDocumentSignatureValidation.GetSignatureDetails(_document);
    }

    /// <summary>Compares current object definitions with the signed revision.</summary>
    /// <returns>The classified changes.</returns>
    [Benchmark]
    public PdfObjectChange[] CompareSignedRevision() => PdfRevisionComparer.Compare(_document.Objects, _signed, _signedEnd);

    /// <summary>Reads fresh cross-reference section offsets without using the revision snapshot cache.</summary>
    /// <returns>The number of section offsets written to caller-owned working storage.</returns>
    [Benchmark]
    public int ReadRevisionOffsets()
    {
        Span<long> sections = stackalloc long[PdfLimits.MaxXrefSections];
        return StoreRevisions.ReadSectionOffsets(_document.Objects, sections);
    }

    /// <summary>Reads a detached revision snapshot without using the document cache.</summary>
    /// <returns>The ordered revision boundaries.</returns>
    [Benchmark]
    public PdfRevision[] ReadRevisionSnapshots() => StoreRevisions.ReadRevisions(_document.Objects);

    /// <summary>Reads cached current and historical signature details.</summary>
    /// <returns>The retained signatures and their signed authority.</returns>
    [Benchmark]
    public IReadOnlyList<PdfSignatureDetails> ReadCachedSignatureDetails() => PdfDocumentSignatureValidation.GetSignatureDetails(_document);

    /// <summary>Discovers current and historical signatures after invalidating only the signature-detail cache.</summary>
    /// <returns>The newly retained signatures and their signed authority.</returns>
    [Benchmark]
    public IReadOnlyList<PdfSignatureDetails> ReadUncachedSignatureDetails()
    {
        Volatile.Write(ref _document.State.SignatureDetails, null);
        return PdfDocumentSignatureValidation.GetSignatureDetails(_document);
    }

    /// <summary>Disposes both revision stores.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _signed.Dispose();
        _document.Dispose();
    }
}
