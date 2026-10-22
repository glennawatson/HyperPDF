// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Looks named destinations up in a name tree of 50,000 names spread over 500 leaves, as a long LaTeX document with links has.</summary>
public class HyperPdfNameTreeBenchmarks
{
    /// <summary>The leaves of the tree.</summary>
    private const int Leaves = 500;

    /// <summary>The names in each leaf.</summary>
    private const int NamesPerLeaf = 100;

    /// <summary>The object number of the tree's root.</summary>
    private const int RootNumber = 5;

    /// <summary>The lookups made by one benchmark call.</summary>
    private const int Lookups = 200;

    /// <summary>The distance between looked up names, so the lookups reach every leaf.</summary>
    private const int LookupStride = 251;

    /// <summary>The names looked up.</summary>
    private readonly PdfValue[] _names = new PdfValue[Lookups];

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Builds the tree outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(BuildTree(), null);
        for (var i = 0; i < Lookups; i++)
        {
            _names[i] = PdfValue.FromString(Encoding.ASCII.GetBytes(Name((i * LookupStride) % (Leaves * NamesPerLeaf))));
        }
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Looks up 200 names spread over the whole tree.</summary>
    /// <returns>The destinations found.</returns>
    [Benchmark]
    public int FindNames()
    {
        var found = 0;
        foreach (var name in _names)
        {
            found += PdfDocumentNavigation.FindNamedDestination(_document, name).IsNull ? 0 : 1;
        }

        return found;
    }

    /// <summary>Names a destination so names sort in number order.</summary>
    /// <param name="index">The destination number.</param>
    /// <returns>The name.</returns>
    private static string Name(int index) => string.Create(CultureInfo.InvariantCulture, $"dest.{index:D6}");

    /// <summary>Builds a PDF whose catalog names 50,000 destinations in a two level tree.</summary>
    /// <returns>The file.</returns>
    private static byte[] BuildTree()
    {
        List<string> objects =
        [
            $"<< /Type /Catalog /Pages 2 0 R /Names << /Dests {RootNumber} 0 R >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            string.Empty,
            string.Empty,
        ];
        var kids = new StringBuilder();
        for (var leaf = 0; leaf < Leaves; leaf++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{RootNumber + 1 + leaf} 0 R ");
        }

        objects[RootNumber - 1] = $"<< /Kids [{kids}] >>";
        for (var leaf = 0; leaf < Leaves; leaf++)
        {
            var first = leaf * NamesPerLeaf;
            var last = first + NamesPerLeaf - 1;
            var pairs = new StringBuilder();
            for (var i = first; i <= last; i++)
            {
                _ = pairs.Append(CultureInfo.InvariantCulture, $"({Name(i)}) [3 0 R /Fit] ");
            }

            objects.Add($"<< /Limits [({Name(first)}) ({Name(last)})] /Names [{pairs}] >>");
        }

        return MiniPdf.Build([.. objects]);
    }
}
