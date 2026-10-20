// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Lists every property of the document's XMP metadata, so a reader can reach them without looking at the file's bytes.</summary>
public interface IDocumentXmpSource : IDocumentFeature
{
    /// <summary>Gets the properties of the catalog's XMP packet.</summary>
    /// <returns>The properties in name order; empty when the document has no XMP packet.</returns>
    IReadOnlyList<DocumentXmpProperty> GetProperties();
}
