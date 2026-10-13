// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

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

    /// <summary>The sizing subscription, owned until disposal so unloaded sheets can be measured.</summary>
    private readonly MultipleDisposable _sizing;

    /// <summary>The current visible sheet's preparation.</summary>
    private CancellationTokenSource? _preparation;

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
        _sizing = [this.WhenChanged(static v => v.ViewModel).SubscribeSafe(SetSheetSize, OnError)];

        // A sheet is an immutable record, so the view follows which one it shows.
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(PrepareAndShow, OnError));
            disposables.Add(EmptyDisposable.Instance.DisposeWith(ReleaseBitmap));
            disposables.Add(EmptyDisposable.Instance.DisposeWith(StopPreparation));
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _sizing.Dispose();
        StopPreparation();
        ReleaseBitmap();
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

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

    /// <summary>Prepares only the sheet this view currently displays.</summary>
    /// <param name="sheet">The visible sheet.</param>
    private void PrepareAndShow(PrintPreviewPage? sheet)
    {
        StopPreparation();
        Show(null);
        _caption.Text = sheet?.Caption;
        if (sheet is null || sheet.Document.IsDisposed)
        {
            return;
        }

        var source = new CancellationTokenSource();
        _preparation = source;
        _ = ShowAfterPreparationAsync(sheet, source);
    }

    /// <summary>Renders a prepared sheet if the view still displays it.</summary>
    /// <param name="sheet">The requested sheet.</param>
    /// <param name="source">Cancels preparation when the view changes or deactivates.</param>
    /// <returns>A task completing after the sheet is drawn or cancelled.</returns>
    private async Task ShowAfterPreparationAsync(PrintPreviewPage sheet, CancellationTokenSource source)
    {
        try
        {
            await sheet.Document.PreparePageAsync(sheet.PageIndex, source.Token).ConfigureAwait(true);
            if (ReferenceEquals(_preparation, source) && ReferenceEquals(ViewModel, sheet))
            {
                await ShowAsync(sheet, source).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            // Changing sheets or closing the view cancels the old sheet's preparation.
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            OnError(error);
        }
        finally
        {
            if (ReferenceEquals(_preparation, source))
            {
                StopPreparation();
            }
        }
    }

    /// <summary>Cancels preparation for a sheet the view no longer displays.</summary>
    private void StopPreparation()
    {
        _preparation?.Cancel();
        _preparation?.Dispose();
        _preparation = null;
    }

    /// <summary>Clears the sheet and its caption, ready for the next one.</summary>
    /// <param name="sheet">The sheet being shown next, or <see langword="null"/>.</param>
    private void Show(PrintPreviewPage? sheet)
    {
        ReleaseBitmap();
        _caption.Text = sheet?.Caption;
    }

    /// <summary>Renders a sheet as it will print, off the UI thread: white paper, annotations included.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="source">Cancels the rendering when the view changes or deactivates.</param>
    /// <returns>A task completing once the sheet is shown or dropped.</returns>
    private async Task ShowAsync(PrintPreviewPage sheet, CancellationTokenSource source)
    {
        Show(sheet);
        if (sheet.Document.IsDisposed || sheet.Size.Width <= 0 || sheet.Size.Height <= 0)
        {
            return;
        }

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var pixels = new PixelSize(Math.Max(1, (int)Math.Round(_image.Width * scaling)), Math.Max(1, (int)Math.Round(_image.Height * scaling)));

        // The bitmap belongs to this call until it is shown, so a sheet change while it renders cannot dispose it mid-draw.
        var bitmap = new WriteableBitmap(pixels, new(ScreenDpi * scaling, ScreenDpi * scaling), PixelFormat.Bgra8888, AlphaFormat.Premul);
        try
        {
            await Task.Run(() => RenderSheet(bitmap, sheet, pixels), source.Token).ConfigureAwait(true);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        if (!ReferenceEquals(_preparation, source) || !ReferenceEquals(ViewModel, sheet))
        {
            bitmap.Dispose();
            return;
        }

        _bitmap = bitmap;
        _image.Source = bitmap;
    }

    /// <summary>Reserves the paper's size before the virtualising panel measures a recycled sheet.</summary>
    /// <param name="sheet">The sheet assigned to the view.</param>
    private void SetSheetSize(PrintPreviewPage? sheet)
    {
        if (sheet is null || sheet.Size.Width <= 0 || sheet.Size.Height <= 0)
        {
            return;
        }

        var scale = SheetSide / Math.Max(sheet.Size.Width, sheet.Size.Height);
        _image.Width = sheet.Size.Width * scale;
        _image.Height = sheet.Size.Height * scale;
        _caption.Text = sheet.Caption;
    }

    /// <summary>Releases the rendered sheet.</summary>
    private void ReleaseBitmap()
    {
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
