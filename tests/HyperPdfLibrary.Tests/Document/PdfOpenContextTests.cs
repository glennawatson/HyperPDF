// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks decoding limits before an open context can rent output buffers.</summary>
public sealed class PdfOpenContextTests
{
    /// <summary>Invalid configuration is rejected before stream decoding starts.</summary>
    /// <param name="limit">The unsupported output limit.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(int.MaxValue)]
    public async Task Create_InvalidDecodeLimit_ThrowsArgumentError(int limit) =>
        await Assert.That(() => PdfOpenContext.Create(new() { MaxDecompressedBytes = limit })).Throws<ArgumentOutOfRangeException>();

    /// <summary>Default options retain the supported decoding ceiling.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_DefaultOptions_PreservesSupportedLimit()
    {
        var context = PdfOpenContext.Create(PdfOpenOptions.Default);
        await Assert.That(context.MaxDecompressedBytes).IsEqualTo(PdfLimits.MaxDecodedLength);
        await Assert.That(context.Recovery).IsTrue();
    }

    /// <summary>Positional options transfer their supplied bounds and cancellation to the context.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_PositionalOptions_PreservesValues(CancellationToken cancellationToken)
    {
        var options = new PdfOpenOptions(null, null, null, null, null, false, true, default, 0, 1, cancellationToken);
        var context = PdfOpenContext.Create(options, cancellationToken);
        await Assert.That(context.MaxDecompressedBytes).IsEqualTo(1);
        await Assert.That(context.Recovery).IsFalse();
        await Assert.That(context.IgnoreXrefStreams).IsTrue();
        await Assert.That(context.CancellationToken).IsEqualTo(cancellationToken);
    }
}
