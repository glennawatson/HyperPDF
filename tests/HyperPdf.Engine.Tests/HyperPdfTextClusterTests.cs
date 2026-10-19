// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that the engine keeps clusters whole when it hands out text: a conjunct is not split by a selection that ends inside it.</summary>
public sealed class HyperPdfTextClusterTests
{
    /// <summary>The page index of the only page.</summary>
    private const int FirstPage = 0;

    /// <summary>The characters of the conjunct.</summary>
    private const int ConjunctLength = 3;

    /// <summary>The index of the virama.</summary>
    private const int Virama = 1;

    /// <summary>Verifies that the page text is the conjunct, a space and "ok".</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConjunctPage_HasTheConjunctAsText()
    {
        using var input = new HyperPdfTestDocument(TextSearchSamples.CreateConjunctPage());

        var text = input.Document.GetText(FirstPage, 0, input.Document.GetCharacterCount(FirstPage));

        await Assert.That(text).IsEqualTo(TextSearchSamples.ConjunctText);
    }

    /// <summary>Verifies that a selection that starts or ends inside the conjunct copies the whole conjunct.</summary>
    /// <param name="start">The first character of the selection.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, 1)]
    [Arguments(Virama, 1)]
    [Arguments(Virama, ConjunctLength - Virama)]
    [Arguments(ConjunctLength - 1, 1)]
    public async Task SelectionInsideAConjunct_CopiesTheWholeConjunct(int start, int count)
    {
        using var input = new HyperPdfTestDocument(TextSearchSamples.CreateConjunctPage());

        var text = input.Document.GetText(FirstPage, start, count);

        await Assert.That(text).IsEqualTo(TextSearchSamples.ConjunctText[..ConjunctLength]);
    }

    /// <summary>Verifies that a selection of the letters after the conjunct copies only those letters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SelectionAfterTheConjunct_CopiesOnlyItsOwnLetters()
    {
        using var input = new HyperPdfTestDocument(TextSearchSamples.CreateConjunctPage());

        var text = input.Document.GetText(FirstPage, ConjunctLength + 1, "ok".Length);

        await Assert.That(text).IsEqualTo("ok");
    }
}
