// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Layout;

/// <summary>The page measurements that decide a fit zoom. They depend on the page sizes, rotation and arrangement, never on the viewport.</summary>
/// <param name="PageCount">The number of pages measured.</param>
/// <param name="WidestPage">The widest lone page row, in points.</param>
/// <param name="WidestSpread">The widest spread row, in points, counting a cover as a full spread.</param>
/// <param name="Tallest">The tallest row, in points.</param>
[DebuggerDisplay("PageFitExtents: {PageCount} pages, page {WidestPage}, spread {WidestSpread}, tallest {Tallest}")]
public sealed record PageFitExtents(int PageCount, double WidestPage, double WidestSpread, double Tallest);
