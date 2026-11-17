// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
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

    /// <summary>Writes the extracted picture to a file.</summary>
    /// <param name="path">Where to save it.</param>
    /// <param name="data">The picture's bytes.</param>
    /// <returns>The completion value.</returns>
    private static RxVoid WriteFile(string path, byte[] data)
    {
        File.WriteAllBytes(path, data);
        return RxVoid.Default;
    }

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
    /// <returns>A signal that ends when the link is added or the question is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> AddLinkToPicked()
    {
        if (!CanShape(out var picked))
        {
            Status = UiText.Get("Pick something first.");
            return Signal.Return(RxVoid.Default);
        }

        var asked = new TextPrompt(UiText.Get("Add a link"), UiText.Get("A web address such as https://example.com, or page: and a page number"), string.Empty, UiText.Get("Add the link"), false);
        return LinkPromptInteraction.Handle(asked).ToSignal()
            .Where(static target => target is { Length: > 0 })
            .Select(target => LinkChosen(picked, target!));
    }

    /// <summary>Makes the picked object a link to the target typed.</summary>
    /// <param name="picked">The picked object.</param>
    /// <param name="target">What was typed.</param>
    /// <returns>The completion value.</returns>
    private RxVoid LinkChosen(PageObjectPick picked, string target)
    {
        if (_owner.Annotations.AddLink(picked.Page, picked.Object.Bounds, target.Trim()))
        {
            _owner.ReportPageChange(picked.Page);
            Status = UiText.Get("Added the link.");
            return RxVoid.Default;
        }

        _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("That is not a web address or a page of this document. Type an address such as https://example.com, or page: and a page number.")));
        return RxVoid.Default;
    }

    /// <summary>Asks for another picture file and draws it in place of the picked picture, in the same frame.</summary>
    /// <returns>A signal that ends when the picture is replaced or the choice is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> ReplacePickedPicture()
    {
        if (!CanShape(out var picked) || picked.Object.Kind != PageObjectKind.Image)
        {
            Status = UiText.Get("Pick a picture first.");
            return Signal.Return(RxVoid.Default);
        }

        return _owner.Annotations.ChoosePictureInteraction.Handle(RxVoid.Default).ToSignal()
            .Where(static path => path is { Length: > 0 })
            .SwitchMap(path => ReplaceWith(picked, path!));
    }

    /// <summary>Reads the chosen picture file and draws it in place of the picked picture.</summary>
    /// <param name="picked">The picked picture.</param>
    /// <param name="path">The picture file.</param>
    /// <returns>A cold signal that ends when the picture is replaced or could not be read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> ReplaceWith(PageObjectPick picked, string path) =>
        Signal.Start(() => File.ReadAllBytes(path), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(bytes => Edit(picked, _ => Editor!.ReplacePicture(picked.Page, picked.Object.Index, bytes), picked.Object.Bounds, UiText.Get("Replaced the picture.")))
            .Catch<RxVoid, IOException>(Unreadable);

    /// <summary>Says the picture could not be read.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends at once.</returns>
    private IObservable<RxVoid> Unreadable(IOException error)
    {
        _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("That picture could not be read.")} {error.Message}"));
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Crops the picked picture or drawing by a share of its size, from one side or from every side. What is cut off stays stored and is only hidden.</summary>
    /// <param name="how">The side and the percent, such as <c>left:10</c>; the side is left, top, right, bottom or all.</param>
    [ReactiveCommand]
    private void CropPickedPicture(string how)
    {
        if (!CanShape(out var picked) || picked.Object.Kind == PageObjectKind.Text || !TryReadCrop(how, out var shares))
        {
            return;
        }

        var bounds = picked.Object.Bounds;
        var kept = new PageRect(
            bounds.Left + (bounds.Width * shares.Left),
            bounds.Top + (bounds.Height * shares.Top),
            bounds.Width * (1F - shares.Left - shares.Right),
            bounds.Height * (1F - shares.Top - shares.Bottom));
        var done = picked.Object.Kind == PageObjectKind.Image ? UiText.Get("Cropped the picture.") : UiText.Get("Cropped it.");
        Track(Edit(picked, _ => Editor!.CropPicture(picked.Page, picked.Object.Index, shares), kept, done));
    }

    /// <summary>Asks where to save the picked picture and writes it there: as the JPEG the document holds, or as a PNG.</summary>
    /// <returns>A signal that ends when the picture is saved or the choice is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> SavePickedPicture()
    {
        if (!CanShape(out var picked) || picked.Object.Kind != PageObjectKind.Image || Editor is not { } editor)
        {
            Status = UiText.Get("Pick a picture first.");
            return Signal.Return(RxVoid.Default);
        }

        return editor.ExtractPicture(picked.Page, picked.Object.Index)
            .TakeUntil(_lifetime)
            .SubscribeOn(TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(file => _owner.PageTools.SaveImageInteraction.Handle(string.Create(CultureInfo.InvariantCulture, $"picture-{picked.Page + 1}.{file.Extension}")).ToSignal()
                .Where(static path => path is { Length: > 0 })
                .SwitchMap(path => WritePicture(path!, file.Data)))
            .Catch<RxVoid, Exception>(SaveFailed);
    }

    /// <summary>Writes the extracted picture on the pool.</summary>
    /// <param name="path">Where to save it.</param>
    /// <param name="data">The picture's bytes.</param>
    /// <returns>A cold signal that ends once the picture is saved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> WritePicture(string path, byte[] data) =>
        Signal.Start(() => WriteFile(path, data), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(_ => PictureSaved());

    /// <summary>Says the picture was saved.</summary>
    /// <returns>The completion value.</returns>
    private RxVoid PictureSaved()
    {
        Status = UiText.Get("Saved the picture.");
        return RxVoid.Default;
    }

    /// <summary>Says why the picture was not saved, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends for an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> SaveFailed(Exception error)
    {
        if (error is OperationCanceledException && _lifetime.IsCancellationRequested)
        {
            return Signal.Return(RxVoid.Default);
        }

        if (error is not (InvalidOperationException or NotSupportedException or IOException or UnauthorizedAccessException))
        {
            return Signal.Throw<RxVoid>(error);
        }

        _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not save the picture.")} {error.Message}"));
        return Signal.Return(RxVoid.Default);
    }
}
