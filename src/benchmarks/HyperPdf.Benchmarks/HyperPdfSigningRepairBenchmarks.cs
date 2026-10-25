// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Rebuilds the cross-reference information of a 2000 object file with 8 KB of data in each object, as signing does for a damaged file.</summary>
public class HyperPdfSigningRepairBenchmarks
{
    /// <summary>The objects after the catalog and the page tree.</summary>
    private const int Objects = 2_000;

    /// <summary>The bytes in each object's data.</summary>
    private const int DataBytes = 8 * 1024;

    /// <summary>The letters the data is made of; digits are included so the scan has numbers to reject.</summary>
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789 \n";

    /// <summary>The step through the alphabet between data bytes.</summary>
    private const int Stride = 7;

    /// <summary>The file.</summary>
    private byte[] _file = [];

    /// <summary>Builds the file.</summary>
    [GlobalSetup]
    public void Setup()
    {
        List<string> objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [] /Count 0 >>"];
        for (var i = 0; i < Objects; i++)
        {
            var seed = i;
            objects.Add(MiniPdf.Stream(string.Create(CultureInfo.InvariantCulture, $"/N {i}"), string.Create(DataBytes, seed, static (span, value) =>
            {
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = Alphabet[((index * Stride) + value) % Alphabet.Length];
                }
            })));
        }

        _file = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(MiniPdf.Build([.. objects])));
    }

    /// <summary>Scans the file for object headers and rebuilds its structure.</summary>
    /// <returns>The number of objects found.</returns>
    [Benchmark]
    public int Rebuild() => PdfRepair.Rebuild(_file).Entries.Count;
}
