// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Pdfium;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Tests for signing forms with a signature or initials: Type, Draw and Image choices, placing with the keyboard,
/// Escape, saving, printing, and remembering a mark only when the user asks.
/// </summary>
public sealed class SignatureMarkTests
{
    /// <summary>The pages in the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The name typed as a signature.</summary>
    private const string Name = "Glenn Watson";

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The scanned picture's width.</summary>
    private const int ScanWidth = 48;

    /// <summary>The scanned picture's height.</summary>
    private const int ScanHeight = 16;

    /// <summary>The margin of white paper around the ink in the scan.</summary>
    private const int ScanMargin = 4;

    /// <summary>How far, in points, a placed annotation may sit from where it was placed; typed text has a small inner margin.</summary>
    private const float PlacementTolerance = 6;

    /// <summary>How close two sizes must be, in points.</summary>
    private const float SizeTolerance = 0.01F;

    /// <summary>The width of the ink in the scan, once the paper is trimmed away.</summary>
    private const int InkWidth = ScanWidth - ScanMargin - ScanMargin;

    /// <summary>The screen resolution the scan is saved at.</summary>
    private const double Dpi = 96;

    /// <summary>The bytes in one row of the scan.</summary>
    private const int ScanStride = ScanWidth * Channels;

    /// <summary>How many times to check a condition.</summary>
    private const int WaitSteps = 200;

    /// <summary>The pause between checks.</summary>
    private static readonly TimeSpan WaitStep = TimeSpan.FromMilliseconds(25);

    /// <summary>A zigzag drawn on the pad.</summary>
    private static readonly PagePoint[] Stroke = [new(10, 60), new(40, 10), new(70, 60), new(100, 10), new(130, 60)];

    /// <summary>Where marks are put, on empty paper below the generated page's text.</summary>
    private static readonly PagePoint EmptyPaper = new(306, 600);

    /// <summary>Signatures and initials both offer Type, Draw and Image, and choosing one keeps the others in view.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <param name="title">The window title.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SignatureMarkKind.Signature, "Your Signature")]
    [Arguments(SignatureMarkKind.Initials, "Your Initials")]
    public async Task OffersTypeDrawAndImage(SignatureMarkKind kind, string title)
    {
        using var request = new SignatureMarkViewModel(kind, null);
        var window = new SignatureMarkWindow { ViewModel = request };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible && window.Title == title);
            RadioButton[] choices = [Find<RadioButton>(window, "TypeOption"), Find<RadioButton>(window, "DrawOption"), Find<RadioButton>(window, "ImageOption")];
            var typing = Find<Control>(window, "TypePanel").IsVisible;
            request.IsDrawn = true;
            var drawing = await UiWait.UntilAsync(() => Find<Control>(window, "DrawPanel").IsVisible && !Find<Control>(window, "TypePanel").IsVisible);
            request.IsImage = true;
            var picture = await UiWait.UntilAsync(() => Find<Control>(window, "ImagePanel").IsVisible && !Find<Control>(window, "DrawPanel").IsVisible);

