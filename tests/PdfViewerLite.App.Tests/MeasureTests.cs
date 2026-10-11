// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Measuring;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks the measuring tool: distances, areas, declared scales and keeping a measurement.</summary>
public sealed class MeasureTests
{
    /// <summary>One inch in PDF points.</summary>
    private const float Inch = 72;

    /// <summary>Two inches in PDF points.</summary>
    private const float TwoInches = 144;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Two clicks measure a distance and finish it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasuresADistance()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("distance.pdf", 1)]);
        var measure = main.SelectedTab!.Measure;
        measure.IsOn = true;
        measure.ScaleText = "1 in = 1 in";

        measure.AddPoint(0, new(Inch, Inch));
        var moved = measure.MovePoint(0, new(TwoInches, Inch));
        measure.AddPoint(0, new(TwoInches, Inch));

        await Assert.That(moved).IsTrue();
        await Assert.That(measure.IsFinished).IsTrue();
        await Assert.That(measure.Result).StartsWith("Distance 1 in");
        await Assert.That(measure.CanKeep).IsTrue();
    }

    /// <summary>Corners clicked and a finish measure an area; switching mode starts again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasuresAnArea()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("area.pdf", 1)]);
        var measure = main.SelectedTab!.Measure;
        measure.IsOn = true;
        measure.IsArea = true;
        measure.ScaleText = "1 in = 1 ft";

        measure.AddPoint(0, new(0, 0));
        measure.AddPoint(0, new(Inch, 0));
        measure.AddPoint(0, new(Inch, Inch));
        measure.AddPoint(0, new(0, Inch));
        measure.Finish();
        var area = measure.Result;
        measure.IsPerimeter = true;

        await Assert.That(area).Contains("Area 1 ft²");
        await Assert.That(measure.Points.Count).IsEqualTo(0);
        await Assert.That(measure.Result).IsEqualTo(string.Empty);
    }

    /// <summary>A drawing's declared scale is used as soon as measuring starts on its page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsesTheDrawingsScale()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "plan.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithViewport());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var measure = main.SelectedTab!.Measure;
        measure.IsOn = true;

        measure.AddPoint(0, new(Inch, Inch));
        measure.AddPoint(0, new(TwoInches, Inch));

        await Assert.That(measure.ScaleText).IsEqualTo(TestPdf.ViewportScale);
        await Assert.That(measure.Result).StartsWith("Distance 10 ft");
    }

    /// <summary>Keeping a measurement adds its lines and its label as annotations, which can be undone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsTheMeasurementOnThePage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("keep.pdf", 1)]);
        var tab = main.SelectedTab!;
        var measure = tab.Measure;
        measure.IsOn = true;
        measure.AddPoint(0, new(Inch, Inch));
        measure.AddPoint(0, new(TwoInches, TwoInches));

        _ = await measure.KeepCommand.Execute().ToTask();

        await Assert.That(tab.Annotations.CanUndo).IsTrue();
        await Assert.That(measure.Points.Count).IsEqualTo(0);
        await Assert.That(tab.Annotations.Items.Any(static i => i.Annotation.Contents.StartsWith("Distance", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>The Measure bar shows while measuring and every control on it has a name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsTheMeasureBar()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("bar.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            main.SelectedTab!.Measure.IsOn = true;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<Avalonia.Controls.Border>().Any(static b => b.Name == "MeasureBar" && b.IsVisible));

            await Assert.That(string.Join(", ", AccessibilityTests.Unnamed(window))).IsEqualTo(string.Empty);
            await Assert.That(main.SelectedTab.Measure.Mode).IsEqualTo(MeasureMode.Distance);
        }
        finally
        {
            window.Close();
        }
    }
}
