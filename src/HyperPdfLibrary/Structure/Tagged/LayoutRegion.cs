// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A run of lines in the builder's array that the page was cut down to.</summary>
/// <param name="Start">The position of the first line.</param>
/// <param name="Count">The number of lines.</param>
[DebuggerDisplay("LayoutRegion: {Count} lines from {Start}")]
internal readonly record struct LayoutRegion(int Start, int Count);
