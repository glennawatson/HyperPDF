// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Rendering;

/// <summary>Lets go of what a document keeps only to draw its pages faster, for a document that stays open but is not shown.</summary>
public interface IIdleRenderMemory : IDocumentFeature
{
    /// <summary>
    /// Releases the pictures recorded of the document's pages. A page is recorded again when it is next drawn, so nothing is
    /// lost but the time to record it.
    /// </summary>
    void Release();
}
