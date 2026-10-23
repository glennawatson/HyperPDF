// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.Core.Media;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the media panel draws the player: its buttons follow the player's state and the video appears as a picture.</summary>
public sealed class MediaPanelViewTests
{
    /// <summary>The position of the screen clip in the test document.</summary>
    private const int ClipPosition = 1;

    /// <summary>The width of the made clip, in pixels.</summary>
    private const int ClipWidth = 160;

    /// <summary>The height of the made clip, in pixels.</summary>
    private const int ClipHeight = 120;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The player's buttons start off, turn on when media is chosen, and the picture is drawn only after Play is pressed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsThePictureAfterPlayIsPressed()
    {
        var tools = MediaToolLocator.Find();
        if (tools is null)
        {
            Skip.Test("FFmpeg is not installed on this computer.");
        }

        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "panel.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateMediaDocument(await MakeClipAsync(tools!, test.Directory)));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        _ = await UiWait.UntilAsync(() => tab.IsLoaded && tab.Media.HasMedia);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            tab.SidebarMode = SidebarMode.Media;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<MediaPanelView>().Any(static panel => panel.IsVisible));
            var panel = window.GetVisualDescendants().OfType<MediaPanelView>().First(static view => view.IsVisible);
            var play = panel.FindControl<Button>("MediaPlayButton")!;
            var pause = panel.FindControl<Button>("MediaPauseButton")!;
            var image = panel.FindControl<Image>("MediaPictureImage")!;

            await Assert.That(await UiWait.UntilAsync(() => !play.IsEffectivelyEnabled)).IsTrue();
            await Assert.That(image.Source).IsNull();

            tab.Media.Selected = tab.Media.Items[ClipPosition];
            _ = await UiWait.UntilAsync(() => play.IsEffectivelyEnabled);
            await Assert.That(pause.IsEffectivelyEnabled).IsFalse();
            await Assert.That(image.Source).IsNull();

            _ = await tab.Media.Player.PlayCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => image.Source is WriteableBitmap { PixelSize: { Width: ClipWidth, Height: ClipHeight } });

            await Assert.That(image.IsVisible).IsTrue();
            await Assert.That(pause.IsEffectivelyEnabled).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Makes a short video with FFmpeg.</summary>
    /// <param name="tools">The FFmpeg programs.</param>
    /// <param name="directory">The folder to write to.</param>
    /// <returns>The bytes of the video.</returns>
    private static async Task<byte[]> MakeClipAsync(MediaTools tools, string directory)
    {
        var path = Path.Combine(directory, "made.mp4");
        var start = new ProcessStartInfo(tools.Ffmpeg) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in new[] { "-v", "error", "-f", "lavfi", "-i", "testsrc=duration=4:size=160x120:rate=10", "-c:v", "mpeg4", "-y", path })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        _ = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return await File.ReadAllBytesAsync(path);
    }
}
