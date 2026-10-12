// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Search;

/// <summary>One search match with the rectangles to highlight.</summary>
/// <param name="Match">The matched characters.</param>
/// <param name="Bounds">The highlight rectangles in page space.</param>
[DebuggerDisplay("SearchHit: {Match}")]
public sealed record SearchHit(TextMatch Match, PageRect[] Bounds);
