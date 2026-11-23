// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Objects;

/// <summary>Told by an object store after a transaction ends or an edit is undone or redone.</summary>
internal interface IObjectsChangedListener
{
    /// <summary>Handles a change to the store's objects. Called without holding the store's lock.</summary>
    /// <param name="scopes">The parts of the document the change can affect.</param>
    /// <param name="record">The history record committed, undone or redone; <see langword="null"/> for a rollback or a change outside the history.</param>
    /// <param name="undone">Whether <paramref name="record"/> was undone rather than committed or redone.</param>
    void OnObjectsChanged(PdfCacheScopes scopes, PdfEditRecord? record, bool undone);
}
