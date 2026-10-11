// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.Theming;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Renders the real window headlessly and checks the pixels.</summary>
public sealed class RenderingTests
{
    /// <summary>The left edge of the measured shape, in page points.</summary>
    private const float MeasureLeft = 100;

    /// <summary>The right edge of the measured shape, in page points.</summary>
    private const float MeasureRight = 300;

    /// <summary>The top of the measured shape, in page points.</summary>
    private const float MeasureTop = 150;

    /// <summary>The bottom of the measured shape, in page points.</summary>
    private const float MeasureBottom = 300;

    /// <summary>The name of the Focus toggle.</summary>
    private const string FocusToggleName = "FocusToggle";

    /// <summary>The pages of the article used for Focus Mode.</summary>
    private const int ArticlePages = 3;

    /// <summary>The Focus Mode text size set in the test.</summary>
    private const double FocusTextSize = 20;

    /// <summary>The index of the soft paper page colour.</summary>
    private const int SoftPaperColour = 1;

    /// <summary>The name of the Read Aloud bar.</summary>
    private const string ReadAloudBarName = "ReadAloudBar";

    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 40;

    /// <summary>The page navigated to.</summary>
    private const int MiddlePage = Pages / 2;

    /// <summary>The number of cached tiles that shows rendering is well under way.</summary>
    private const int ExpectedTiles = 6;

    /// <summary>Half, for finding the middle of a field.</summary>
    private const float Half = 0.5F;

    /// <summary>The number of annotations the annotation test adds.</summary>
    private const int AnnotationCount = 2;

    /// <summary>The page count of the page by page document.</summary>
    private const int PageByPagePages = 4;

    /// <summary>The page that the Page Down key reaches from the second page.</summary>
    private const int SpreadPage = 2;

    /// <summary>The name of the page count text.</summary>
    private const string PageCountName = "PageCountText";

    /// <summary>How far apart, in pixels, centres may be and still read as one line.</summary>
    private const double AlignmentTolerance = 1;

    /// <summary>The bytes in a captured pixel.</summary>
    private const int BytesPerFramePixel = 4;

    /// <summary>How different from the background a pixel must be to count as drawn.</summary>
    private const int InkThreshold = 60;

    /// <summary>How many characters the caret test steps.</summary>
    private const int CaretSteps = 3;

    /// <summary>The length of the "Page 1" heading, before the next line starts.</summary>
    private const int HeadingLength = 6;

    /// <summary>The layers in the layered test document.</summary>
    private const int LayerCount = 2;

    /// <summary>Masks off the alpha channel.</summary>
    private const uint RgbMask = 0xFFFFFFU;

    /// <summary>Where the annotation test puts its note.</summary>
    private static readonly PagePoint NoteAt = new(420, 150);

