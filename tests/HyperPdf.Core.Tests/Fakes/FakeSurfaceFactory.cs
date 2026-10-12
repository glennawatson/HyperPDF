// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Tests.Fakes;

/// <summary>Creates <see cref="FakeSurface"/> instances.</summary>
internal sealed class FakeSurfaceFactory : IRenderSurfaceFactory
{
    /// <inheritdoc/>
    public IRenderSurface Create(int width, int height) => new FakeSurface(width, height);
}
