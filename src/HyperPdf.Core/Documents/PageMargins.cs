// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>How much to take off each side of a page when it is cropped, in points as the page is shown.</summary>
/// <param name="Left">The width taken off the left.</param>
/// <param name="Top">The height taken off the top.</param>
/// <param name="Right">The width taken off the right.</param>
/// <param name="Bottom">The height taken off the bottom.</param>
[DebuggerDisplay("PageMargins: left {Left}, top {Top}, right {Right}, bottom {Bottom}")]
public readonly record struct PageMargins(float Left, float Top, float Right, float Bottom);
