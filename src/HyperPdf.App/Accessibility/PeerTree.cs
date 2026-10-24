// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Automation.Peers;

namespace HyperPdf.App.Accessibility;

/// <summary>A snapshot together with the peers it was built from, which are needed to carry out an action on the UI thread.</summary>
/// <param name="Snapshot">The elements.</param>
/// <param name="Peers">The peers by element number; read only on the UI thread.</param>
[DebuggerDisplay("PeerTree: {Snapshot}")]
internal sealed record PeerTree(AccessibleSnapshot Snapshot, IReadOnlyDictionary<int, AutomationPeer> Peers);
