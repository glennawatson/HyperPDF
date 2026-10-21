// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Tests;

/// <summary>Makes the render head that tests of the view models run with.</summary>
internal static class TestHeads
{
    /// <summary>Creates a render head with no toolkit behind it.</summary>
    /// <returns>The head.</returns>
    internal static IRenderHead Create() => new FakeRenderHead();
}
