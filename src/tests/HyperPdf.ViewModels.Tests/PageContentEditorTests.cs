// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that the objects a page is made of can be listed, moved, changed and removed, and that each edit can be undone.</summary>
public sealed class PageContentEditorTests
{
    /// <summary>The distance the move test shifts an object, in points.</summary>
    private const float Shift = 40;

    /// <summary>The text the replace test writes.</summary>
    private const string Replacement = "Changed words";

    /// <summary>Lists the text of a tagged document as objects with a place on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsTheTextObjectsOfAPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-list.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;

        var objects = await editor.ReadObjectsAsync(0, CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(objects.Length).IsGreaterThan(0);
            await Assert.That(Array.Exists(objects, static o => o.Kind == PageObjectKind.Text && o.Text.Length > 0 && o.Bounds.Width > 0)).IsTrue();
        }
    }

    /// <summary>Moves a text object down and right, then takes the move back with undo.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesAnObjectAndUndoPutsItBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-move.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(static o => o.Kind == PageObjectKind.Text);

        await editor.MoveAsync(0, before.Index, Shift, Shift, CancellationToken.None);
        var moved = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(o => o.Text == before.Text);
        var manager = DocumentFeature<IPageManagementSource>.Get(tab.TryGetDocument())!.PageManager;
        var undone = await manager.UndoAsync(CancellationToken.None);
        var restored = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(o => o.Text == before.Text);

        using (Assert.Multiple())
        {
            await Assert.That(moved.Bounds.Left).IsEqualTo(before.Bounds.Left + Shift).Within(1);
            await Assert.That(moved.Bounds.Top).IsEqualTo(before.Bounds.Top + Shift).Within(1);
            await Assert.That(undone).IsTrue();
            await Assert.That(restored.Bounds.Left).IsEqualTo(before.Bounds.Left).Within(1);
            await Assert.That(restored.Bounds.Top).IsEqualTo(before.Bounds.Top).Within(1);
        }
    }

    /// <summary>Replaces the words of a text object in place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacesTheWordsOfATextObject()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-replace.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(static o => o.Kind == PageObjectKind.Text);

        await editor.ReplaceTextAsync(0, before.Index, Replacement, CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var written = Array.Find(after, static o => o.Text.Contains(Replacement, StringComparison.Ordinal));

        using (Assert.Multiple())
        {
            await Assert.That(Array.Exists(after, o => o.Text == before.Text)).IsFalse();
            await Assert.That(written.Text).IsNotNull();
            await Assert.That(written.Bounds.Left).IsEqualTo(before.Bounds.Left).Within(Shift);
        }
    }

    /// <summary>Removes an object from the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletesAnObject()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-delete.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var target = before.First(static o => o.Kind == PageObjectKind.Text);

        await editor.DeleteAsync(0, target.Index, CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);

        await Assert.That(Array.Exists(after, o => o.Text == target.Text)).IsFalse();
    }
}
