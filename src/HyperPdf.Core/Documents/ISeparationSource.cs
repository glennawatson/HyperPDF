// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Says which pages of a preseparated document are colour separations, and which printed page each belongs to.</summary>
public interface ISeparationSource : IDocumentFeature
{
    /// <summary>Reads the separation details, then gives the printed pages.</summary>
    /// <returns>A cold signal giving the printed pages once, or <see cref="DocumentSeparations.Empty"/> for a document of ordinary pages; unsubscribing cancels the read.</returns>
    IObservable<DocumentSeparations> ReadSeparations();

    /// <summary>Gets the printed pages.</summary>
    /// <returns>The printed pages; <see cref="DocumentSeparations.Empty"/> for a document of ordinary pages.</returns>
    DocumentSeparations GetSeparations();
}
