// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;

namespace HyperPdf.Benchmarks;

/// <summary>Measures data snapshots and colour equality.</summary>
public class DataRecordBenchmarks
{
    /// <summary>The image edge length.</summary>
    private const int Edge = 128;

    /// <summary>The BGRA channel count.</summary>
    private const int Channels = 4;

    /// <summary>The opaque alpha value.</summary>
    private const byte Opaque = 255;

    /// <summary>The source signature scan.</summary>
    private readonly byte[] _pixels = new byte[Edge * Edge * Channels];

    /// <summary>The first tone.</summary>
    private readonly PageTone _first = new(0x2A2826U, 0xD2CDC5U);

    /// <summary>The equal tone with its own lookup table.</summary>
    private readonly PageTone _second = new(0x2A2826U, 0xD2CDC5U);

    /// <summary>Builds an opaque scan with a margin.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pixels.AsSpan().Fill(Opaque);
        _pixels.AsSpan((Edge + 1) * Channels, Channels - 1).Clear();
    }

    /// <summary>Creates a remembered tab.</summary>
    /// <returns>The tab snapshot.</returns>
    [Benchmark]
    public SessionTab RememberTab() => new() { FilePath = "documents/sample.pdf", PageIndex = 1, IsSelected = true };

    /// <summary>Prepares imported signature pixels.</summary>
    /// <returns>The signature.</returns>
    [Benchmark]
    public SignatureImage? PrepareSignature() => SignatureImage.Create(_pixels, Edge, Edge, true);

    /// <summary>Compares equal tones with separate lookup tables.</summary>
    /// <returns>Whether their colours match.</returns>
    [Benchmark]
    public bool CompareTones() => _first.Equals(_second);
}
