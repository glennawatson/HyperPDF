// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Documents;

/// <summary>A clickable area on a page.</summary>
/// <param name="Bounds">The clickable area in page space.</param>
/// <param name="Target">The destination.</param>
[DebuggerDisplay("PageLink: {Target} @ {Bounds}")]
public readonly record struct PageLink(PageRect Bounds, LinkTarget Target);
