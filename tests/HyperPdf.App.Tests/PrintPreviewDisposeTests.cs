// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks a print preview that is closed while it is still building leaves nothing behind.</summary>
public sealed class PrintPreviewDisposeTests
{
    /// <summary>A build that finishes after the window closed opens no preview and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuildFinishingAfterDisposalOpensNothing()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter()), engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("closing.pdf", 1)]);
        var gate = engine.ArmExport();
        var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);

        // The exporter signals once the build is inside it, so the window closes mid-build on every machine.
        _ = await UiWait.UntilAsync(() => gate.Reached.Task.IsCompleted, static () => "The build did not reach the exporter.");
        var building = preview.IsBuilding;

        preview.Dispose();
        gate.Release();
        _ = await UiWait.UntilAsync(() => gate.Found.Task.IsCompleted, static () => "The held build did not finish.");

        // The finished build deletes the file it wrote instead of opening it.
        _ = await UiWait.UntilAsync(() => gate.Exported.All(static path => !File.Exists(path)), static () => "The build left its file behind.");

        using (Assert.Multiple())
        {
            await Assert.That(building).IsTrue();
            await Assert.That(gate.Exported.Count).IsGreaterThan(0);
            await Assert.That(preview.PreviewPath).IsNull();
            await Assert.That(preview.Sheets.Count).IsEqualTo(0);
        }
    }
}
