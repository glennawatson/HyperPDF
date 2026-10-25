// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests that cross-reference sections cannot make the table grow past what the file holds.</summary>
public sealed class XrefBoundsTests
{
    /// <summary>A count the object number limit allows but the rest of the file cannot hold.</summary>
    private const int UnbackedCount = 5_000_000;

    /// <summary>A count above the object number limit.</summary>
    private const int OverLimitCount = 2_000_000_000;

    /// <summary>The objects in the sample files: the catalog and page tree.</summary>
    private const int ObjectCount = 2;

    /// <summary>The generation of the free head entry.</summary>
    private const int FreeGeneration = 65_535;

    /// <summary>The object number of the cross-reference stream.</summary>
    private const int XrefObject = 3;

    /// <summary>The /Size of the cross-reference stream sample.</summary>
    private const int StreamSize = 4;

    /// <summary>The catalog object.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree object.</summary>
    private const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>A classic subsection that announces more entries than the file can hold is rejected, and the file is rebuilt.</summary>
    /// <param name="count">The announced entry count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(UnbackedCount)]
    [Arguments(OverLimitCount)]
    public async Task ClassicSubsectionLargerThanFileIsRejected(int count)
    {
        var file = ClassicFile(count);
        using var store = StoreOpening.Open(file, null);
        await Assert.That(store.WasRepaired).IsTrue();
        await Assert.That(store.Size).IsEqualTo(ObjectCount + 1);
        await Assert.That(store.Catalog.IsName(KnownName.Type, KnownName.Catalog)).IsTrue();
    }

    /// <summary>A cross-reference stream subsection beyond the object number limit is skipped without growing the table.</summary>
    /// <param name="skippedInMiddle">Whether valid entries follow the invalid subsection.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StreamSubsectionBeyondLimitIsSkipped(bool skippedInMiddle)
    {
        var file = StreamFile(skippedInMiddle);
        using var store = StoreOpening.Open(file, null);
        await Assert.That(store.WasRepaired).IsFalse();
        await Assert.That(store.UsesXrefStreams).IsTrue();
        await Assert.That(store.Size).IsEqualTo(StreamSize);
        await Assert.That(store.Catalog.IsName(KnownName.Type, KnownName.Catalog)).IsTrue();
    }

    /// <summary>Builds a file whose classic table announces a given number of entries but lists only the real ones.</summary>
    /// <param name="count">The announced count.</param>
    /// <returns>The file.</returns>
    private static byte[] ClassicFile(int count)
    {
        var pdf = new RawPdf().Object(1, Catalog).Object(ObjectCount, Pages);
        var start = pdf.Position;
        _ = pdf.Append(string.Create(CultureInfo.InvariantCulture, $"xref\n0 {count}\n0000000000 {FreeGeneration} f \n"));
        _ = pdf.Append(string.Create(CultureInfo.InvariantCulture, $"{pdf.OffsetOf(1):D10} 00000 n \n{pdf.OffsetOf(ObjectCount):D10} 00000 n \n"));
        _ = pdf.Append(string.Create(CultureInfo.InvariantCulture, $"trailer\n<< /Size {ObjectCount + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n"));
        return pdf.ToArray();
    }

    /// <summary>Builds a file whose cross-reference stream has a real subsection and one that starts far beyond the limit.</summary>
    /// <param name="skippedInMiddle">Whether valid entries follow the invalid subsection.</param>
    /// <returns>The file.</returns>
    private static byte[] StreamFile(bool skippedInMiddle)
    {
        var pdf = new RawPdf().Object(1, Catalog).Object(ObjectCount, Pages);
        var self = pdf.Position;
        List<RawPdf.XrefStreamEntry> entries = [new(0, 0, FreeGeneration), new(1, pdf.OffsetOf(1), 0),];
        if (skippedInMiddle)
        {
            entries.Add(new(1, 0, 0));
        }

        entries.Add(new(1, pdf.OffsetOf(ObjectCount), 0));
        entries.Add(new(1, self, 0));
        if (!skippedInMiddle)
        {
            entries.Add(new(1, 0, 0));
        }

        var index = skippedInMiddle
            ? $"[0 {ObjectCount} {OverLimitCount} 1 {ObjectCount} {StreamSize - ObjectCount}]"
            : $"[0 {StreamSize} {OverLimitCount} 1]";
        return pdf.XrefStream(XrefObject, StreamSize, index, "/Root 1 0 R", entries).ToArray();
    }
}
