// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>The side panel a document asks to be shown when it opens (its <c>/PageMode</c>).</summary>
public enum OpenPanel
{
    /// <summary>The document asks for no panel.</summary>
    None = 0,

    /// <summary>The document outline.</summary>
    Outline = 1,

    /// <summary>The page thumbnails.</summary>
    Thumbnails = 2,

    /// <summary>The files embedded in the document.</summary>
    Attachments = 3,

    /// <summary>The optional content layers.</summary>
    Layers = 4,
}
