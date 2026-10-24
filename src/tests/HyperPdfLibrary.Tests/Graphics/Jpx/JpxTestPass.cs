// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>One coding pass of the test block coder.</summary>
/// <param name="Plane">The bit-plane.</param>
/// <param name="Type">The pass type: 0 significance, 1 refinement, 2 cleanup.</param>
internal readonly record struct JpxTestPass(int Plane, int Type);
