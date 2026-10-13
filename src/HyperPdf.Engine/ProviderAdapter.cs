// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Signatures.Signing;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Engine;

/// <summary>Lets the library use the app's external signature provider.</summary>
[DebuggerDisplay("ProviderAdapter: {Algorithm}")]
internal sealed class ProviderAdapter : IPdfSignatureProvider
{
    /// <summary>The app's provider.</summary>
    private readonly ISignatureProvider _provider;

    /// <summary>Initializes a new instance of the <see cref="ProviderAdapter"/> class.</summary>
    /// <param name="provider">The app's provider.</param>
    internal ProviderAdapter(ISignatureProvider provider) => _provider = provider;

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> Certificates => _provider.Identity.Certificates;

    /// <inheritdoc/>
    public PdfSignatureAlgorithm Algorithm => SigningMapping.ToAlgorithm(_provider.Identity.KeyKind);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, PdfDigestMethod digest, CancellationToken cancellationToken) =>
        _provider.SignAsync(data, SigningMapping.FromDigest(digest), cancellationToken);
}
