// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>A document that provides undoable page management.</summary>
public interface IPageManagementSource : IDocumentFeature
{
    /// <summary>Gets the page editor for this open document.</summary>
    IPageManager PageManager { get; }
}
