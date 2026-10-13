// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks the bounded pool that keeps large decoder buffers for reuse.</summary>
public sealed class ScratchPoolTests
{
    /// <summary>A length that uses the bounded pool: 1 MiB of bytes.</summary>
    private const int Large = 1024 * 1024;

    /// <summary>A length that uses the shared array pool.</summary>
    private const int Small = 1024;

    /// <summary>A length the shared pool serves with an array of exactly 64 KiB.</summary>
    private const int SharedRequest = 40_000;

    /// <summary>A length just over what the shared pool serves.</summary>
    private const int BoundedRequest = 70_000;

    /// <summary>How many times over the budget the test returns buffers.</summary>
    private const int Overfill = 2;

    /// <summary>Four megabytes, as a budget that fits a few large buffers.</summary>
    private const long Budget = 4L * Large;

    /// <summary>A returned large buffer is handed out again for a request of about the same size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReusesALargeBuffer()
    {
        var owner = new ScratchPools();
        var pool = owner.Get<Marker>();
        var first = pool.Rent(Large);
        pool.Return(first);

        var second = pool.Rent(Large);
        pool.Return(second);
        owner.Trim();

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(second.Length).IsGreaterThanOrEqualTo(Large);
    }

    /// <summary>Buffers kept for reuse count against the budget, and trimming gives the room back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsBuffersWithinTheBudgetAndTrimReleasesThem()
    {
        var owner = new ScratchPools { Budget = Budget };
        var pool = owner.Get<byte>();
        var buffers = new List<byte[]>();
        for (var i = 0; i < Budget / Large * Overfill; i++)
        {
            buffers.Add(pool.Rent(Large + (i * Small)));
        }

        foreach (var buffer in buffers)
        {
            pool.Return(buffer);
        }

        var kept = owner.RetainedBytes;
        owner.Trim();

        await Assert.That(kept).IsGreaterThan(0);
        await Assert.That(kept).IsLessThanOrEqualTo(Budget);
        await Assert.That(owner.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>With no budget nothing is kept, and lowering the budget trims at once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ALowBudgetKeepsNothing()
    {
        var owner = new ScratchPools();
        var pool = owner.Get<byte>();
        var buffer = pool.Rent(Large);
        pool.Return(buffer);
        var kept = owner.RetainedBytes;

        owner.Budget = 0;
        var afterLowering = owner.RetainedBytes;
        pool.Return(pool.Rent(Large));

        await Assert.That(kept).IsGreaterThan(0);
        await Assert.That(afterLowering).IsEqualTo(0);
        await Assert.That(owner.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>Small requests come from the shared array pool and are never kept by the bounded pool.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallBuffersUseTheSharedPool()
    {
        var pool = new ScratchPools().Get<byte>();
        var buffer = pool.Rent(Small + 1);
        pool.Return(buffer);

        // The shared pool hands out power of two sizes; the bounded pool rounds to eighth steps instead.
        await Assert.That(System.Numerics.BitOperations.IsPow2(buffer.Length)).IsTrue();
        await Assert.That(buffer.Length).IsGreaterThan(Small);
    }

    /// <summary>A request just under the shared pool's limit goes back to the shared pool, which hands the same array out again, and is not kept by the bounded pool.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARequestUnderTheLimitReturnsToTheSharedPool()
    {
        var owner = new ScratchPools();
        var pool = owner.Get<byte>();
        var first = pool.Rent(SharedRequest);
        pool.Return(first);
        var second = pool.Rent(SharedRequest);
        pool.Return(second);

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(owner.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>A request just over the shared pool's limit is kept by the bounded pool and handed out again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARequestOverTheLimitIsKeptByTheBoundedPool()
    {
        var owner = new ScratchPools();
        var pool = owner.Get<byte>();
        var first = pool.Rent(BoundedRequest);
        pool.Return(first);
        var second = pool.Rent(BoundedRequest);
        pool.Return(second);

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(owner.RetainedBytes).IsGreaterThan(0);
        owner.Trim();
    }

    /// <summary>Closing one document releases its own large arrays without trimming another document's cache.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentCloseOnlyReleasesItsOwnBuffers()
    {
        var bytes = TestPdf.Create(1);
        ScratchPools firstOwner;
        ScratchPools secondOwner;
        using (var second = PdfDocumentReader.Open(bytes, null))
        {
            secondOwner = second.Objects.ScratchPools;
            var secondPool = secondOwner.Get<byte>();
            secondPool.Return(secondPool.Rent(Large));
            using (var first = PdfDocumentReader.Open(bytes, null))
            {
                firstOwner = first.Objects.ScratchPools;
                var firstPool = firstOwner.Get<byte>();
                firstPool.Return(firstPool.Rent(Large));
            }

            await Assert.That(ReferenceEquals(firstOwner, secondOwner)).IsFalse();
            await Assert.That(firstOwner.RetainedBytes).IsEqualTo(0);
            await Assert.That(secondOwner.RetainedBytes).IsGreaterThan(0);
        }

        await Assert.That(secondOwner.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>Documents given one viewer cache leave its retained arrays for that cache's owner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharedServiceCacheSurvivesDocumentClose()
    {
        var owner = new ScratchPools();
        var services = new PdfDrawingServices(new()) { SharedScratchPools = owner };
        var options = new PdfOpenOptions { DrawingServices = services };
        var bytes = TestPdf.Create(1);
        var pool = owner.Get<byte>();
        pool.Return(pool.Rent(Large));
        using (var first = PdfDocumentReader.OpenWith(bytes, options))
        using (var second = PdfDocumentReader.OpenWith(bytes, options))
        {
            await Assert.That(ReferenceEquals(first.Objects.ScratchPools, second.Objects.ScratchPools)).IsTrue();
        }

        await Assert.That(owner.RetainedBytes).IsGreaterThan(0);
        owner.Trim();
        await Assert.That(owner.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>A buffer still in use when its owner closes is dropped when returned.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosedOwnerDoesNotRetainLateReturns()
    {
        var owner = new ScratchPools();
        var pool = owner.Get<byte>();
        var buffer = pool.Rent(Large);

        owner.Close();
        pool.Return(buffer);

        await Assert.That(owner.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>A type used only by these tests, so no other test shares its pool slots.</summary>
    /// <param name="Value">The byte.</param>
    private readonly record struct Marker(byte Value);
}
