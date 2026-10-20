// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Describes what a U3D file holds in plain words, for people who cannot see the picture of it.</summary>
public static class U3dSummary
{
    /// <summary>Describes a scene.</summary>
    /// <param name="scene">The scene.</param>
    /// <returns>A sentence or two: what the file declares, and what was not read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scene"/> is <see langword="null"/>.</exception>
    public static string Describe(U3dScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var text = new StringBuilder("3D model");
        _ = text.Append(CultureInfo.InvariantCulture, $" with {Plural(scene.Geometry.Length, "shape")}");
        _ = text.Append(CultureInfo.InvariantCulture, $", {Plural(scene.Nodes.Length, "scene node")}");
        _ = text.Append(CultureInfo.InvariantCulture, $", {Plural(scene.CountNodes(U3dNodeKind.View), "view")}");
        _ = text.Append(CultureInfo.InvariantCulture, $", {Plural(scene.CountResources(U3dBlockType.MaterialResource), "material")}");
        _ = text.Append(CultureInfo.InvariantCulture, $" and {Plural(scene.CountResources(U3dBlockType.TextureDeclaration), "texture")}.");
        _ = text.Append(" The shapes are not drawn; the picture shown is the one the document supplies.");
        if (scene.Extensions.Length > 0)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $" It uses extensions that are not read: {string.Join(", ", scene.Extensions)}.");
        }

        foreach (var problem in scene.Problems)
        {
            _ = text.Append(' ').Append(problem);
        }

        return text.ToString();
    }

    /// <summary>Writes a count with its noun.</summary>
    /// <param name="count">The count.</param>
    /// <param name="noun">The noun in its singular form.</param>
    /// <returns>The count and noun, such as "1 view" or "2 views".</returns>
    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : string.Create(CultureInfo.InvariantCulture, $"{count} {noun}s");
}
