// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>
/// Builds the object attributes a screen reader reads beside the role: the heading level, the place in a set, the
/// landmark kind and the toolkit. Orca takes <c>level</c>, <c>posinset</c> and <c>setsize</c> from these.
/// </summary>
internal static class AtSpiAttributes
{
    /// <summary>The attribute that names the toolkit.</summary>
    private const string ToolkitKey = "toolkit";

    /// <summary>The attribute that holds a heading's level.</summary>
    private const string LevelKey = "level";

    /// <summary>The attribute that holds the place in a set.</summary>
    private const string PositionKey = "posinset";

    /// <summary>The attribute that holds the size of a set.</summary>
    private const string SetSizeKey = "setsize";

    /// <summary>The attribute that holds the markup role, such as a landmark's kind.</summary>
    private const string RolesKey = "xml-roles";

    /// <summary>The attribute that holds the tag a web author would have used.</summary>
    private const string TagKey = "tag";

    /// <summary>Gets the attributes of an element.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The attributes.</returns>
    internal static Dictionary<string, string> For(AccessibleNode node)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { [ToolkitKey] = AtSpiNames.ToolkitName };
        AddHeading(node, attributes);
        AddSet(node, attributes);
        if (node.LandmarkType.Length > 0)
        {
            attributes[RolesKey] = node.LandmarkType;
        }

        return attributes;
    }

    /// <summary>Adds the level and tag of a heading.</summary>
    /// <param name="node">The element.</param>
    /// <param name="attributes">The attributes being built.</param>
    private static void AddHeading(AccessibleNode node, Dictionary<string, string> attributes)
    {
        if (node.HeadingLevel <= 0)
        {
            return;
        }

        var level = node.HeadingLevel.ToString(CultureInfo.InvariantCulture);
        attributes[LevelKey] = level;
        attributes[TagKey] = $"h{level}";
    }

    /// <summary>Adds the place of an element among its siblings of the same kind.</summary>
    /// <param name="node">The element.</param>
    /// <param name="attributes">The attributes being built.</param>
    private static void AddSet(AccessibleNode node, Dictionary<string, string> attributes)
    {
        if (node.PositionInSet <= 0 || node.SetSize <= 0)
        {
            return;
        }

        attributes[PositionKey] = node.PositionInSet.ToString(CultureInfo.InvariantCulture);
        attributes[SetSizeKey] = node.SetSize.ToString(CultureInfo.InvariantCulture);
    }
}
