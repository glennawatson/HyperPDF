// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks packed Unicode scalar lookup and malformed side tables.</summary>
public sealed class PackedUnicodeTests
{
    /// <summary>BMP and supplementary values round trip, including repeated supplementary scalars.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalarsRoundTrip()
    {
        int[] values = [0, 0x3042, 0x20000, 0x1F100, 0x20000];
        var table = CidToUnicodeTable.Parse(PackedTables.PackUnicode(values));
        for (var cid = 0; cid < values.Length; cid++)
        {
            await Assert.That(table.Lookup(cid)).IsEqualTo(values[cid]);
        }

        await Assert.That(table.Lookup(-1)).IsEqualTo(0);
        await Assert.That(table.Lookup(values.Length)).IsEqualTo(0);
    }

    /// <summary>A surrogate-range marker cannot reference an absent side-table value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsMissingSupplementaryIndex() =>
        await Assert.That(static () => CidToUnicodeTable.Parse([0x02, 0x02, 0, 0x80, 0xE0, 0x06, 0])).Throws<PdfException>();

    /// <summary>A side table cannot hold more indices than the surrogate range provides.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOversizedSideTable() =>
        await Assert.That(static () => CidToUnicodeTable.Parse([0x02, 0, 0x81, 0x10])).Throws<PdfException>();

    /// <summary>BMP values belong in the flat array, not the supplementary side table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsBmpValueInSideTable() =>
        await Assert.That(static () => CidToUnicodeTable.Parse([0x02, 0, 1, 0xFF, 0x7F])).Throws<PdfException>();

    /// <summary>Incomplete packed resources are rejected before publication.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsTruncatedSideTable() =>
        await Assert.That(static () => CidToUnicodeTable.Parse([0x02, 1, 0])).Throws<PdfException>();

    /// <summary>UTF-16 surrogates are not valid input scalar values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsUnpairedSurrogate() =>
        await Assert.That(static () => PackedTables.PackUnicode([0xD800])).Throws<InvalidDataException>();
}
