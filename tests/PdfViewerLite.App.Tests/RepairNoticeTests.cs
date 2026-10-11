// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests the calm notice shown once when a file had to be repaired to be opened.</summary>
public sealed class RepairNoticeTests
{
    /// <summary>Verifies a damaged file shows the notice, details come and go on request, and dismissing it keeps it away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedFileShowsTheNoticeOnce()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        var path = Path.Combine(test.Directory, "damaged.pdf");
        var damaged = Encoding.Latin1.GetString(TestPdf.Create(1)).Replace("startxref", "startxxxx", StringComparison.Ordinal);
        await File.WriteAllBytesAsync(path, Encoding.Latin1.GetBytes(damaged));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;

        await Assert.That(await UiWait.UntilAsync(() => tab.RepairNotice is not null)).IsTrue();
        await Assert.That(tab.RepairNotice).IsEqualTo(RepairWarnings.Summary);
        await Assert.That(tab.RepairDetails).IsNull();

        _ = await tab.ToggleRepairDetailsCommand.Execute().ToTask();
        await Assert.That(tab.RepairDetails).IsNotNull();
        _ = await tab.ToggleRepairDetailsCommand.Execute().ToTask();
        await Assert.That(tab.RepairDetails).IsNull();

        _ = await tab.DismissRepairNoticeCommand.Execute().ToTask();
        tab.GoToPage(0);
        await Assert.That(tab.RepairNotice).IsNull();
    }

    /// <summary>Verifies a clean file shows no notice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanFileShowsNoNotice()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        var path = Path.Combine(test.Directory, "clean.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(1));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;

        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        await Assert.That(tab.RepairNotice).IsNull();
    }
}
