// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>A dialog that waits under another dialog that opened over it, such as a tool window under the file dialog it asked for.</summary>
/// <param name="Overlay">The dialog that waits.</param>
/// <param name="Links">What the dialog is joined to while it waits: its changes and its wish to close, and the dialog itself.</param>
[DebuggerDisplay("OverlayFrame: {Overlay}")]
internal sealed record OverlayFrame(Overlay Overlay, IDisposable Links);
