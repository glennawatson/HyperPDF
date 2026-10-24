// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Turns element numbers into AT-SPI object paths and back.</summary>
internal static class AtSpiPaths
{
    /// <summary>Gets the object path of an element.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="rootId">The number of the application element, which has a path of its own.</param>
    /// <returns>The path.</returns>
    internal static string For(int id, int rootId) =>
        id == rootId ? AtSpiNames.RootPath : string.Create(CultureInfo.InvariantCulture, $"{AtSpiNames.ObjectPrefix}/{id}");

    /// <summary>Reads the element number out of an object path.</summary>
    /// <param name="path">The path of a request.</param>
    /// <param name="rootId">The number of the application element.</param>
    /// <param name="id">The element's number.</param>
    /// <returns>Whether the path names an element of the tree.</returns>
    internal static bool TryParse(string path, int rootId, out int id)
    {
        id = -1;
        if (!path.StartsWith(AtSpiNames.ObjectPrefix, StringComparison.Ordinal) || path.Length <= AtSpiNames.ObjectPrefix.Length + 1)
        {
            return false;
        }

        var segment = path.AsSpan(AtSpiNames.ObjectPrefix.Length + 1);
        if (segment.SequenceEqual(AtSpiNames.RootSegment))
        {
            id = rootId;
            return true;
        }

        return int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }
}
