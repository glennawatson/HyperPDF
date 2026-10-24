// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Accessibility;

/// <summary>The names of the actions an element lists in <see cref="AccessibleNode.Actions"/>, which a platform bridge maps to its own patterns and interfaces.</summary>
public static class AccessibleActions
{
    /// <summary>Gets the name of the action that clicks a button or opens an item.</summary>
    public static string Click { get; } = "click";

    /// <summary>Gets the name of the action that switches a check box or toggle.</summary>
    public static string Toggle { get; } = "toggle";

    /// <summary>Gets the name of the action that opens an expandable element.</summary>
    public static string Expand { get; } = "expand";

    /// <summary>Gets the name of the action that closes an expandable element.</summary>
    public static string Collapse { get; } = "collapse";

    /// <summary>Gets the name of the action that selects an item.</summary>
    public static string Select { get; } = "select";
}
