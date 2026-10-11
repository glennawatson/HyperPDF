// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Render.Skia;

namespace HyperPdfLibrary.Tests.Drawing;

/// <summary>Tests ownership of reusable Skia drawing sessions.</summary>
public sealed class SkiaRenderBackendTests
{
    /// <summary>Each backend keeps its own session on the same thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackendInstancesDoNotShareDrawingSession()
    {
        var left = new SkiaRenderBackend();
        var right = new SkiaRenderBackend();
        var leftSession = left.GetDrawingSession();
        var rightSession = right.GetDrawingSession();

        await Assert.That(ReferenceEquals(leftSession, left.GetDrawingSession())).IsTrue();
        await Assert.That(ReferenceEquals(leftSession, rightSession)).IsFalse();
    }

    /// <summary>One backend gives concurrent worker threads separate sessions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WorkerThreadsReceiveSeparateDrawingSessions()
    {
        var backend = new SkiaRenderBackend();
        var first = Task.Factory.StartNew(backend.GetDrawingSession, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var second = Task.Factory.StartNew(backend.GetDrawingSession, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var sessions = await Task.WhenAll(first, second);

        await Assert.That(ReferenceEquals(sessions[0], sessions[1])).IsFalse();
    }
}
