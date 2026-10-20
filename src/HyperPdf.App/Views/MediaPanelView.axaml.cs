// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Media;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The sound and video in the document, with the player and the buttons that save the chosen one or open it with the system player.</summary>
[DebuggerDisplay("MediaPanelView: {ViewModel}")]
public sealed partial class MediaPanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The dots per inch of the picture, which sets only how big it would print.</summary>
    private const double PictureDpi = 96;

    /// <summary>The bytes in each pixel of a decoded picture.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Initializes a new instance of the <see cref="MediaPanelView"/> class.</summary>
    public MediaPanelView()
    {
        InitializeComponent();
        MediaList.ItemTemplate = new FuncDataTemplate<DocumentMedia>(static (_, _) => new MediaItemView());
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the list is filled.
            disposables.Add(ItemAutomation.NameItems(MediaList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsMediaMode, static v => v.MediaPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Items, static v => v.MediaList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Media.Selected, static v => v.MediaList.SelectedItem, static item => item, static item => item as DocumentMedia));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.SaveCommand, static v => v.SaveMediaButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.OpenSelectedCommand, static v => v.OpenMediaButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Media.SaveInteraction).HandleWith(SaveMediaAsync));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.Player.PlayCommand, static v => v.MediaPlayButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.Player.PauseCommand, static v => v.MediaPauseButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.Player.StopCommand, static v => v.MediaStopButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.Player.SkipBackCommand, static v => v.MediaSkipBackButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.Player.SkipForwardCommand, static v => v.MediaSkipForwardButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Player.Status, static v => v.MediaStatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Player.Clock, static v => v.MediaClockText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Player.Caption, static v => v.MediaCaptionText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Player.Caption, static v => v.MediaCaptionText.IsVisible, static text => text.Length > 0));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Player.DurationSeconds, static v => v.MediaPositionBar.Maximum));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Player.PositionSeconds, static v => v.MediaPositionBar.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Player.HasPicture, static v => v.MediaPictureImage.IsVisible));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Media.Player.FrameVersion).Skip(1).SubscribeSafe(_ => DrawPicture(), ErrorTrace.Write));
        });
    }

    /// <summary>Copies a picture into a bitmap, row by row when the bitmap pads its rows.</summary>
    /// <param name="player">The player holding the picture.</param>
    /// <param name="bitmap">The bitmap to fill.</param>
    /// <param name="size">The size of the picture.</param>
    private static unsafe void CopyPicture(MediaPlayerViewModel player, WriteableBitmap bitmap, FrameSize size)
    {
        using var locked = bitmap.Lock();
        var rowBytes = size.Width * BytesPerPixel;
        if (locked.RowBytes == rowBytes)
        {
            _ = player.TryCopyFrame(new((void*)locked.Address, size.ByteCount));
            return;
        }

        var scratch = new byte[size.ByteCount];
        if (!player.TryCopyFrame(scratch))
        {
            return;
        }

        for (var row = 0; row < size.Height; row++)
        {
            Marshal.Copy(scratch, row * rowBytes, locked.Address + (row * locked.RowBytes), rowBytes);
        }
    }

    /// <summary>Copies the player's latest picture into the picture drawn in the panel.</summary>
    private void DrawPicture()
    {
        if (ViewModel?.Media.Player is not { } player || player.FrameSize is not { IsEmpty: false } size)
        {
            MediaPictureImage.Source = null;
            return;
        }

        if (MediaPictureImage.Source is not WriteableBitmap picture || picture.PixelSize.Width != size.Width || picture.PixelSize.Height != size.Height)
        {
            var pixels = new PixelSize(size.Width, size.Height);
            var dpi = new Vector(PictureDpi, PictureDpi);
            picture = new(pixels, dpi, PixelFormat.Bgra8888, AlphaFormat.Opaque);
            MediaPictureImage.Source = picture;
        }

        CopyPicture(player, picture, size);
        MediaPictureImage.InvalidateVisual();
    }

    /// <summary>Asks where to save media, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveMediaAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save Media", SuggestedFileName = context.Input });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
