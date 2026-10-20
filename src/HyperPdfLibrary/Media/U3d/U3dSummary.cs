// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

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
        var text = new DefaultInterpolatedStringHandler(0, 0, CultureInfo.InvariantCulture);
        text.AppendLiteral("3D model with ");
        Count(ref text, scene.Geometry.Length, "shape");
        text.AppendLiteral(", ");
        Count(ref text, scene.Nodes.Length, "scene node");
        text.AppendLiteral(", ");
        Count(ref text, scene.CountNodes(U3dNodeKind.View), "view");
        text.AppendLiteral(", ");
        Count(ref text, scene.CountResources(U3dBlockType.MaterialResource), "material");
        text.AppendLiteral(" and ");
        Count(ref text, scene.CountResources(U3dBlockType.TextureDeclaration), "texture");
        text.AppendLiteral(". The shapes are not drawn; the picture shown is the one the document supplies.");
        if (scene.Extensions.Length > 0)
        {
            text.AppendLiteral(" It uses extensions that are not read: ");
            text.AppendFormatted(string.Join(", ", scene.Extensions));
            text.AppendLiteral(".");
        }

        foreach (var problem in scene.Problems)
        {
            text.AppendLiteral(" ");
            text.AppendFormatted(problem);
        }

        return text.ToStringAndClear();
    }

    /// <summary>Writes a count with its noun, such as "1 view" or "2 views".</summary>
    /// <param name="text">Receives the words.</param>
    /// <param name="count">The count.</param>
    /// <param name="noun">The noun in its singular form.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Count(ref DefaultInterpolatedStringHandler text, int count, string noun)
    {
        text.AppendFormatted(count);
        text.AppendLiteral(" ");
        text.AppendFormatted(noun);
        text.AppendLiteral(count == 1 ? string.Empty : "s");
    }
}
