// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.Engine;

/// <summary>Provides IPageContentEditor through the owning document.</summary>
internal sealed class HyperPdfPageContentEditorService : IPageContentEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageContentEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageContentEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<PageObjectInfo[]> ReadObjectsAsync(int pageIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReadObjectsAsync(_owner, pageIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask MoveAsync(int pageIndex, int objectIndex, float deltaX, float deltaY, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.MoveAsync(_owner, pageIndex, objectIndex, new(deltaX, deltaY), cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DeleteAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.DeleteAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ReplaceTextAsync(int pageIndex, int objectIndex, string text, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReplaceTextAsync(_owner, pageIndex, objectIndex, text, cancellationToken);
}
