// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks the protect and remove protection windows: masked passwords, names for screen readers, the warning and the owner row.</summary>
public sealed class ProtectWindowTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 680;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 860;

    /// <summary>The name of the save button.</summary>
    private const string SaveButtonName = "SaveButton";

    /// <summary>The name of the owner password box in both windows.</summary>
    private const string OwnerBoxName = "OwnerBox";

    /// <summary>The name of the document the windows are made for.</summary>
    private const string SourceName = "contract.pdf";

    /// <summary>Password boxes are masked and named, the buttons have defaults, and the status lines are announced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProtectWindowMasksPasswordsAndNamesControls()
    {
        using var test = new TestServices();
        using var vm = new ProtectViewModel(new FakeDocumentProtector(), test.CreateDocument(SourceName, TestPdf.Create(Pages)));
        var window = new ProtectWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);

            foreach (var name in new[] { "UserBox", "UserRepeatBox", OwnerBoxName, "OwnerRepeatBox", "CurrentOwnerBox" })
            {
                var box = window.FindControl<TextBox>(name)!;
                await Assert.That(box.PasswordChar).IsNotEqualTo('\0');
                await Assert.That(AutomationProperties.GetName(box)).IsNotEmpty();
            }

            await Assert.That(window.FindControl<StackPanel>("CurrentOwnerRow")!.IsVisible).IsFalse();
            await Assert.That(window.FindControl<CheckBox>("AccessibilityCheck")!.IsChecked).IsTrue();
            await Assert.That(window.FindControl<Button>(SaveButtonName)!.IsDefault).IsTrue();
            await Assert.That(window.FindControl<Button>("CloseButton")!.IsCancel).IsTrue();
            await Assert.That(AutomationProperties.GetLiveSetting(window.FindControl<TextBlock>("AccessibilityWarningText")!)).IsEqualTo(AutomationLiveSetting.Polite);
            await Assert.That(AutomationProperties.GetLiveSetting(window.FindControl<TextBlock>("ErrorText")!)).IsEqualTo(AutomationLiveSetting.Assertive);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Clearing the screen reader permission shows the warning at once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClearingScreenReaderAccessShowsTheWarning()
    {
        using var test = new TestServices();
        using var vm = new ProtectViewModel(new FakeDocumentProtector(), test.CreateDocument(SourceName, TestPdf.Create(Pages)));
        var window = new ProtectWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            var warning = window.FindControl<TextBlock>("AccessibilityWarningText")!;
            await Assert.That(warning.Text).IsNullOrEmpty();

            window.FindControl<CheckBox>("AccessibilityCheck")!.IsChecked = false;

            _ = await UiWait.UntilAsync(() => !string.IsNullOrEmpty(warning.Text));
            await Assert.That(warning.Text).Contains("screen readers");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The keyboard starts in the box that is needed first: the current owner password when the document asks for it, otherwise the reading password.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeyboardStartsInTheBoxThatIsNeededFirst()
    {
        using var test = new TestServices();
        var path = test.CreateDocument(SourceName, TestPdf.Create(Pages));
        using var plain = new ProtectViewModel(new FakeDocumentProtector(), path);
        using var locked = new ProtectViewModel(new FakeDocumentProtector { Status = new(true, false, false) }, path);
        using var removal = new RemoveProtectionViewModel(new FakeDocumentProtector { Status = new(true, false, false) }, path);
        using var freeRemoval = new RemoveProtectionViewModel(new FakeDocumentProtector { Status = new(true, true, false) }, path);
        var plainWindow = new ProtectWindow { ViewModel = plain, Width = WindowWidth, Height = WindowHeight };
        var lockedWindow = new ProtectWindow { ViewModel = locked, Width = WindowWidth, Height = WindowHeight };
        var removalWindow = new RemoveProtectionWindow { ViewModel = removal };
        var freeWindow = new RemoveProtectionWindow { ViewModel = freeRemoval };
        Window[] windows = [plainWindow, lockedWindow, removalWindow, freeWindow];
        string[] focusedNames = ["UserBox", "CurrentOwnerBox", OwnerBoxName, SaveButtonName];

        // Only the active window holds the keyboard, so each window is shown on its own.
        for (var i = 0; i < windows.Length; i++)
        {
            var window = windows[i];
            var name = focusedNames[i];
            window.Show();
            try
            {
                var control = window.FindControl<Control>(name)!;
                var focused = await UiWait.UntilAsync(
                    () => control.IsFocused,
                    () => $"{name} visible {control.IsEffectivelyVisible} focusable {control.Focusable} active {window.IsActive} focused {window.FocusManager?.GetFocusedElement()}");

                await Assert.That(focused).IsTrue().Because(name);
            }
            finally
            {
                window.Close();
            }
        }
    }

    /// <summary>A document opened with its reading password asks for the owner password in both windows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnerPasswordRowShowsOnlyWhenNeeded()
    {
        using var test = new TestServices();
        var path = test.CreateDocument(SourceName, TestPdf.Create(Pages));
        using var needed = new RemoveProtectionViewModel(new FakeDocumentProtector { Status = new(true, false, false) }, path);
        using var notNeeded = new RemoveProtectionViewModel(new FakeDocumentProtector { Status = new(true, true, false) }, path);
        var asked = new RemoveProtectionWindow { ViewModel = needed };
        var free = new RemoveProtectionWindow { ViewModel = notNeeded };
        asked.Show();
        free.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => asked.IsVisible && free.IsVisible);

            await Assert.That(asked.FindControl<StackPanel>("OwnerRow")!.IsVisible).IsTrue();
            await Assert.That(free.FindControl<StackPanel>("OwnerRow")!.IsVisible).IsFalse();
            await Assert.That(asked.FindControl<TextBox>(OwnerBoxName)!.PasswordChar).IsNotEqualTo('\0');
            await Assert.That(asked.FindControl<Button>(SaveButtonName)!.IsDefault).IsTrue();
        }
        finally
        {
            asked.Close();
            free.Close();
        }
    }
}
