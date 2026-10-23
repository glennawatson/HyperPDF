// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading the structure of a U3D model and describing it; allocations come from the EventPipe trace.</summary>
public class U3dReaderBenchmarks
{
    /// <summary>The sample model.</summary>
    private byte[] _model = [];

    /// <summary>The model read once, for the description.</summary>
    private U3dScene _scene = null!;

    /// <summary>Makes the model.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _model = U3dBuilder.SampleModel();
        _scene = U3dReader.Read(_model);
    }

    /// <summary>Reads the model's blocks, nodes, shapes and resources.</summary>
    /// <returns>The node count.</returns>
    [Benchmark]
    public int ReadModel() => U3dReader.Read(_model).Nodes.Length;

    /// <summary>Reads a file that is not a model, which is refused after one block.</summary>
    /// <returns>The problem count.</returns>
    [Benchmark]
    public int ReadNotAModel() => U3dReader.Read("This is not a model."u8).Problems.Length;

    /// <summary>Describes the scene in words.</summary>
    /// <returns>The length of the description.</returns>
    [Benchmark]
    public int Describe() => U3dSummary.Describe(_scene).Length;
}
