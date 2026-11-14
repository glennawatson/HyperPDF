// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>A global the compositor advertised.</summary>
/// <param name="Name">The registry name used to bind it. Zero means the compositor did not offer it.</param>
/// <param name="Version">The newest version the compositor supports.</param>
[DebuggerDisplay("WaylandGlobal {Name} v{Version}")]
internal readonly record struct WaylandGlobal(uint Name, uint Version);
