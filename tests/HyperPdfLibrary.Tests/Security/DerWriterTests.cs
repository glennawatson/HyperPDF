// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Signing;

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

    /// <summary>The number of random trees written.</summary>
    private const int RandomTrees = 400;

    /// <summary>The deepest nesting of a random tree; below the room the writer is given.</summary>
    private const int RandomDepth = 5;

    /// <summary>The number of kinds of random value.</summary>
    private const int KindCount = 3;

    /// <summary>The kind that is an octet string.</summary>
    private const int OctetKind = 0;

    /// <summary>The kind that is a sequence.</summary>
    private const int SequenceKind = 1;

    /// <summary>The kind that is a SET OF.</summary>
    private const int SetOfKind = 2;

    /// <summary>One more than the most children of a random sequence.</summary>
    private const int MaxChildren = 5;

    /// <summary>One more than the most elements of a random SET OF, enough to go past the elements sorted on the stack.</summary>
    private const int MaxSetElements = 80;

    /// <summary>One more than the most bytes in an element of a random SET OF.</summary>
    private const int MaxSetElementBytes = 4;

    /// <summary>The number of choices of length range.</summary>
    private const int BoundaryOdds = 4;

    /// <summary>The band of lengths around the short form limit.</summary>
    private const int ShortBand = 0;

    /// <summary>The band of lengths around the one-byte long form limit.</summary>
    private const int OneByteBand = 1;

    /// <summary>The band of lengths up to 70,000.</summary>
    private const int LargeBand = 2;

    /// <summary>Added to a length to make the exclusive upper bound two past it.</summary>
    private const int TwoBytesFrom = 2;

    /// <summary>One more than the longest small length.</summary>
    private const int SmallLength = 40;

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

    /// <summary>Random nested sequences, octet strings of random lengths up to 70,000 bytes and SET OFs with many elements are written as the BCL writer writes them and are strict DER.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RandomTreesMatchTheBclWriter()
    {
        var mismatches = new List<int>();
        for (var tree = 0; tree < RandomTrees; tree++)
        {
            if (!RandomTreeMatches())
            {
                mismatches.Add(tree);
            }
        }

        await Assert.That(mismatches).IsEmpty();
    }

    /// <summary>Writes one random tree to both writers.</summary>
    /// <returns><see langword="true"/> when the bytes are the same and strict DER.</returns>
    private static bool RandomTreeMatches()
    {
        var expected = new AsnWriter(AsnEncodingRules.DER);
        var writer = new DerWriter(stackalloc int[MaxDepth], SmallCapacity);
        try
        {
            FillRandom(ref writer, expected, 0);
            var actual = writer.ToArray();
            return actual.AsSpan().SequenceEqual(expected.Encode()) && StrictDer.Check(actual) is null;
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes a random value to both writers.</summary>
    /// <param name="writer">The writer under test.</param>
    /// <param name="expected">The BCL writer.</param>
    /// <param name="depth">The nesting level.</param>
    private static void FillRandom(ref DerWriter writer, AsnWriter expected, int depth)
    {
        var kind = depth >= RandomDepth ? OctetKind : RandomNumberGenerator.GetInt32(KindCount);
        switch (kind)
        {
            case SequenceKind:
            {
                writer.Begin(DerWriter.SequenceTag);
                using (expected.PushSequence())
                {
                    for (var i = RandomNumberGenerator.GetInt32(MaxChildren); i > 0; i--)
                    {
                        FillRandom(ref writer, expected, depth + 1);
                    }
                }

                writer.End();
                break;
            }

            case SetOfKind:
            {
                writer.Begin(DerWriter.SetTag);
                using (expected.PushSetOf())
                {
                    for (var i = RandomNumberGenerator.GetInt32(MaxSetElements); i > 0; i--)
                    {
                        var element = RandomNumberGenerator.GetBytes(RandomNumberGenerator.GetInt32(MaxSetElementBytes));
                        expected.WriteOctetString(element);
                        writer.WriteOctetString(element);
                    }
                }

                writer.EndSetOf();
                break;
            }

            default:
            {
                var contents = RandomNumberGenerator.GetBytes(RandomLength());
                expected.WriteOctetString(contents);
                writer.WriteOctetString(contents);
                break;
            }
        }
    }

    /// <summary>Picks a length near the interesting boundaries now and then, and a small one otherwise.</summary>
    /// <returns>The length.</returns>
    private static int RandomLength() => RandomNumberGenerator.GetInt32(BoundaryOdds) switch
    {
        ShortBand => RandomNumberGenerator.GetInt32(LastShortLength - 1, FirstLongLength + 1),
        OneByteBand => RandomNumberGenerator.GetInt32(LastOneByteLength - 1, LastOneByteLength + TwoBytesFrom),
        LargeBand => RandomNumberGenerator.GetInt32(ThreeByteLength),
        _ => RandomNumberGenerator.GetInt32(SmallLength),
    };

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
