// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Text;

/// <summary>The kinds of file a document's text can be written to.</summary>
public enum DocumentTextFormat
{
    /// <summary>Plain text, a blank line between pages.</summary>
    PlainText = 0,

    /// <summary>Markdown, with headings and lists where the page has them.</summary>
    Markdown = 1,

    /// <summary>HTML, with headings, lists and tables where the page has them.</summary>
    Html = 2,
}
