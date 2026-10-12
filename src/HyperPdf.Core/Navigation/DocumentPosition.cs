// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Navigation;

/// <summary>A location in a document.</summary>
/// <param name="PageIndex">The zero based page.</param>
/// <param name="OffsetFraction">The vertical position within the page, from 0 (top) to 1 (bottom).</param>
[DebuggerDisplay("DocumentPosition: Page {PageIndex} @ {OffsetFraction}")]
public readonly record struct DocumentPosition(int PageIndex, double OffsetFraction);
