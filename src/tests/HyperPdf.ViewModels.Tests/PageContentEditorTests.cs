// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that the objects a page is made of can be listed, moved, changed and removed, and that each edit can be undone.</summary>
public sealed class PageContentEditorTests
{
    /// <summary>The distance the move test shifts an object, in points.</summary>
    private const float Shift = 40;

    /// <summary>The factor the resize test scales by.</summary>
    private const float Double = 2;

    /// <summary>Two, for halves and quarter turns.</summary>
    private const float Two = 2;

    /// <summary>The pages of the document the page-to-page copy test opens.</summary>
    private const int TwoPages = 2;

    /// <summary>The lines of text on each page of that document.</summary>
    private const int TextLines = 3;

    /// <summary>How far down a copy is placed to land in a blank part of the page, in points.</summary>
    private const float FarDown = 400;

    /// <summary>How far a copy's box may lie from the original's moved box, in points, since the copy's box has room around the object.</summary>
    private const float CopyReach = 8;

    /// <summary>The left edge of the added objects, in points.</summary>
    private const float Left = 100;

    /// <summary>The top edge of the first added object, in points.</summary>
    private const float Top = 500;

    /// <summary>The side of an added shape, in points.</summary>
    private const float BoxSize = 60;

    /// <summary>How far the next added shape lies from the one before, in points.</summary>
    private const float Spread = 80;

    /// <summary>The width of the added text, in points.</summary>
    private const float TextWidth = 150;

    /// <summary>The size of the added text, in points.</summary>
    private const float TextSize = 18;

    /// <summary>The number of objects the test adds.</summary>
    private const int AddedCount = 4;

    /// <summary>A PNG picture of one red pixel.</summary>
    private const string OnePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    /// <summary>Some Greek words, which Windows ANSI cannot hold.</summary>
    private const string Greek = "Καλημέρα κόσμε";

    /// <summary>A line of Cyrillic letters, which Windows ANSI cannot hold.</summary>
    private const string Cyrillic = "Привет мир";

    /// <summary>The words that are added.</summary>
    private const string Added = "Added by the editor";

    /// <summary>Red as 0xRRGGBB.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>The alpha of an opaque colour.</summary>
    private const uint Opaque = 0xFF000000;

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
        var undone = await manager.Undo().ToTask();
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

    /// <summary>Sets a text object in a bold serif font at a new size, keeping its words and its place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SetsATextObjectInAnotherFontAndSize()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-restyle.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(static o => o.Kind == PageObjectKind.Text);

        await editor.RestyleTextAsync(0, before.Index, new(PageTextFamily.Serif, true, false, TextSize), CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var written = Array.Find(after, o => o.Kind == PageObjectKind.Text && o.Text.Contains(before.Text, StringComparison.Ordinal));
        var style = await editor.ReadStyleAsync(0, written.Index, CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(written.Text).IsNotNull();
            await Assert.That(style.FontSize).IsEqualTo(TextSize).Within(1);
            await Assert.That(written.Bounds.Left).IsEqualTo(before.Bounds.Left).Within(Shift);
        }
    }

    /// <summary>Duplicates an object further down the page, and one undo takes the copy away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DuplicatesAnObjectAndUndoTakesTheCopyAway()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-copy.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var before = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var target = Array.Find(before, static o => o.Kind == PageObjectKind.Text);

        await editor.CopyAsync(0, target.Index, 0, new(Shift, Shift), CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var copy = Array.Find(after, o => Math.Abs(o.Bounds.Left - (target.Bounds.Left + Shift)) < CopyReach && Math.Abs(o.Bounds.Top - (target.Bounds.Top + Shift)) < CopyReach);
        var manager = DocumentFeature<IPageManagementSource>.Get(tab.TryGetDocument())!.PageManager;
        _ = await manager.Undo().ToTask();
        var restored = await editor.ReadObjectsAsync(0, CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(after.Length).IsEqualTo(before.Length + 1);
            await Assert.That(copy.Text).IsEqualTo(target.Text);
            await Assert.That(copy.Bounds.Width).IsEqualTo(target.Bounds.Width).Within(CopyReach * Two);
            await Assert.That(restored.Length).IsEqualTo(before.Length);
        }
    }

