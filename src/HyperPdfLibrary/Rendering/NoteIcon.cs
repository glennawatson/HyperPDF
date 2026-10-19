// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>The icons a text annotation can have (its <c>/Name</c>, ISO 32000-2 12.5.6.4).</summary>
internal enum NoteIcon
{
    /// <summary>A speech box, the default.</summary>
    Comment = 0,

    /// <summary>A page with a folded corner.</summary>
    Note = 1,

    /// <summary>A key.</summary>
    Key = 2,

    /// <summary>A circle with a question mark.</summary>
    Help = 3,

    /// <summary>A paragraph sign.</summary>
    Paragraph = 4,

    /// <summary>A paragraph sign with an insertion mark, for a new paragraph.</summary>
    NewParagraph = 5,

    /// <summary>An insertion mark.</summary>
    Insert = 6,
}
