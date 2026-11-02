// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A camera: where it is, where it looks, and the transforms that take the world to the screen.</summary>
/// <param name="View">The transform from the world to the camera.</param>
/// <param name="Projection">The transform from the camera to clip space.</param>
/// <param name="Position">The camera's position in the world.</param>
/// <param name="Forward">The unit vector along which the camera looks.</param>
/// <param name="Name">The name of the view node, or empty for the camera made to frame the whole scene.</param>
[DebuggerDisplay("U3dCamera: {Name}")]
internal readonly record struct U3dCamera(Matrix4x4 View, Matrix4x4 Projection, Vector3 Position, Vector3 Forward, string Name);
