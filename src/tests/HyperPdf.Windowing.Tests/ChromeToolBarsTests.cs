// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;

namespace HyperPdf.Windowing.Tests;

/// <summary>Verifies low-dialog signature bars in <see cref="ChromeToolBars"/>: inline digital-signature verification and one-click remembered hand signatures.</summary>
[NotInParallel]
public sealed class ChromeToolBarsTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1280;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 800;

    /// <summary>The page count of the test documents.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text per page in the unsigned document.</summary>
    private const int Lines = 4;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The text of the inline signature verification button.</summary>
    private const string CheckSignaturesText = "Check signatures";

    /// <summary>The text of the hand-signature button on the Fill and Sign bar.</summary>
    private const string SignatureButtonText = "Signature";

    /// <summary>Opening a signed PDF shows the signed-document bar lazily and checks signatures inline without opening a modal dialog.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SignedBar_SignedDocument_VerifiesInlineWithoutOpeningModalDialog()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-signedbar-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        SignedBarOutcome outcome;
        try
        {
            using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
            var path = Path.Combine(folder, "signed.pdf");
            await File.WriteAllBytesAsync(path, TestSignedPdf.Create(Pages, certificate));
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            var tab = session.Main.SelectedTab!;

            var barShownAtOpen = PumpUntil(session, () => chrome.Bars.Exists(static item => item.Text == CheckSignaturesText));
            var lazyBeforePress = tab.SignaturesIfMade is null;

            var checkIndex = chrome.Bars.FindIndex(static item => item.Text == CheckSignaturesText);
            _ = chrome.PressControl(false, checkIndex);
            var verifiedInline = PumpUntil(
                session,
                () => chrome.Bars.Exists(static item => item.Text.Contains(TestSignedPdf.SignerName, StringComparison.Ordinal))
                    && chrome.Bars.Exists(static item => item.Text == "Details…"));

            outcome = new(barShownAtOpen, lazyBeforePress, verifiedInline, !chrome.HasOverlay);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.BarShownAtOpen).IsTrue();
        await Assert.That(outcome.LazyBeforePress).IsTrue();
        await Assert.That(outcome.VerifiedInline).IsTrue();
        await Assert.That(outcome.NoModalDialog).IsTrue();
    }

    /// <summary>When a hand signature is remembered, the Fill and Sign bar offers Change signature alongside Signature and pressing Signature places it on the page with zero dialogs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FillAndSignBar_RememberedSignature_PlacesDirectlyWithoutDialog()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-fillsign-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        FillSignOutcome outcome;
        try
        {
            var path = Path.Combine(folder, "unsigned.pdf");
            await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            services.SignatureMarks.Set(SignatureMarkKind.Signature, SignatureMark.Typed(SignatureMarkKind.Signature, "Grace Hopper"));
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            var tab = session.Main.SelectedTab!;

            ((ICommand)tab.FillAndSign.ToggleCommand).Execute(null);
            var barReady = PumpUntil(session, () => chrome.Bars.Exists(static item => item.Text == "Change signature…"));
            var signIndex = chrome.Bars.FindIndex(static item => item.Text == SignatureButtonText);
            _ = chrome.PressControl(false, signIndex);
            var placing = PumpUntil(session, () => tab.FillAndSign.Placement is not null);
            outcome = new(barReady, placing, !chrome.HasOverlay);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.OffersChangeSignature).IsTrue();
        await Assert.That(outcome.StartedPlacing).IsTrue();
        await Assert.That(outcome.NoDialogOpened).IsTrue();
    }

    /// <summary>Pumps the session until a condition holds or the timeout elapses.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">The condition.</param>
    /// <returns><see langword="true"/> when met before timeout.</returns>
    private static bool PumpUntil(ViewerSession session, Func<bool> done)
    {
        var started = Stopwatch.GetTimestamp();
        while (!done())
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                return false;
            }

            _ = session.PumpOnce(PumpMilliseconds);
        }

        return true;
    }

    /// <summary>Records the signed-document bar verification checks.</summary>
    /// <param name="BarShownAtOpen">Whether the signed-document bar appeared on open.</param>
    /// <param name="LazyBeforePress">Whether <c>SignaturesViewModel</c> stayed unmade until requested.</param>
    /// <param name="VerifiedInline">Whether signer status and Details appeared in place.</param>
    /// <param name="NoModalDialog">Whether no modal dialog opened.</param>
    private readonly record struct SignedBarOutcome(bool BarShownAtOpen, bool LazyBeforePress, bool VerifiedInline, bool NoModalDialog);

    /// <summary>Records the remembered Fill and Sign bar checks.</summary>
    /// <param name="OffersChangeSignature">Whether the Change signature button was shown.</param>
    /// <param name="StartedPlacing">Whether pressing Signature started page placement immediately.</param>
    /// <param name="NoDialogOpened">Whether no modal dialog opened.</param>
    private readonly record struct FillSignOutcome(bool OffersChangeSignature, bool StartedPlacing, bool NoDialogOpened);
}
