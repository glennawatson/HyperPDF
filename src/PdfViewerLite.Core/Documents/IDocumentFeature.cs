// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>An optional capability of an open document, such as annotation editing or form filling, found with <see cref="DocumentFeature{TFeature}"/>.</summary>
public interface IDocumentFeature
{
    /// <summary>Gets the document the feature works on.</summary>
    IDocument Document { get; }
}
