// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the Tags panel can be used with a screen reader or low vision: everything has a name, and each input has a visible label.</summary>
public sealed class TagsPanelAccessibilityTests
{
    /// <summary>The width of the test window.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The height of the test window.</summary>
    private const double WindowHeight = 800;

    /// <summary>Every button, list and input in the panel has a name, and every input is tied to a label that is on screen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryControlHasANameAndEveryInputHasAVisibleLabel()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-a11y.pdf", TestPdf.CreateTagged())]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            main.SelectedTab!.IsTagsMode = true;
            var panel = window.GetVisualDescendants().OfType<TagsPanelView>().Single();
            var controls = panel.GetVisualDescendants().OfType<Control>().Where(static c => c is Button or ListBox or TextBox or ComboBox).ToList();
            var ours = controls.Where(static c => c.Name is { Length: > 0 } name && !name.StartsWith("PART_", StringComparison.Ordinal));
            var unnamed = ours.Where(static c => string.IsNullOrWhiteSpace(AutomationProperties.GetName(c))).Select(static c => c.Name).ToList();
            var inputs = new[] { "TypeBox", "AlternateBox", "ActualBox", "LanguageBox", "TitleBox" };
            var unlabelled = inputs.Where(name => AutomationProperties.GetLabeledBy(panel.FindNamed<Control>(name)) is not TextBlock { Text.Length: > 0 }).ToList();

            using (Assert.Multiple())
            {
                await Assert.That(controls.Count).IsGreaterThan(inputs.Length);
                await Assert.That(unnamed).IsEmpty();
                await Assert.That(unlabelled).IsEmpty();
            }
        }
        finally
        {
            window.Close();
        }
    }
}
