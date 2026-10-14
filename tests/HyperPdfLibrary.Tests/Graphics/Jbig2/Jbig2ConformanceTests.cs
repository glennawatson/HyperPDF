// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Decodes the JBIG2 streams of the electronic conformance data of the JBIG2 standard and compares every page with the bitmap the data
/// gives. The data is not in the repository: set the environment variable to the folder that holds its .jb2 and .bmp files.
/// </summary>
public sealed class Jbig2ConformanceTests
{
    /// <summary>The environment variable that names the folder with the conformance data.</summary>
    private const string FolderVariable = "HYPERPDF_JBIG2_CONFORMANCE";

    /// <summary>The extension of the conformance streams.</summary>
    private const string StreamPattern = "*.jb2";

    /// <summary>
    /// The pages whose coded data departs from the text of T.88, so a decoder that follows the text cannot reproduce the bitmap the
    /// data gives. Each is decoded without a comparison. The keys are the stream's file name and the page number.
    /// </summary>
    private static readonly FrozenDictionary<string, string> DepartsFromText = new Dictionary<string, string>
    {
        ["codeStreamTest1_TT1.jb2 page 1"] =
            "Huffman symbol ID table (7.4.3.1.7): run code N means a code length of N in the text, but the stream decodes only when a run code's own prefix and N further bits form the code.",
        ["codeStreamTest1_TT2.jb2 page 1"] =
            "As TT1 page 1; its Huffman symbol dictionary also gives a BMSIZE (6.5.9) that is not the length of the MMR data that follows it.",
        ["codeStreamTest1_TT5.jb2 page 1"] =
            "Refinement/aggregate symbol dictionary (segment 2): it refers to no dictionary but refines the one before it, writes refinement AT pixels (0,0,0,0) that 6.3.5.4 "
            + "does not allow, and codes template 0 (figure 12) with the reference row above twice and never the row below; it decodes exactly only with all three changes.",
        ["codeStreamTest2_TT6.jb2 page 1"] =
            "Text region with SBREFINE = 1: the stream leaves out the instance T offset (IAIT, 6.4.9) and ignores STRIPT; it decodes exactly only with both changes.",
        ["codeStreamTest3_TT8.jb2 page 1"] =
            "Colour extension (COLEXTFLAG, region flags 0x08 and page flags 0x81) on top of the TT6 text region coding; ISO 32000-2 section 7.4.7 forbids colour extension in PDF.",
    }.ToFrozenDictionary();

    /// <summary>Every page of every conformance stream decodes to the bitmap the conformance data gives for it, except those that depart from the text of T.88.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryStreamMatchesItsExpectedBitmaps()
    {
        var folder = FindFolder();
        var failures = new List<string>();
        var compared = 0;
        foreach (var path in Directory.EnumerateFiles(folder, StreamPattern, SearchOption.AllDirectories).Order())
        {
            var file = new Jbig2ConformanceFile(await File.ReadAllBytesAsync(path));
            foreach (var page in file.PageNumbers)
            {
                var expectedPath = Path.ChangeExtension(path, null) + string.Create(CultureInfo.InvariantCulture, $"_TT{page - 1:D2}.bmp");
                if (!File.Exists(expectedPath))
                {
                    continue;
                }

                var difference = Compare(file, page, new(await File.ReadAllBytesAsync(expectedPath)), out var first);
                var key = string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(path)} page {page}");
                if (DepartsFromText.ContainsKey(key))
                {
                    continue;
                }

                compared++;
                if (difference > 0)
                {
                    failures.Add(string.Create(CultureInfo.InvariantCulture, $"{key}: {difference} pixels differ, first at {first}"));
                }
            }
        }

        await Assert.That(compared).IsGreaterThan(0);
        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Finds the conformance folder from the environment.</summary>
    /// <returns>The folder.</returns>
    private static string FindFolder()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            return folder;
        }

        Skip.Test($"Set {FolderVariable} to the folder with ITU-T T.88's conformance streams to run these tests.");
        return string.Empty;
    }

    /// <summary>Decodes a page and counts the pixels that differ from the expected bitmap.</summary>
    /// <param name="file">The stream.</param>
    /// <param name="page">The page number.</param>
    /// <param name="expected">The expected bitmap.</param>
    /// <param name="firstDifference">Receives the first differing pixel, or null.</param>
    /// <returns>The number of differing pixels; the whole page when nothing decoded.</returns>
    private static int Compare(Jbig2ConformanceFile file, int page, Jbig2ConformanceBitmap expected, out Jbig2Point? firstDifference)
    {
        var stride = Jbig2Decoder.GetRowBytes(expected.Width);
        var decoded = new byte[stride * expected.Height];
        firstDifference = null;
        if (!Jbig2Decoder.TryDecode(file.Segments(page), file.Segments(0), expected.Width, expected.Height, decoded))
        {
            return expected.Width * expected.Height;
        }

        var differences = 0;
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var index = (y * stride) + (x >> Jbig2Bits.ByteShift);
                var mask = Jbig2Bits.FirstPixelBit >> (x & Jbig2Bits.BitMask);
                if ((decoded[index] & mask) == 0 == ((expected.Rows[index] & mask) != 0))
                {
                    continue;
                }

                differences++;
                firstDifference ??= new(x, y);
            }
        }

        return differences;
    }
}
