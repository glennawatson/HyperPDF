// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Adds many distinct images to one page's resources, as a gallery or tiled page does, to show how the cost grows with their number.</summary>
public class HyperPdfPageResourcesBenchmarks
{
    /// <summary>The builder the resources belong to.</summary>
    private readonly PdfDocumentBuilder _sink = new();

    /// <summary>Gets or sets the images added.</summary>
    [Params(16, 512)]
    public int Images { get; set; }

    /// <summary>Adds the images to a new set of page resources.</summary>
    /// <returns>The number of images added.</returns>
    [Benchmark]
    public int AddImages()
    {
        var resources = new PdfPageResources(_sink);
        for (var i = 0; i < Images; i++)
        {
            _ = resources.AddImage(new(i + 1, 0));
        }

        return Images;
    }
}
