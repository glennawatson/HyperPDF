// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Documents;

/// <summary>One place an article is read: a rectangle on a page.</summary>
/// <param name="PageIndex">The zero based page.</param>
/// <param name="Bounds">The rectangle in page space, or <see langword="null"/> when the file gives none.</param>
[DebuggerDisplay("ArticleBead: page {PageIndex}")]
public readonly record struct ArticleBead(int PageIndex, PageRect? Bounds);
