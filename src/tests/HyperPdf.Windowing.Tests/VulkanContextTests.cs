// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that a Vulkan device can be created. The test skips on machines with no Vulkan driver.</summary>
public sealed class VulkanContextTests
{
    /// <summary>A context is created on a real driver, names its GPU and shuts down cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContextCreatesADeviceAndDisposes()
    {
        using var context = VulkanContext.TryCreate();
        if (context is null)
        {
            Skip.Test("This machine has no Vulkan driver.");
            return;
        }

        await Assert.That(context.DeviceName).IsNotEmpty();
    }

    /// <summary>The driver accepts the render pass, layouts, samplers and both pipelines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DriverAcceptsThePipelines()
    {
        using var context = VulkanContext.TryCreate();
        if (context is null)
        {
            Skip.Test("This machine has no Vulkan driver.");
            return;
        }

        using var pipelines = new VulkanPipelines(context);

        await Assert.That(pipelines.TilePipeline).IsNotEqualTo(0UL);
        await Assert.That(pipelines.SolidPipeline).IsNotEqualTo(0UL);
    }
}
