// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the render hub asks its render head for tiles and frames, so any toolkit can present them.</summary>
public sealed class RenderHubTests
{
    /// <summary>A small tile cache budget in bytes.</summary>
    private const long CacheBytes = 1024 * 1024;

    /// <summary>The document number used for removal.</summary>
    private const int DocumentId = 7;

    /// <summary>The recovery requests the recovery test makes.</summary>
    private const int Requests = 3;

    /// <summary>The hub asks the head for its tile factory once and releases it with the hub.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HubOwnsTheSurfaceFactoryItsHeadMakes()
    {
        var head = new FakeRenderHead();
        var hub = new RenderHub(CacheBytes, head);
        var createdWhileOpen = head.FactoriesCreated;
        var disposedWhileOpen = head.FactoriesDisposed;

        hub.Dispose();

        await Assert.That(createdWhileOpen).IsEqualTo(1);
        await Assert.That(disposedWhileOpen).IsEqualTo(0);
        await Assert.That(head.FactoriesDisposed).IsEqualTo(1);
    }

    /// <summary>Removing a document's tiles starts a frame through the head and changes both version numbers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingDocumentTilesRequestsAFrame()
    {
        var head = new FakeRenderHead();
        using var hub = new RenderHub(CacheBytes, head);
        var tiles = hub.TileVersion;
        var previews = hub.PreviewVersion;

        hub.RemoveDocumentTiles(DocumentId);

        await Assert.That(head.InvalidateCount).IsEqualTo(1);
        await Assert.That(hub.TileVersion).IsEqualTo(tiles + 1);
        await Assert.That(hub.PreviewVersion).IsEqualTo(previews + 1);
    }

    /// <summary>A closed hub no longer asks the head for frames.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosedHubRequestsNoFrames()
    {
        var head = new FakeRenderHead();
        var hub = new RenderHub(CacheBytes, head);
        hub.Dispose();

        hub.RemoveDocumentTiles(DocumentId);

        await Assert.That(head.InvalidateCount).IsEqualTo(0);
    }

    /// <summary>
    /// A recovery request from a render thread always reaches the head as a frame, and never as more frames than requests.
    /// Whether two requests share a frame depends on whether the UI thread ran between them, so the test does not fix that.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecoveryRequestsAreServedWithoutExtraFrames()
    {
        var head = new FakeRenderHead();
        using var hub = new RenderHub(CacheBytes, head);
        var recover = head.RecoverSoftware!;

        recover();
        recover();
        _ = await TestWait.UntilAsync(() => head.InvalidateCount > 0);
        var served = head.InvalidateCount;

        // The first callback has run, so the next request cannot be dropped as a repeat of one already waiting.
        recover();
        _ = await TestWait.UntilAsync(() => head.InvalidateCount > served);

        await Assert.That(head.InvalidateCount).IsLessThanOrEqualTo(Requests);
    }

    /// <summary>A hub made without a head is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HubRequiresAHead() =>
        await Assert.That(static () => new RenderHub(CacheBytes, null!)).Throws<ArgumentNullException>();
}
