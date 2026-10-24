// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The header and footer chosen for a page and how tall they are.</summary>
/// <param name="Header">The header, or <see langword="null"/> for none.</param>
/// <param name="Footer">The footer, or <see langword="null"/> for none.</param>
/// <param name="HeaderHeight">The header's height in points, zero for none.</param>
/// <param name="FooterHeight">The footer's height in points, zero for none.</param>
[DebuggerDisplay("RunningSizes: header {HeaderHeight}, footer {FooterHeight}")]
internal readonly record struct RunningSizes(PdfRunningContent? Header, PdfRunningContent? Footer, float HeaderHeight, float FooterHeight);
