// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks that the structure index finds elements by object number, dictionary and element id alike.</summary>
public sealed class StructureIndexTests
{
    /// <summary>The object number of the first element.</summary>
    private const int FirstNumber = 7;

    /// <summary>The object number of the second element.</summary>
    private const int SecondNumber = 9;

    /// <summary>An object number no element has.</summary>
    private const int MissingNumber = 99;

    /// <summary>The number of elements the first test adds.</summary>
    private const int Both = 2;

    /// <summary>An element is found by its object number and by its dictionary, and both give the same element.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectNumberAndDictionaryFindTheSameElement()
    {
        var index = new StructureIndex();
        var first = Add(index, FirstNumber);
        var second = Add(index, SecondNumber);

        await Assert.That(ReferenceEquals(index.Find(new PdfObjectId(FirstNumber, 0)), first)).IsTrue();
        await Assert.That(ReferenceEquals(index.Find(new PdfObjectId(SecondNumber, 0)), second)).IsTrue();
        await Assert.That(ReferenceEquals(index.Find(first.Dictionary), first)).IsTrue();
        await Assert.That(ReferenceEquals(index.Find(second.Dictionary), second)).IsTrue();
        await Assert.That(index.Find(new PdfObjectId(MissingNumber, 0))).IsNull();
        await Assert.That(index.Find(new PdfDictionary(null))).IsNull();
        await Assert.That(index.Count).IsEqualTo(Both);
    }

    /// <summary>An element written directly in its parent has no object number and is found only by its dictionary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DirectElementIsFoundByDictionary()
    {
        var index = new StructureIndex();
        var direct = new PdfStructureElement(new(null), default, null);
        index.Add(direct);

        await Assert.That(ReferenceEquals(index.Find(direct.Dictionary), direct)).IsTrue();
        await Assert.That(index.Find(default(PdfObjectId))).IsNull();
        await Assert.That(index.Count).IsEqualTo(1);
    }

    /// <summary>The first element with an object number, a dictionary or an id keeps it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheFirstElementKeepsAKey()
    {
        var index = new StructureIndex();
        var dictionary = new PdfDictionary(null);
        var first = new PdfStructureElement(dictionary, new(FirstNumber, 0), null) { ElementId = "same" };
        var repeat = new PdfStructureElement(dictionary, new(FirstNumber, 0), null) { ElementId = "same" };
        index.Add(first);
        index.Add(repeat);

        await Assert.That(ReferenceEquals(index.Find(new PdfObjectId(FirstNumber, 0)), first)).IsTrue();
        await Assert.That(ReferenceEquals(index.Find(dictionary), first)).IsTrue();
        await Assert.That(ReferenceEquals(index.FindById("same"), first)).IsTrue();
    }

    /// <summary>Elements added after the map by dictionary was first used are found by dictionary too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ElementsAddedAfterTheFirstDictionaryLookupAreFound()
    {
        var index = new StructureIndex();
        var early = Add(index, FirstNumber);
        _ = index.Find(early.Dictionary);

        var late = Add(index, SecondNumber);

        await Assert.That(ReferenceEquals(index.Find(late.Dictionary), late)).IsTrue();
        await Assert.That(ReferenceEquals(index.Find(new PdfObjectId(SecondNumber, 0)), late)).IsTrue();
    }

    /// <summary>Adds an element written as its own object.</summary>
    /// <param name="index">The index.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The element.</returns>
    private static PdfStructureElement Add(StructureIndex index, int number)
    {
        var element = new PdfStructureElement(new(null), new(number, 0), null);
        index.Add(element);
        return element;
    }
}
