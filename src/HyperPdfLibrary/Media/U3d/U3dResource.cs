// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A named resource that the scene refers to: a light, view, shader, material, texture or motion.</summary>
/// <param name="Kind">The block type that declared it.</param>
/// <param name="Name">The resource's name.</param>
[DebuggerDisplay("U3dResource: {Kind} {Name}")]
public sealed record U3dResource(U3dBlockType Kind, string Name);
