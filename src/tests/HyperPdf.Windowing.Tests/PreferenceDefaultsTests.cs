// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome.Forms;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Windowing.Tests;

/// <summary>Tests what the preferences window says about each setting and how it puts settings back to what a new install starts with.</summary>
[NotInParallel]
public sealed class PreferenceDefaultsTests
{
    /// <summary>The width of the window the services are made for.</summary>
    private const int Width = 800;

    /// <summary>The height of the window the services are made for.</summary>
    private const int Height = 600;

    /// <summary>The name of the tool bar setting.</summary>
    private const string ToolBar = "Tool bar";

    /// <summary>The name of the setting for what draws the window.</summary>
    private const string Drawing = "Drawing (next start)";

    /// <summary>The words of the button that puts every setting back.</summary>
    private const string ResetAll = "Put every setting back to its default";

    /// <summary>The words of the button that undoes that.</summary>
    private const string BringBack = "Bring my settings back from before that";

    /// <summary>Every setting says what it does, so resting the pointer on any of them explains it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EverySettingSaysWhatItDoes()
    {
        var silent = new List<string>();
        Run(preferences =>
        {
            // The rows that only show for one choice are shown too, so their help is checked.
            preferences.PageTone = (int)PageToneChoice.Custom;
            foreach (var row in Rows(preferences))
            {
                if (row.Kind is FormRowKind.Choice or FormRowKind.Toggle or FormRowKind.Field && row.Help is not { Length: > 0 })
                {
                    silent.Add(row.Label);
                }
            }
        });

        await Assert.That(silent).IsEmpty();
    }

    /// <summary>The list of what draws the window holds only what this computer can use, and marks the value a new install starts with.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawingListHoldsOnlyWhatThisComputerCanUse()
    {
        IReadOnlyList<string> options = [];
        Run(preferences => options = Row(preferences, Drawing).Options);

        await Assert.That(options).Contains("Automatic (default)");
        await Assert.That(options).Contains("Processor");
        await Assert.That(options.Contains("Direct3D 11")).IsEqualTo(OperatingSystem.IsWindows());
        await Assert.That(options.Contains("Direct3D 12")).IsEqualTo(OperatingSystem.IsWindows());
        await Assert.That(options.Contains("Vulkan")).IsEqualTo(OperatingSystem.IsWindows() || OperatingSystem.IsLinux());
    }

    /// <summary>A setting that was changed has a button that puts it back, and one at its starting value has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedSettingIsPutBackByItsButton()
    {
        string? helpWhenChanged = null;
        var helpAfter = string.Empty;
        var helpAtStart = string.Empty;
        var toolbarAfter = -1;
        Run(preferences =>
        {
            helpAtStart = Row(preferences, ToolBar).ResetHelp;
            preferences.Toolbar = 1;
            var changed = Row(preferences, ToolBar);
            helpWhenChanged = changed.ResetHelp;
            Send(preferences, new(changed.Id, 0, string.Empty, FormBuilder.ResetRequest));
            toolbarAfter = preferences.Toolbar;
            helpAfter = Row(preferences, ToolBar).ResetHelp;
        });

        await Assert.That(helpAtStart).IsNull();
        await Assert.That(helpWhenChanged).IsEqualTo("Put this back to its default: Text beside icons");
        await Assert.That(toolbarAfter).IsEqualTo(0);
        await Assert.That(helpAfter).IsNull();
    }

    /// <summary>One button puts every setting back, and the other brings them back as they were.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EverySettingIsPutBackAndBroughtBack()
    {
        var afterReset = new List<int>();
        var afterBringBack = new List<int>();
        var canBringBackBefore = true;
        var canBringBackAfter = false;
        Run(preferences =>
        {
            preferences.Toolbar = 1;
            preferences.Motion = 1;
            preferences.CheckSpelling = false;
            canBringBackBefore = Row(preferences, BringBack).Enabled;
            var request = PreferencesForm.For(preferences);
            using var edits = new Signal<FormEdit>();
            using var bound = request.Bind(edits);
            edits.OnNext(new(Described(request, ResetAll).Id, 0, string.Empty, null));
            afterReset.AddRange([preferences.Toolbar, preferences.Motion, preferences.CheckSpelling ? 1 : 0]);
            canBringBackAfter = Described(request, BringBack).Enabled;
            edits.OnNext(new(Described(request, BringBack).Id, 0, string.Empty, null));
            afterBringBack.AddRange([preferences.Toolbar, preferences.Motion, preferences.CheckSpelling ? 1 : 0]);
        });

        await Assert.That(canBringBackBefore).IsFalse();
        await Assert.That(afterReset).IsEquivalentTo([0, 0, 1]);
        await Assert.That(canBringBackAfter).IsTrue();
        await Assert.That(afterBringBack).IsEquivalentTo([1, 1, 0]);
    }

    /// <summary>The values of a new install are only read: showing them writes nothing to the settings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadingTheDefaultsChangesNoSetting()
    {
        var toolbar = -1;
        var defaultToolbar = -1;
        Run(preferences =>
        {
            preferences.Toolbar = 1;
            defaultToolbar = preferences.Defaults.Toolbar;
            preferences.Defaults.Toolbar = 1;
            toolbar = preferences.Toolbar;
        });

        await Assert.That(defaultToolbar).IsEqualTo(0);
        await Assert.That(toolbar).IsEqualTo(1);
    }

    /// <summary>Gets the rows the preferences window shows now.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>The rows.</returns>
    private static List<FormRow> Rows(PreferencesViewModel preferences)
    {
        var builder = new FormBuilder();
        PreferencesForm.For(preferences).Describe(builder);
        return builder.Rows;
    }

    /// <summary>Finds a row of the preferences window by its name.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <param name="label">The row's name.</param>
    /// <returns>The row.</returns>
    private static FormRow Row(PreferencesViewModel preferences, string label) => Rows(preferences).Find(row => row.Label == label)!;

    /// <summary>Finds a row of a window by its name, describing the window again so the row is as it is now.</summary>
    /// <param name="request">The window.</param>
    /// <param name="label">The row's name.</param>
    /// <returns>The row.</returns>
    private static FormRow Described(FormRequest request, string label)
    {
        var builder = new FormBuilder();
        request.Describe(builder);
        return builder.Rows.Find(row => row.Label == label)!;
    }

    /// <summary>Sends one change to the preferences as the window would.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <param name="edit">The change.</param>
    private static void Send(PreferencesViewModel preferences, FormEdit edit)
    {
        using var edits = new Signal<FormEdit>();
        using var bound = PreferencesForm.For(preferences).Bind(edits);
        edits.OnNext(edit);
    }

    /// <summary>Runs a check against preferences kept in a folder of their own, which is removed after.</summary>
    /// <param name="check">The check.</param>
    private static void Run(Action<PreferencesViewModel> check)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-preference-defaults-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            _ = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var preferences = new PreferencesViewModel(services);
            check(preferences);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }
}
