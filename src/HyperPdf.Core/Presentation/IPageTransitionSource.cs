// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Presentation;

/// <summary>Gives the transition and advance time a page asks for in a presentation.</summary>
public interface IPageTransitionSource : IDocumentFeature
{
    /// <summary>Gets what a page asks for when it appears in a presentation.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The transition and advance time, or <see langword="null"/> when the page asks for neither.</returns>
    PageTransition? GetTransition(int pageIndex);
}
