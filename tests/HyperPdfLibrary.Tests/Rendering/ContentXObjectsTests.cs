// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Tests bounded execution of nested Form XObject references.</summary>
public sealed class ContentXObjectsTests
{
    /// <summary>The number of Form XObjects in the over-deep chain.</summary>
    private const int HostileDepth = 10_000;

    /// <summary>The page width and height in points.</summary>
    private const int PageSize = 100;

    /// <summary>The first additional object number in the rendering fixture.</summary>
    private const int FirstForm = 5;

    /// <summary>The other Form XObject in the cycle.</summary>
    private const int OtherForm = 6;

    /// <summary>The Form's call to its nested XObject.</summary>
    private const string NextFormContent = "/Next Do";

    /// <summary>The draw-depth cap bounds mutually recursive forms and preserves following page content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawForm_CircularForms_ContinuesAfterDo()
    {
        var pdf = Page();
        _ = pdf.AddStream("/Subtype /Form /BBox [0 0 1 1] /Resources << /XObject << /Next 6 0 R >> >>", NextFormContent);
        _ = pdf.AddStream("/Subtype /Form /BBox [0 0 1 1] /Resources << /XObject << /Next 5 0 R >> >>", NextFormContent);

        var recorder = GraphicsStateRecorder.Run(pdf);

        await Assert.That(recorder.Paints.Count).IsEqualTo(1);
        await Assert.That(pdf.NextNumber).IsEqualTo(OtherForm + 1);
    }

    /// <summary>A ten thousand-form chain is cut before reaching its deep paint.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawForm_DeepForms_CutsDeepPaint()
    {
        var pdf = Page();
        for (var index = 0; index < HostileDepth; index++)
        {
            var resources = string.Create(
                CultureInfo.InvariantCulture,
                $"/Subtype /Form /BBox [0 0 1 1] /Resources << /XObject << /Next {FirstForm + index + 1} 0 R >> >>");
            _ = pdf.AddStream(resources, index == HostileDepth - 1 ? "0 0 1 1 re f" : NextFormContent);
        }

        var recorder = GraphicsStateRecorder.Run(pdf);

        await Assert.That(recorder.Paints.Count).IsEqualTo(1);
    }

    /// <summary>A form without its own resources inherits the page's resources, including its cyclic Do target.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawForm_InheritedResourceCycle_ContinuesAfterDo()
    {
        var pdf = Page();
        _ = pdf.AddStream("/Subtype /Form /BBox [0 0 1 1]", "/Start Do");

        var recorder = GraphicsStateRecorder.Run(pdf);

        await Assert.That(recorder.Paints.Count).IsEqualTo(1);
    }

    /// <summary>Builds a page that calls a form and then paints one rectangle.</summary>
    /// <returns>The raw PDF builder.</returns>
    private static RenderTestPdf Page() => new(PageSize, PageSize) { Resources = "/XObject << /Start 5 0 R >>", Content = "/Start Do 0 0 1 1 re f" };
}
