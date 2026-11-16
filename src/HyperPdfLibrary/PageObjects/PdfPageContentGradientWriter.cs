// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Writes a gradient fill as a shading pattern whose coordinates are those of the page (ISO 32000-2, 8.7.3.1 and 8.7.4.3).</summary>
internal static class PdfPageContentGradientWriter
{
    /// <summary>The shading type of an axial shading.</summary>
    private const int AxialShading = 2;

    /// <summary>The shading type of a radial shading.</summary>
    private const int RadialShading = 3;

    /// <summary>The function type of an exponential interpolation.</summary>
    private const int ExponentialFunction = 2;

    /// <summary>The pattern type of a shading pattern.</summary>
    private const int ShadingPattern = 2;

    /// <summary>The value of a colour component at full strength.</summary>
    private const float Full = 1F;

    /// <summary>The half of a distance or a size.</summary>
    private const float Half = 0.5F;

    /// <summary>The degrees in a half turn.</summary>
    private const float HalfTurn = 180F;

    /// <summary>The components of a colour in red, green and blue.</summary>
    private const int Rgb = 3;

    /// <summary>Sets the fill colour to a shading pattern made for the object's bounds.</summary>
    /// <param name="state">The owned parse or content state.</param>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object whose bounds the gradient spans.</param>
    /// <param name="gradient">The gradient.</param>
    internal static void WriteFill(PdfPageContent state, ref PdfContentBuilder builder, PdfPageObject item, PdfGradient gradient)
    {
        var objects = state.Document.Objects;
        var names = objects.Names;
        var bounds = item.Bounds;
        var pattern = new PdfDictionary(objects);
        pattern.Set(names.Intern("PatternType"), PdfValue.FromInteger(ShadingPattern));
        pattern.Set(names.Intern("Shading"), PdfValue.FromDictionary(PdfPageContentGradientWriter.MakeShading(objects, bounds, gradient)));

        var name = PdfPageContentEditing.AllocateName(state, KnownName.Pattern, "RgP");
        state.Generated.Add(new(KnownName.Pattern, name, PdfValue.FromDictionary(pattern), null));
        PdfPageContentPaintWriter.PaintNamed(state, ref builder, new(names.Intern("Pattern"), [], name), false);
    }

    /// <summary>Adds a device colour to an array as red, green and blue, each from 0 to 1.</summary>
    /// <param name="array">The array.</param>
    /// <param name="paint">The colour.</param>
    internal static void AddRgb(PdfArray array, PdfPaint paint)
    {
        var c = paint.Components;
        switch (paint.ColorSpace.ToKnownName())
        {
            case KnownName.DeviceRGB when c.Length >= Rgb:
                {
                    AddAll(array, Math.Clamp(c[0], 0F, Full), Math.Clamp(c[1], 0F, Full), Math.Clamp(c[2], 0F, Full));
                    break;
                }

            case KnownName.DeviceCMYK when c.Length >= PdfPageContentWriter.CmykComponents:
                {
                    var black = Full - Math.Clamp(c[PdfPageContentWriter.CmykComponents - 1], 0F, Full);
                    AddAll(array, (Full - Math.Clamp(c[0], 0F, Full)) * black, (Full - Math.Clamp(c[1], 0F, Full)) * black, (Full - Math.Clamp(c[2], 0F, Full)) * black);
                    break;
                }

            default:
                {
                    var gray = c.Length > 0 ? Math.Clamp(c[0], 0F, Full) : 0F;
                    AddAll(array, gray, gray, gray);
                    break;
                }
        }
    }

    /// <summary>Makes the shading dictionary.</summary>
    /// <param name="objects">The store that owns the new objects.</param>
    /// <param name="bounds">The bounds of the object.</param>
    /// <param name="gradient">The gradient.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary MakeShading(PdfObjectStore objects, PdfRectangle bounds, PdfGradient gradient)
    {
        var names = objects.Names;
        var shading = new PdfDictionary(objects);
        var centerX = bounds.Left + (bounds.Width * Half);
        var centerY = bounds.Bottom + (bounds.Height * Half);
        var coords = new PdfArray(objects);
        if (gradient.Shape == PdfGradientShape.Radial)
        {
            var radius = MathF.Max(MathF.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height)) * Half, float.Epsilon);
            shading.Set(names.Intern("ShadingType"), PdfValue.FromInteger(RadialShading));
            PdfPageContentGradientWriter.AddAll(coords, centerX, centerY, 0F, centerX, centerY, radius);
        }
        else
        {
            // The line runs through the middle of the box and is as long as the box is across in its direction, so the ends reach the corners.
            var radians = gradient.AngleDegrees * MathF.PI / HalfTurn;
            var dx = MathF.Cos(radians);
            var dy = MathF.Sin(radians);
            var half = ((MathF.Abs(dx) * bounds.Width) + (MathF.Abs(dy) * bounds.Height)) * Half;
            shading.Set(names.Intern("ShadingType"), PdfValue.FromInteger(AxialShading));
            PdfPageContentGradientWriter.AddAll(coords, centerX - (dx * half), centerY - (dy * half), centerX + (dx * half), centerY + (dy * half));
        }

        shading.Set(names.Intern("ColorSpace"), PdfValue.FromName(KnownName.DeviceRGB));
        shading.Set(names.Intern("Coords"), PdfValue.FromArray(coords));
        shading.Set(names.Intern("Function"), PdfValue.FromDictionary(PdfPageContentGradientWriter.MakeFunction(objects, gradient)));
        var extend = new PdfArray(objects);
        extend.Add(PdfValue.FromBoolean(true));
        extend.Add(PdfValue.FromBoolean(true));
        shading.Set(names.Intern("Extend"), PdfValue.FromArray(extend));
        return shading;
    }

    /// <summary>Makes the exponential interpolation between the two colours.</summary>
    /// <param name="objects">The store that owns the new objects.</param>
    /// <param name="gradient">The gradient.</param>
    /// <returns>The function dictionary.</returns>
    private static PdfDictionary MakeFunction(PdfObjectStore objects, PdfGradient gradient)
    {
        var names = objects.Names;
        var function = new PdfDictionary(objects);
        function.Set(names.Intern("FunctionType"), PdfValue.FromInteger(ExponentialFunction));
        var domain = new PdfArray(objects);
        PdfPageContentGradientWriter.AddAll(domain, 0F, Full);
        function.Set(names.Intern("Domain"), PdfValue.FromArray(domain));
        var start = new PdfArray(objects);
        PdfPageContentGradientWriter.AddRgb(start, gradient.Start);
        function.Set(names.Intern("C0"), PdfValue.FromArray(start));
        var end = new PdfArray(objects);
        PdfPageContentGradientWriter.AddRgb(end, gradient.End);
        function.Set(names.Intern("C1"), PdfValue.FromArray(end));
        function.Set(names.Intern("N"), PdfValue.FromInteger(1));
        return function;
    }

    /// <summary>Adds numbers to an array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="values">The numbers.</param>
    private static void AddAll(PdfArray array, params ReadOnlySpan<float> values)
    {
        foreach (var value in values)
        {
            array.Add(PdfValue.FromReal(value));
        }
    }
}
