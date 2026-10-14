// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The window that makes a signature or initials: typed, drawn or from a picture, with a preview. The user chooses
/// whether the mark is remembered on this computer, and can forget a remembered one.
/// </summary>
[DebuggerDisplay("SignatureMarkViewModel: {Kind}: {Style}")]
public sealed partial class SignatureMarkViewModel : ReactiveObject, IDisposable
{
    /// <summary>The name shown for a remembered picture.</summary>
    private const string SavedPictureName = "Remembered picture";

    /// <summary>The drawn points so far, stroke after stroke.</summary>
    private readonly List<PagePoint> _points = [];

    /// <summary>The number of points in each drawn stroke.</summary>
    private readonly List<int> _strokeLengths = [];

    /// <summary>Owns the derived values.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether there is a mark to use.</summary>
    private readonly IObservable<bool> _canUse;

    /// <summary>Whether a remembered mark can be forgotten.</summary>
    private readonly IObservable<bool> _canForget;

    /// <summary>Initializes a new instance of the <see cref="SignatureMarkViewModel"/> class.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <param name="saved">The remembered mark of this kind, or <see langword="null"/>.</param>
    public SignatureMarkViewModel(SignatureMarkKind kind, SignatureMark? saved)
    {
        Kind = kind;
        HasSaved = saved is not null;
        Remember = saved is not null;
        Style = saved?.Style ?? SignatureMarkStyle.Typed;
        if (saved is { Style: SignatureMarkStyle.Typed })
        {
            Text = saved.Text;
        }
        else if (saved is { Style: SignatureMarkStyle.Drawn })
        {
            Drawing = saved;
        }
        else if (saved is { Style: SignatureMarkStyle.Image })
        {
            // A remembered picture already had its paper removed when it was made.
            RemovePaper = false;
            SourceImage = new(SavedPictureName, saved.Pixels, (int)saved.Width, (int)saved.Height);
        }

        _imageMarkHelper = this.WhenChanged(static vm => vm.SourceImage, static vm => vm.RemovePaper, PrepareImage).ToProperty(this, static vm => vm.ImageMark);
        _previewHelper = this.WhenChanged(static vm => vm.Style, static vm => vm.Text, static vm => vm.Drawing, static vm => vm.ImageMark, Choose)
            .ToProperty(this, static vm => vm.Preview);
        _subscriptions.Add(_imageMarkHelper);
        _subscriptions.Add(_previewHelper);
        _canUse = this.WhenChanged(static vm => vm.Preview).Select(static preview => preview is not null);
        _canForget = this.WhenChanged(static vm => vm.HasSaved);
        Answered = Signal.Merge(UseCommand, CancelCommand);
    }

    /// <summary>Gets whether this makes a signature or initials.</summary>
    public SignatureMarkKind Kind { get; }

    /// <summary>Gets the window title.</summary>
    public string Title => Kind == SignatureMarkKind.Initials ? "Your Initials" : "Your Signature";

    /// <summary>Gets the label of the box the mark is typed in.</summary>
    public string TextLabel => Kind == SignatureMarkKind.Initials ? "Your initials" : "Your name, as you sign it";

    /// <summary>Gets the label of the button that uses the mark.</summary>
    public string UseText => Kind == SignatureMarkKind.Initials ? "Use Initials" : "Use Signature";

    /// <summary>Gets or sets how the mark is made.</summary>
    [Reactive(nameof(IsTyped), nameof(IsDrawn), nameof(IsImage))]
    public partial SignatureMarkStyle Style { get; set; }

    /// <summary>Gets or sets a value indicating whether the mark is typed.</summary>
    public bool IsTyped
    {
        get => Style == SignatureMarkStyle.Typed;
        set => SetStyle(value, SignatureMarkStyle.Typed);
    }

    /// <summary>Gets or sets a value indicating whether the mark is drawn.</summary>
    public bool IsDrawn
    {
        get => Style == SignatureMarkStyle.Drawn;
        set => SetStyle(value, SignatureMarkStyle.Drawn);
    }

    /// <summary>Gets or sets a value indicating whether the mark is a picture.</summary>
    public bool IsImage
    {
        get => Style == SignatureMarkStyle.Image;
        set => SetStyle(value, SignatureMarkStyle.Image);
    }

    /// <summary>Gets or sets the typed text.</summary>
    [Reactive]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Gets the drawn mark so far, or <see langword="null"/>.</summary>
    [Reactive]
    public partial SignatureMark? Drawing { get; private set; }

    /// <summary>Gets the chosen picture, before its paper is removed.</summary>
    [Reactive]
    public partial DecodedImage? SourceImage { get; private set; }

    /// <summary>Gets or sets a value indicating whether white paper is removed from the picture.</summary>
    [Reactive]
    public partial bool RemovePaper { get; set; } = true;

    /// <summary>Gets why the chosen file could not be used, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? ImageError { get; private set; }

