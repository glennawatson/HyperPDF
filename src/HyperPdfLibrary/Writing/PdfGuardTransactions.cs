// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>Opens the transaction that a save-time guard makes its changes in.</summary>
internal static class PdfGuardTransactions
{
    /// <summary>Joins the open transaction, or starts one the caller commits.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="label">The undo label of a new transaction.</param>
    /// <param name="owned">Whether a new transaction was started.</param>
    /// <returns>The transaction.</returns>
    internal static PdfEditTransaction JoinOrBegin(PdfObjectStore store, string label, out bool owned)
    {
        lock (store.Gate)
        {
            if (StoreTransactions.GetCurrentTransaction(store) is { } open)
            {
                owned = false;
                return open;
            }

            owned = true;
            return StoreTransactions.BeginTransaction(store, label);
        }
    }
}
