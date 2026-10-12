// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Platform.Linux.Accessibility;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests for <see cref="AtSpiFocusAnnouncementCheck"/>.</summary>
public sealed class AtSpiFocusAnnouncementCheckTests
{
    /// <summary>Registrations that make Avalonia's bridge send focus announcements count as a listening screen reader.</summary>
    /// <param name="eventName">The registered event.</param>
    /// <param name="expected">Whether it counts.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("object:state-changed:focused", true)]
    [Arguments("Object:children-changed", true)]
    [Arguments("window:activate", true)]
    [Arguments("focus:", true)]
    [Arguments("*", true)]
    [Arguments("mouse:button", false)]
    [Arguments("keyboard:press", false)]
    [Arguments("", false)]
    [Arguments(" ", false)]
    public async Task ClassifiesRegistrations(string eventName, bool expected) =>
        await Assert.That(AtSpiFocusAnnouncementCheck.IsObjectEvent(eventName)).IsEqualTo(expected);

    /// <summary>Without an accessibility bus the check never acts and disposes cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsInactiveWithoutABus()
    {
        using var check = new AtSpiFocusAnnouncementCheck();
        await Assert.That(check.IsActive).IsFalse();
        await Assert.That(await check.PrepareRepeatAsync(0, CancellationToken.None)).IsFalse();
    }
}
