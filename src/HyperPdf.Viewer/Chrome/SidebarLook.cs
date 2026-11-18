// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What one part of the side panel is laid out with: how its controls look, and the command each of them runs with its own value.</summary>
/// <param name="Look">The text measurer, colours and sizes.</param>
/// <param name="Pick">The command a control of the part runs, with what the control stands for as its parameter.</param>
[DebuggerDisplay("SidebarLook: text {Look.Metrics.TextSize} px")]
internal sealed record SidebarLook(ChromeLook Look, ICommand Pick);
