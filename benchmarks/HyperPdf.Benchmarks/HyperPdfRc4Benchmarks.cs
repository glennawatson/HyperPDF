// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Security;

namespace HyperPdf.Benchmarks;

/// <summary>Runs the RC4 cipher over a long stream and over a short string, where the 256 step key setup is most of the cost.</summary>
public class HyperPdfRc4Benchmarks
{
    /// <summary>The bytes of a stream.</summary>
    private const int StreamBytes = 64 * 1024;

    /// <summary>The bytes of a short string.</summary>
    private const int StringBytes = 32;

    /// <summary>The key length in bytes.</summary>
    private const int KeyBytes = 16;

    /// <summary>The key.</summary>
    private readonly byte[] _key = new byte[KeyBytes];

    /// <summary>A stream's bytes.</summary>
    private readonly byte[] _stream = new byte[StreamBytes];

    /// <summary>A string's bytes.</summary>
    private readonly byte[] _string = new byte[StringBytes];

    /// <summary>Fills the key and data.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < KeyBytes; i++)
        {
            _key[i] = (byte)(i + 1);
        }

        _stream.AsSpan().Fill(1);
        _string.AsSpan().Fill(1);
    }

    /// <summary>Ciphers a 64 KiB stream.</summary>
    /// <returns>A byte of the result.</returns>
    [Benchmark]
    public byte ApplyStream()
    {
        Rc4.Apply(_key, _stream);
        return _stream[0];
    }

    /// <summary>Ciphers a 32 byte string.</summary>
    /// <returns>A byte of the result.</returns>
    [Benchmark]
    public byte ApplyString()
    {
        Rc4.Apply(_key, _string);
        return _string[0];
    }
}
