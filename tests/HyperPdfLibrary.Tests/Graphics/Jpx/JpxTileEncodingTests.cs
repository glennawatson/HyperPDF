// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Checks tile scratch ownership and lifetime boundaries.</summary>
public sealed class JpxTileEncodingTests
{
    /// <summary>The square image side.</summary>
    private const int Side = 192;

    /// <summary>The sample depth.</summary>
    private const int Depth = 8;

    /// <summary>Rejects a tile encode after its buffers have been returned.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposedTileRejectsEncoding()
    {
        var pools = new ScratchPools();
        try
        {
            var tile = CreateTile(pools);
            tile.Dispose();
            await Assert.That(() => EncodeTile(tile, CancellationToken.None)).Throws<ObjectDisposedException>();
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Repeated disposal cannot place the same buffer in two rental slots.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RepeatedDisposalReturnsEachBufferOnce()
    {
        var pools = new ScratchPools();
        try
        {
            var tile = CreateTile(pools);
            tile.Dispose();
            var retained = pools.RetainedBytes;
            tile.Dispose();
            await Assert.That(pools.RetainedBytes).IsEqualTo(retained);

            var first = pools.Ints.Rent(Side * Side);
            var second = pools.Ints.Rent(Side * Side);
            try
            {
                await Assert.That(first).IsNotSameReferenceAs(second);
            }
            finally
            {
                pools.Ints.Return(first);
                pools.Ints.Return(second);
            }
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>An owner that closes during an encode cannot retain its returned planes.</summary>
    /// <param name="cancellationToken">Cancels tile encoding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClosedOwnerDropsLateReturns(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        var tile = CreateTile(pools);
        pools.Close();
        EncodeTile(tile, cancellationToken);
        tile.Dispose();

        await Assert.That(pools.RetainedBytes).IsEqualTo(0);
        foreach (var plane in tile.Planes)
        {
            await Assert.That(plane).IsNull();
        }
    }

    /// <summary>Independent callers cannot rent each other's large component buffers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SeparateOwnersKeepSeparatePlanes()
    {
        var firstPools = new ScratchPools();
        var secondPools = new ScratchPools();
        try
        {
            var first = CreateTile(firstPools);
            var firstPlane = first.Planes[0];
            first.Dispose();
            using var second = CreateTile(secondPools);

            await Assert.That(second.Planes[0]).IsNotSameReferenceAs(firstPlane);
            await Assert.That(firstPools.RetainedBytes).IsGreaterThan(0);
            await Assert.That(secondPools.RetainedBytes).IsEqualTo(0);
        }
        finally
        {
            firstPools.Close();
            secondPools.Close();
        }
    }

    /// <summary>Cancellation precedes tile mutation and leaves the lease reusable.</summary>
    /// <param name="cancellationToken">Cancels the subsequent encode.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CancellationLeavesTileReusable(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        try
        {
            using var tile = CreateTile(pools);
            await Assert.That(() => EncodeTile(tile, source.Token)).Throws<OperationCanceledException>();
            await Assert.That(tile.Tile.Blocks.Count).IsEqualTo(0);
            EncodeTile(tile, cancellationToken);
            await Assert.That(tile.Tile.Blocks.Count).IsGreaterThan(0);
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Encodes a single grayscale tile.</summary>
    /// <param name="tile">The tile lifetime.</param>
    /// <param name="cancellationToken">Cancels tile encoding.</param>
    private static void EncodeTile(JpxTileEncodingState tile, CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = JpxTileEncoding.Encode(tile, new byte[Side * Side], 0, ref output, cancellationToken);
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Creates tile parameters from the encoder's own headers.</summary>
    /// <param name="pools">The caller's reusable storage.</param>
    /// <returns>The tile lifetime.</returns>
    /// <exception cref="InvalidOperationException">The encoder headers are invalid.</exception>
    private static JpxTileEncodingState CreateTile(ScratchPools pools)
    {
        var settings = JpxEncodeSettings.Create(new(Side, Side, 1, Depth, ReadOnlyMemory<byte>.Empty), new());
        var quantization = JpxQuantPlan.Create(settings);
        var geometry = JpxGeometry.Read(JpxEncodeHeaders.Siz(settings))!;
        var main = new JpxHeaderState(settings.Components);
        if (!main.Read(JpxMarkers.CodingStyle, JpxEncodeHeaders.Cod(settings), 0)
            || !main.Read(JpxMarkers.Quantization, quantization.ToSegment(), 0))
        {
            throw new InvalidOperationException("The encoder headers were invalid.");
        }

        return JpxTileEncoding.Create(settings, geometry, JpxTileParameters.Resolve(main, new(settings.Components)), quantization, pools);
    }
}
