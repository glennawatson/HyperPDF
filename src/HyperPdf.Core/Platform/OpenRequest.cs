// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Platform;

/// <summary>A request to open documents in the running window.</summary>
/// <param name="Uris">Paths or URIs to open; empty to just raise the window.</param>
/// <param name="ActivationToken">The window activation token, if any, so the desktop lets the window come forward.</param>
[DebuggerDisplay("OpenRequest: {Uris.Count} uris")]
public sealed record OpenRequest(IReadOnlyList<string> Uris, string? ActivationToken);
