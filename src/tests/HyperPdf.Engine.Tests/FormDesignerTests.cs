// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests designing form fields through a HyperPDF document.</summary>
public sealed class FormDesignerTests
{
    /// <summary>The name of the text field.</summary>
    private const string TextName = "Name";

    /// <summary>The left edge of the first widget.</summary>
    private const float Left = 72;

    /// <summary>The top edge of the first widget.</summary>
    private const float Top = 100;

    /// <summary>The width of a widget.</summary>
    private const float Width = 200;

    /// <summary>The height of a widget.</summary>
    private const float Height = 24;

    /// <summary>How far a moved rectangle may differ, in points.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The character limit set by a change.</summary>
    private const int Limit = 20;

    /// <summary>A page the document does not have.</summary>
    private const int MissingPage = 5;

    /// <summary>The name after the rename.</summary>
    private const string PersonName = "Person.Name";

    /// <summary>A field can be created, read back with its widget where it was placed, changed, renamed and removed, each as one undo step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldIsCreatedChangedRenamedAndRemoved()
    {
        using var pair = new HyperPdfTestDocument(TestPdf.Create(1));
        var designer = DocumentFeature<IFormDesigner>.Get(pair.Document)!;
        var bounds = new PageRect(Left, Top, Width, Height);

        var created = await designer.CreateFieldAsync(new(FormFieldKind.Text, TextName, [new(0, bounds)]) { Value = "Ada" }, CancellationToken.None);
        var fields = await designer.GetFieldsAsync(CancellationToken.None);
        var widget = fields.Single().Widgets.Single();

        await Assert.That(created.Succeeded).IsTrue();
        await Assert.That(fields.Single().Kind).IsEqualTo(FormFieldKind.Text);
        await Assert.That(fields.Single().Value).IsEqualTo("Ada");
        await Assert.That(widget.Bounds.Left).IsEqualTo(Left).Within(Tolerance);
        await Assert.That(widget.Bounds.Top).IsEqualTo(Top).Within(Tolerance);
        await Assert.That(widget.Bounds.Width).IsEqualTo(Width).Within(Tolerance);

        var changed = await designer.ChangeFieldAsync(TextName, new() { Value = "Grace", MaxLength = Limit }, CancellationToken.None);
        var renamed = await designer.RenameFieldAsync(TextName, PersonName, CancellationToken.None);
        fields = await designer.GetFieldsAsync(CancellationToken.None);

        await Assert.That(changed.Succeeded).IsTrue();
        await Assert.That(renamed.Succeeded).IsTrue();
        await Assert.That(fields.Single().Name).IsEqualTo(PersonName);
        await Assert.That(fields.Single().Value).IsEqualTo("Grace");
        await Assert.That(fields.Single().MaxLength).IsEqualTo(Limit);

        var moved = await designer.MoveWidgetAsync(PersonName, 0, new(Left, Top + Height, Width, Height), CancellationToken.None);
        var removed = await designer.RemoveFieldAsync(PersonName, CancellationToken.None);

        await Assert.That(moved.Succeeded).IsTrue();
        await Assert.That(removed.Succeeded).IsTrue();
        await Assert.That((await designer.GetFieldsAsync(CancellationToken.None)).Count).IsEqualTo(0);
    }

    /// <summary>A name that is already used and a field that does not exist are refused with a reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusalsComeBackWithAReason()
    {
        using var pair = new HyperPdfTestDocument(TestPdf.Create(1));
        var designer = DocumentFeature<IFormDesigner>.Get(pair.Document)!;
        var design = new FormFieldDesign(FormFieldKind.Text, TextName, [new(0, new(Left, Top, Width, Height))]);
        _ = await designer.CreateFieldAsync(design, CancellationToken.None);

        var duplicate = await designer.CreateFieldAsync(design, CancellationToken.None);
        var missing = await designer.RemoveFieldAsync("Nothing", CancellationToken.None);
        var offPage = await designer.CreateFieldAsync(design with { Name = "Other", Widgets = [new(MissingPage, new(Left, Top, Width, Height))] }, CancellationToken.None);

        await Assert.That(duplicate.Succeeded).IsFalse();
        await Assert.That(duplicate.Problem).IsNotNull();
        await Assert.That(missing.Succeeded).IsFalse();
        await Assert.That(missing.Problem).IsNotNull();
        await Assert.That(offPage.Succeeded).IsFalse();
    }
}
