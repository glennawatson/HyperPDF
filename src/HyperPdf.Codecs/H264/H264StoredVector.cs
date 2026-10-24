// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>A motion vector with the difference that was read to make it.</summary>
/// <param name="Vector">The motion vector.</param>
/// <param name="Difference">The motion vector difference.</param>
internal readonly record struct H264StoredVector(MotionVector Vector, MotionVector Difference);
