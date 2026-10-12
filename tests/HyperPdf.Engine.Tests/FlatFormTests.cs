// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms.Detection;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests finding places to write on a printed form, through a HyperPDF document.</summary>
public sealed class FlatFormTests
{
    /// <summary>How close edges must be, in points.</summary>
    private const float Tolerance = 2;

    /// <summary>The line's left end.</summary>
    private const float Left = 100;

    /// <summary>The line's height below the top.</summary>
    private const float LineDepth = 192;

    /// <summary>The box's top.</summary>
    private const float BoxTop = 240;

    /// <summary>The comb's boxes.</summary>
    private const int CombCells = 6;

    /// <summary>The places drawn.</summary>
    private const int Places = 3;

    /// <summary>The line, the box and the six character boxes are found where they are drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsPrintedFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-flat-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateFlatForm());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var regions = new List<FormRegion>();
            new FlatFormFinder().Find(document, 0, regions);
            var line = regions.Single(static r => r.Kind == FormRegionKind.Underline);
            var box = regions.Single(static r => r.Kind == FormRegionKind.Box);
            var comb = regions.Single(static r => r.Kind == FormRegionKind.Comb);

            await Assert.That(regions.Count).IsEqualTo(Places);
            await Assert.That(line.Bounds.Left).IsEqualTo(Left).Within(Tolerance);
            await Assert.That(line.Bounds.Bottom).IsEqualTo(LineDepth).Within(Tolerance);
            await Assert.That(box.Bounds.Top).IsEqualTo(BoxTop).Within(Tolerance);
            await Assert.That(comb.Cells).IsEqualTo(CombCells);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
