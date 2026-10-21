// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks that hover answers over form fields, notes and comment badges stay the same as the tab's content changes.</summary>
public sealed class PointerHoverTests
{
    /// <summary>Half, for finding the middle of a rectangle.</summary>
    private const float Half = 0.5F;

    /// <summary>The text typed into the field.</summary>
    private const string Typed = "Ada";

    /// <summary>A point far outside every page.</summary>
    private static readonly PagePoint OffPage = new(-1000, -1000);

    /// <summary>The first corner of the shape that carries the comment.</summary>
    private static readonly PagePoint ShapeStart = new(100, 300);

    /// <summary>The second corner of the shape that carries the comment.</summary>
    private static readonly PagePoint ShapeEnd = new(220, 380);

    /// <summary>A field found by a hover test shows the text typed into it after the edit, not a copy from before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldHitTestSeesAnEditMadeUnderAStillPointer()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "form.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateForm());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        List<FormField> fields = [];
        DocumentFeature<IFormFiller>.Cast(tab.TryGetDocument()!)!.GetFields(0, fields);
        var field = fields.First(static f => f.Kind == FormFieldKind.Text && !f.IsReadOnly);
        var inside = new PagePoint(field.Bounds.Left + 1, field.Bounds.Top + 1);

        var before = tab.Forms.HitTest(0, inside);
        var again = tab.Forms.HitTest(0, inside);
        _ = tab.Forms.Activate(before!);
        tab.Forms.EditText = Typed;
        tab.Forms.Commit();
        var after = tab.Forms.HitTest(0, inside);

        await Assert.That(before).IsNotNull();
        await Assert.That(again!.Index).IsEqualTo(before!.Index);
        await Assert.That(after).IsNotNull();
        await Assert.That(after!.Value).IsEqualTo(Typed);
        await Assert.That(tab.Forms.HitTest(0, OffPage)).IsNull();
    }

    /// <summary>The single-read comment test agrees with the badge and note tests as a comment is added and the annotation is removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommentTriggerAgreesWithBadgeAndNoteTestsAcrossEdits()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("comment.pdf", 1)]);
        var tab = main.SelectedTab!;
        _ = tab.Annotations.AddShape(0, AnnotationKind.Rectangle, ShapeStart, ShapeEnd);
        var editor = DocumentFeature<IAnnotationEditor>.Cast(tab.TryGetDocument()!)!;
        List<PageAnnotation> found = [];
        editor.GetAnnotations(0, found);
        var shape = found.Single();
        var area = AnnotationsViewModel.CommentBadgeBounds(shape);
        var badge = new PagePoint(area.Left + (area.Width * Half), area.Top + (area.Height * Half));

        var plain = tab.Annotations.IsOverCommentTrigger(0, badge);
        _ = editor.SetContents(0, shape.Index, "Check this");
        var commented = tab.Annotations.IsOverCommentTrigger(0, badge);
        var agrees = commented == (tab.Annotations.HitTestCommentBadge(0, badge) is not null || tab.Annotations.HitTest(0, badge) is { Kind: AnnotationKind.Note });
        _ = editor.Remove(0, shape.Index);
        var removed = tab.Annotations.IsOverCommentTrigger(0, badge);

        await Assert.That(plain).IsFalse();
        await Assert.That(commented).IsTrue();
        await Assert.That(agrees).IsTrue();
        await Assert.That(removed).IsFalse();
    }
}
