// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Draws a still frame of a decoded U3D model with a small software rasteriser: perspective or orthographic views, ambient,
/// directional and point lights, textures and transparency. Specular highlights, fog, backdrops, overlays, animation and trim
/// curves are not drawn. It reads the model and never runs or opens anything in it.
/// </summary>
public static class U3dRenderer
{
    /// <summary>Draws a frame.</summary>
    /// <param name="model">The decoded model.</param>
    /// <param name="options">The size, view and background.</param>
    /// <param name="cancellationToken">Stops the drawing between faces.</param>
    /// <returns>The frame.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options give a size that is not positive or that has more pixels than <see cref="U3dRenderOptions.MaxPixels"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static U3dFrame Render(U3dModel model, U3dRenderOptions options, CancellationToken cancellationToken)
    {
        Check(model, options);
        using var canvas = new U3dCanvas(options.Width, options.Height, options.Background);
        var view = Run(model, options, canvas, cancellationToken);
        return new(options.Width, options.Height, canvas.Pixels, view, canvas.Drawn);
    }

    /// <summary>
    /// Lights and places the triangles of a frame without filling them, so that a painter with its own triangle drawing, such as a GPU
    /// canvas, can draw them. Triangles come far to near; there is no depth buffer, so triangles that cross each other are not
    /// resolved as exactly as <see cref="Render"/> resolves them.
    /// </summary>
    /// <param name="model">The decoded model.</param>
    /// <param name="options">The size, view and background.</param>
    /// <param name="cancellationToken">Stops the work between faces.</param>
    /// <returns>The triangles.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options give a size that is not positive or that has more pixels than <see cref="U3dRenderOptions.MaxPixels"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static U3dTriangleList Collect(U3dModel model, U3dRenderOptions options, CancellationToken cancellationToken)
    {
        Check(model, options);
        var collector = new U3dTriangleCollector(options.Width, options.Height);
        var view = Run(model, options, collector, cancellationToken);
        return new(options.Width, options.Height, options.Background, collector.FarToNear(), view);
    }

    /// <summary>Checks the arguments of a drawing call.</summary>
    /// <param name="model">The model.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options give an invalid size.</exception>
    private static void Check(U3dModel model, U3dRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsValid)
        {
            throw new ArgumentException("The frame size is not positive or has too many pixels.", nameof(options));
        }
    }

    /// <summary>Lights the scene and sends every triangle to a target.</summary>
    /// <param name="model">The model.</param>
    /// <param name="options">The options.</param>
    /// <param name="target">Takes the triangles.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The name of the view node the frame is seen from; empty for the camera that framed the model.</returns>
    private static string Run(U3dModel model, U3dRenderOptions options, IU3dTriangleTarget target, CancellationToken cancellationToken)
    {
        var scene = new U3dRenderScene(model);
        var graph = new U3dSceneGraph(model.Nodes, U3dLimits.Default.MaxInstances);
        var camera = U3dCameras.Create(scene, graph, options.ViewName, (float)options.Width / options.Height);
        var rig = U3dLightRig.Collect(scene, graph, camera.Forward);
        for (var i = 0; i < model.Nodes.Length; i++)
        {
            if (model.Nodes[i].Kind == U3dNodeKind.Model && model.Nodes[i].Visibility != 0)
            {
                DrawNode(scene, graph, new(model.Nodes[i], i, camera, rig), target, cancellationToken);
            }
        }

        return camera.Name;
    }

    /// <summary>Draws a model node in every place it is in the world.</summary>
    /// <param name="scene">The model with its resources indexed.</param>
    /// <param name="graph">The scene's places.</param>
    /// <param name="request">The node, the camera and the lights.</param>
    /// <param name="canvas">Receives the triangles.</param>
    /// <param name="cancellationToken">Stops the drawing.</param>
    private static void DrawNode(U3dRenderScene scene, U3dSceneGraph graph, in NodeRequest request, IU3dTriangleTarget canvas, CancellationToken cancellationToken)
    {
        foreach (var mesh in scene.MeshesOf(request.Node.Resource))
        {
            var styles = scene.StylesFor(request.Node.Name, HighestShading(mesh) + 1);
            foreach (var world in graph.PlacesOf(request.Index))
            {
                U3dMeshDrawer.Draw(new(mesh, world, request.Camera, request.Rig, styles, request.Node.Visibility), canvas, cancellationToken);
            }
        }
    }

    /// <summary>Finds the highest shader list index a mesh's faces use.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The index; 0 for a mesh with no faces.</returns>
    private static int HighestShading(U3dMesh mesh)
    {
        var highest = 0;
        foreach (var face in mesh.Faces)
        {
            highest = Math.Max(highest, face.Shading);
        }

        return highest;
    }

    /// <summary>A model node to draw with the camera and lights.</summary>
    /// <param name="Node">The node.</param>
    /// <param name="Index">The node's index in the scene.</param>
    /// <param name="Camera">The camera.</param>
    /// <param name="Rig">The lights.</param>
    private readonly record struct NodeRequest(U3dSceneNode Node, int Index, U3dCamera Camera, U3dLightRig Rig);
}
