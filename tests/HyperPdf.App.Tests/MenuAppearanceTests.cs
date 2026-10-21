// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks menus are drawn as solid, bordered panels that stay clear of the page behind them (docs/COMFORT.md, low vision).</summary>
public sealed class MenuAppearanceTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The least height of a menu row, so rows stay easy to read and hit.</summary>
    private const double MinRowHeight = 32;

    /// <summary>The alpha of a fully solid colour.</summary>
    private const byte Solid = 255;

    /// <summary>An open menu has a solid background, a visible border and roomy rows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenMenuIsSolidBorderedAndRoomy()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("menu.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<Button>().Any(static b => b.Name == "DocumentMenuButton"));
            var button = window.GetVisualDescendants().OfType<Button>().First(static b => b.Name == "DocumentMenuButton");
            button.Flyout!.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            var presenter = await WaitForPresenterAsync(window);
            var row = presenter.GetVisualDescendants().OfType<MenuItem>().First();
            Save(window);

            await Assert.That(presenter.Background is ISolidColorBrush { Color.A: Solid }).IsTrue();
            await Assert.That(presenter.BorderThickness.Left).IsGreaterThan(0);
            await Assert.That(presenter.BorderBrush is ISolidColorBrush { Color.A: Solid }).IsTrue();
            await Assert.That(row.Bounds.Height).IsGreaterThanOrEqualTo(MinRowHeight);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Finds the open menu, which may live in the window's overlay or in a popup of its own.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The menu panel.</returns>
    private static async Task<MenuFlyoutPresenter> WaitForPresenterAsync(Window window)
    {
        MenuFlyoutPresenter? found = null;
        _ = await UiWait.UntilAsync(() =>
        {
            found = FindPresenter(window);
            return found is not null;
        });
        return found!;
    }

    /// <summary>Looks for a menu panel under the window and its popups.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The panel, or <see langword="null"/>.</returns>
    private static MenuFlyoutPresenter? FindPresenter(Window window)
    {
        if (window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().FirstOrDefault() is { } direct)
        {
            return direct;
        }

        foreach (var popup in window.GetVisualDescendants().OfType<Popup>())
        {
            if (popup.Child is Visual child && child.GetVisualDescendants().Prepend(child).OfType<MenuFlyoutPresenter>().FirstOrDefault() is { } inside)
            {
                return inside;
            }
        }

        return null;
    }

    /// <summary>Saves the window's frame when <c>HYPERPDF_SCREENSHOTS</c> names a folder.</summary>
    /// <param name="window">The window.</param>
    private static void Save(Window window)
    {
        var directory = Environment.GetEnvironmentVariable("HYPERPDF_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(directory, "document-menu.png"), new PngBitmapEncoderOptions());
    }
}
