// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>One build attempt on a <see cref="PdfSigningWorkspace"/>: its open transaction, and where object numbering restarts.</summary>
/// <param name="Transaction">The transaction the attempt edits in; disposing the attempt rolls it back.</param>
/// <param name="Store">The copy's objects.</param>
/// <param name="NextNumber">The number the next added object had before the attempt.</param>
internal readonly record struct PdfSigningAttempt(PdfEditTransaction Transaction, PdfObjectStore Store, int NextNumber) : IDisposable
{
    /// <summary>Rolls back the attempt's changes and numbers the next attempt's objects as this one's were.</summary>
    public void Dispose()
    {
        Transaction.Dispose();
        StoreTransactions.ResetNextNumber(Store, NextNumber);
    }
}
