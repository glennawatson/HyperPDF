// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>The markup languages a page's text is written in.</summary>
internal enum TextExportFormat
{
    /// <summary>Hypertext markup, with paragraph and emphasis elements.</summary>
    Html = 0,

    /// <summary>Markdown, with asterisk emphasis and blank-line paragraphs.</summary>
    Markdown = 1,
}
