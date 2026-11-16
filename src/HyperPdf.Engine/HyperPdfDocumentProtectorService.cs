// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Protection;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Engine;

/// <summary>Provides IDocumentProtector through the owning document.</summary>
internal sealed class HyperPdfDocumentProtectorService : IDocumentProtector
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfDocumentProtectorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfDocumentProtectorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProtectionStatus GetStatus() => HyperPdfDocumentProtection.GetStatus(_owner);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<ProtectionResult> Protect(ProtectionRequest request, Stream destination) =>
        Signal.FromAsync(token => HyperPdfDocumentProtection.ProtectAsync(_owner, request, destination, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<ProtectionResult> RemoveProtection(string? ownerPassword, Stream destination) =>
        Signal.FromAsync(token => HyperPdfDocumentProtection.RemoveAsync(_owner, ownerPassword, destination, token));
}