            await Assert.That(window.Title).IsEqualTo(title);
            await Assert.That(choices.Select(static choice => choice.Content as string ?? string.Empty)).IsEquivalentTo(["Type", "Draw", "Image"]);
            await Assert.That(Array.TrueForAll(choices, static choice => choice.IsEffectivelyVisible)).IsTrue();
            await Assert.That(typing).IsTrue();
            await Assert.That(drawing).IsTrue();
            await Assert.That(picture).IsTrue();
            await Assert.That(Find<Button>(window, "UseButton").Content).IsEqualTo(request.UseText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The Fill &amp; Sign row offers Signature and Initials; the mark is moved with the arrow keys, resized with + and
    /// -, placed with Enter, picked up again with Adjust, and removed with Delete.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesMovesResizesAndRemovesWithTheKeyboard()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("keyboard.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var fill = tab.FillAndSign;
            _ = await fill.StartCommand.Execute().ToTask();
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var offered = await UiWait.UntilAsync(() => Find<Button>(view, "SignatureButton").IsEffectivelyVisible && Find<Button>(view, "InitialsButton").IsEffectivelyVisible);

            var typed = await TypeSignatureAsync(window, fill);
            var focused = await UiWait.UntilAsync(() => canvas.IsFocused);
            var start = fill.Placement!.Bounds;
            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Press(window, Key.Down, PhysicalKey.ArrowDown);
            Press(window, Key.Left, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            var moved = fill.Placement!.Bounds;
            Press(window, Key.Add, PhysicalKey.NumPadAdd);
            Press(window, Key.Add, PhysicalKey.NumPadAdd);
            Press(window, Key.Subtract, PhysicalKey.NumPadSubtract);
            var resized = fill.Placement!.Bounds;
            Press(window, Key.Enter, PhysicalKey.Enter);
            var placed = tab.Annotations.Items.Select(static item => item.Annotation).ToList();

            _ = await fill.AdjustCommand.Execute().ToTask();
            var pickedUp = await UiWait.UntilAsync(() => fill.Placement is not null && canvas.IsFocused && tab.Annotations.Items.Count == 0);
            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Press(window, Key.Enter, PhysicalKey.Enter);
            var adjusted = tab.Annotations.Items.Select(static item => item.Annotation).ToList();
            Press(window, Key.Delete, PhysicalKey.Delete);

            await Assert.That(offered).IsTrue();
            await Assert.That(typed).IsTrue();
            await Assert.That(focused).IsTrue();
            await Assert.That(moved).IsEqualTo(start with { Left = start.Left + SignatureMarkLayout.MoveStep - SignatureMarkLayout.FineMoveStep, Top = start.Top + SignatureMarkLayout.MoveStep });
            await Assert.That(Math.Abs(resized.Width - (moved.Width * SignatureMarkLayout.ResizeStep))).IsLessThan(SizeTolerance);
            await Assert.That(placed.Count).IsEqualTo(1);
            await Assert.That(placed[0].Kind).IsEqualTo(AnnotationKind.Signature);
            await Assert.That(Math.Abs(placed[0].Bounds.Left - resized.Left)).IsLessThan(PlacementTolerance);
            await Assert.That(pickedUp).IsTrue();
            await Assert.That(adjusted.Count).IsEqualTo(1);
            await Assert.That(Math.Abs(adjusted[0].Bounds.Left - (placed[0].Bounds.Left + SignatureMarkLayout.MoveStep))).IsLessThan(PlacementTolerance);
            await Assert.That(tab.Annotations.Items.Count).IsEqualTo(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Escape stops placing without changing the document, from the page or from the tool row; cancelling an
    /// adjustment puts the placed mark back where it was.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EscapeCancelsPlacingWithoutChangingTheDocument()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("escape.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var fill = tab.FillAndSign;
            var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IAnnotationEditor))!;
            _ = await fill.StartCommand.Execute().ToTask();
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            _ = await UiWait.UntilAsync(() => Find<Button>(view, "SignatureButton").IsEffectivelyVisible);

            _ = await TypeSignatureAsync(window, fill);
            _ = await UiWait.UntilAsync(() => canvas.IsFocused);
            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Press(window, Key.Escape, PhysicalKey.Escape);
            var cancelledOnPage = fill.Placement is null;

            _ = await TypeSignatureAsync(window, fill);
            var placeButton = Find<Button>(view, "PlaceMarkButton");
            _ = await UiWait.UntilAsync(() => placeButton.IsEffectivelyVisible && placeButton.Focus());
            Press(window, Key.Escape, PhysicalKey.Escape);
            var cancelledOnButton = fill.Placement is null;
            var unchanged = !editor.HasUnsavedChanges && tab.Annotations.Items.Count == 0;

            _ = await TypeSignatureAsync(window, fill);
            _ = fill.Commit();
            var original = tab.Annotations.Items.Single().Annotation.Bounds;
            _ = await fill.AdjustCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => canvas.IsFocused);
            Press(window, Key.Down, PhysicalKey.ArrowDown);
            Press(window, Key.Escape, PhysicalKey.Escape);
            var restored = tab.Annotations.Items.Single().Annotation.Bounds;

