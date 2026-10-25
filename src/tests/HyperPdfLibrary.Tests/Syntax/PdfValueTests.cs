// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Checks the compact representation and value semantics of <see cref="PdfValue"/>.</summary>
public sealed class PdfValueTests
{
    /// <summary>The expected size of the compact value representation.</summary>
    private const int ExpectedSize = 16;

    /// <summary>An integer payload used for kind equality checks.</summary>
    private const int SampleInteger = 1;

    /// <summary>A zero reference generation.</summary>
    private const int SampleGeneration = 0;

    /// <summary>The first byte offset used by a string range.</summary>
    private const int StringOffset = 1;

    /// <summary>The number of bytes in the test string range.</summary>
    private const int StringLength = 3;

    /// <summary>The other byte offset used by a string range.</summary>
    private const int OtherStringOffset = 2;

    /// <summary>The NaN payload kept by the real value.</summary>
    private const long NanBits = 0x7FF8_0000_0000_1234L;

    /// <summary>The bytes used by both string values.</summary>
    private static readonly byte[] StringBytes = "abcdef"u8.ToArray();

    /// <summary>The value stores its kind and payload in sixteen bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SizeIsSixteenBytes() => await Assert.That(Unsafe.SizeOf<PdfValue>()).IsEqualTo(ExpectedSize);

    /// <summary>Every kind reports its original kind after construction.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KindsArePreserved()
    {
        var array = new PdfArray(null);
        var dictionary = new PdfDictionary(null);
        var stream = new PdfStream(dictionary, []);
        PdfValue[] values =
        [
            PdfValue.Null,
            PdfValue.FromBoolean(false),
            PdfValue.FromInteger(long.MinValue),
            PdfValue.FromReal(BitConverter.Int64BitsToDouble(NanBits)),
            PdfValue.FromName(KnownName.Type),
            PdfValue.FromString(StringBytes),
            PdfValue.FromArray(array),
            PdfValue.FromDictionary(dictionary),
            PdfValue.FromStream(stream),
            PdfValue.FromReference(new(int.MinValue, int.MaxValue)),
        ];
        PdfKind[] expected =
        [
            PdfKind.Null,
            PdfKind.Boolean,
            PdfKind.Integer,
            PdfKind.Real,
            PdfKind.Name,
            PdfKind.String,
            PdfKind.Array,
            PdfKind.Dictionary,
            PdfKind.Stream,
            PdfKind.Reference,
        ];

        for (var i = 0; i < values.Length; i++)
        {
            await Assert.That(values[i].Kind).IsEqualTo(expected[i]);
        }
    }

    /// <summary>Integer and real payloads retain all 64 bits, including NaN payloads and signed zero.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NumericPayloadBitsArePreserved()
    {
        var integer = PdfValue.FromInteger(long.MinValue);
        var real = PdfValue.FromReal(BitConverter.Int64BitsToDouble(NanBits));
        var negativeZero = PdfValue.FromReal(BitConverter.Int64BitsToDouble(long.MinValue));

        using (Assert.Multiple())
        {
            await Assert.That(integer.AsInteger()).IsEqualTo(long.MinValue);
            await Assert.That(BitConverter.DoubleToInt64Bits(real.AsNumber())).IsEqualTo(NanBits);
            await Assert.That(BitConverter.DoubleToInt64Bits(negativeZero.AsNumber())).IsEqualTo(long.MinValue);
            await Assert.That(real == PdfValue.FromReal(BitConverter.Int64BitsToDouble(NanBits))).IsTrue();
        }
    }

    /// <summary>Inline kind markers keep equal payload bits from different kinds distinct.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineKindsRemainDistinctInEquality()
    {
        using (Assert.Multiple())
        {
            await Assert.That(PdfValue.FromBoolean(true) == PdfValue.FromInteger(SampleInteger)).IsFalse();
            await Assert.That(PdfValue.FromInteger(SampleInteger) == PdfValue.FromReal(SampleInteger)).IsFalse();
            await Assert.That(PdfValue.FromName(new(SampleInteger)) == PdfValue.FromInteger(SampleInteger)).IsFalse();
            await Assert.That(PdfValue.FromReference(new(SampleInteger, SampleGeneration)) == PdfValue.FromInteger(SampleInteger)).IsFalse();
            await Assert.That(PdfValue.FromBoolean(true) == PdfValue.FromBoolean(true)).IsTrue();
        }
    }

    /// <summary>Reference ids keep both complete integer fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReferenceFieldsArePreserved()
    {
        var id = new PdfObjectId(int.MinValue, int.MaxValue);
        var value = PdfValue.FromReference(id);

        await Assert.That(value.AsReference()).IsEqualTo(id);
    }

    /// <summary>Strings preserve buffer identity, offset and length in equality and byte access.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StringRangeAndIdentityArePreserved()
    {
        var first = PdfValue.FromString(StringBytes, StringOffset, StringLength);
        var same = PdfValue.FromString(StringBytes, StringOffset, StringLength);
        var otherRange = PdfValue.FromString(StringBytes, OtherStringOffset, StringLength);
        var equalBytes = PdfValue.FromString("bcd"u8.ToArray());

        using (Assert.Multiple())
        {
            await Assert.That(first == same).IsTrue();
            await Assert.That(first == otherRange).IsFalse();
            await Assert.That(first == equalBytes).IsFalse();
            await Assert.That(first.AsStringBytes().SequenceEqual("bcd"u8)).IsTrue();
        }
    }

    /// <summary>Reference-valued kinds keep identity-based equality.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectValuesUseReferenceIdentity()
    {
        var array = new PdfArray(null);
        var otherArray = new PdfArray(null);
        var sameArray = PdfValue.FromArray(array);
        var dictionary = new PdfDictionary(null);
        var sameDictionary = PdfValue.FromDictionary(dictionary);
        var stream = new PdfStream(dictionary, []);

        using (Assert.Multiple())
        {
            await Assert.That(PdfValue.FromArray(array) == sameArray).IsTrue();
            await Assert.That(PdfValue.FromArray(otherArray) == sameArray).IsFalse();
            await Assert.That(PdfValue.FromDictionary(dictionary) == sameDictionary).IsTrue();
            await Assert.That(PdfValue.FromStream(stream) == PdfValue.FromStream(stream)).IsTrue();
            await Assert.That(PdfValue.FromStream(stream) == sameDictionary).IsFalse();
        }
    }
}
