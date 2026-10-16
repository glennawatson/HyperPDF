// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures namespace URI comparison across PDF text-string encodings.</summary>
public class NamespaceUriEqualityBenchmarks
{
    /// <summary>The byte-order mark length.</summary>
    private const int MarkLength = 2;

    /// <summary>The bytes in a UTF-16 code unit.</summary>
    private const int Utf16Bytes = 2;

    /// <summary>The URI length exceeding stack character storage.</summary>
    private const int LongCharacters = 1_000;

    /// <summary>The URI encoded as PDFDocEncoding.</summary>
    private byte[] _left = [];

    /// <summary>The URI encoded as UTF-16BE.</summary>
    private byte[] _right = [];

    /// <summary>The long URI encoded as PDFDocEncoding.</summary>
    private byte[] _longLeft = [];

    /// <summary>The long URI encoded as UTF-16BE.</summary>
    private byte[] _longRight = [];

    /// <summary>The URI containing an unmatched UTF-16 surrogate.</summary>
    private byte[] _malformedLeft = [];

    /// <summary>The same replacement character in malformed UTF-8, or a distinct value.</summary>
    private byte[] _malformedRight = [];

    /// <summary>The long URI containing unmatched UTF-16 surrogates.</summary>
    private byte[] _longMalformedLeft = [];

    /// <summary>The long replacement-character URI in UTF-8, or a distinct value.</summary>
    private byte[] _longMalformedRight = [];

    /// <summary>Gets or sets whether both encodings name the same URI.</summary>
    [Params(false, true)]
    public bool Equivalent { get; set; }

    /// <summary>Creates equivalent or distinct URI bytes without changing the comparison workload.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _left = "urn:example:left"u8.ToArray();
        var encoded = Encoding.BigEndianUnicode.GetBytes(Equivalent ? "urn:example:left" : "urn:example:right");
        _right = new byte[MarkLength + encoded.Length];
        ReadOnlySpan<byte> mark = [0xFE, 0xFF];
        mark.CopyTo(_right);
        encoded.CopyTo(_right.AsSpan(MarkLength));
        var longText = new string('x', LongCharacters);
        _longLeft = Encoding.ASCII.GetBytes(longText);
        _longRight = [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(Equivalent ? longText : new('y', LongCharacters))];
        _malformedLeft = [0xFE, 0xFF, 0xD8, 0x00];
        _malformedRight = Equivalent ? [0xEF, 0xBB, 0xBF, 0xFF] : [0x78];
        _longMalformedLeft = new byte[MarkLength + (LongCharacters * Utf16Bytes)];
        mark.CopyTo(_longMalformedLeft);
        for (var offset = MarkLength; offset < _longMalformedLeft.Length; offset += Utf16Bytes)
        {
            _longMalformedLeft[offset] = 0xD8;
        }

        _longMalformedRight = PdfText.EncodeUtf8(new(Equivalent ? '\uFFFD' : 'x', LongCharacters));
    }

    /// <summary>Compares decoded URI values rather than their encoded byte identity.</summary>
    /// <returns>Whether the values name the same namespace.</returns>
    [Benchmark]
    public bool Equal() => NamespaceUriEquality.Equal(_left, _right, CancellationToken.None);

    /// <summary>Compares long URI values using transient pooled character storage.</summary>
    /// <returns>Whether the long encodings name the same namespace.</returns>
    [Benchmark]
    public bool EqualLong() => NamespaceUriEquality.Equal(_longLeft, _longRight, CancellationToken.None);

    /// <summary>Compares malformed encodings using the decoder's replacement semantics.</summary>
    /// <returns>Whether the malformed values decode to the same URI.</returns>
    [Benchmark]
    public bool EqualMalformed() => NamespaceUriEquality.Equal(_malformedLeft, _malformedRight, CancellationToken.None);

    /// <summary>Compares long malformed runs through the pooled replacement-decoding path.</summary>
    /// <returns>Whether the malformed encodings decode to the same URI.</returns>
    [Benchmark]
    public bool EqualLongMalformed() => NamespaceUriEquality.Equal(_longMalformedLeft, _longMalformedRight, CancellationToken.None);
}
