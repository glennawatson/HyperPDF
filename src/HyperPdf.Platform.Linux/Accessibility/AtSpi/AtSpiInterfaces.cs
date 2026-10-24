// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Finds the method and property tables of each AT-SPI interface by its name.</summary>
internal static class AtSpiInterfaces
{
    /// <summary>The method tables by interface name.</summary>
    private static readonly Dictionary<string, Dictionary<string, AtSpiMethod>> MethodTables = new(StringComparer.Ordinal)
    {
        [AtSpiNames.AccessibleInterface] = AtSpiAccessibleMethods.Methods,
        [AtSpiNames.ComponentInterface] = AtSpiComponentMethods.Methods,
        [AtSpiNames.ActionInterface] = AtSpiActionMethods.Methods,
        [AtSpiNames.TextInterface] = AtSpiTextMethods.Methods,
        [AtSpiNames.DocumentInterface] = AtSpiDocumentMethods.Methods,
        [AtSpiNames.ApplicationInterface] = AtSpiApplicationMethods.Methods,
    };

    /// <summary>The property tables by interface name; interfaces without properties are absent.</summary>
    private static readonly Dictionary<string, Dictionary<string, AtSpiProperty>> PropertyTables = new(StringComparer.Ordinal)
    {
        [AtSpiNames.AccessibleInterface] = AtSpiAccessibleMethods.Properties,
        [AtSpiNames.ActionInterface] = AtSpiActionMethods.Properties,
        [AtSpiNames.TextInterface] = AtSpiTextMethods.Properties,
        [AtSpiNames.ApplicationInterface] = AtSpiApplicationMethods.Properties,
    };

    /// <summary>Finds a method.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <param name="member">The method name.</param>
    /// <param name="method">The method.</param>
    /// <returns>Whether the interface has the method.</returns>
    internal static bool TryGetMethod(string interfaceName, string member, out AtSpiMethod method)
    {
        method = null!;
        return MethodTables.TryGetValue(interfaceName, out var table) && table.TryGetValue(member, out method!);
    }

    /// <summary>Finds a property.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <param name="name">The property name.</param>
    /// <param name="property">The property.</param>
    /// <returns>Whether the interface has the property.</returns>
    internal static bool TryGetProperty(string interfaceName, string name, out AtSpiProperty property)
    {
        property = null!;
        return PropertyTables.TryGetValue(interfaceName, out var table) && table.TryGetValue(name, out property!);
    }

    /// <summary>Lists the names of the methods an interface answers.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <returns>The names, or none for an interface without methods.</returns>
    internal static IEnumerable<string> GetMethodNames(string interfaceName) =>
        MethodTables.TryGetValue(interfaceName, out var table) ? table.Keys : [];

    /// <summary>Lists the names of the properties an interface answers.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <returns>The names, or none for an interface without properties.</returns>
    internal static IEnumerable<string> GetPropertyNames(string interfaceName) =>
        PropertyTables.TryGetValue(interfaceName, out var table) ? table.Keys : [];

    /// <summary>Gets all the properties of an interface.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <returns>The properties by name, or an empty table.</returns>
    internal static Dictionary<string, AtSpiProperty> GetProperties(string interfaceName) =>
        PropertyTables.TryGetValue(interfaceName, out var table) ? table : [];
}
