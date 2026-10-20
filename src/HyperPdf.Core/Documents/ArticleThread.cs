// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>An article that runs through the document as a chain of beads, read in the order the file gives (ISO 32000-2 §12.4.3).</summary>
/// <param name="Title">The article's title, or a plain label such as "Article 2" when it has none.</param>
/// <param name="Beads">The places the article is read, in reading order.</param>
[DebuggerDisplay("ArticleThread: {Title} {Beads.Count} beads")]
public sealed record ArticleThread(string Title, IReadOnlyList<ArticleBead> Beads);
