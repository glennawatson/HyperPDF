// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Signatures.Signing;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Engine;

/// <summary>Lets the library use the app's timestamp authority.</summary>
[DebuggerDisplay("TimestamperAdapter")]
internal sealed class TimestamperAdapter : IPdfTimestampClient
{
    /// <summary>The app's timestamp authority.</summary>
    private readonly ISignatureTimestamper _timestamper;

    /// <summary>Initializes a new instance of the <see cref="TimestamperAdapter"/> class.</summary>
    /// <param name="timestamper">The app's timestamp authority.</param>
    internal TimestamperAdapter(ISignatureTimestamper timestamper) => _timestamper = timestamper;

    /// <inheritdoc/>
    public int EstimatedTokenSize => _timestamper.EstimatedTokenSize;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, PdfDigestMethod digest, CancellationToken cancellationToken) =>
        _timestamper.TimestampAsync(hash, SigningMapping.FromDigest(digest), cancellationToken);
}
