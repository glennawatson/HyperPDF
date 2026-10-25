// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.TestAssets;

/// <summary>The trap network a <see cref="TrapNetworkPdf"/> file gives page 1.</summary>
/// <param name="Network">The entries of the trap network annotation after its type, subtype and rectangle.</param>
/// <param name="PageEntries">Extra entries of page 1, such as <c>/LastModified</c>.</param>
/// <param name="NetworkFirst">Whether the network is listed first in <c>/Annots</c> instead of last.</param>
/// <param name="TwoNetworks">Whether page 1 lists a second trap network annotation as well.</param>
[DebuggerDisplay("TrapNetworkSample: {Network}")]
public sealed record TrapNetworkSample(string Network, string PageEntries, bool NetworkFirst, bool TwoNetworks);
