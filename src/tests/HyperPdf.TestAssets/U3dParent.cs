// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.TestAssets;

/// <summary>A parent of a test scene node and the transform that places the node under it.</summary>
/// <param name="Name">The parent's name; empty for the root of the scene.</param>
/// <param name="Transform">The transform, in the row-vector layout of <see cref="Matrix4x4"/>.</param>
public readonly record struct U3dParent(string Name, Matrix4x4 Transform);
