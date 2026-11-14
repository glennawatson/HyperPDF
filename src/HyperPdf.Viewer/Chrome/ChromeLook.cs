// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the window's controls are drawn with.</summary>
/// <param name="Runs">The drawn text.</param>
/// <param name="Theme">The colours.</param>
/// <param name="Metrics">The sizes.</param>
[DebuggerDisplay("ChromeLook: text {Metrics.TextSize} px")]
internal sealed record ChromeLook(TextRuns Runs, ChromeTheme Theme, ChromeMetrics Metrics);
