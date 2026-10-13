// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.App.Tests;

/// <summary>A prepared signature with fixed bytes, so the external signing window can be tested without an engine.</summary>
internal sealed class FakePendingSignature : IPendingSignature
{
    /// <summary>The room reserved for the signature.</summary>
    private const int Capacity = 4096;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> SignedAttributes { get; } = new byte[] { 1, 2, 3 };

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> DocumentDigest { get; } = new byte[] { 4, 5, 6 };

    /// <inheritdoc/>
    public SigningDigest Digest => default;

    /// <inheritdoc/>
    public SigningFormat Format => default;

    /// <inheritdoc/>
    public string FieldName => "Signature1";

    /// <inheritdoc/>
    public int ContentsCapacity => Capacity;
}
