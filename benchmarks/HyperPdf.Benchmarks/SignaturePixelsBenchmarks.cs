// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;

namespace HyperPdf.Benchmarks;

/// <summary>Measures paper removal for signature scans of different sizes.</summary>
public class SignaturePixelsBenchmarks
{
    /// <summary>The number of bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The opaque alpha value.</summary>
    private const byte Opaque = 255;

    /// <summary>The scan restored before each operation.</summary>
    private byte[] _original = [];

    /// <summary>A premultiplied picture with soft, partly transparent ink, as a decoder gives it.</summary>
    private byte[] _translucent = [];

    /// <summary>The working image.</summary>
    private byte[] _pixels = [];

    /// <summary>Gets or sets the number of pixels in the scan.</summary>
    [Params(0, 100_000, 1_000_000)]
    public int PixelCount { get; set; }

    /// <summary>Creates an opaque scan with soft grey ink edges.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _original = new byte[PixelCount * Channels];
        _pixels = new byte[_original.Length];
        _translucent = new byte[_original.Length];
        for (var offset = 0; offset < _original.Length; offset += Channels)
        {
            var shade = (byte)((offset / Channels) % Opaque);
            _original.AsSpan(offset, Channels).Fill(shade);
            _original[offset + Channels - 1] = Opaque;

            // Premultiplied colour never exceeds its alpha, so the same shade serves as both.
            _translucent.AsSpan(offset, Channels).Fill(shade);
        }
    }

    /// <summary>Restores the scan and removes paper in the measured operation.</summary>
    [Benchmark]
    public void RemovePaper()
    {
        _original.AsSpan().CopyTo(_pixels);
        SignaturePixels.RemoveWhitePaper(_pixels);
    }

    /// <summary>Restores the scan and converts it from premultiplied to straight alpha, as reading a picture does.</summary>
    [Benchmark]
    public void Unpremultiply()
    {
        _translucent.AsSpan().CopyTo(_pixels);
        SignaturePixels.Unpremultiply(_pixels);
    }
}
