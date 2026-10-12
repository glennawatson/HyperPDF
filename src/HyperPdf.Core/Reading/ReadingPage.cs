// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>A page's text in reading order, without page numbers and running headers or footers.</summary>
/// <param name="PageIndex">The page.</param>
/// <param name="Blocks">The blocks, in the order to read them; footnotes last.</param>
[DebuggerDisplay("ReadingPage: Page {PageIndex}: {Blocks.Count} blocks")]
public sealed record ReadingPage(int PageIndex, IReadOnlyList<ReadingBlock> Blocks);
