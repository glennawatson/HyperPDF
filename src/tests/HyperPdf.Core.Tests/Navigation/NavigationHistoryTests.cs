// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Navigation;

namespace HyperPdf.Core.Tests.Navigation;

/// <summary>Tests for <see cref="NavigationHistory"/>.</summary>
public sealed class NavigationHistoryTests
{
    /// <summary>Verifies back and forward move between recorded positions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GoesBackAndForward()
    {
        const int laterPage = 9;
        var history = new NavigationHistory();
        history.Push(new(0, 0));
        var current = new DocumentPosition(laterPage, 0);

        await Assert.That(history.TryGoBack(current, out var back)).IsTrue();
        await Assert.That(back.PageIndex).IsEqualTo(0);
        await Assert.That(history.TryGoForward(back, out var forward)).IsTrue();
        await Assert.That(forward.PageIndex).IsEqualTo(laterPage);
        await Assert.That(history.CanGoForward).IsFalse();
    }

    /// <summary>Verifies a new jump clears forward history.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PushClearsForward()
    {
        const int otherPage = 4;
        var history = new NavigationHistory();
        history.Push(new(0, 0));
        _ = history.TryGoBack(new(otherPage, 0), out _);

        history.Push(new(1, 0));

        await Assert.That(history.CanGoForward).IsFalse();
        await Assert.That(history.BackCount).IsEqualTo(1);
    }
}
