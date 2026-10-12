// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.App.ViewModels;

/// <summary>How a tab looked before presenting, restored when presenting ends.</summary>
/// <param name="ZoomMode">The zoom mode.</param>
/// <param name="Zoom">The zoom, for a free zoom.</param>
/// <param name="PageByPage">Whether pages were shown one at a time.</param>
/// <param name="SidebarVisible">Whether the sidebar was shown.</param>
[DebuggerDisplay("PresentationState: {ZoomMode} page by page={PageByPage}")]
public readonly record struct PresentationState(ZoomMode ZoomMode, double Zoom, bool PageByPage, bool SidebarVisible);
