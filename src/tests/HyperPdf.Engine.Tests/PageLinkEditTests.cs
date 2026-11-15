// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that links added over an area of a page are read back as links to the address or page they were given.</summary>
public sealed class PageLinkEditTests
{
    /// <summary>The number of pages of the test document.</summary>
    private const int Pages = 3;

    /// <summary>The corner and size of the area every link covers.</summary>
    private const float Corner = 50F;

    /// <summary>The width and height of the area every link covers.</summary>
    private const float Size = 100F;

    /// <summary>The area the links cover.</summary>
    private static readonly PageRect Area = new(Corner, Corner, Size, Size);

    /// <summary>A link to a web address is read back as one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AWebLinkIsReadBack()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var editor = DocumentFeature<IAnnotationEditor>.Get(test.Document)!;

        var added = editor.AddLink(0, Area, "https://example.com/");
        var links = test.Document.GetLinks(0);

        await Assert.That(added).IsGreaterThanOrEqualTo(0);
        await Assert.That(links.Any(static link => link.Target.Kind == LinkTargetKind.Uri && link.Target.Uri == "https://example.com/")).IsTrue();
    }

    /// <summary>A link to a page is read back as one to that page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APageLinkIsReadBack()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var editor = DocumentFeature<IAnnotationEditor>.Get(test.Document)!;

        var added = editor.AddLink(0, Area, "page:3");
        var links = test.Document.GetLinks(0);

        await Assert.That(added).IsGreaterThanOrEqualTo(0);
        await Assert.That(links.Any(static link => link.Target.Kind == LinkTargetKind.Page && link.Target.PageIndex == Pages - 1)).IsTrue();
    }

    /// <summary>A target that is not an address, and a page that is not there, add nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ABadTargetAddsNothing()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var editor = DocumentFeature<IAnnotationEditor>.Get(test.Document)!;

        var before = test.Document.GetLinks(0).Count;

        await Assert.That(editor.AddLink(0, Area, "not an address")).IsEqualTo(-1);
        await Assert.That(editor.AddLink(0, Area, "page:99")).IsEqualTo(-1);
        await Assert.That(test.Document.GetLinks(0).Count).IsEqualTo(before);
    }
}
