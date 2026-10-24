// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The reference frames and vectors of a 4x4 block for both lists, as the deblocking filter compares them.</summary>
/// <param name="Uid0">The identity of the list 0 reference frame, or -1 when the block does not use list 0.</param>
/// <param name="Vector0">The list 0 vector.</param>
/// <param name="Uid1">The identity of the list 1 reference frame, or -1 when the block does not use list 1.</param>
/// <param name="Vector1">The list 1 vector.</param>
internal readonly record struct H264BlockMotion(int Uid0, MotionVector Vector0, int Uid1, MotionVector Vector1)
{
    /// <summary>Gets the number of vectors the block uses.</summary>
    internal int Count => (Uid0 >= 0 ? 1 : 0) + (Uid1 >= 0 ? 1 : 0);

    /// <summary>Gets the reference frame of the only vector of a block that uses one.</summary>
    internal int OnlyUid => Uid0 >= 0 ? Uid0 : Uid1;

    /// <summary>Gets the only vector of a block that uses one.</summary>
    internal MotionVector OnlyVector => Uid0 >= 0 ? Vector0 : Vector1;
}
