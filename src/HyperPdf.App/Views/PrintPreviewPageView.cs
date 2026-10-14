// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>One sheet in the print preview: the page on white paper with its caption beneath.</summary>
[DebuggerDisplay("PrintPreviewPageView: {ViewModel}")]
public sealed class PrintPreviewPageView : ReactiveUI.Avalonia.ReactiveUserControl<PrintPreviewPage>, IDisposable
{
    /// <summary>The longest side of a sheet on screen.</summary>
    private const double SheetSide = 620;

    /// <summary>The space above and below each sheet.</summary>
    private const double SheetGap = 8;

    /// <summary>The dots per inch of a screen at scale 1.</summary>
    private const double ScreenDpi = 96;

    /// <summary>The sheet image.</summary>
    private readonly Image _image = new() { Stretch = Stretch.Fill };

    /// <summary>The caption.</summary>
    private readonly TextBlock _caption = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 6, 0, 0) };

    /// <summary>Owns what the view keeps while unloaded sheets can be measured: its caption and size follow the sheet.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>The rendered sheet.</summary>
    private WriteableBitmap? _bitmap;

    /// <summary>Initializes a new instance of the <see cref="PrintPreviewPageView"/> class.</summary>
    public PrintPreviewPageView()
    {
        _caption.Classes.Add("secondary");

        // Every sheet takes the same square slot, upright or sideways, so the list never re-estimates where sheets sit.
        var paper = new Border
        {
            Background = Brushes.White,
            BoxShadow = BoxShadows.Parse("0 1 4 0 #40000000"),
            Child = _image,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var slot = new Panel { Width = SheetSide, Height = SheetSide };
        slot.Children.Add(paper);
        var sheet = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, SheetGap) };
        sheet.Children.Add(slot);
        sheet.Children.Add(_caption);
        Content = sheet;

        // Virtualised sheets receive their model before loading; sizing must precede the first measure.
        var sheets = this.WhenChanged(static v => v.ViewModel);
        _lifetime.Add(sheets.SubscribeSafe(ReserveSheetSize, ErrorTrace.Write));
        _lifetime.Add(sheets.Select(static page => page?.Caption).BindTo(_caption, static caption => caption.Text));

        // A sheet is an immutable record, so the view follows which one it shows: the old picture goes at once, and the
        // new sheet is prepared and drawn off the UI thread. Showing another sheet ends the work for the one before, and
        // disposing the view ends the activation, which cancels the work and releases the picture.
        _lifetime.Add(this.WhenActivated(disposables =>
        {
            disposables.Add(sheets
                .SelectWith(this, static (view, page) => view.RenderRequestFor(page))
                .SwitchMap(RenderedSheet)
                .SubscribeSafe(ShowBitmap, ErrorTrace.Write));
            disposables.Add(Scope.Create(this, static view => view.ShowBitmap(null)));
        }));
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _lifetime.Dispose();

    /// <summary>Draws a sheet into a bitmap.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="sheet">The sheet.</param>
    /// <param name="pixels">The bitmap's size.</param>
    private static unsafe void RenderSheet(WriteableBitmap bitmap, PrintPreviewPage sheet, PixelSize pixels)
    {
        using var buffer = bitmap.Lock();
        var span = new Span<byte>((void*)buffer.Address, buffer.RowBytes * buffer.Size.Height);
        var info = new PageRenderInfo(sheet.PageIndex, pixels.Width / sheet.Size.Width, PageRotation.None, 0, 0, RenderFlags.Annotations | RenderFlags.Printing);
        _ = sheet.Document.Render(info, new(span, pixels.Width, pixels.Height, buffer.RowBytes));
    }

    /// <summary>
    /// Gets the picture of a sheet as it will print: nothing at once, then the sheet drawn on white paper once it is prepared.
    /// The work stops when the subscription ends.
    /// </summary>
    /// <param name="request">The sheet and the size to draw it at, or no sheet.</param>
    /// <returns>The pictures; a <see langword="null"/> picture clears the old one.</returns>
    private static IObservable<WriteableBitmap?> RenderedSheet(SheetRender? request) => request is not { } render
        ? Signal.Return<WriteableBitmap?>(null)
        : Signal.FromAsync(token => RenderAsync(render, token)).StartWith((WriteableBitmap?)null);

    /// <summary>Prepares a sheet and renders it off the UI thread.</summary>
    /// <param name="render">The sheet and the size to draw it at.</param>
    /// <param name="token">Cancels the work when the view shows another sheet or deactivates.</param>
    /// <returns>The picture, or <see langword="null"/> when the work was cancelled or failed.</returns>
    private static async Task<WriteableBitmap?> RenderAsync(SheetRender render, CancellationToken token)
    {
        var (sheet, pixels, scaling) = render;
        WriteableBitmap? bitmap = null;
        try
        {
            await sheet.Document.PreparePageAsync(sheet.PageIndex, token).ConfigureAwait(true);

            // The bitmap belongs to this call until it is returned, so a sheet change while it renders cannot dispose it mid-draw.
            bitmap = new(pixels, new(ScreenDpi * scaling, ScreenDpi * scaling), PixelFormat.Bgra8888, AlphaFormat.Premul);
            var drawing = bitmap;
            await Task.Run(() => RenderSheet(drawing, sheet, pixels), token).ConfigureAwait(true);
            token.ThrowIfCancellationRequested();
            return bitmap;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Changing sheets or closing the view cancels the old sheet's work.
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            ErrorTrace.Write(error);
        }

        bitmap?.Dispose();
        return null;
    }

    /// <summary>Works out what to draw for the sheet the view shows now, on the UI thread.</summary>
    /// <param name="sheet">The sheet, or <see langword="null"/>.</param>
    /// <returns>The request, or <see langword="null"/> when there is nothing to draw.</returns>
    private SheetRender? RenderRequestFor(PrintPreviewPage? sheet)
    {
        if (sheet is null || sheet.Document.IsDisposed || sheet.Size.Width <= 0 || sheet.Size.Height <= 0)
        {
            return null;
        }

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var pixels = new PixelSize(Math.Max(1, (int)Math.Round(_image.Width * scaling)), Math.Max(1, (int)Math.Round(_image.Height * scaling)));
        return new(sheet, pixels, scaling);
    }

    /// <summary>Shows a picture of the sheet and releases the one before; no picture leaves the paper blank.</summary>
    /// <param name="bitmap">The picture, or <see langword="null"/>.</param>
    private void ShowBitmap(WriteableBitmap? bitmap)
    {
        _image.Source = bitmap;
        _bitmap?.Dispose();
        _bitmap = bitmap;
    }

    /// <summary>Reserves the paper's size before the virtualising panel measures a recycled sheet.</summary>
    /// <param name="sheet">The sheet assigned to the view.</param>
    private void ReserveSheetSize(PrintPreviewPage? sheet)
    {
        if (sheet is null || sheet.Size.Width <= 0 || sheet.Size.Height <= 0)
        {
            return;
        }

        var scale = SheetSide / Math.Max(sheet.Size.Width, sheet.Size.Height);
        _image.Width = sheet.Size.Width * scale;
        _image.Height = sheet.Size.Height * scale;
    }

    /// <summary>A sheet to draw and the size to draw it at.</summary>
    /// <param name="Sheet">The sheet.</param>
    /// <param name="Pixels">The bitmap's size.</param>
    /// <param name="Scaling">The display scale.</param>
    private sealed record SheetRender(PrintPreviewPage Sheet, PixelSize Pixels, double Scaling);
}
