// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Gives the ways a document organises its pages beyond its outline: document parts and article threads.</summary>
public interface IDocumentPartSource : IDocumentFeature
{
    /// <summary>Gets the document's parts as a tree.</summary>
    /// <returns>The top-level parts; empty when the document has none or the structure cannot be read.</returns>
    IReadOnlyList<DocumentPart> GetParts();

    /// <summary>Gets the document's article threads.</summary>
    /// <returns>The threads; empty when the document has none or they cannot be read.</returns>
    IReadOnlyList<ArticleThread> GetThreads();
}
