// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// The lights that shine on a frame. A light node shines the way a view node looks: along its own Y axis, as ECMA-363 8.3.3 prefers
/// for views. A scene with no light that is switched on is lit by a dim ambient light and a light at the viewer.
/// </summary>
internal sealed class U3dLightRig
{
    /// <summary>The ambient level of the light a scene with no lights gets.</summary>
    private const float FallbackAmbient = 0.3F;

    /// <summary>The level of the light at the viewer that a scene with no lights gets.</summary>
    private const float FallbackDirect = 0.7F;

    /// <summary>The point and spot lights and the directional lights.</summary>
    private readonly List<U3dLightSource> _sources = [];

    /// <summary>Gets the total ambient light.</summary>
    internal Vector3 Ambient { get; private set; }

    /// <summary>Makes the rig of a scene.</summary>
    /// <param name="scene">The model with its resources indexed.</param>
    /// <param name="graph">The scene's places.</param>
    /// <param name="forward">The direction the camera looks, for the fallback light.</param>
    /// <returns>The rig.</returns>
    internal static U3dLightRig Collect(U3dRenderScene scene, U3dSceneGraph graph, Vector3 forward)
    {
        var rig = new U3dLightRig();
        var nodes = scene.Model.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Kind == U3dNodeKind.Light && scene.LightNamed(nodes[i].Resource) is { IsEnabled: true } light)
            {
                foreach (var world in graph.PlacesOf(i))
                {
                    rig.Add(light, world);
                }
            }
        }

        if (rig._sources.Count == 0 && rig.Ambient == Vector3.Zero)
        {
            rig.Ambient = new(FallbackAmbient);
            rig._sources.Add(new(U3dLightKind.Directional, new(FallbackDirect), -forward, new(1, 0, 0)));
        }

        return rig;
    }

    /// <summary>Works out the light that reaches a point on a surface.</summary>
    /// <param name="position">The point, in world coordinates.</param>
    /// <param name="normal">The unit normal of the surface at the point, facing the viewer.</param>
    /// <returns>The direct light that reaches the point, from 0 up.</returns>
    internal Vector3 Direct(Vector3 position, Vector3 normal)
    {
        var sum = Vector3.Zero;
        foreach (var source in _sources)
        {
            var toLight = source.Kind == U3dLightKind.Directional ? source.Vector : source.Vector - position;
            var distance = toLight.Length();
            if (distance <= 0)
            {
                continue;
            }

            var attenuation = source.Kind == U3dLightKind.Directional ? 1 : Attenuate(source.Attenuation, distance);
            sum += source.Color * (MathF.Max(0, Vector3.Dot(normal, toLight / distance)) * attenuation);
        }

        return sum;
    }

    /// <summary>Works out how much a point or spot light fades over a distance: one over the constant, linear and quadratic terms (ECMA-363 9.8.1.5).</summary>
    /// <param name="factors">The constant, linear and quadratic factors.</param>
    /// <param name="distance">The distance from the light.</param>
    /// <returns>The share of the light that arrives.</returns>
    private static float Attenuate(Vector3 factors, float distance) =>
        1 / MathF.Max(factors.X + (factors.Y * distance) + (factors.Z * distance * distance), float.Epsilon);

    /// <summary>Adds a light placed in the world.</summary>
    /// <param name="light">The light resource.</param>
    /// <param name="world">The light node's world matrix.</param>
    private void Add(U3dLight light, Matrix4x4 world)
    {
        var color = light.Color * light.Intensity;
        if (light.Kind == U3dLightKind.Ambient)
        {
            Ambient += color;
            return;
        }

        var vector = light.Kind == U3dLightKind.Directional
            ? -Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, world))
            : world.Translation;
        _sources.Add(new(light.Kind, color, vector, light.Attenuation));
    }

    /// <summary>One light that is not ambient.</summary>
    /// <param name="Kind">The kind of light.</param>
    /// <param name="Color">The colour times the intensity.</param>
    /// <param name="Vector">For a directional light, the unit vector toward it; for a point or spot light, its position.</param>
    /// <param name="Attenuation">The constant, linear and quadratic factors.</param>
    private readonly record struct U3dLightSource(U3dLightKind Kind, Vector3 Color, Vector3 Vector, Vector3 Attenuation);
}
