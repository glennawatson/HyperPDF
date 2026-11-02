// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A shading modifier: the shaders a model node draws each of its mesh's shader lists with (ECMA-363 9.7.5).</summary>
/// <param name="Name">The name of the node chain the modifier belongs to.</param>
/// <param name="Lists">For each shader list, the names of its shaders. Only the first shader of a list is drawn.</param>
[DebuggerDisplay("U3dShadingBinding: {Name} {Lists.Length} lists")]
public sealed record U3dShadingBinding(string Name, string[][] Lists);
