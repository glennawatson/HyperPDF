// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// The undoable edit a content normalisation makes. It joins the transaction already open on the document, or starts one
/// when the first page needs a change, so a document that needs none records nothing.
/// </summary>
internal sealed class NormalisationEdit
{
    /// <summary>The undo label of the edit.</summary>
    private const string Label = "Normalise page content";

    /// <summary>The objects the edit changes.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The transaction, started on first use.</summary>
    private PdfEditTransaction? _transaction;

    /// <summary>Whether this edit started the transaction, so it commits or rolls it back.</summary>
    private bool _owned;

    /// <summary>Initializes a new instance of the <see cref="NormalisationEdit"/> class.</summary>
    /// <param name="store">The objects the edit changes.</param>
    internal NormalisationEdit(PdfObjectStore store) => _store = store;

    /// <summary>Gets or sets the number of pages changed.</summary>
    internal int Pages { get; set; }

    /// <summary>Gets the transaction, starting or joining one on first use.</summary>
    internal PdfEditTransaction Transaction => _transaction ??= JoinOrBegin();

    /// <summary>Commits the transaction when this edit started it.</summary>
    internal void Commit()
    {
        if (!_owned || _transaction is null)
        {
            return;
        }

        _transaction.AddKinds(PdfChangeKinds.PageChanges);
        _transaction.Commit();
    }

    /// <summary>Rolls the transaction back when this edit started it.</summary>
    internal void Rollback()
    {
        if (_owned)
        {
            _transaction?.Rollback();
        }
    }

    /// <summary>Joins the open transaction, or starts one.</summary>
    /// <returns>The transaction.</returns>
    private PdfEditTransaction JoinOrBegin()
    {
        lock (_store.Gate)
        {
            if (StoreTransactions.GetCurrentTransaction(_store) is { } open)
            {
                return open;
            }

            _owned = true;
            return StoreTransactions.BeginTransaction(_store, Label);
        }
    }
}
