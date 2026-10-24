// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Rendering;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Makes the render head that the shared services builder gives the app's tests: the real Avalonia one.</summary>
internal static class TestHeads
{
    /// <summary>Creates the Avalonia render head.</summary>
    /// <returns>The head.</returns>
    internal static IRenderHead Create() => new AvaloniaRenderHead();
}
