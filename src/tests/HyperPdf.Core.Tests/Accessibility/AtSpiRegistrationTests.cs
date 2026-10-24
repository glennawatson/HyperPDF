// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Platform.Linux.Accessibility;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>Starts the real AT-SPI server on a private bus that has a fake accessibility service, as a desktop would have.</summary>
/// <param name="fixture">The private bus shared by the tests of this class.</param>
[ClassDataSource<AccessibilityBusFixture>(Shared = SharedType.PerClass)]
[NotInParallel]
public sealed class AtSpiRegistrationTests(AccessibilityBusFixture fixture)
{
    /// <summary>How long a test waits for the server to attach to the registry.</summary>
    private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a test lets the server run before the registry appears.</summary>
    private static readonly TimeSpan LateRegistryDelay = TimeSpan.FromSeconds(1);

    /// <summary>Verifies that the app finds out that assistive technology is on.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AssistiveTechnology_IsDetected()
    {
        RequireBus();
        using var service = await fixture.StartBusServiceAsync(true);

        await Assert.That(await LinuxAccessibility.IsAssistiveTechnologyEnabledAsync()).IsTrue();
    }

    /// <summary>Verifies that the app finds out that assistive technology is off.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AssistiveTechnology_IsOffWhenTheSwitchIsOff()
    {
        RequireBus();
        using var service = await fixture.StartBusServiceAsync(false);

        await Assert.That(await LinuxAccessibility.IsAssistiveTechnologyEnabledAsync()).IsFalse();
    }

    /// <summary>Verifies that the server connects to the accessibility bus and attaches itself to the registry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Server_AttachesToTheRegistry()
    {
        RequireBus();
        using var service = await fixture.StartBusServiceAsync(true);
        using var registry = await fixture.StartRegistryAsync();
        using var server = await LinuxAccessibility.StartAsync(new FakeAccessibleTree());

        await Assert.That(server).IsNotNull();
        await Assert.That((await registry.Service.Embedded.WaitAsync(AttachTimeout)).Length).IsGreaterThan(0);
    }

    /// <summary>Verifies that a server started before the registry keeps trying and attaches once the registry appears.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Server_AttachesWhenTheRegistryAppearsLater()
    {
        RequireBus();
        using var service = await fixture.StartBusServiceAsync(true);
        using var server = await LinuxAccessibility.StartAsync(new FakeAccessibleTree());
        await Task.Delay(LateRegistryDelay);
        using var registry = await fixture.StartRegistryAsync();

        await Assert.That(server).IsNotNull();
        await Assert.That((await registry.Service.Embedded.WaitAsync(AttachTimeout)).Length).IsGreaterThan(0);
    }

    /// <summary>Skips the test when there is no D-Bus daemon to run it on.</summary>
    private void RequireBus()
    {
        if (fixture.Bus is null)
        {
            Skip.Test("dbus-daemon is not installed.");
        }
    }
}