            await Assert.That(cancelledOnPage).IsTrue();
            await Assert.That(cancelledOnButton).IsTrue();
            await Assert.That(unchanged).IsTrue();
            await Assert.That(Math.Abs(restored.Top - original.Top)).IsLessThan(PlacementTolerance);
            await Assert.That(fill.HasPlacedMark).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A typed, drawn or picture mark is saved into the PDF and is still there, inked, when the file is reopened.</summary>
    /// <param name="style">How the mark is made.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SignatureMarkStyle.Typed)]
    [Arguments(SignatureMarkStyle.Drawn)]
    [Arguments(SignatureMarkStyle.Image)]
    public async Task SavedMarkSurvivesReopening(SignatureMarkStyle style)
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("save.pdf", Pages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var bounds = await PlaceAsync(tab, style);
        var path = Path.Combine(test.Directory, "saved.pdf");

        var saved = tab.Save(path);
        using var reopened = new PdfiumEngine().Open(path, null);
        List<PageAnnotation> annotations = [];
        ((IAnnotationEditor)DocumentFeatures.CastFeature(reopened, typeof(IAnnotationEditor))!).GetAnnotations(0, annotations);

        await Assert.That(saved).IsTrue();
        await Assert.That(annotations.Count).IsEqualTo(1);
        await Assert.That(annotations[0].Kind).IsEqualTo(AnnotationKind.Signature);
        await Assert.That(Math.Abs(annotations[0].Bounds.Left - bounds.Left)).IsLessThan(PlacementTolerance);
        await Assert.That(Math.Abs(annotations[0].Bounds.Top - bounds.Top)).IsLessThan(PlacementTolerance);
        await Assert.That(PageInk.Count(reopened, 0, bounds, RenderFlags.None)).IsEqualTo(0);
        await Assert.That(PageInk.Count(reopened, 0, bounds, RenderFlags.Annotations)).IsGreaterThan(0);
    }

    /// <summary>A typed, drawn or picture mark is in the copy sent to the printer.</summary>
    /// <param name="style">How the mark is made.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SignatureMarkStyle.Typed)]
    [Arguments(SignatureMarkStyle.Drawn)]
    [Arguments(SignatureMarkStyle.Image)]
    public async Task PrintedCopyKeepsTheMark(SignatureMarkStyle style)
    {
        var printer = new RecordingPrinter();
        using var test = new TestServices(new PrintingPlatform(printer));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("print-sign.pdf", 1)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        printer.InkArea = await PlaceAsync(tab, style);
        using var preview = tab.PrintPreviewInteraction.RegisterHandler(ConfirmWhenReadyAsync);

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(printer.AnnotationCount).IsGreaterThanOrEqualTo(0);
        await Assert.That(printer.InkPixels).IsGreaterThan(0);
        await Assert.That(tab.Notice).IsEqualTo($"Sent to {RecordingPrinter.PrinterName}.");
    }

    /// <summary>
    /// A mark is remembered only when the user ticks Remember; a remembered mark comes back after the viewer restarts,
    /// is offered next time, and Forget removes it at once, even when the window is then cancelled.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReuseIsOptionalAndARememberedMarkCanBeForgotten()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("reuse.pdf", Pages)]);
        var fill = main.SelectedTab!.FillAndSign;
        var file = test.Services.SignatureMarkStore.FilePath;

        using (Answer(fill, SignatureMarkStyle.Typed, false))
        {
            _ = await fill.SignatureCommand.Execute().ToTask();
        }

        _ = fill.Cancel();
        var notRemembered = test.Services.SignatureMarks.Signature is null && !File.Exists(file);
        using (Answer(fill, SignatureMarkStyle.Drawn, true))
        {
            _ = await fill.SignatureCommand.Execute().ToTask();
        }

        _ = fill.Cancel();
        var remembered = test.Services.SignatureMarks.Signature;
        SignatureMark? afterRestart;
        using (var restarted = new AppServices(new SettingsStore(test.Services.SettingsStore.FilePath), new PdfiumEngine(), new FallbackPlatform()))
        {
            afterRestart = restarted.SignatureMarks.Signature;
        }

