// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests for when a file is repaired and which copy of an object wins afterwards.</summary>
public sealed class XrefRepairTests
{
    /// <summary>The page tree's object number.</summary>
    private const int PagesNumber = 2;

    /// <summary>The cross-reference stream entry type of an object packed in an object stream.</summary>
    private const byte CompressedType = 2;

    /// <summary>The catalog object.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree object.</summary>
    private const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>The trailer entries of the sample files.</summary>
    private const string Trailer = "/Root 1 0 R";

    /// <summary>The number of an object that is written as <c>null</c>.</summary>
    private const int NullObject = 3;

    /// <summary>The number of an ordinary object that the table can point at the wrong place.</summary>
    private const int PlainObject = 4;

    /// <summary>The number of a second ordinary object.</summary>
    private const int SecondObject = 5;

    /// <summary>The value of <see cref="PlainObject"/>.</summary>
    private const int PlainValue = 44;

    /// <summary>The /Size of the table sample.</summary>
    private const int TableSize = 6;

    /// <summary>An offset that holds no object header.</summary>
    private const int WrongOffset = 10;

    /// <summary>The object stream number in the trigger samples.</summary>
    private const int Container = 6;

    /// <summary>The number of the object packed in the stream.</summary>
    private const int Packed = 7;

    /// <summary>The value of the packed object.</summary>
    private const int PackedValue = 77;

    /// <summary>The first object stream in the ordering samples, which comes first in the file but has the higher number.</summary>
    private const int EarlyStream = 20;

    /// <summary>The second object stream in the ordering samples, which comes later in the file but has the lower number.</summary>
    private const int LateStream = 10;

    /// <summary>The object that both object streams hold.</summary>
    private const int Shared = 30;

    /// <summary>An object that a plain copy before an object stream and the stream both hold.</summary>
    private const int PlainBeforeStream = 31;

    /// <summary>An object that a plain copy after an object stream and the stream both hold.</summary>
    private const int PlainAfterStream = 32;

    /// <summary>The /Size of the xref stream sample.</summary>
    private const int StreamSize = 10;

    /// <summary>The xref stream's object number.</summary>
    private const int XrefObject = 9;

    /// <summary>The plain object the wrong compressed entry points away from.</summary>
    private const int Lost = 8;

    /// <summary>The value of <see cref="Lost"/>.</summary>
    private const int LostValue = 88;

    /// <summary>The first value of the ordering samples.</summary>
    private const int First = 1;

    /// <summary>The second value of the ordering samples.</summary>
    private const int Second = 2;

    /// <summary>The third value of the ordering samples.</summary>
    private const int Third = 3;

    /// <summary>An object parsed as <c>null</c> at the right place does not start a repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NullObjectAtRightPlaceDoesNotRepair()
    {
        using var store = StoreOpening.Open(TableFile([]), null);
        var value = StoreReading.GetObject(store, new(NullObject, 0));
        await Assert.That(value.IsNull).IsTrue();
        await Assert.That(store.WasRepaired).IsFalse();
    }

    /// <summary>An entry that points at no object header starts a repair, and the object is found by the rebuilt table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongOffsetRepairsAndFindsObject()
    {
        using var store = StoreOpening.Open(TableFile(new() { [PlainObject] = WrongOffset }), null);
        var value = StoreReading.GetObject(store, new(PlainObject, 0));
        await Assert.That(value.AsInteger()).IsEqualTo(PlainValue);
        await Assert.That(store.WasRepaired).IsTrue();
    }

    /// <summary>An entry that points at another object's header also starts a repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OffsetOfOtherObjectRepairs()
    {
        var wrong = new Dictionary<int, int>();
        var probe = TableFile(wrong);
        using (var first = StoreOpening.Open(probe, null))
        {
            await Assert.That(first.WasRepaired).IsFalse();
        }

        wrong[PlainObject] = OffsetOfObject(probe, SecondObject);
        using var store = StoreOpening.Open(TableFile(wrong), null);
        await Assert.That(StoreReading.GetObject(store, new(PlainObject, 0)).AsInteger()).IsEqualTo(PlainValue);
        await Assert.That(store.WasRepaired).IsTrue();
    }

    /// <summary>A repair keeps the objects already parsed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepairKeepsCachedObjects()
    {
        using var store = StoreOpening.Open(TableFile(new() { [PlainObject] = WrongOffset }), null);
        var before = StoreReading.GetObject(store, new(PagesNumber, 0)).AsDictionary();
        _ = StoreReading.GetObject(store, new(PlainObject, 0));
        var after = StoreReading.GetObject(store, new(PagesNumber, 0)).AsDictionary();
        await Assert.That(store.WasRepaired).IsTrue();
        await Assert.That(before).IsNotNull();
        await Assert.That(ReferenceEquals(before, after)).IsTrue();
    }

    /// <summary>A compressed entry whose object stream does not hold the object starts a repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompressedEntryNotInStreamRepairs()
    {
        using var store = StoreOpening.Open(XrefStreamFile(), null);
        var value = StoreReading.GetObject(store, new(Lost, 0));
        await Assert.That(value.AsInteger()).IsEqualTo(LostValue);
        await Assert.That(store.WasRepaired).IsTrue();
    }

