// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>A point on a page, as picked by a click.</summary>
/// <param name="Page">The zero based page.</param>
/// <param name="Point">The point, in page space.</param>
[DebuggerDisplay("PageLocation: Page {Page} at {Point}")]
public readonly record struct PageLocation(int Page, PagePoint Point);