        var offeredSaved = false;
        SignatureMark? offeredPreview = null;
        using (fill.MarkInteraction.RegisterHandler(async context =>
        {
            offeredSaved = context.Input.HasSaved && context.Input.Remember;
            offeredPreview = context.Input.Result;
            _ = await context.Input.ForgetCommand.Execute().ToTask();
            context.SetOutput(false);
        }))
        {
            _ = await fill.SignatureCommand.Execute().ToTask();
        }

        await Assert.That(notRemembered).IsTrue();
        await Assert.That(remembered).IsNotNull();
        await Assert.That(afterRestart).IsNotNull();
        await Assert.That(afterRestart!.Points.ToArray()).IsEquivalentTo(remembered!.Points.ToArray());
        await Assert.That(test.Services.SignatureMarks.Initials).IsNull();
        await Assert.That(offeredSaved).IsTrue();
        await Assert.That(offeredPreview?.Style).IsEqualTo(SignatureMarkStyle.Drawn);
        await Assert.That(test.Services.SignatureMarks.Signature).IsNull();
        await Assert.That(File.Exists(file)).IsFalse();
        await Assert.That(fill.Placement).IsNull();
    }

    /// <summary>A picture file is read, its white paper removed or kept as chosen, and a file that is not a picture is explained.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAPictureFileAndRemovesThePaper()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "scan.png");
        using (var bitmap = ScanBitmap())
        {
            await using var file = File.Create(path);
            bitmap.Save(file, new PngBitmapEncoderOptions());
        }

        var notPicture = Path.Combine(test.Directory, "notes.txt");
        await File.WriteAllTextAsync(notPicture, "not a picture");
        using var request = new SignatureMarkViewModel(SignatureMarkKind.Signature, null);

        var image = SignatureImageFile.Load(path);
        request.UseImage(image);
        var trimmed = request.Result?.Width;
        var previewed = await UiWait.UntilAsync(() => request.Preview is { Width: InkWidth });
        request.RemovePaper = false;
        var kept = request.Result?.Width;
        request.UseImage(SignatureImageFile.Load(notPicture));

        await Assert.That(image).IsNotNull();
        await Assert.That(image!.Width).IsEqualTo(ScanWidth);
        await Assert.That(request.Style).IsEqualTo(SignatureMarkStyle.Image);
        await Assert.That(trimmed).IsEqualTo((float)InkWidth);
        await Assert.That(previewed).IsTrue();
        await Assert.That(kept).IsEqualTo((float)ScanWidth);
        await Assert.That(request.ImageError).IsNotNull();
        await Assert.That(request.Result).IsNotNull();
    }

    /// <summary>Makes the mark window answer with a mark made one way.</summary>
    /// <param name="fill">The tab's Fill &amp; Sign state.</param>
    /// <param name="style">How to make the mark.</param>
    /// <param name="remember">Whether to tick Remember.</param>
    /// <returns>The registration.</returns>
    private static IDisposable Answer(FillAndSignViewModel fill, SignatureMarkStyle style, bool remember) =>
        fill.MarkInteraction.RegisterHandler(async context =>
        {
            var request = context.Input;
            switch (style)
            {
                case SignatureMarkStyle.Drawn:
                    {
                        _ = await request.AddStrokeCommand.Execute(Stroke).ToTask();
                        break;
                    }

                case SignatureMarkStyle.Image:
                    {
                        request.UseImage(Scan());
                        break;
                    }

                default:
                    {
                        request.Text = Name;
                        break;
                    }
            }

            request.Remember = remember;
            context.SetOutput(true);
        });

    /// <summary>
    /// Asks for a signature through the real window, typing the name and pressing Enter with the keyboard only, and
    /// waits for placing to start.
    /// </summary>
    /// <param name="owner">The main window.</param>
    /// <param name="fill">The tab's Fill &amp; Sign state.</param>
    /// <returns><see langword="true"/> when the window opened, took the name and closed with placing started.</returns>
    private static async Task<bool> TypeSignatureAsync(Window owner, FillAndSignViewModel fill)
    {
        var asking = fill.SignatureCommand.Execute().ToTask();
        SignatureMarkWindow? dialog = null;
        var opened = await UiWait.UntilAsync(() => (dialog = owner.OwnedWindows.OfType<SignatureMarkWindow>().FirstOrDefault(static shown => shown.IsVisible)) is not null);
        if (!opened || dialog is null)
        {
            return false;
        }

        var box = Find<TextBox>(dialog, "MarkTextBox");
        var ready = await UiWait.UntilAsync(() => box.IsFocused);
        dialog.KeyTextInput(Name);
        var previewed = await UiWait.UntilAsync(() => dialog.ViewModel?.Preview is not null && Find<Button>(dialog, "UseButton").IsEffectivelyEnabled);
        Press(dialog, Key.Enter, PhysicalKey.Enter);
        _ = await asking;
        return ready && previewed && fill.Placement is not null;
    }

    /// <summary>Makes a mark one way and places it on empty paper on the first page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="style">How to make the mark.</param>
    /// <returns>Where it was placed.</returns>
    private static async Task<PageRect> PlaceAsync(DocumentTabViewModel tab, SignatureMarkStyle style)
    {
        var fill = tab.FillAndSign;
        using (Answer(fill, style, false))
        {
            _ = await fill.SignatureCommand.Execute().ToTask();
        }

        _ = fill.MoveTo(0, EmptyPaper);
        var bounds = fill.Placement!.Bounds;
        _ = fill.Commit();
        return bounds;
    }

    /// <summary>A scan of a signature: a black bar on white paper.</summary>
    /// <returns>The picture.</returns>
    private static DecodedImage Scan()
    {
        var pixels = new byte[ScanWidth * ScanHeight * Channels];
        for (var y = 0; y < ScanHeight; y++)
        {
            for (var x = 0; x < ScanWidth; x++)
            {
                var ink = x is >= ScanMargin and < ScanWidth - ScanMargin && y is >= ScanMargin and < ScanHeight - ScanMargin;
                var offset = ((y * ScanWidth) + x) * Channels;
                pixels.AsSpan(offset, Channels - 1).Fill(ink ? (byte)0 : byte.MaxValue);
                pixels[offset + Channels - 1] = byte.MaxValue;
            }
        }

        return new("scan.png", pixels, ScanWidth, ScanHeight);
    }

    /// <summary>The scan as a bitmap, to save as a PNG file.</summary>
    /// <returns>The bitmap.</returns>
    private static WriteableBitmap ScanBitmap()
    {
        var scan = Scan();
        var bitmap = new WriteableBitmap(new(ScanWidth, ScanHeight), new(Dpi, Dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = bitmap.Lock();
        var pixels = scan.Pixels.ToArray();
        for (var row = 0; row < ScanHeight; row++)
        {
            System.Runtime.InteropServices.Marshal.Copy(pixels, row * ScanStride, frame.Address + (row * frame.RowBytes), ScanStride);
        }

        return bitmap;
    }

    /// <summary>Presses and releases a key.</summary>
    /// <param name="window">The window.</param>
    /// <param name="key">The key.</param>
    /// <param name="physical">The physical key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None) =>
        window.KeyPress(key, modifiers, physical, null);

    /// <summary>Finds a named control.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">Where to look.</param>
    /// <param name="name">The name.</param>
    /// <returns>The control.</returns>
    /// <exception cref="InvalidOperationException">There is no such control.</exception>
    private static T Find<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ?? throw new InvalidOperationException($"{name} not found.");

    /// <summary>Confirms the print preview once its sheets are ready and the printers are listed, so the job goes to the printer.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private static async Task ConfirmWhenReadyAsync(IInteractionContext<PrintPreviewViewModel, bool> context)
    {
        var preview = context.Input;
        for (var i = 0; i < WaitSteps && !(preview.IsValid && !preview.IsBuilding && preview.Destination == PrintDestination.Printer); i++)
        {
            await Task.Delay(WaitStep);
        }

        context.SetOutput(true);
    }
}
