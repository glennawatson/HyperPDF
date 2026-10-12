// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Checks explicit errors, missing content, geometry and snapshot ownership.</summary>
public sealed class RasterScoringTests
{
    /// <summary>A white page cannot satisfy a required colored region.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingContentIsDetected()
    {
        var score = RasterScoring.AgainstOracle(ComparisonTestRasters.Solid(ComparisonTestRasters.White), ComparisonTestRasters.Oracle(ComparisonTestRasters.Red, 0));
        const long pixels = ComparisonTestRasters.Edge * ComparisonTestRasters.Edge;
        await Assert.That(score.IsAcceptable).IsFalse();
        await Assert.That(score.CheckedPixels).IsEqualTo(pixels);
        await Assert.That(score.ErrorPixels).IsEqualTo(pixels);
        await Assert.That(score.BlueAbsoluteError).IsEqualTo(pixels * byte.MaxValue);
        await Assert.That(score.GreenAbsoluteError).IsEqualTo(pixels * byte.MaxValue);
        await Assert.That(score.RedAbsoluteError).IsEqualTo(0);
        await Assert.That(score.AlphaAbsoluteError).IsEqualTo(0);
        await Assert.That(score.MaximumChannelError).IsEqualTo(byte.MaxValue);
    }

    /// <summary>Transparent blank pixels cannot masquerade as opaque page content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlankTransparentContentIsDetected()
    {
        var score = RasterScoring.AgainstOracle(ComparisonTestRasters.Solid(default), ComparisonTestRasters.Oracle(ComparisonTestRasters.White, 0));
        await Assert.That(score.IsAcceptable).IsFalse();
        await Assert.That(score.AlphaAbsoluteError).IsGreaterThan(0);
    }

    /// <summary>Equal byte lengths cannot hide different raster dimensions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DimensionMismatchFailsBeforePixelComparison()
    {
        const int wide = ComparisonTestRasters.Edge * ComparisonTestRasters.Edge;
        var pixels = ComparisonTestRasters.Solid(ComparisonTestRasters.Red);
        var wrongGeometry = new ComparisonRaster(wide, 1, pixels.Pixels);
        var score = RasterScoring.AgainstOracle(wrongGeometry, ComparisonTestRasters.Oracle(ComparisonTestRasters.Red, 0));
        var similarity = RasterScoring.Compare(wrongGeometry, pixels, 0);
        await Assert.That(score.DimensionsMatch).IsFalse();
        await Assert.That(score.IsAcceptable).IsFalse();
        await Assert.That(score.CheckedPixels).IsEqualTo(0);
        await Assert.That(similarity.IsAcceptable).IsFalse();
    }

    /// <summary>Copying input prevents later caller edits from changing a comparison snapshot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RasterOwnsItsInputSnapshot()
    {
        var source = new byte[ComparisonRaster.BytesPerPixel];
        Array.Fill(source, byte.MaxValue);
        var snapshot = new ComparisonRaster(1, 1, source);
        source[0] = 0;
        await Assert.That(snapshot.Pixels.Span[0]).IsEqualTo(byte.MaxValue);
    }

    /// <summary>Invalid dimensions and incomplete pixel data fail immediately.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidRasterGeometryIsRejected()
    {
        await Assert.That(static () => new ComparisonRaster(0, 1, ReadOnlyMemory<byte>.Empty)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => new ComparisonRaster(1, 1, ReadOnlyMemory<byte>.Empty)).Throws<ArgumentException>();
    }

    /// <summary>An empty oracle cannot pass through a vacuous zero-error score.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyOracleIsRejected() =>
        await Assert.That(static () => new StandardsOracle(1, 1, ReadOnlyMemory<ExpectedPixelRegion>.Empty)).Throws<ArgumentException>();

    /// <summary>Out-of-bounds and overlapping expected rectangles cannot distort checked-pixel counts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidOracleRegionsAreRejected()
    {
        var region = new ExpectedPixelRegion(0, 0, 1, 1, ComparisonTestRasters.Red, 0, "Independent test expectation");
        await Assert.That(() => new StandardsOracle(1, 1, new[] { region with { X = 1 } })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new StandardsOracle(1, 1, new[] { region, region })).Throws<ArgumentException>();
    }
}
