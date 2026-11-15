// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The commands of Edit mode that work on a picked picture: drawing another picture in its place, cropping it and saving it as a file.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>The separator between the side and the percent in the words a crop command takes, such as <c>left:10</c>.</summary>
    private const char CropSeparator = ':';

    /// <summary>The name that stands for every side in the words a crop command takes.</summary>
    private const string EverySide = "all";

    /// <summary>The percent that means the whole of the picture.</summary>
    private const float WholePicture = 100F;

    /// <summary>The percent that, cut off two opposite sides, would leave nothing of the picture.</summary>
    private const float HalfPicture = 50F;

    /// <summary>Gets the interaction that asks for the target of a link; the output is the words typed, or <see langword="null"/> when the question is given up.</summary>
    public Interaction<TextPrompt, string?> LinkPromptInteraction { get; } = new();

    /// <summary>Reads the side and percent a crop command is given.</summary>
    /// <param name="how">The words, such as <c>left:10</c>.</param>
    /// <param name="shares">Receives the shares of the picture cut off each side.</param>
    /// <returns><see langword="true"/> when the words name a side and a percent that leaves some of the picture.</returns>
    private static bool TryReadCrop(string how, out PageMargins shares)
    {
        shares = default;
        var parts = how.Split(CropSeparator);
        if (parts.Length != 2 || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || percent is <= 0 or >= HalfPicture)
        {
            return false;
        }

        var share = percent / WholePicture;
        shares = parts[0] switch
        {
            "left" => new(share, 0, 0, 0),
            "top" => new(0, share, 0, 0),
            "right" => new(0, 0, share, 0),
            "bottom" => new(0, 0, 0, share),
            EverySide => new(share, share, share, share),
            _ => default,
        };
        return shares != default;
    }

    /// <summary>Asks for a web address or a page and makes the picked object a link to it.</summary>
    /// <returns>A task completing when the link is added or the question is given up.</returns>
    [ReactiveCommand]
    private async Task AddLinkToPickedAsync()
    {
        if (!CanShape(out var picked))
        {
            Status = UiText.Get("Pick something first.");
            return;
        }

        var asked = new TextPrompt(UiText.Get("Add a link"), UiText.Get("A web address such as https://example.com, or page: and a page number"), string.Empty, UiText.Get("Add the link"), false);
        if (await LinkPromptInteraction.Handle(asked).ToTask().ConfigureAwait(true) is not { Length: > 0 } target)
        {
            return;
        }

        if (_owner.Annotations.AddLink(picked.Page, picked.Object.Bounds, target.Trim()))
        {
            _owner.ReportPageChange(picked.Page);
            Status = UiText.Get("Added the link.");
            return;
        }

        _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("That is not a web address or a page of this document. Type an address such as https://example.com, or page: and a page number.")));
    }

    /// <summary>Asks for another picture file and draws it in place of the picked picture, in the same frame.</summary>
    /// <returns>A task completing when the picture is replaced or the choice is given up.</returns>
    [ReactiveCommand]
    private async Task ReplacePickedPictureAsync()
    {
        if (!CanShape(out var picked) || picked.Object.Kind != PageObjectKind.Image)
        {
            Status = UiText.Get("Pick a picture first.");
            return;
        }

        if (await _owner.Annotations.ChoosePictureInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, _lifetime).ConfigureAwait(true);
        }
        catch (IOException ex)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("That picture could not be read.")} {ex.Message}"));
            return;
        }

        var done = UiText.Get("Replaced the picture.");
        await EditAsync(picked, _ => Editor!.ReplacePictureAsync(picked.Page, picked.Object.Index, bytes, _lifetime).AsTask(), picked.Object.Bounds, done).ConfigureAwait(true);
    }

    /// <summary>Crops the picked picture by a share of its size, from one side or from every side. What is cut off stays stored and is only hidden.</summary>
    /// <param name="how">The side and the percent, such as <c>left:10</c>; the side is left, top, right, bottom or all.</param>
    [ReactiveCommand]
    private void CropPickedPicture(string how)
    {
        if (!CanShape(out var picked) || picked.Object.Kind != PageObjectKind.Image || !TryReadCrop(how, out var shares))
        {
            return;
        }

        var bounds = picked.Object.Bounds;
        var kept = new PageRect(
            bounds.Left + (bounds.Width * shares.Left),
            bounds.Top + (bounds.Height * shares.Top),
            bounds.Width * (1F - shares.Left - shares.Right),
            bounds.Height * (1F - shares.Top - shares.Bottom));
        _ = EditAsync(picked, _ => Editor!.CropPictureAsync(picked.Page, picked.Object.Index, shares, _lifetime).AsTask(), kept, UiText.Get("Cropped the picture."));
    }

    /// <summary>Asks where to save the picked picture and writes it there: as the JPEG the document holds, or as a PNG.</summary>
    /// <returns>A task completing when the picture is saved or the choice is given up.</returns>
    [ReactiveCommand]
    private async Task SavePickedPictureAsync()
    {
        if (!CanShape(out var picked) || picked.Object.Kind != PageObjectKind.Image || Editor is not { } editor)
        {
            Status = UiText.Get("Pick a picture first.");
            return;
        }

        try
        {
            var file = await Task.Run(() => editor.ExtractPictureAsync(picked.Page, picked.Object.Index, _lifetime).AsTask(), _lifetime).ConfigureAwait(true);
            var name = string.Create(CultureInfo.InvariantCulture, $"picture-{picked.Page + 1}.{file.Extension}");
            if (await _owner.PageTools.SaveImageInteraction.Handle(name).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
            {
                return;
            }

            await File.WriteAllBytesAsync(path, file.Data, _lifetime).ConfigureAwait(true);
            Status = UiText.Get("Saved the picture.");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not save the picture.")} {ex.Message}"));
        }
    }
}
