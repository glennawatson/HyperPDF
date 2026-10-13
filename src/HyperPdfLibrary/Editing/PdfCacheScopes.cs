// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>
/// The parts of a document that page-keyed and tree-keyed caches are built from. An edit names the parts it changes, and
/// only the caches built from those parts are dropped. An edit that names nothing is assumed to change every part.
/// </summary>
[Flags]
internal enum PdfCacheScopes
{
    /// <summary>The edit changes none of the parts: annotations, form values, metadata, bookmarks, attachments, layers and similar.</summary>
    None = 0,

    /// <summary>Pages were inserted, deleted, moved or reordered, so a page index may now mean another page.</summary>
    PageSet = 1 << 0,

    /// <summary>A page's content streams, resources, fonts or boxes, or the document's reading direction, changed.</summary>
    PageContent = 1 << 1,

    /// <summary>The structure tree objects, or what the tree reads from the catalog, changed.</summary>
    Structure = 1 << 2,

    /// <summary>Every part, for an edit that does not say what it changes.</summary>
    All = PageSet | PageContent | Structure,
}
