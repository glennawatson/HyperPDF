// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Automation.Peers;

namespace HyperPdf.App.Accessibility;

/// <summary>A window's peer, with the facts about the window that its peer does not give.</summary>
/// <param name="Peer">The window's automation peer.</param>
/// <param name="IsActive">Whether the window is the active one.</param>
/// <param name="IsModal">Whether the window is a dialog that blocks the rest of the application.</param>
/// <param name="Number">The operating system's number for the window, or 0 when it has none.</param>
[DebuggerDisplay("WindowPeer: Active {IsActive}")]
internal sealed record WindowPeer(AutomationPeer Peer, bool IsActive, bool IsModal, long Number);
