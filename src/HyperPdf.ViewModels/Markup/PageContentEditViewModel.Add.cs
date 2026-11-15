// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The part of Edit mode that adds new objects to the page's own content.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>How much of the page's width a new shape takes.</summary>
    private const float NewShapeShare = 0.3F;

    /// <summary>The height of a new shape as a share of its width.</summary>
    private const float NewShapeHeight = 0.6F;

    /// <summary>The size of the text of a new line of words, in points.</summary>
    private const float NewTextSize = 18F;

    /// <summary>The width a new line of words is given before the page says how wide it is, in points.</summary>
    private const float NewTextWidth = 120F;

    /// <summary>The line width of a new outline, in points.</summary>
    private const float NewLineWidth = 2F;

    /// <summary>Half of a length.</summary>
    private const float HalfShare = 0.5F;

    /// <summary>The colour of a new outline, 0xRRGGBB: a dark blue.</summary>
    private const uint NewLineColour = 0x1F3A93;

    /// <summary>The colour of a new fill, 0xRRGGBB: a light blue.</summary>
    private const uint NewFillColour = 0xBBDEFB;

    /// <summary>How much of the page's width a new picture may take.</summary>
    private const float PictureShare = 0.4F;

    /// <summary>The colour of new text, 0xRRGGBB: black.</summary>
    private const uint NewTextColour = 0x000000;

    /// <summary>The words of a new line of text, which a double-click then retypes.</summary>
    private const string NewWords = "New text";

    /// <summary>The shortest a line of text on the page may be to be picked after it is added, in points.</summary>
    private const float PickTolerance = 3F;

    /// <summary>Gets or sets the font family new text is set in.</summary>
    [Reactive]
    public partial PageTextFamily NewTextFamily { get; set; }

    /// <summary>Gets or sets a value indicating whether new text is bold.</summary>
    [Reactive]
    public partial bool NewTextBold { get; set; }

    /// <summary>Gets or sets a value indicating whether new text is italic.</summary>
    [Reactive]
    public partial bool NewTextItalic { get; set; }

    /// <summary>Adds a new object in the middle of the current page, then picks it so its handles show.</summary>
    /// <param name="kind">What to add.</param>
    /// <returns><see langword="true"/> when Edit mode is on and the object was asked for.</returns>
    public bool AddObject(PageObjectDraftKind kind)
    {
        if (!IsEditing || TextEdit is not null || Editor is null)
        {
            return false;
        }

        var page = Math.Clamp(_owner.CurrentPageIndex, 0, Math.Max(0, _owner.Source.PageSizes.Length - 1));
        _ = AddAsync(page, DraftFor(kind, _owner.Source.PageSizes[page], NewTextFamily, NewTextBold, NewTextItalic));
        return true;
    }

    /// <summary>Makes the new object's description: its box in the middle of the page and the colours a new object starts with.</summary>
    /// <param name="kind">What to add.</param>
    /// <param name="size">The size of the page.</param>
    /// <param name="family">The font family of new text.</param>
    /// <param name="bold">Whether new text is bold.</param>
    /// <param name="italic">Whether new text is italic.</param>
    /// <returns>The draft.</returns>
    private static PageObjectDraft DraftFor(PageObjectDraftKind kind, PageSize size, PageTextFamily family, bool bold, bool italic)
    {
        var width = kind == PageObjectDraftKind.Text ? NewTextWidth : size.Width * NewShapeShare;
        var height = kind == PageObjectDraftKind.Text ? NewTextSize : width * NewShapeHeight;
        var box = new PageRect((size.Width - width) * HalfShare, (size.Height - height) * HalfShare, width, height);
        return kind switch
        {
            PageObjectDraftKind.Line => new(kind, box, false, 0, true, NewLineColour, NewLineWidth, string.Empty, 0F, PageTextFamily.Sans, false, false),
            PageObjectDraftKind.Text => new(kind, box, true, NewTextColour, false, 0, 0F, NewWords, NewTextSize, family, bold, italic),
            _ => new(kind, box, true, NewFillColour, true, NewLineColour, NewLineWidth, string.Empty, 0F, PageTextFamily.Sans, false, false),
        };
    }

    /// <summary>Adds a rectangle to the current page, in the middle of it.</summary>
    [ReactiveCommand]
    private void AddRectangle() => _ = AddObject(PageObjectDraftKind.Rectangle);

    /// <summary>Adds an ellipse to the current page, in the middle of it.</summary>
    [ReactiveCommand]
    private void AddEllipse() => _ = AddObject(PageObjectDraftKind.Ellipse);

    /// <summary>Adds a line to the current page, in the middle of it.</summary>
    [ReactiveCommand]
    private void AddLine() => _ = AddObject(PageObjectDraftKind.Line);

    /// <summary>Adds a line of text to the current page, in the middle of it.</summary>
    [ReactiveCommand]
    private void AddText() => _ = AddObject(PageObjectDraftKind.Text);

    /// <summary>Asks for a picture file and places it on the current page, in the middle of it.</summary>
    /// <returns>A task completing when the picture is placed.</returns>
    [ReactiveCommand]
    private async Task AddPictureAsync()
    {
        if (!IsEditing || TextEdit is not null || Editor is not { } editor)
        {
            return;
        }

        if (await _owner.Annotations.ChoosePictureInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return;
        }

        var page = Math.Clamp(_owner.CurrentPageIndex, 0, Math.Max(0, _owner.Source.PageSizes.Length - 1));
        var size = _owner.Source.PageSizes[page];
        var side = size.Width * PictureShare;
        var area = new PageRect((size.Width - side) * HalfShare, (size.Height - side) * HalfShare, side, side);
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, _lifetime).ConfigureAwait(true);
            await Task.Run(() => editor.AddPictureAsync(page, area, bytes, _lifetime).AsTask(), _lifetime).ConfigureAwait(true);
            _ = _pages.Remove(page);
            await LoadAsync(page).ConfigureAwait(true);
            Selected = FindLast(page, PageObjectKind.Image);
            _ = LoadStyleAsync();
            _owner.ReportPageChange(page);
            Status = UiText.Get("Added. Drag its handles to size it, or drag it to move it.");
            MarksVersion++;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or NotSupportedException or ArgumentOutOfRangeException)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("That picture could not be read. Choose a PNG, JPEG, BMP or WebP file.")} {ex.Message}"));
        }
    }

    /// <summary>Writes the new object, reads the page again and picks what was written.</summary>
    /// <param name="page">The page.</param>
    /// <param name="draft">What to add.</param>
    /// <returns>A task completing when the page is read again.</returns>
    private async Task AddAsync(int page, PageObjectDraft draft)
    {
        try
        {
            await Task.Run(() => Editor!.AddAsync(page, draft, _lifetime).AsTask(), _lifetime).ConfigureAwait(true);
            _ = _pages.Remove(page);
            await LoadAsync(page).ConfigureAwait(true);
            Selected = FindNewest(page, draft);
            _ = LoadStyleAsync();
            _owner.ReportPageChange(page);
            Status = UiText.Get("Added. Drag its handles to size it, or drag it to move it.");
            MarksVersion++;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not edit the page.")} {ex.Message}"));
        }
    }

    /// <summary>Finds the last object of a kind on a page, which is the one just added.</summary>
    /// <param name="page">The page.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The pick, or <see langword="null"/> when the page lists none.</returns>
    private PageObjectPick? FindLast(int page, PageObjectKind kind)
    {
        if (!_pages.TryGetValue(page, out var objects))
        {
            return null;
        }

        for (var i = objects.Length - 1; i >= 0; i--)
        {
            if (objects[i].Kind == kind)
            {
                return new(page, objects[i]);
            }
        }

        return null;
    }

    /// <summary>Finds the object just added: the last one of its kind that lies where the draft was put.</summary>
    /// <param name="page">The page.</param>
    /// <param name="draft">What was added.</param>
    /// <returns>The pick, or <see langword="null"/> when the page does not list it.</returns>
    private PageObjectPick? FindNewest(int page, in PageObjectDraft draft)
    {
        if (!_pages.TryGetValue(page, out var objects))
        {
            return null;
        }

        var kind = draft.Kind == PageObjectDraftKind.Text ? PageObjectKind.Text : PageObjectKind.Drawing;
        for (var i = objects.Length - 1; i >= 0; i--)
        {
            var bounds = objects[i].Bounds;
            if (objects[i].Kind == kind && MathF.Abs(bounds.Left - draft.Bounds.Left) <= PickTolerance && MathF.Abs(bounds.Top - draft.Bounds.Top) <= draft.Bounds.Height)
            {
                return new(page, objects[i]);
            }
        }

        return null;
    }
}
