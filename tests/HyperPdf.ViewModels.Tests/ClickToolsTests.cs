// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks what clicking a page does for the annotation tools that work by one click.</summary>
public sealed class ClickToolsTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 2;

    /// <summary>Where the click lands.</summary>
    private static readonly PagePoint ClickAt = new(200, 200);

    /// <summary>The stamp tool puts a stamp where the page was clicked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StampToolAddsAStamp()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("click-stamp.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        annotations.Tool = AnnotationTool.Stamp;

        ClickTools.Run(annotations, 0, ClickAt, true);

        await Assert.That(annotations.Items.Count).IsEqualTo(1);
        await Assert.That(annotations.Items[0].Annotation.Kind).IsEqualTo(AnnotationKind.Stamp);
    }

    /// <summary>The caret tool puts an insertion mark where the page was clicked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretToolAddsACaret()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("click-caret.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        annotations.Tool = AnnotationTool.Caret;

        ClickTools.Run(annotations, 0, ClickAt, true);

        await Assert.That(annotations.Items.Count).IsEqualTo(1);
        await Assert.That(annotations.Items[0].Annotation.Kind).IsEqualTo(AnnotationKind.Caret);
    }

    /// <summary>A tool that does not work by one click leaves the page alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherToolsDoNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("click-other.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        annotations.Tool = AnnotationTool.Highlight;

        ClickTools.Run(annotations, 0, ClickAt, true);

        await Assert.That(annotations.Items.Count).IsEqualTo(0);
    }

    /// <summary>A missing annotation state is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingAnnotationsAreRefused() =>
        await Assert.That(static () => ClickTools.Run(null!, 0, default, true)).Throws<ArgumentNullException>();
}
