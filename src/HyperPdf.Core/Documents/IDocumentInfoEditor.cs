// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Changes the document's title, author, subject and keywords, which readers and search tools show.</summary>
public interface IDocumentInfoEditor : IDocumentFeature
{
    /// <summary>Sets the document's title, author, subject and keywords, as one step that can be undone.</summary>
    /// <param name="edit">The new values; an empty value clears that entry.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetInfo(DocumentInfoEdit edit);
}
