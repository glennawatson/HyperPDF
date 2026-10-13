// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks that closing a window's view model lets go of the documents its tabs held.</summary>
public sealed class WindowLifetimeTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>Disposing a window's view model closes the documents of its tabs, as closing each tab does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposingTheWindowClosesTheDocumentsOfItsTabs()
    {
        using var test = new TestServices();
        var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("one.pdf", Pages), test.CreateDocument("two.pdf", Pages)]);
        var opened = test.Services.Pool.OpenCount;

        main.Dispose();

        using (Assert.Multiple())
        {
            await Assert.That(opened).IsGreaterThan(0);
            await Assert.That(test.Services.Pool.OpenCount).IsEqualTo(0);
        }
    }
}
