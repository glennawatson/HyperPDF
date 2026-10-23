// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>What every request needs to know about the server it came to: the tree it serves and the bus name it is known by.</summary>
/// <param name="Tree">The elements being served.</param>
/// <param name="BusName">The unique name of the server's connection on the accessibility bus, which references to elements carry.</param>
[DebuggerDisplay("AtSpiSession: {BusName}")]
internal sealed record AtSpiSession(IAccessibleTree Tree, string BusName)
{
    /// <summary>Gets the number of the application element.</summary>
    internal int RootId => Tree.RootId;
}
