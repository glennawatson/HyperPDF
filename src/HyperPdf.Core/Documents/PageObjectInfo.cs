// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Documents;

/// <summary>A piece of page content that can be picked, moved, changed or removed.</summary>
/// <param name="Index">The object's number on its page. It is only valid until the page content is edited.</param>
/// <param name="Kind">What the object is.</param>
/// <param name="Bounds">Where the object is on the page, in page space.</param>
/// <param name="Text">The text of a text object, or an empty string.</param>
[DebuggerDisplay("PageObjectInfo: {Kind} {Index} at {Bounds}")]
public readonly record struct PageObjectInfo(int Index, PageObjectKind Kind, PageRect Bounds, string Text);