    /// <summary>A compressed entry that the object stream does hold does not start a repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompressedEntryInStreamDoesNotRepair()
    {
        using var store = StoreOpening.Open(XrefStreamFile(), null);
        var value = StoreReading.GetObject(store, new(Packed, 0));
        await Assert.That(value.AsInteger()).IsEqualTo(PackedValue);
        await Assert.That(store.WasRepaired).IsFalse();
    }

    /// <summary>Of two object streams that hold an object, the one later in the file wins, even when its number is lower.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LaterObjectStreamWins()
    {
        using var store = StoreOpening.Open(OrderingFile(), null);
        await Assert.That(store.WasRepaired).IsTrue();
        await Assert.That(StoreReading.GetObject(store, new(Shared, 0)).AsInteger()).IsEqualTo(Second);
    }

    /// <summary>A plain object before an object stream loses to it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectStreamBeatsEarlierPlainObject()
    {
        using var store = StoreOpening.Open(OrderingFile(), null);
        await Assert.That(StoreReading.GetObject(store, new(PlainBeforeStream, 0)).AsInteger()).IsEqualTo(Second);
    }

    /// <summary>A plain object after an object stream beats it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainObjectAfterStreamWins()
    {
        using var store = StoreOpening.Open(OrderingFile(), null);
        await Assert.That(StoreReading.GetObject(store, new(PlainAfterStream, 0)).AsInteger()).IsEqualTo(Third);
    }

    /// <summary>Finds an object's offset in a file by searching for its header.</summary>
    /// <param name="file">The file.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The offset.</returns>
    private static int OffsetOfObject(byte[] file, int number) => file.AsSpan().IndexOf(Encoding.ASCII.GetBytes($"\n{number} 0 obj")) + 1;

    /// <summary>Builds a file with a classic table.</summary>
    /// <param name="wrongOffsets">Offsets to write instead of the real ones.</param>
    /// <returns>The file.</returns>
    private static byte[] TableFile(Dictionary<int, int> wrongOffsets) =>
        new RawPdf().Object(1, Catalog).Object(PagesNumber, Pages)
            .Object(NullObject, "null").Object(PlainObject, Digit(PlainValue))
            .Object(SecondObject, "55").Table(TableSize, Trailer, wrongOffsets).ToArray();

    /// <summary>Builds a file whose cross-reference stream puts one object in an object stream correctly and one wrongly.</summary>
    /// <returns>The file.</returns>
    private static byte[] XrefStreamFile()
    {
        var pdf = new RawPdf().Object(1, Catalog).Object(PagesNumber, Pages).Object(Lost, Digit(LostValue)).ObjectStream(Container, [Packed], [Digit(PackedValue)]);
        var self = pdf.Position;
        var entries = new List<RawPdf.XrefStreamEntry>();
        for (var number = 0; number < StreamSize; number++)
        {
            entries.Add(EntryFor(pdf, number, self));
        }

        return pdf.XrefStream(XrefObject, StreamSize, $"[0 {StreamSize}]", Trailer, entries).ToArray();
    }

    /// <summary>Gets the xref stream entry of an object in <see cref="XrefStreamFile"/>.</summary>
    /// <param name="pdf">The file so far.</param>
    /// <param name="number">The object number.</param>
    /// <param name="self">The cross-reference stream's own offset.</param>
    /// <returns>The entry.</returns>
    private static RawPdf.XrefStreamEntry EntryFor(RawPdf pdf, int number, int self) => number switch
    {
        0 => new(0, 0, ushort.MaxValue),
        1 or PagesNumber or Container => new(1, pdf.OffsetOf(number), 0),
        Packed or Lost => new(CompressedType, Container, 0),
        XrefObject => new(1, self, 0),
        _ => new(0, 0, 0),
    };

    /// <summary>
    /// Builds a file with no table. Object stream 20 comes first and holds 30, 31 and 32 with value 1; plain 31 sits before it
    /// and plain 32 after it. Object stream 10 comes later and holds 30 with value 2.
    /// </summary>
    /// <returns>The file.</returns>
    private static byte[] OrderingFile() =>
        new RawPdf().Object(
        1,
        Catalog).Object(
        PagesNumber,
        Pages).Object(
        PlainBeforeStream,
        "9").ObjectStream(
        EarlyStream,
        [Shared,
        PlainBeforeStream,
        PlainAfterStream],
        [Digit(First),
        Digit(First),
        Digit(First)]).Object(
        PlainAfterStream,
        Digit(Third)).ObjectStream(
        LateStream,
        [Shared,
        PlainBeforeStream],
        [Digit(Second),
        Digit(Second)]).Append("trailer\n<< /Size 40 /Root 1 0 R >>\n%%EOF\n").ToArray();

    /// <summary>Formats a small number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Digit(int value) => value.ToString(CultureInfo.InvariantCulture);
}
