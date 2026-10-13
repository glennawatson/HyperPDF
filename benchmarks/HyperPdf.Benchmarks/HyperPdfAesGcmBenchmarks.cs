// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures opening an AES-GCM (ISO/TS 32003) file, decrypting and encrypting a stream, and saving the file again.</summary>
public class HyperPdfAesGcmBenchmarks
{
    /// <summary>The size of the stream decrypted and encrypted.</summary>
    private const int StreamSize = 64 * 1024;

    /// <summary>The file made by the independent test builder.</summary>
    private byte[] _file = [];

    /// <summary>A stream protected with AES-GCM.</summary>
    private byte[] _protected = [];

    /// <summary>The plain bytes of the stream.</summary>
    private byte[] _plain = [];

    /// <summary>The handler for the file.</summary>
    private PdfSecurityHandler _handler = null!;

    /// <summary>The opened file the save benchmark writes.</summary>
    private PdfObjectStore _store = null!;

    /// <summary>Makes the file, the handler and a protected stream.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _file = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        _plain = new byte[StreamSize];
        RandomNumberGenerator.Fill(_plain);
        _handler = AesGcmPdf.CreateHandler(_file);
        _protected = _handler.EncryptStream(new(AesGcmPdf.ContentNumber, 0), _plain);
        _store = StoreOpening.Open(_file, AesGcmPdf.UserPassword);
    }

    /// <summary>Releases the handler and the opened file.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _handler.Dispose();
        _store.Dispose();
    }

    /// <summary>Opens the file with its password and reads the page content stream.</summary>
    /// <returns>The content length.</returns>
    [Benchmark(Baseline = true)]
    public int OpenAndReadContent()
    {
        using var store = StoreOpening.Open(_file, AesGcmPdf.UserPassword);
        return StoreReading.GetObject(store, new(AesGcmPdf.ContentNumber, 0)).AsStream()!.DecodeToArray().Length;
    }

    /// <summary>Authenticates and decrypts a 64 KiB stream.</summary>
    /// <returns>The plain length.</returns>
    [Benchmark]
    public int DecryptStream() => _handler.TryDecryptStream(new(AesGcmPdf.ContentNumber, 0), _protected, out var plain) ? plain.Length : 0;

    /// <summary>Encrypts a 64 KiB stream under a new IV.</summary>
    /// <returns>The stored length.</returns>
    [Benchmark]
    public int EncryptStream() => _handler.EncryptStream(new(AesGcmPdf.ContentNumber, 0), _plain).Length;

    /// <summary>Saves the opened file with a classic table, encrypting every stream and string again.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int CompactSave() => PdfCompactWriter.Save(_store, PdfCompactOptions.Classic).Length;
}
