// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests that <see cref="DerWriter"/> writes the bytes <see cref="AsnWriter"/> writes for the same values.</summary>
public sealed class DerWriterTests
{
    /// <summary>The levels of nesting the writer is given room for.</summary>
    private const int MaxDepth = 8;

    /// <summary>The bytes the writer starts with, small enough that the buffer has to grow.</summary>
    private const int SmallCapacity = 16;

    /// <summary>The most a one-byte length holds.</summary>
    private const int LastShortLength = 127;

    /// <summary>The least a two-byte length needs.</summary>
    private const int FirstLongLength = 128;

    /// <summary>The most a one-byte length after the length marker holds.</summary>
    private const int LastOneByteLength = 255;

    /// <summary>A length that needs three bytes after the length marker.</summary>
    private const int ThreeByteLength = 70_000;

    /// <summary>The OID of id-data in dotted form.</summary>
    private const string DataOid = "1.2.840.113549.1.7.1";

    /// <summary>The hours the sample time is ahead of UTC.</summary>
    private const int OffsetHours = 10;

    /// <summary>The filler byte of the long values.</summary>
    private const byte Filler = 0x5A;

    /// <summary>Gets the encoded contents of id-data.</summary>
    private static ReadOnlySpan<byte> DataEncoded => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x01];

    /// <summary>Lengths around every boundary of the DER length forms are written as the BCL writer writes them.</summary>
    /// <param name="length">The length of an octet string inside a sequence.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(LastShortLength - 1)]
    [Arguments(LastShortLength)]
    [Arguments(FirstLongLength)]
    [Arguments(LastOneByteLength)]
    [Arguments(LastOneByteLength + 1)]
    [Arguments(ThreeByteLength)]
    public async Task LengthsMatchTheBclWriter(int length)
    {
        var contents = new byte[length];
        Array.Fill(contents, Filler);
        var expected = new AsnWriter(AsnEncodingRules.DER);
        using (expected.PushSequence())
        {
            expected.WriteObjectIdentifier(DataOid);
            using (expected.PushSequence())
            {
                expected.WriteOctetString(contents);
            }
        }

        await Assert.That(WriteNested(contents)).IsEquivalentTo(expected.Encode());
    }

    /// <summary>A SET OF with elements of different sizes is sorted by its encodings, as the BCL writer sorts it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SetOfIsSortedLikeTheBclWriter()
    {
        byte[][] elements = [[0x05, 0x00], [0x04, 0x03, 0x01, 0x02, 0x03], [0x04, 0x01, 0xFF], [0x02, 0x01, 0x07], [0x04, 0x01, 0x10]];
        var expected = new AsnWriter(AsnEncodingRules.DER);
        using (expected.PushSetOf())
        {
            foreach (var element in elements)
            {
                expected.WriteEncodedValue(element);
            }
        }

        await Assert.That(WriteSet(elements)).IsEquivalentTo(expected.Encode());
    }

    /// <summary>Integers, NULL and UTCTime are written as the BCL writer writes them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrimitivesMatchTheBclWriter()
    {
        var time = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(OffsetHours));
        var expected = new AsnWriter(AsnEncodingRules.DER);
        using (expected.PushSequence())
        {
            expected.WriteInteger(0);
            expected.WriteInteger(1);
            expected.WriteInteger(LastShortLength);
            expected.WriteInteger(FirstLongLength);
            expected.WriteInteger(LastOneByteLength + 1);
            expected.WriteInteger(int.MaxValue);
            expected.WriteNull();
            expected.WriteUtcTime(time);
        }

        await Assert.That(WritePrimitives(time)).IsEquivalentTo(expected.Encode());
    }

    /// <summary>Writes an OID and an octet string inside two sequences.</summary>
    /// <param name="contents">The octet string.</param>
    /// <returns>The encoding.</returns>
    private static byte[] WriteNested(byte[] contents)
    {
        var writer = new DerWriter(stackalloc int[MaxDepth], SmallCapacity);
        try
        {
            writer.Begin(DerWriter.SequenceTag);
            writer.WriteObjectIdentifier(DataEncoded);
            writer.Begin(DerWriter.SequenceTag);
            writer.WriteOctetString(contents);
            writer.End();
            writer.End();
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes already encoded elements as a SET OF.</summary>
    /// <param name="elements">The elements.</param>
    /// <returns>The encoding.</returns>
    private static byte[] WriteSet(byte[][] elements)
    {
        var writer = new DerWriter(stackalloc int[MaxDepth], SmallCapacity);
        try
        {
            writer.Begin(DerWriter.SetTag);
            foreach (var element in elements)
            {
                writer.WriteEncoded(element);
            }

            writer.EndSetOf();
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes some integers, a NULL and a time inside a sequence.</summary>
    /// <param name="time">The time.</param>
    /// <returns>The encoding.</returns>
    private static byte[] WritePrimitives(DateTimeOffset time)
    {
        var writer = new DerWriter(stackalloc int[MaxDepth], SmallCapacity);
        try
        {
            writer.Begin(DerWriter.SequenceTag);
            writer.WriteInteger(0);
            writer.WriteInteger(1);
            writer.WriteInteger(LastShortLength);
            writer.WriteInteger(FirstLongLength);
            writer.WriteInteger(LastOneByteLength + 1);
            writer.WriteInteger(int.MaxValue);
            writer.WriteNull();
            writer.WriteUtcTime(time);
            writer.End();
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }
}
