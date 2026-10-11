// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests ownership of temporary parser state.</summary>
public sealed class ParserStateTests
{
    /// <summary>The value held by the inner scratch stack.</summary>
    private const int InnerValue = 2;

    /// <summary>The number of values that forces scratch to spill past its inline storage.</summary>
    private const int WideArrayCount = 80;

    /// <summary>The data bytes in the self-referential stream.</summary>
    private const int StreamBodyLength = 3;

    /// <summary>Two nested scratch stacks keep their values separate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedScratchStacksKeepTheirOwnValues()
    {
        var result = ReadNestedScratch();
        await Assert.That(result.Outer).IsEqualTo(1);
        await Assert.That(result.Inner).IsEqualTo(InnerValue);
    }

    /// <summary>Concurrent parses keep nested arrays and dictionaries independent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentCompositeParsesKeepTheirOwnValues()
    {
        const int ParseCount = 64;
        var results = await Task.WhenAll(Enumerable.Range(0, ParseCount).Select(static number => Task.Run(() => ParseComposite(number))));

        for (var number = 0; number < results.Length; number++)
        {
            await Assert.That(results[number]).IsEqualTo(number);
        }
    }

    /// <summary>Values beyond the inline scratch capacity remain in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WideArrayKeepsAllValues()
    {
        var array = ParseWideArray();

        await Assert.That(array.Count).IsEqualTo(WideArrayCount);
        await Assert.That(array.GetRaw(0).AsInteger()).IsEqualTo(0);
        await Assert.That(array.GetRaw(WideArrayCount - 1).AsInteger()).IsEqualTo(WideArrayCount - 1);
    }

    /// <summary>Dictionary keys and values beyond the inline scratch capacity remain paired.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WideDictionaryKeepsKeysWithValues()
    {
        var result = ParseWideDictionary();

        await Assert.That(result.Dictionary.Count).IsEqualTo(WideArrayCount);
        await Assert.That(result.Dictionary.GetRaw(result.Names.Intern("K0")).AsInteger()).IsEqualTo(0);
        await Assert.That(result.Dictionary.GetRaw(result.Names.Intern($"K{WideArrayCount - 1}")).AsInteger()).IsEqualTo(WideArrayCount - 1);
    }

    /// <summary>One store's recursion limit does not prevent a different store from parsing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParseDepthBelongsToObjectStore()
    {
        using var first = CreateStore();
        using var second = CreateStore();
        first.ParseDepthState = StoreParsing.MaxParseDepth;

        var firstParsed = StoreParsing.TryParseObjectAt(first, 0, out _, out _);
        var secondParsed = StoreParsing.TryParseObjectAt(second, 0, out var secondId, out var secondValue);
        first.ParseDepthState = 0;
        var firstAfterReset = StoreParsing.TryParseObjectAt(first, 0, out _, out _);

        await Assert.That(firstParsed).IsFalse();
        await Assert.That(secondParsed).IsTrue();
        await Assert.That(secondId.Number).IsEqualTo(1);
        await Assert.That(secondValue.AsArray()!.Count).IsEqualTo(1);
        await Assert.That(firstAfterReset).IsTrue();
        await Assert.That(first.ParseDepthState).IsEqualTo(0);
        await Assert.That(second.ParseDepthState).IsEqualTo(0);
    }

    /// <summary>A stream that names itself as its length stops at the recursion limit and releases its depth.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelfReferentialStreamRestoresParseDepth()
    {
        var xref = new XrefTable();
        xref.Set(1, XrefEntryType.InFile, 0, 0);
        using var store = new PdfObjectStore(
            new MemoryPdfByteSource("1 0 obj << /Length 1 0 R >>\nstream\nabc\nendstream\nendobj"u8.ToArray()),
            true,
            xref);

        var parsed = StoreParsing.TryParseObjectAt(store, 0, out _, out var value);

        await Assert.That(parsed).IsTrue();
        await Assert.That(value.AsStream()!.RawLength).IsEqualTo(StreamBodyLength);
        await Assert.That(store.ParseDepthState).IsEqualTo(0);
    }

    /// <summary>Parses a composite that contains its own number.</summary>
    /// <param name="number">The number to parse.</param>
    /// <returns>The nested number.</returns>
    private static int ParseComposite(int number)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes($"<< /A [{number}] >>");
        var parser = new PdfParser(bytes, 0, null, new());
        return (int)parser.ParseValue().AsDictionary()!.GetArray(KnownName.A)!.GetRaw(0).AsInteger();
    }

    /// <summary>Parses an array wider than the scratch's inline storage.</summary>
    /// <returns>The array.</returns>
    private static PdfArray ParseWideArray()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes($"[{string.Join(" ", Enumerable.Range(0, WideArrayCount))}]");
        var parser = new PdfParser(bytes, 0, null, new());
        return parser.ParseValue().AsArray()!;
    }

    /// <summary>Parses a dictionary wider than the scratch's inline storage.</summary>
    /// <returns>The dictionary and its name table.</returns>
    private static WideDictionary ParseWideDictionary()
    {
        var entries = Enumerable.Range(0, WideArrayCount).Select(static number => $"/K{number} {number}");
        var bytes = System.Text.Encoding.ASCII.GetBytes($"<< {string.Join(" ", entries)} >>");
        var names = new PdfNameTable();
        var parser = new PdfParser(bytes, 0, null, names);
        return new(parser.ParseValue().AsDictionary()!, names);
    }

    /// <summary>Reads independent values from two scratch stacks active on one thread.</summary>
    /// <returns>The values.</returns>
    private static ScratchValues ReadNestedScratch()
    {
        ParseScratch outer = default;
        ParseScratch inner = default;
        outer.Push(0, PdfValue.FromInteger(1));
        inner.Push(0, PdfValue.FromInteger(InnerValue));
        return new((int)outer.PopArray(0, null).GetRaw(0).AsInteger(), (int)inner.PopArray(0, null).GetRaw(0).AsInteger());
    }

    /// <summary>Creates a store over one raw indirect object.</summary>
    /// <returns>The store.</returns>
    private static PdfObjectStore CreateStore() => new(new MemoryPdfByteSource("1 0 obj [7] endobj"u8.ToArray()), true, new XrefTable());

    /// <summary>The values held by two nested scratch stacks.</summary>
    /// <param name="Outer">The outer value.</param>
    /// <param name="Inner">The inner value.</param>
    private readonly record struct ScratchValues(int Outer, int Inner);

    /// <summary>A parsed wide dictionary and the names used to read it.</summary>
    /// <param name="Dictionary">The parsed dictionary.</param>
    /// <param name="Names">The document's name table.</param>
    private readonly record struct WideDictionary(PdfDictionary Dictionary, PdfNameTable Names);
}
