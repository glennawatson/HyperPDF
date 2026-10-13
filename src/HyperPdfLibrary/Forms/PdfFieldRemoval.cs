// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>What removing fields did.</summary>
/// <param name="Removed">The fully qualified names of the fields asked for that were removed. Fields below them went with them.</param>
/// <param name="Skipped">The names that were not removed, each with a plain reason.</param>
/// <param name="WidgetCount">The number of widgets taken off pages.</param>
/// <param name="Unresolved">The places that refer to a removed field by name and that were left as they were.</param>
[DebuggerDisplay("PdfFieldRemoval: {Removed.Length} removed, {Skipped.Length} skipped")]
public sealed record PdfFieldRemoval(string[] Removed, string[] Skipped, int WidgetCount, PdfFormReference[] Unresolved);
