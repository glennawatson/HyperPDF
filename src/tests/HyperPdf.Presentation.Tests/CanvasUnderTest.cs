// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Tests;

/// <summary>A canvas state and the host it works through, in a scrolling area of the test size.</summary>
/// <param name="Host">The host.</param>
/// <param name="State">The state.</param>
internal readonly record struct CanvasUnderTest(FakeCanvasHost Host, PageCanvasState State)
{
    /// <summary>The width of the scrolling area.</summary>
    internal const double ViewWidth = 800;

    /// <summary>The height of the scrolling area.</summary>
    internal const double ViewHeight = 600;

    /// <summary>Makes a canvas state for a tab.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The host and the state working through it.</returns>
    internal static CanvasUnderTest Create(DocumentTabViewModel tab)
    {
        var view = new CanvasSize(ViewWidth, ViewHeight);
        var host = new FakeCanvasHost { ViewportSize = view, ScrollerSize = view, ExtentSize = view };
        return new(host, new(host) { Tab = tab });
    }
}
