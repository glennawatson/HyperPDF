// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Parts of a document that HyperPDF cannot show or run, so the reader can be told rather than left guessing.</summary>
[Flags]
public enum UnsupportedContent
{
    /// <summary>Everything in the document can be shown.</summary>
    None = 0,

    /// <summary>An XFA form, of which only the fallback pages made for other viewers are shown.</summary>
    XfaForm = 1 << 0,

    /// <summary>Document or field JavaScript beyond the common number, date and sum formats that are understood.</summary>
    JavaScript = 1 << 1,

    /// <summary>Sound, video or rich media.</summary>
    Multimedia = 1 << 2,

    /// <summary>3D models.</summary>
    ThreeD = 1 << 3,

    /// <summary>A PDF portfolio, whose files are listed under Attachments without the portfolio's own layout.</summary>
    Portfolio = 1 << 4,

    /// <summary>Images packed in a way HyperPDF does not know, which show as grey boxes.</summary>
    UnreadableImages = 1 << 5,
}
