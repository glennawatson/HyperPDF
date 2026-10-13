// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>The class and anchor of a mark glyph in a mark attachment lookup.</summary>
/// <param name="Class">The mark class.</param>
/// <param name="Anchor">The mark's anchor.</param>
public readonly record struct MarkAnchor(int Class, Anchor Anchor);
