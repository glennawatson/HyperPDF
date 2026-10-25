// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for <see cref="PdfObjectWriter"/>.</summary>
public sealed class ObjectWriterTests
{
    /// <summary>An integer sample.</summary>
    private const long SampleInteger = -1_234_567_890_123;

    /// <summary>A real sample that formats exactly.</summary>
    private const double SampleReal = -1234.5625;

    /// <summary>The object number of the sample reference.</summary>
    private const int ReferenceNumber = 12;

    /// <summary>The generation of the sample reference.</summary>
    private const int ReferenceGeneration = 3;

    /// <summary>The page count in the sample dictionary.</summary>
    private const int SampleCount = 3;

    /// <summary>Every byte value, for binary strings.</summary>
    private const int ByteValues = 256;

    /// <summary>The sample stream's object number.</summary>
    private const int StreamNumber = 7;

    /// <summary>Null, booleans and numbers write and parse back to the same value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalarsRoundTrip()
    {
        var names = new PdfNameTable();

        await Assert.That(RoundTrip(PdfValue.Null, names).IsNull).IsTrue();
        await Assert.That(RoundTrip(PdfValue.FromBoolean(true), names).AsBoolean()).IsTrue();
        await Assert.That(RoundTrip(PdfValue.FromBoolean(false), names).AsBoolean(true)).IsFalse();
        await Assert.That(RoundTrip(PdfValue.FromInteger(SampleInteger), names).AsInteger()).IsEqualTo(SampleInteger);
        await Assert.That(RoundTrip(PdfValue.FromReal(SampleReal), names).AsNumber()).IsEqualTo(SampleReal);
        await Assert.That(Write(PdfValue.FromReal(SampleReal), names)).IsEqualTo("-1234.5625");
    }

    /// <summary>Names with delimiters, spaces, hashes and non-ASCII bytes are escaped and parse back to the same name.</summary>
    /// <param name="spelling">The name.</param>
    /// <param name="expected">The expected syntax.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Type", "/Type")]
    [Arguments("A B", "/A#20B")]
    [Arguments("x#1(y)/z", "/x#231#28y#29#2Fz")]
    [Arguments("café", "/caf#C3#A9")]
    public async Task NamesRoundTrip(string spelling, string expected)
    {
        var names = new PdfNameTable();
        var name = names.Intern(spelling);
        var value = PdfValue.FromName(name);

        await Assert.That(Write(value, names)).IsEqualTo(expected);
        await Assert.That(RoundTrip(value, names).AsName()).IsEqualTo(name);
    }

    /// <summary>Strings with delimiters, line ends and control bytes use escapes and parse back unchanged.</summary>
    /// <param name="text">The string, as Latin-1.</param>
    /// <param name="expected">The expected syntax.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", "()")]
    [Arguments("plain text", "(plain text)")]
    [Arguments("a(b)c\\d", "(a\\(b\\)c\\\\d)")]
    [Arguments("line\r\nend\t\u0001", "(line\\r\\nend\\t\\001)")]
    public async Task LiteralStringsRoundTrip(string text, string expected)
    {
        var names = new PdfNameTable();
        var bytes = Encoding.Latin1.GetBytes(text);
        var value = PdfValue.FromString(bytes);

        await Assert.That(Write(value, names)).IsEqualTo(expected);
        await Assert.That(RoundTrip(value, names).AsStringBytes().ToArray()).IsEquivalentTo(bytes);
    }

    /// <summary>Binary strings are written in the shorter hexadecimal form and parse back unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BinaryStringsUseHex()
    {
        var names = new PdfNameTable();
        var bytes = new byte[ByteValues];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(ByteValues - 1 - i);
        }

        var value = PdfValue.FromString(bytes);
        var written = Write(value, names);

        await Assert.That(written).StartsWith("<FFFEFD");
        await Assert.That(RoundTrip(value, names).AsStringBytes().ToArray()).IsEquivalentTo(bytes);
    }

    /// <summary>Arrays, dictionaries and references are written compactly and parse back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContainersRoundTrip()
    {
        var names = new PdfNameTable();
        var kids = new PdfArray(null);
        kids.Add(PdfValue.FromReference(new(ReferenceNumber, ReferenceGeneration)));
        kids.Add(PdfValue.FromArray(new(null)));
        var dictionary = new PdfDictionary(null);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Page));
        dictionary.Set(KnownName.Count, PdfValue.FromInteger(SampleCount));
        dictionary.Set(KnownName.Kids, PdfValue.FromArray(kids));
        dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(new(null)));
        var value = PdfValue.FromDictionary(dictionary);

        await Assert.That(Write(value, names)).IsEqualTo("<</Type /Page /Count 3 /Kids [12 3 R []] /Resources <<>>>>");
        var parsed = RoundTrip(value, names).AsDictionary()!;
        await Assert.That(parsed.GetInteger(KnownName.Count)).IsEqualTo(SampleCount);
        await Assert.That(parsed.GetArray(KnownName.Kids)!.GetRaw(0).AsReference()).IsEqualTo(new(ReferenceNumber, ReferenceGeneration));
        await Assert.That(Write(PdfValue.FromDictionary(parsed), names)).IsEqualTo(Write(value, names));
    }

    /// <summary>A stream is written as an indirect object with its /Length set to the data written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamsWriteLengthAndData()
    {
        var names = new PdfNameTable();
        var dictionary = new PdfDictionary(null);
        dictionary.Set(KnownName.Length, PdfValue.FromReference(new(ReferenceNumber, 0)));
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        var stream = new PdfStream(dictionary, "q Q"u8.ToArray(), 0, SampleCount, default, false);
        var written = WriteObject(new(StreamNumber, 0), PdfValue.FromStream(stream), names);

        await Assert.That(written).IsEqualTo("7 0 obj\n<</Type /XObject /Length 3>>\nstream\r\nq Q\r\nendstream\nendobj\n");
    }

    /// <summary>A stream inside an array cannot be written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedStreamsThrow()
    {
        var names = new PdfNameTable();
        var array = new PdfArray(null);
        array.Add(PdfValue.FromStream(new(new PdfDictionary(null), [])));

        await Assert.That(() => Write(PdfValue.FromArray(array), names)).Throws<PdfException>();
    }

    /// <summary>Writes a value as text.</summary>
    /// <param name="value">The value.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The syntax.</returns>
    private static string Write(PdfValue value, PdfNameTable names)
    {
        var writer = new PdfObjectWriter(names);
        try
        {
            writer.WriteValue(value);
            return Encoding.Latin1.GetString(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes an indirect object as text.</summary>
    /// <param name="id">The object id.</param>
    /// <param name="value">The value.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The syntax.</returns>
    private static string WriteObject(PdfObjectId id, PdfValue value, PdfNameTable names)
    {
        var writer = new PdfObjectWriter(names);
        try
        {
            writer.WriteIndirectObject(id, value);
            return Encoding.Latin1.GetString(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes a value and parses it back.</summary>
    /// <param name="value">The value.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The parsed value.</returns>
    private static PdfValue RoundTrip(PdfValue value, PdfNameTable names)
    {
        var parser = new PdfParser(Encoding.Latin1.GetBytes(Write(value, names)), 0, null, names);
        return parser.ParseValue();
    }
}
