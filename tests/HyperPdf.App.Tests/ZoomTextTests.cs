// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the zoom label changes only when the rounded percent does.</summary>
public sealed class ZoomTextTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The percent the first zoom shows.</summary>
    private const string FirstPercent = "150%";

    /// <summary>The percent the second zoom shows.</summary>
    private const string SecondPercent = "200%";

    /// <summary>A zoom that rounds to the same percent leaves the label alone, and a new percent raises it once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomTextRaisesOnlyWhenPercentChanges()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("zoom-text.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var seen = new List<string>();
        using var subscription = tab.WhenChanged(static x => x.ZoomText).Skip(1).SubscribeSafe(seen.Add, static _ => { });

        _ = await tab.SetZoomCommand.Execute("150").ToTask();
        _ = await tab.SetZoomCommand.Execute("150.2").ToTask();
        _ = await tab.SetZoomCommand.Execute("200").ToTask();

        await Assert.That(tab.ZoomText).IsEqualTo(SecondPercent);
        await Assert.That(seen.ToArray()).IsEquivalentTo([FirstPercent, SecondPercent]);
    }
}
