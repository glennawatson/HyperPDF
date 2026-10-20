// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.Engine;

/// <summary>Provides IDocumentMediaSource through the owning document.</summary>
/// <param name="owner">The owning document.</param>
internal sealed class HyperPdfDocumentMediaSourceService(HyperPdfDocument owner) : IDocumentMediaSource
{
    /// <inheritdoc/>
    public IDocument Document => owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<DocumentMedia> GetMedia() => HyperPdfDocumentMediaItems.GetMedia(owner);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SaveMedia(int index, Stream destination) => HyperPdfDocumentMediaItems.SaveMedia(owner, index, destination);
}
