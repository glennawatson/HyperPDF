// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Checks image-release ownership without requiring a physical graphics device.</summary>
public sealed class SkiaGpuImageRetirementTests
{
    /// <summary>Draining one backend leaves the other backend's images alive.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackendQueuesAreIndependent()
    {
        var first = new SkiaGpuImageRetirement();
        var second = new SkiaGpuImageRetirement();
        using var surface = SKSurface.Create(new SKImageInfo(1, 1));
        using var otherSurface = SKSurface.Create(new SKImageInfo(1, 1));
        using var firstImage = surface.Snapshot();
        using var secondImage = otherSurface.Snapshot();
        first.Enqueue(firstImage);
        second.Enqueue(secondImage);

        first.Drain();
        await Assert.That(firstImage.Handle).IsEqualTo(nint.Zero);
        await Assert.That(secondImage.Handle).IsNotEqualTo(nint.Zero);

        second.Retire();
        await Assert.That(secondImage.Handle).IsEqualTo(nint.Zero);
    }

    /// <summary>An abandoned backend releases images that arrive after retirement.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImagesArrivingAfterRetirementAreReleased()
    {
        var retirement = new SkiaGpuImageRetirement();
        retirement.Retire();
        using var surface = SKSurface.Create(new SKImageInfo(1, 1));
        using var image = surface.Snapshot();

        retirement.Enqueue(image);

        await Assert.That(image.Handle).IsEqualTo(nint.Zero);
    }
}
