// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Tests.Fakes;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks sizing and cleanup of individual preview sheets.</summary>
public sealed class PrintPreviewPageViewTests
{
    /// <summary>The short paper side in points.</summary>
    private const float ShortSide = 300;

    /// <summary>The long paper side in points.</summary>
    private const float LongSide = 600;

    /// <summary>The short preview side in screen units.</summary>
    private const double ScaledShortSide = 310;

    /// <summary>The long preview side in screen units.</summary>
    private const double ScaledLongSide = 620;

    /// <summary>The tolerance for rounding the scaled paper dimensions.</summary>
    private const double SizeTolerance = 1e-9;

    /// <summary>Waits for requested font data before drawing a preview sheet.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WaitsForPagePreparation()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken preparationToken = default;
        using var document = new FakeDocument("preview-preparation.pdf", [new(ShortSide, LongSide)])
        {
            Preparation = (_, token) =>
            {
                preparationToken = token;
                return new(release.Task.WaitAsync(token));
            },
        };
        using var view = new PrintPreviewPageView { ViewModel = new(document, 0, new(ShortSide, LongSide), "1 of 1") };
        var window = new Window { Content = view };
        window.Show();
        try
        {
            var image = view.GetVisualDescendants().OfType<Image>().Single();
            await Assert.That(await UiWait.UntilAsync(() => preparationToken.CanBeCanceled)).IsTrue();
            await Assert.That(image.Source).IsNull();
            await Assert.That(document.RenderCount).IsEqualTo(0);
            _ = release.TrySetResult();
            await Assert.That(await UiWait.UntilAsync(() => image.Source is not null)).IsTrue();
            await Assert.That(document.RenderCount).IsGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Closing a preview prevents a delayed preparation from drawing into it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingPreviewCancelsPendingPreparation()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken preparationToken = default;
        using var document = new FakeDocument("preview-cancellation.pdf", [new(ShortSide, LongSide)])
        {
            Preparation = (_, token) =>
            {
                preparationToken = token;
                return new(release.Task.WaitAsync(token));
            },
        };
        var view = new PrintPreviewPageView { ViewModel = new(document, 0, new(ShortSide, LongSide), "1 of 1") };
        var window = new Window { Content = view };
        window.Show();
        try
        {
            var image = view.GetVisualDescendants().OfType<Image>().Single();
            await Assert.That(await UiWait.UntilAsync(() => preparationToken.CanBeCanceled)).IsTrue();
            view.Dispose();
            await Assert.That(preparationToken.IsCancellationRequested).IsTrue();
            _ = release.TrySetResult();
            await Assert.That(image.Source).IsNull();
            await Assert.That(document.RenderCount).IsEqualTo(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Checks that a recycled sheet has its paper size before it is loaded.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReservesSheetSizeBeforeLoading()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("sheet-size.pdf", 1)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        await Assert.That(await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding)).IsTrue();
        using var view = new PrintPreviewPageView { ViewModel = preview.Sheets[0] with { Size = new(ShortSide, LongSide) } };
        var panel = (StackPanel)view.Content!;
        var slot = (Panel)panel.Children[0];
        var image = (Image)((Border)slot.Children[0]).Child!;
        await Assert.That(slot.Height).IsEqualTo(slot.Width);
        await Assert.That(image.Width).IsEqualTo(ScaledShortSide).Within(SizeTolerance);
        await Assert.That(image.Height).IsEqualTo(ScaledLongSide).Within(SizeTolerance);
        view.ViewModel = preview.Sheets[0] with { Size = new(LongSide, ShortSide), Caption = "Landscape" };
        await Assert.That(image.Width).IsEqualTo(ScaledLongSide).Within(SizeTolerance);
        await Assert.That(image.Height).IsEqualTo(ScaledShortSide).Within(SizeTolerance);
        await Assert.That(((TextBlock)panel.Children[1]).Text).IsEqualTo("Landscape");
    }

    /// <summary>Checks that cleanup through the disposable interface releases the displayed bitmap.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposeReleasesRenderedSheet()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("sheet-disposal.pdf", 1)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        await Assert.That(await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding)).IsTrue();
        var view = new PrintPreviewPageView { ViewModel = preview.Sheets[0] };
        var window = new Window { Content = view };
        window.Show();
        try
        {
            var image = view.GetVisualDescendants().OfType<Image>().Single();
            using (MultipleDisposable owner = [view])
            {
                await Assert.That(await UiWait.UntilAsync(() => image.Source is not null)).IsTrue();
            }

            await Assert.That(image.Source).IsNull();
        }
        finally
        {
            window.Close();
        }
    }
}
