// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures checking the PDF MAC (ISO/TS 32004) of an encrypted file: the token alone, and the whole document with one and with two revisions.</summary>
public class HyperPdfPdfMacBenchmarks
{
    /// <summary>The digest of the benchmark tokens.</summary>
    private const string Digest = "SHA-256";

    /// <summary>The file with one revision and a standalone token.</summary>
    private byte[] _oneRevision = [];

    /// <summary>The file with two revisions, each with a standalone token.</summary>
    private byte[] _twoRevisions = [];

    /// <summary>A token made by the independent test builder.</summary>
    private byte[] _token = [];

    /// <summary>The file key of the benchmark files.</summary>
    private byte[] _fileKey = [];

    /// <summary>The digest of the one-revision file.</summary>
    private byte[] _dataDigest = [];

    /// <summary>The opened file with an edit the save benchmarks write.</summary>
    private PdfObjectStore _store = null!;

    /// <summary>Makes the files and the token.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        _oneRevision = MacPdf.Create(protection, Digest, true);
        _twoRevisions = MacPdf.AddUnsignedRevision(_oneRevision, protection, "Second title", true);
        _fileKey = protection.FileKey;
        _dataDigest = MacToken.Hash(Digest, _oneRevision);
        _token = MacToken.Build(_fileKey, MacProtection.Salt, Digest, _dataDigest, null);
        _store = StoreOpening.Open(_oneRevision, AesGcmPdf.UserPassword);
        var info = _store.Trailer.GetDictionary(KnownName.Info)!.Clone();
        info.Set(_store.Names.Intern("Subject"), PdfValue.FromString("edited subject"u8.ToArray()));
        StoreEditing.Replace(_store, new(MacPdf.InfoNumber, 0), PdfValue.FromDictionary(info));
    }

    /// <summary>Releases the opened file.</summary>
    [GlobalCleanup]
    public void Cleanup() => _store.Dispose();

    /// <summary>Checks one token: derives the key-encryption key, unwraps the MAC key and checks the attributes and the MAC.</summary>
    /// <returns>Whether the token is valid.</returns>
    [Benchmark(Baseline = true)]
    public bool VerifyToken() => PdfMacTokenReader.Verify(_token, _fileKey, MacProtection.Salt).Status == PdfMacTokenStatus.Valid;

    /// <summary>Checks the only revision of a file, without the cost of opening it.</summary>
    /// <returns>Whether the file is intact.</returns>
    [Benchmark]
    public bool ValidateOneRevision()
    {
        using var document = PdfDocumentReader.Open(_oneRevision, AesGcmPdf.UserPassword);
        return PdfMacValidator.Validate(document, CancellationToken.None).IsIntact;
    }

    /// <summary>Checks both revisions of a file, each against its own token.</summary>
    /// <returns>Whether the file is intact.</returns>
    [Benchmark]
    public bool ValidateTwoRevisions()
    {
        using var document = PdfDocumentReader.Open(_twoRevisions, AesGcmPdf.UserPassword);
        return PdfMacValidator.Validate(document, CancellationToken.None).IsIntact;
    }

    /// <summary>Makes a token: derives the key-encryption key, wraps a fresh MAC key and builds and authenticates the CMS structure.</summary>
    /// <returns>The token length.</returns>
    [Benchmark]
    public int CreateToken() => PdfMacTokenWriter.CreateStandalone(_fileKey, MacProtection.Salt, _dataDigest).Length;

    /// <summary>Saves the edited file incrementally, which writes the update and gives it a new token over the whole file.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int IncrementalSaveWithToken() => PdfIncrementalWriter.Save(_store).Length;

    /// <summary>Saves the edited file compactly, which writes every object again and gives the file a new token.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int CompactSaveWithToken() => PdfCompactWriter.Save(_store, PdfCompactOptions.Classic).Length;

    /// <summary>Opens the file and does not check the PDF MAC, to show what the check adds.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int OpenOnly()
    {
        using var document = PdfDocumentReader.Open(_oneRevision, AesGcmPdf.UserPassword);
        return document.PageCount;
    }
}
