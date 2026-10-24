// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The motion of a block for one reference list.</summary>
/// <param name="Ref">The reference frame, or <see langword="null"/> when the block does not use this list.</param>
/// <param name="RefIdx">The reference index.</param>
/// <param name="Vector">The motion vector in quarter samples.</param>
internal readonly record struct H264ListMotion(H264RefPicture? Ref, int RefIdx, MotionVector Vector);