    /// <summary>Gets the picture made into a mark, or <see langword="null"/>.</summary>
    [ObservableAsProperty]
    public partial SignatureMark? ImageMark { get; }

    /// <summary>Gets the mark as it would be placed, or <see langword="null"/> when there is nothing yet.</summary>
    [ObservableAsProperty]
    public partial SignatureMark? Preview { get; }

    /// <summary>Gets or sets a value indicating whether the mark is remembered on this computer for next time.</summary>
    [Reactive]
    public partial bool Remember { get; set; }

    /// <summary>Gets a value indicating whether a mark of this kind is remembered.</summary>
    [Reactive]
    public partial bool HasSaved { get; private set; }

    /// <summary>Gets a value indicating whether the user forgot the remembered mark.</summary>
    public bool Forgotten { get; private set; }

    /// <summary>Gets the interaction asking for a picture file.</summary>
    public Interaction<RxVoid, string?> BrowseImageInteraction { get; } = new();

    /// <summary>Gets the answer, which closes the window: <see langword="true"/> to use the mark.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Gets the mark to place, made from the current choices, or <see langword="null"/> when there is nothing yet.</summary>
    public SignatureMark? Result =>
        Choose(Style, Text, Drawing, Style == SignatureMarkStyle.Image ? PrepareImage(SourceImage, RemovePaper) : null) is { } mark ? mark with { Kind = Kind } : null;

    /// <summary>Uses a picture already read from a file.</summary>
    /// <param name="image">The picture, or <see langword="null"/> when the file could not be read.</param>
    public void UseImage(DecodedImage? image)
    {
        if (image is null)
        {
            ImageError = "This file could not be read as a picture. Choose a PNG, JPEG, BMP or WebP file.";
            return;
        }

        ImageError = null;
        SourceImage = image;
        Style = SignatureMarkStyle.Image;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Prepares a picture: removes white paper when asked and trims empty margins.</summary>
    /// <param name="image">The picture.</param>
    /// <param name="removePaper">Whether white paper becomes transparent.</param>
    /// <returns>The mark, or <see langword="null"/> when there is no picture or nothing visible in it.</returns>
    private static SignatureMark? PrepareImage(DecodedImage? image, bool removePaper) =>
        image is not null && SignatureImage.Create(image.Pixels.Span, image.Width, image.Height, removePaper) is { } prepared
            ? SignatureMark.FromImage(SignatureMarkKind.Signature, prepared)
            : null;

    /// <summary>Picks the mark for the chosen style.</summary>
    /// <param name="style">The style.</param>
    /// <param name="text">The typed text.</param>
    /// <param name="drawing">The drawn mark.</param>
    /// <param name="image">The picture mark.</param>
    /// <returns>The mark, or <see langword="null"/>.</returns>
    private static SignatureMark? Choose(SignatureMarkStyle style, string text, SignatureMark? drawing, SignatureMark? image) => style switch
    {
        SignatureMarkStyle.Typed => SignatureMark.Typed(SignatureMarkKind.Signature, text),
        SignatureMarkStyle.Drawn => drawing,
        SignatureMarkStyle.Image => image,
        _ => null,
    };

    /// <summary>Uses the mark, which closes the window.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [ReactiveCommand(CanExecute = nameof(_canUse))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Use() => true;

    /// <summary>Closes the window without a mark.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Chooses a style when its option is turned on.</summary>
    /// <param name="on">Whether the option is being turned on.</param>
    /// <param name="style">The style.</param>
    private void SetStyle(bool on, SignatureMarkStyle style)
    {
        if (on)
        {
            Style = style;
        }
    }

    /// <summary>Adds a drawn stroke. The first stroke replaces a remembered drawing.</summary>
    /// <param name="stroke">The stroke's points, in the drawing pad's units.</param>
    [ReactiveCommand]
    private void AddStroke(PagePoint[] stroke)
    {
        if (stroke is not { Length: > 0 })
        {
            return;
        }

        _points.AddRange(stroke);
        _strokeLengths.Add(stroke.Length);
        Drawing = SignatureMark.Drawn(SignatureMarkKind.Signature, CollectionsMarshal.AsSpan(_points), CollectionsMarshal.AsSpan(_strokeLengths));
        Style = SignatureMarkStyle.Drawn;
    }

    /// <summary>Clears the drawing so the user can start again.</summary>
    [ReactiveCommand]
    private void ClearDrawing()
    {
        _points.Clear();
        _strokeLengths.Clear();
        Drawing = null;
    }

    /// <summary>Asks for a picture of a signature and reads it.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task ChooseImageAsync()
    {
        if (await BrowseImageInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return;
        }

        UseImage(await Task.Run(() => SignatureImageFile.Load(path)).ConfigureAwait(true));
    }

    /// <summary>Forgets the remembered mark of this kind straight away.</summary>
    [ReactiveCommand(CanExecute = nameof(_canForget))]
    private void Forget()
    {
        Forgotten = true;
        HasSaved = false;
        Remember = false;
    }
}
