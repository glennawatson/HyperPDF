// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Tests the calm notice shown once when a file had to be repaired to be opened.</summary>
public sealed class RepairNoticeTests
{
    /// <summary>Verifies a damaged file shows the notice, details come and go on request, and dismissing it keeps it away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedFileShowsTheNoticeOnce()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "damaged.pdf");
        var damaged = Encoding.Latin1.GetString(TestPdf.Create(1)).Replace("startxref", "startxxxx", StringComparison.Ordinal);
        await File.WriteAllBytesAsync(path, Encoding.Latin1.GetBytes(damaged));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;

        await Assert.That(await UiWait.UntilAsync(() => tab.Notices.RepairNotice is not null)).IsTrue();
        await Assert.That(tab.Notices.RepairNotice).IsEqualTo(RepairWarnings.Summary);
        await Assert.That(tab.Notices.RepairDetails).IsNull();

        _ = await tab.Notices.ToggleRepairDetailsCommand.Execute().ToTask();
        await Assert.That(tab.Notices.RepairDetails).IsNotNull();
        _ = await tab.Notices.ToggleRepairDetailsCommand.Execute().ToTask();
        await Assert.That(tab.Notices.RepairDetails).IsNull();

        _ = await tab.Notices.DismissRepairNoticeCommand.Execute().ToTask();
        tab.GoToPage(0);
        await Assert.That(tab.Notices.RepairNotice).IsNull();
    }

    /// <summary>Verifies a clean file shows no notice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanFileShowsNoNotice()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "clean.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(1));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;

        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        await Assert.That(tab.Notices.RepairNotice).IsNull();
    }
}
