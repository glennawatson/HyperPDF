// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;

namespace HyperPdf.App.Tests;

/// <summary>Checks that each choice about following a document's open hints is offered, kept in the settings file and read back.</summary>
public sealed class OpenHintPreferencesTests
{
    /// <summary>The settings file used by the tests.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>Verifies that every hint starts on, except that bookmark colours start ignored, and that each choice round trips.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OpenHintChoices_RoundTrip()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, SettingsFile);
        using (var preferences = new PreferencesViewModel(test.Services))
        {
            await Assert.That(preferences.FollowOpenAction).IsTrue();
            await Assert.That(preferences.FollowPageMode).IsTrue();
            await Assert.That(preferences.FollowPageLayout).IsTrue();
            await Assert.That(preferences.FollowDisplayDocTitle).IsTrue();
            await Assert.That(preferences.FollowPrintDefaults).IsTrue();
            await Assert.That(preferences.IgnoreOutlineColours).IsTrue();
            preferences.FollowOpenAction = false;
            preferences.FollowPageMode = false;
            preferences.FollowPageLayout = false;
            preferences.FollowDisplayDocTitle = false;
            preferences.FollowPrintDefaults = false;
            preferences.IgnoreOutlineColours = false;
        }

        var saved = new SettingsStore(path).Load();
        using var nextRun = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), new AvaloniaRenderHead());
        using var reloaded = new PreferencesViewModel(nextRun);

        await Assert.That(saved.FollowOpenAction).IsFalse();
        await Assert.That(saved.FollowPageMode).IsFalse();
        await Assert.That(saved.FollowPageLayout).IsFalse();
        await Assert.That(saved.FollowDisplayDocTitle).IsFalse();
        await Assert.That(saved.FollowPrintDefaults).IsFalse();
        await Assert.That(saved.IgnoreOutlineColours).IsFalse();
        await Assert.That(reloaded.FollowOpenAction).IsFalse();
        await Assert.That(reloaded.IgnoreOutlineColours).IsFalse();
    }

    /// <summary>Verifies that page transitions and timings start off, so nothing moves by itself, and that each choice round trips.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PresentationChoices_StartOffAndRoundTrip()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, SettingsFile);
        using (var preferences = new PreferencesViewModel(test.Services))
        {
            await Assert.That(preferences.FollowPageTransitions).IsFalse();
            await Assert.That(preferences.FollowPageTimings).IsFalse();
            preferences.FollowPageTransitions = true;
            preferences.FollowPageTimings = true;
        }

        var saved = new SettingsStore(path).Load();
        using var nextRun = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), new AvaloniaRenderHead());
        using var reloaded = new PreferencesViewModel(nextRun);

        await Assert.That(saved.FollowPageTransitions).IsTrue();
        await Assert.That(saved.FollowPageTimings).IsTrue();
        await Assert.That(reloaded.FollowPageTransitions).IsTrue();
        await Assert.That(reloaded.FollowPageTimings).IsTrue();
    }
}
