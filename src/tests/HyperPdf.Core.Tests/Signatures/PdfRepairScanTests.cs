// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Signatures;

/// <summary>Tests that the keyword search in <see cref="PdfRepair.ScanHeaders"/> finds the headers a scan of every byte would.</summary>
public sealed class PdfRepairScanTests
{
    /// <summary>The seed of the damage.</summary>
    private const uint Seed = 13_579;

    /// <summary>The damaged copies made of each file.</summary>
    private const int Copies = 200;

    /// <summary>The bytes damaged in each copy.</summary>
    private const int Damage = 12;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>Gets the bytes damage is drawn from, the ones that make or break headers.</summary>
    private static ReadOnlySpan<byte> Letters => "0123456789 \r\n\0objendobj<>/Catalog"u8;

    /// <summary>Whole and damaged files give the same headers and catalog as the byte by byte scan.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScanHeaders_WholeAndDamagedFiles_MatchesByteByByteScan()
    {
        var original = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            MiniPdf.Stream("/A 1", "12 0 obj"));
        var state = Seed;
        for (var copy = 0; copy < Copies; copy++)
        {
            var file = (byte[])original.Clone();
            for (var i = 0; copy > 0 && i < Damage; i++)
            {
                file[(int)(Next(ref state) % (uint)file.Length)] = Letters[(int)(Next(ref state) % (uint)Letters.Length)];
            }

            var actual = PdfRepair.ScanHeaders(file, out var actualCatalog);
            var expected = Reference(file, out var expectedCatalog);

            await Assert.That(actualCatalog).IsEqualTo(expectedCatalog).Because($"copy {copy}");
            await Assert.That(actual.Count).IsEqualTo(expected.Count).Because($"copy {copy}");
            foreach (var (number, entry) in expected)
            {
                await Assert.That(actual.TryGetValue(number, out var found) && found == entry).IsTrue().Because($"copy {copy} object {number}");
            }
        }
    }

    /// <summary>Gets the next number of a small repeatable generator.</summary>
    /// <param name="state">The generator state.</param>
    /// <returns>The number.</returns>
    private static uint Next(ref uint state)
    {
        state ^= state << ShiftA;
        state ^= state >> ShiftB;
        state ^= state << ShiftC;
        return state;
    }

    /// <summary>Scans every byte for a header, as the repair once did.</summary>
    /// <param name="file">The file.</param>
    /// <param name="catalog">Receives the last catalog's number, or -1.</param>
    /// <returns>The headers found.</returns>
    private static Dictionary<int, XrefEntry> Reference(byte[] file, out int catalog)
    {
        var entries = new Dictionary<int, XrefEntry>();
        catalog = -1;
        for (var i = 0; i < file.Length; i++)
        {
            if (!IsTokenStart(file, i) || ObjectNumberAt(file, i) is not (>= 0 and var number))
            {
                continue;
            }

            entries[number] = new(XrefEntry.InUse, i, 0);
            catalog = IsCatalog(file, i) ? number : catalog;
        }

        return entries;
    }

    /// <summary>Determines whether a number could start at an index.</summary>
    /// <param name="file">The file.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> when a token starts there.</returns>
    private static bool IsTokenStart(byte[] file, int index) =>
        char.IsAsciiDigit((char)file[index]) && (index == 0 || PdfSyntax.IsSpace(file[index - 1]) || PdfSyntax.IsDelimiter(file[index - 1]));

    /// <summary>Reads the object number of an <c>N G obj</c> header.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">Where the header should start.</param>
    /// <returns>The number, or -1.</returns>
    private static int ObjectNumberAt(byte[] file, int offset)
    {
        var index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, offset), out var number);
        index = index < 0 || index >= file.Length || !PdfSyntax.IsSpace(file[index]) ? -1 : PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out _);
        index = index < 0 ? -1 : PdfSyntax.SkipSpace(file, index);
        var isHeader = index >= 0 && file.AsSpan(index).StartsWith("obj"u8) && (index + "obj"u8.Length >= file.Length || !char.IsAsciiLetterOrDigit((char)file[index + "obj"u8.Length]));
        return isHeader && number <= int.MaxValue ? (int)number : -1;
    }

    /// <summary>Determines whether the object at an offset is a catalog dictionary.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">The object's offset.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsCatalog(byte[] file, int offset)
    {
        var index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, offset), out _);
        index = index < 0 ? -1 : PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out _);
        index = index < 0 ? -1 : PdfSyntax.SkipSpace(file, PdfSyntax.SkipSpace(file, index) + "obj"u8.Length);
        if (index < 0 || index + 1 >= file.Length || file[index] != (byte)'<' || file[index + 1] != (byte)'<')
        {
            return false;
        }

        var value = PdfSyntax.FindKey(file, index, "Type"u8);
        return value >= 0 && value < file.Length && file[value] == (byte)'/' && file.AsSpan(value + 1, PdfSyntax.TokenEnd(file, value + 1) - value - 1).SequenceEqual("Catalog"u8);
    }
}