    /// <summary>Draws an area measurement over the page with its result, and saves a screenshot when asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsAMeasurement()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("measure.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var measure = main.SelectedTab!.Measure;
            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > ExpectedTiles);
            measure.IsOn = true;
            measure.IsArea = true;
            measure.ScaleText = "1 cm = 1 m";
            foreach (var (x, y) in (ReadOnlySpan<(float, float)>)[(MeasureLeft, MeasureTop), (MeasureRight, MeasureTop), (MeasureRight, MeasureBottom), (MeasureLeft, MeasureBottom)])
            {
                measure.AddPoint(0, new(x, y));
            }

            measure.Finish();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "measure.png");

            await Assert.That(frame).IsNotNull();
            await Assert.That(measure.Result).StartsWith("Area");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies pages and thumbnails render, then saves a screenshot when PDFVIEWERLITE_SCREENSHOTS is set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersDocumentWindow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("first.pdf", Pages), test.CreateDocument("second.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var rendered = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > ExpectedTiles);
            await Assert.That(rendered).IsTrue();

            using var frame = window.CaptureRenderedFrame();
            Save(frame, "window.png");
            await Assert.That(frame).IsNotNull();

            // The code-behind bindings fill the tool bar.
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var pageCount = view.GetVisualDescendants().OfType<TextBlock>().Single(static t => t.Name == PageCountName);
            var zoom = view.GetVisualDescendants().OfType<Button>().Single(static b => b.Name == "ZoomButton");
            await Assert.That(pageCount.Text).IsEqualTo($"of {Pages}");
            await Assert.That(zoom.Content as string).EndsWith("%");
            await Assert.That(pageCount.IsVisible && pageCount.Bounds.Width > 0).IsTrue();

            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            tab.GoToPage(MiddlePage);
            var scrolled = await UiWait.UntilAsync(() => tab.CurrentPageIndex >= MiddlePage - 1);
            await Assert.That(scrolled).IsTrue();
            await Assert.That(scroller.Offset.Y).IsGreaterThan(0);

            test.Services.Settings.ColorScheme = ColorSchemeChoice.Calm;
            test.Services.ApplySettings();
            tab.Search.Open();
            tab.Search.Query = "quick brown";
            _ = await UiWait.UntilAsync(() => tab.Search.Results.Count > 0 && test.Services.RenderHub.Scheduler.QueueLength == 0);
            using var calm = window.CaptureRenderedFrame();
            Save(calm, "calm-search.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the start page shows when nothing is open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsStartPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var start = window.GetVisualDescendants().OfType<StartView>().Single();
            await Assert.That(start.IsVisible).IsTrue();
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "start.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies each built-in colour scheme themes the chrome and the pages, saving a screenshot of each.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesEachScheme()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("schemes.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        var application = Application.Current!;
        try
        {
            ColorSchemeChoice[] choices = [ColorSchemeChoice.Calm, ColorSchemeChoice.HighContrast, ColorSchemeChoice.Dark, ColorSchemeChoice.Light];
            foreach (var choice in choices)
            {
                test.Services.Settings.ColorScheme = choice;
                test.Services.ApplySettings();
                DesktopThemeApplier.Apply(application, test.Services.CurrentTheme);
                _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > ExpectedTiles && test.Services.RenderHub.Scheduler.QueueLength == 0);
                using var frame = window.CaptureRenderedFrame();
                Save(frame, $"scheme-{choice}.png");

                var foreground = (ISolidColorBrush)application.Resources["AppForeground"]!;
                await Assert.That(foreground.Color.ToUInt32() & RgbMask).IsEqualTo(test.Services.CurrentTheme.Scheme.Text);
                await Assert.That(main.SelectedTab!.PageTone).IsEqualTo(test.Services.CurrentTheme.PageTone);
            }
        }
        finally
        {
            window.Close();
            DesktopThemeApplier.Apply(application, ThemeResolver.Resolve(new(), null));
        }
    }

    /// <summary>Verifies the annotation tools show, and a highlight and note appear on the page and in the sidebar.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsAnnotations()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("annotated.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            using var prompt = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Check this figure"));
            var lines = new List<PageRect>();
            tab.TryGetDocument()!.GetTextBounds(0, 0, TestPdf.Sentence.Length, lines);
            tab.Annotations.IsAnnotating = true;
            tab.Annotations.Tool = AnnotationTool.Highlight;
            tab.SidebarMode = SidebarMode.Annotations;
            _ = tab.Annotations.MarkText(AnnotationKind.Highlight, new Dictionary<int, List<PageRect>> { [0] = lines }, AnnotationColors.Sand);
            await tab.Annotations.AddNoteAsync(0, NoteAt);

            // The comment sidebar narrows the pages, so fewer tiles show than in a full window; settled is enough.
            await Assert.That(await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > 0 && test.Services.RenderHub.Scheduler.QueueLength == 0)).IsTrue();
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "annotate.png");

            var bar = window.GetVisualDescendants().OfType<Border>().Single(static b => b.Name == "AnnotateBar");
            await Assert.That(bar.IsVisible).IsTrue();
            await Assert.That(tab.Annotations.Items.Count).IsEqualTo(AnnotationCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies clicking a text field opens an editor over it, and that typing fills the field.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillsFormFields()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = Path.Combine(test.Directory, "form.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateForm());
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var fields = new List<FormField>();
            ((IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);
            var nameField = fields.Single(static f => f.Kind == FormFieldKind.Text);
            var agree = fields.Single(static f => f.Kind == FormFieldKind.CheckBox);
            var centre = new PagePoint(nameField.Bounds.Left + (nameField.Bounds.Width * Half), nameField.Bounds.Top + (nameField.Bounds.Height * Half));

            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > 0 && test.Services.RenderHub.Scheduler.QueueLength == 0);
            var hit = tab.Forms.HitTest(0, centre);
            _ = tab.Forms.Activate(hit!);
            _ = tab.Forms.Activate(agree);
            tab.Forms.EditText = "Glenn Watson";
            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Scheduler.QueueLength == 0);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "form.png");
            var editor = window.GetVisualDescendants().OfType<TextBox>().Single(static t => t.Name == "FieldEditor");
            var editorShown = editor.IsVisible;
            tab.Forms.Commit();
            fields.Clear();
            ((IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);

            await Assert.That(hit).IsNotNull();
            await Assert.That(editorShown).IsTrue();
            await Assert.That(fields.Single(static f => f.Kind == FormFieldKind.Text).Value).IsEqualTo("Glenn Watson");
            await Assert.That(fields.Single(static f => f.Kind == FormFieldKind.CheckBox).IsChecked).IsTrue();
            await Assert.That(tab.HasUnsavedChanges).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies page by page shows one whole page at a time and Page Down steps to the next whole page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsOnePageAtATime()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages.pdf", PageByPagePages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            tab.ZoomMode = Core.Layout.ZoomMode.FitPage;
            tab.IsPageByPage = true;
            _ = await UiWait.UntilAsync(() => scroller.Extent.Height >= scroller.Viewport.Height * PageByPagePages);
            var viewport = scroller.Viewport.Height;

            tab.GoToPage(1);
            var onSecond = await UiWait.UntilAsync(() => Math.Abs(scroller.Offset.Y - viewport) < 1);
            _ = canvas.Focus();
            window.KeyPress(Avalonia.Input.Key.PageDown, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.PageDown, null);
            var onThird = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SpreadPage && Math.Abs(scroller.Offset.Y - (viewport * SpreadPage)) < 1);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "page-by-page.png");

            await Assert.That(scroller.Extent.Height).IsEqualTo(viewport * PageByPagePages);
            await Assert.That(onSecond).IsTrue();
            await Assert.That(onThird).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Verifies the page arrows, the page box and the page count share one centre line, and that both arrows' drawn
    /// strokes are centred on it too, measured from the rendered pixels.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlignsPageNavigation()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("align.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = await UiWait.UntilAsync(() => view.GetVisualDescendants().OfType<TextBlock>().Single(static t => t.Name == PageCountName).Bounds.Width > 0);
            var previous = Find<Button>(view, "PreviousPageButton");
            var next = Find<Button>(view, "NextPageButton");
            var box = Find<TextBox>(view, "PageBox");
            var count = Find<TextBlock>(view, PageCountName);
            using var frame = window.CaptureRenderedFrame()!;
            Save(frame, "page-navigation.png");
            var pixels = ReadPixels(frame, out var stride);
            var line = CentreY(previous, window);

            await Assert.That(Math.Abs(CentreY(next, window) - line)).IsLessThan(AlignmentTolerance);
            await Assert.That(Math.Abs(CentreY(box, window) - line)).IsLessThan(AlignmentTolerance);
            await Assert.That(Math.Abs(CentreY(count, window) - line)).IsLessThan(AlignmentTolerance);
            await Assert.That(Math.Abs(InkCentreY(pixels, stride, previous, window) - line)).IsLessThan(AlignmentTolerance);
            await Assert.That(Math.Abs(InkCentreY(pixels, stride, next, window) - line)).IsLessThan(AlignmentTolerance);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies Read Aloud shows its bar with Pause while reading, ticks the tool bar button and marks the sentence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsReadAloudBar()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("read.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var reader = main.SelectedTab!.ReadAloud;
            _ = await UiWait.UntilAsync(() => Find<Border>(view, ReadAloudBarName) is not null);
            Find<ToggleButton>(view, "ReadAloudToggle").IsChecked = true;

            await Assert.That(await UiWait.UntilAsync(() => reader.SpokenBounds.Count > 0 && Find<Button>(view, "PauseButton").IsVisible)).IsTrue();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = window.CaptureRenderedFrame()!;
            Save(frame, "read-aloud.png");

            var voiceText = Find<ComboBox>(view, "VoiceBox").GetVisualDescendants().OfType<TextBlock>().Any(static t => t.Text == "One, Test voice" && t.IsEffectivelyVisible);
            await Assert.That(voiceText).IsTrue();
            await Assert.That(reader.IsOpen).IsTrue();
            await Assert.That(Find<Border>(view, ReadAloudBarName).IsVisible).IsTrue();
            await Assert.That(Find<Button>(view, "PlayButton").IsVisible).IsFalse();
            await Assert.That(Find<ComboBox>(view, "VoiceBox").SelectedIndex).IsEqualTo(0);
            await Assert.That(Find<Button>(view, "DownloadVoiceButton").IsVisible).IsFalse();

            Find<Button>(view, "CloseReadAloudButton").Command!.Execute(null);
            await Assert.That(await UiWait.UntilAsync(() => !Find<Border>(view, ReadAloudBarName).IsVisible)).IsTrue();
            await Assert.That(Find<ToggleButton>(view, "ReadAloudToggle").IsChecked == true).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies Focus Mode shows the text alone in reading order, follows the text settings and keeps the place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsFocusMode()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new FallbackPlatform(), speech);
        var path = Path.Combine(test.Directory, "article.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(ArticlePages));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = await UiWait.UntilAsync(() => Find<ToggleButton>(view, FocusToggleName) is not null);
            Find<ToggleButton>(view, FocusToggleName).IsChecked = true;

            var focus = Find<FocusView>(view, "FocusPane");
            await Assert.That(await UiWait.UntilAsync(() => focus.PageViews().Count > 0 && focus.PageViews()[0].BlockViews().Count > 0)).IsTrue();
            var texts = focus.PageViews()[0].BlockViews().Select(static b => b.ViewModel!.Text).ToArray();
            await Assert.That(texts[0]).IsEqualTo("Reading Order in Practice");
            await Assert.That(texts[1]).StartsWith("The left column opens");
            await Assert.That(texts[^1]).StartsWith("1 A footnote");
            await Assert.That(Find<ScrollViewer>(view, "Scroller").IsVisible).IsFalse();

            tab.FocusMode.FontSize = FocusTextSize;
            tab.FocusMode.PageColour = SoftPaperColour;
            tab.ReadAloud.IsOpen = true;
            await Assert.That(await UiWait.UntilAsync(() => focus.PageViews()[0].BlockViews().Exists(static b => !b.ViewModel!.Spoken.IsEmpty))).IsTrue();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using (var frame = window.CaptureRenderedFrame()!)
            {
                Save(frame, "focus-mode.png");
            }

            await Assert.That(focus.PageViews()[0].BlockViews()[0].BodyText.FontSize).IsGreaterThan(FocusTextSize);
            tab.ReadAloud.IsOpen = false;
            focus.ViewModel!.ReportTop(1, 0);
            Find<ToggleButton>(view, FocusToggleName).IsChecked = false;

            await Assert.That(await UiWait.UntilAsync(() => tab.CurrentPageIndex == 1)).IsTrue();
            await Assert.That(Find<ScrollViewer>(view, "Scroller").IsVisible).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies presenting hides everything but the page, steps pages with the arrow keys and Esc restores the view.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PresentsOnePageFullScreen()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("present.pdf", PageByPagePages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var chrome = Find<StackPanel>(view, "Chrome");
            var tabBar = Find<Border>(window, "TabBar");
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var zoomBefore = tab.ZoomMode;

            _ = await tab.PresentCommand.Execute().ToTask();
            var presenting = await UiWait.UntilAsync(() => !chrome.IsVisible && !tabBar.IsVisible && window.WindowState == WindowState.FullScreen && tab.CurrentPageIndex == 0);
            _ = canvas.Focus();
            window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.ArrowRight, null);
            var stepped = await UiWait.UntilAsync(() => tab.CurrentPageIndex == 1);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "presenting.png");
            window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
            var restored = await UiWait.UntilAsync(() => chrome.IsVisible && tabBar.IsVisible && window.WindowState != WindowState.FullScreen);

            await Assert.That(presenting).IsTrue();
            await Assert.That(stepped).IsTrue();
            await Assert.That(restored).IsTrue();
            await Assert.That(tab.IsPageByPage).IsFalse();
            await Assert.That(tab.ZoomMode).IsEqualTo(zoomBefore);
            await Assert.That(tab.SidebarVisible).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies caret navigation starts on the current page and moves by character and by line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesCaretThroughText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret.pdf", PageByPagePages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > 0);

            _ = await tab.ToggleCaretModeCommand.Execute().ToTask();
            var started = await UiWait.UntilAsync(() => canvas.Caret == (0, 0));
            for (var i = 0; i < CaretSteps; i++)
            {
                window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.ArrowRight, null);
            }

            var moved = await UiWait.UntilAsync(() => canvas.Caret == (0, CaretSteps));
            window.KeyPress(Avalonia.Input.Key.Down, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.ArrowDown, null);
            var down = await UiWait.UntilAsync(() => canvas.Caret.Char > HeadingLength);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "caret.png");

            await Assert.That(started).IsTrue();
            await Assert.That(moved).IsTrue();
            await Assert.That(down).IsTrue();
            await Assert.That(canvas.Caret.Page).IsEqualTo(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the print preview shows a sheet for each page that will print, saving a screenshot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsPrintPreview()
    {
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter()));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("preview.pdf", PageByPagePages)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var window = new PrintPreviewWindow { ViewModel = preview, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var ready = await UiWait.UntilAsync(() => preview.IsValid && preview.Destination == PrintDestination.Printer
                && window.GetVisualDescendants().OfType<Image>().Any(static image => image.Source is not null));
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "print-preview.png");

            await Assert.That(ready).IsTrue();
            await Assert.That(preview.Sheets.Count).IsEqualTo(PageByPagePages);
            await Assert.That(preview.Summary).IsEqualTo("4 sheets");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the layers panel lists a layered document's layers and ticking one shows it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsAndHidesLayers()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "layers.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithLayers());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var toggle = Find<ToggleButton>(view, "LayersToggle");
            var layers = tab.Layers;
            var listed = await UiWait.UntilAsync(() => layers.HasLayers && toggle.IsVisible);
            tab.IsLayersMode = true;
            layers.Items[1].IsVisible = true;
            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > 0 && test.Services.RenderHub.Scheduler.QueueLength == 0);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "layers.png");
            var source = (Core.Documents.ILayerSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(Core.Documents.ILayerSource))!;

            await Assert.That(listed).IsTrue();
            await Assert.That(layers.Items.Count).IsEqualTo(LayerCount);
            await Assert.That(source.GetLayers()[1].IsVisible).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Finds a named control.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">Where to look.</param>
    /// <param name="name">The name.</param>
    /// <returns>The control.</returns>
    private static T Find<T>(Visual root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    /// <summary>Gets a control's vertical centre in window coordinates.</summary>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The centre.</returns>
    private static double CentreY(Control control, Visual window) =>
        control.TranslatePoint(new(0, control.Bounds.Height * Half), window)!.Value.Y;

    /// <summary>Copies a frame's BGRA pixels.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="stride">The bytes per row.</param>
    /// <returns>The pixels.</returns>
    private static byte[] ReadPixels(WriteableBitmap frame, out int stride)
    {
        using var locked = frame.Lock();
        stride = locked.RowBytes;
        var pixels = new byte[stride * locked.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(locked.Address, pixels, 0, pixels.Length);
        return pixels;
    }

    /// <summary>Finds the vertical centre of what is drawn inside a control, against its top-left background.</summary>
    /// <param name="pixels">The frame pixels.</param>
    /// <param name="stride">The bytes per row.</param>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The centre of the drawn rows.</returns>
    private static double InkCentreY(byte[] pixels, int stride, Control control, Visual window)
    {
        var origin = control.TranslatePoint(default, window)!.Value;
        var left = (int)origin.X;
        var top = (int)origin.Y;
        var width = (int)control.Bounds.Width;
        var height = (int)control.Bounds.Height;
        var background = pixels.AsSpan((top * stride) + (left * BytesPerFramePixel), BytesPerFramePixel).ToArray();
        var first = -1;
        var last = -1;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var pixel = pixels.AsSpan((y * stride) + (x * BytesPerFramePixel), BytesPerFramePixel);
                if (Math.Abs(pixel[0] - background[0]) + Math.Abs(pixel[1] - background[1]) + Math.Abs(pixel[2] - background[2]) <= InkThreshold)
                {
                    continue;
                }

                first = first < 0 ? y : first;
                last = y;
                break;
            }
        }

        return (first + last + 1) * (double)Half;
    }

    /// <summary>Saves a frame when screenshots are requested.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="name">The file name.</param>
    private static void Save(WriteableBitmap? frame, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PDFVIEWERLITE_SCREENSHOTS");
        if (frame is null || string.IsNullOrEmpty(directory))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }
}
