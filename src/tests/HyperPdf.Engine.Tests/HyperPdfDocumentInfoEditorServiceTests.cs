// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests for <see cref="HyperPdfDocumentInfoEditorService"/>.</summary>
public sealed class HyperPdfDocumentInfoEditorServiceTests
{
    /// <summary>The document's title, author, subject and keywords change, and the document reads them back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SetInfo_ChangesTheDetailsTheDocumentReadsBack()
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(1));
        var editor = DocumentFeature<IDocumentInfoEditor>.Cast(test.Document)!;

        var result = await editor.SetInfo(new("A new title", "Someone", "What it is about", "one, two")).ToTask();
        var metadata = test.Document.GetMetadata();

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(metadata.Title).IsEqualTo("A new title");
        await Assert.That(metadata.Author).IsEqualTo("Someone");
        await Assert.That(metadata.Subject).IsEqualTo("What it is about");
        await Assert.That(metadata.Keywords).IsEqualTo("one, two");
    }
}
