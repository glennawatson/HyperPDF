// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>Receives one event while the connection is read.</summary>
/// <param name="reader">The event, positioned at its first argument.</param>
internal delegate void WaylandMessageHandler(ref WaylandMessageReader reader);