    /// <summary>Duplicates text into a blank part of the page and checks the copy is really drawn there, with its own colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADuplicateIsDrawnWhereItWasPlaced()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-copy-ink.pdf", TestPdf.CreateTagged())]);
        var document = main.SelectedTab!.TryGetDocument()!;
        var editor = DocumentFeature<IPageContentEditor>.Get(document)!;
        var target = Array.Find(await editor.ReadObjectsAsync(0, CancellationToken.None), static o => o.Kind == PageObjectKind.Text);
        var blank = target.Bounds with { Top = target.Bounds.Top + FarDown };
        var empty = PageInk.Count(document, 0, blank, RenderFlags.None);

        await editor.CopyAsync(0, target.Index, 0, new(0, FarDown), CancellationToken.None);
        var drawn = PageInk.Count(document, 0, blank, RenderFlags.None);

        using (Assert.Multiple())
        {
            await Assert.That(empty).IsEqualTo(0);
            await Assert.That(drawn).IsGreaterThan(0);
        }
    }

    /// <summary>Copies a text object onto another page, where it is text with the same words, so it can be retyped there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACopyOnAnotherPageIsStillText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-copy-page.pdf", TestPdf.CreateTextFilled(TwoPages, TextLines))]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var target = Array.Find(await editor.ReadObjectsAsync(0, CancellationToken.None), static o => o.Kind == PageObjectKind.Text);
        var before = await editor.ReadObjectsAsync(1, CancellationToken.None);

        await editor.CopyAsync(0, target.Index, 1, new(0, FarDown), CancellationToken.None);
        var after = await editor.ReadObjectsAsync(1, CancellationToken.None);
        var copy = Array.Find(after, o => o.Kind == PageObjectKind.Text && o.Text == target.Text && o.Bounds.Top > target.Bounds.Top + Shift);

        using (Assert.Multiple())
        {
            await Assert.That(after.Length).IsEqualTo(before.Length + 1);
            await Assert.That(copy.Text).IsEqualTo(target.Text);
        }
    }

    /// <summary>Brings an object to the front: it keeps its words and place and is still listed once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BringsAnObjectToTheFront()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-front.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var target = Array.Find(before, static o => o.Kind == PageObjectKind.Text);

        await editor.BringToFrontAsync(0, target.Index, CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var found = Array.FindAll(after, o => o.Text == target.Text);

        using (Assert.Multiple())
        {
            await Assert.That(found.Length).IsEqualTo(1);
            await Assert.That(found[0].Bounds.Left).IsEqualTo(target.Bounds.Left).Within(Two);
            await Assert.That(found[0].Bounds.Top).IsEqualTo(target.Bounds.Top).Within(Two);
        }
    }

    /// <summary>Sends an object to the back: it keeps its words and place, is still listed once, and is the first object the page lists.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SendsAnObjectToTheBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-back.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var target = Array.FindLast(before, static o => o.Kind == PageObjectKind.Text);

        await editor.SendToBackAsync(0, target.Index, CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var found = Array.FindAll(after, o => o.Text == target.Text);

        using (Assert.Multiple())
        {
            await Assert.That(after.Length).IsEqualTo(before.Length);
            await Assert.That(found.Length).IsEqualTo(1);
            await Assert.That(found[0].Index).IsEqualTo(after[0].Index);
            await Assert.That(found[0].Bounds.Top).IsEqualTo(target.Bounds.Top).Within(Two);
        }
    }

    /// <summary>Moves the first text object one step forward, then one step back, and the order of the objects follows each step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesAnObjectOneStepForwardAndBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-step.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var firstText = before[0].Text;
        var secondText = before[1].Text;

        await editor.BringForwardAsync(0, before[0].Index, CancellationToken.None);
        var forward = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var movedAt = Array.FindIndex(forward, o => o.Text == firstText);
        var neighbourAt = Array.FindIndex(forward, o => o.Text == secondText);
        await editor.SendBackwardAsync(0, forward[movedAt].Index, CancellationToken.None);
        var back = await editor.ReadObjectsAsync(0, CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(forward.Length).IsEqualTo(before.Length);
            await Assert.That(neighbourAt).IsLessThan(movedAt);
            await Assert.That(back.Length).IsEqualTo(before.Length);
            await Assert.That(Array.FindIndex(back, o => o.Text == firstText)).IsLessThan(Array.FindIndex(back, o => o.Text == secondText));
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

    /// <summary>Makes a text object twice as wide and tall by a change in page space, and takes it back with undo.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResizesAnObjectAndUndoPutsItBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-resize.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(static o => o.Kind == PageObjectKind.Text);
        var change = HyperPdf.ViewModels.Markup.PageObjectHandles.Fit(before.Bounds, before.Bounds with { Width = before.Bounds.Width * Double, Height = before.Bounds.Height * Double });

        await editor.TransformAsync(0, before.Index, change, CancellationToken.None);
        var bigger = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(o => o.Text == before.Text);
        var manager = DocumentFeature<IPageManagementSource>.Get(tab.TryGetDocument())!.PageManager;
        var undone = await manager.Undo().ToTask();
        var restored = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(o => o.Text == before.Text);

        using (Assert.Multiple())
        {
            await Assert.That(bigger.Bounds.Width).IsEqualTo(before.Bounds.Width * Double).Within(1);
            await Assert.That(bigger.Bounds.Height).IsEqualTo(before.Bounds.Height * Double).Within(1);
            await Assert.That(bigger.Bounds.Left).IsEqualTo(before.Bounds.Left).Within(1);
            await Assert.That(bigger.Bounds.Top).IsEqualTo(before.Bounds.Top).Within(1);
            await Assert.That(undone).IsTrue();
            await Assert.That(restored.Bounds.Width).IsEqualTo(before.Bounds.Width).Within(1);
        }
    }

    /// <summary>Turns a text object a quarter turn about its middle, so its box swaps width and height.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TurnsAnObjectAboutItsMiddle()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-turn.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(static o => o.Kind == PageObjectKind.Text);
        var middle = new System.Numerics.Vector2(before.Bounds.Left + (before.Bounds.Width / Two), before.Bounds.Top + (before.Bounds.Height / Two));

        await editor.TransformAsync(0, before.Index, System.Numerics.Matrix3x2.CreateRotation(MathF.PI / Two, middle), CancellationToken.None);
        var after = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(o => o.Text == before.Text);

        using (Assert.Multiple())
        {
            await Assert.That(after.Bounds.Width).IsEqualTo(before.Bounds.Height).Within(1);
            await Assert.That(after.Bounds.Height).IsEqualTo(before.Bounds.Width).Within(1);
        }
    }

    /// <summary>Changes the fill colour of a text object and reads it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesTheFillColourOfAnObject()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-colour.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(static o => o.Kind == PageObjectKind.Text);

        await editor.SetColorsAsync(0, before.Index, new(true, Red, false, 0), CancellationToken.None);
        var after = (await editor.ReadObjectsAsync(0, CancellationToken.None)).First(o => o.Text == before.Text);
        var style = await editor.ReadStyleAsync(0, after.Index, CancellationToken.None);

        await Assert.That(style.Fill).IsEqualTo(Red | Opaque);
    }

    /// <summary>Places a one pixel picture on a page, which then lists a picture in the area it was given.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesAPictureOnAPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-picture.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var area = new HyperPdf.Core.Geometry.PageRect(Left, Top, BoxSize, BoxSize);

        await editor.AddPictureAsync(0, area, Convert.FromBase64String(OnePixelPng), CancellationToken.None);
        var picture = Array.Find(await editor.ReadObjectsAsync(0, CancellationToken.None), static o => o.Kind == PageObjectKind.Image);

        using (Assert.Multiple())
        {
            await Assert.That(picture.Bounds.Width).IsEqualTo(BoxSize).Within(Two);
            await Assert.That(picture.Bounds.Left).IsEqualTo(Left).Within(Two);
        }
    }

    /// <summary>Adds a rectangle, an ellipse, a line and some words to a page, which then lists them where they were put.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsShapesAndTextToAPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-add.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None)).Length;
        var box = new HyperPdf.Core.Geometry.PageRect(Left, Top, BoxSize, BoxSize);

        await editor.AddAsync(0, Shape(PageObjectDraftKind.Rectangle, box, true, true), CancellationToken.None);
        await editor.AddAsync(0, Shape(PageObjectDraftKind.Ellipse, box with { Left = Left + Spread }, true, false), CancellationToken.None);
        await editor.AddAsync(0, Shape(PageObjectDraftKind.Line, box with { Left = Left + Spread + Spread }, false, true), CancellationToken.None);
        await editor.AddAsync(0, Words(), CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var rectangle = Array.Find(after, static o => o.Kind == PageObjectKind.Drawing && Math.Abs(o.Bounds.Left - Left) < Two && Math.Abs(o.Bounds.Top - Top) < Two);
        var words = Array.Find(after, static o => o.Text == Added);

        using (Assert.Multiple())
        {
            await Assert.That(after.Length).IsGreaterThanOrEqualTo(before + AddedCount);
            await Assert.That(rectangle.Bounds.Width).IsEqualTo(BoxSize).Within(Two);
            await Assert.That(rectangle.Bounds.Height).IsEqualTo(BoxSize).Within(Two);
            await Assert.That(words.Text).IsEqualTo(Added).Because(string.Join("|", after.Select(static o => $"{o.Kind}:{o.Text}:{o.Bounds}")));
            await Assert.That(words.Bounds.Left).IsEqualTo(Left).Within(Two);
        }
    }

    /// <summary>Adds a line in Cyrillic, which the built-in fonts cannot show, and the page lists it as words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsALineOfLettersTheBuiltInFontsLack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-add-cyrillic.pdf", TestPdf.CreateTagged())]);
        var document = main.SelectedTab!.TryGetDocument()!;
        var editor = DocumentFeature<IPageContentEditor>.Get(document)!;
        var area = new HyperPdf.Core.Geometry.PageRect(Left, Top, TextWidth, TextSize);
        var draft = new PageObjectDraft(PageObjectDraftKind.Text, area, true, 0, false, 0, 0F, Cyrillic, TextSize, PageTextFamily.Sans, false, false);
        var empty = PageInk.Count(document, 0, area, RenderFlags.None);

        await editor.AddAsync(0, draft, CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);
        var inked = PageInk.Count(document, 0, area, RenderFlags.None);
        var text = string.Concat(after.Where(static o => o.Kind == PageObjectKind.Text).Select(static o => o.Text));

        using (Assert.Multiple())
        {
            await Assert.That(empty).IsEqualTo(0);
            await Assert.That(inked).IsGreaterThan(0);
            await Assert.That(text).Contains(Cyrillic).Because(string.Join("|", after.Select(static o => $"{o.Kind}:{o.Text}")));
            await Assert.That(Array.Exists(after, static o => o.Kind == PageObjectKind.Text && o.Text == Cyrillic)).IsTrue().Because(string.Join("|", after.Select(static o => $"{o.Kind}:{o.Text}")));
        }
    }

    /// <summary>Retypes a text object with Greek words, which the built-in fonts cannot show, and the page reads them back as one object.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetypesATextObjectWithLettersTheBuiltInFontsLack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("content-retype-greek.pdf", TestPdf.CreateTagged())]);
        var editor = DocumentFeature<IPageContentEditor>.Get(main.SelectedTab!.TryGetDocument())!;
        var target = Array.Find(await editor.ReadObjectsAsync(0, CancellationToken.None), static o => o.Kind == PageObjectKind.Text);

        await editor.ReplaceTextAsync(0, target.Index, Greek, CancellationToken.None);
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);

        await Assert.That(Array.Exists(after, static o => o.Kind == PageObjectKind.Text && o.Text == Greek)).IsTrue().Because(string.Join("|", after.Select(static o => o.Text)));
    }

    /// <summary>Makes the draft of a shape in red.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="box">Where it goes.</param>
    /// <param name="filled">Whether it is filled.</param>
    /// <param name="outlined">Whether it has an outline.</param>
    /// <returns>The draft.</returns>
    private static PageObjectDraft Shape(PageObjectDraftKind kind, HyperPdf.Core.Geometry.PageRect box, bool filled, bool outlined) =>
        new(kind, box, filled, Red, outlined, Red, Two, string.Empty, 0F, PageTextFamily.Sans, false, false);

    /// <summary>Makes the draft of a line of bold serif text.</summary>
    /// <returns>The draft.</returns>
    private static PageObjectDraft Words() =>
        new(PageObjectDraftKind.Text, new(Left, Top + Spread, TextWidth, TextSize), true, 0, false, 0, 0F, Added, TextSize, PageTextFamily.Serif, true, false);
}
