// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Applies the content operators that set or use colour, and visits the colour spaces they use.</summary>
internal static class PdfAColourOperators
{
    /// <summary>Text rendering modes that fill: 0, 2, 4 and 6.</summary>
    private const int FillModes = 0b0101_0101;

    /// <summary>Text rendering modes that stroke: 1, 2, 5 and 6.</summary>
    private const int StrokeModes = 0b0110_0110;

    /// <summary>Applies a colour operator: <c>g G rg RG k K cs CS scn SCN</c>.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="name">The last operand when it is a name, or none.</param>
    /// <param name="group">The operator's group.</param>
    internal static void Apply(PdfAColourState state, PdfAContentRun run, PdfName name, PdfAOperatorGroup group)
    {
        var stroke = group is PdfAOperatorGroup.StrokeGray or PdfAOperatorGroup.StrokeRgb or PdfAOperatorGroup.StrokeCmyk
            or PdfAOperatorGroup.StrokeSpace or PdfAOperatorGroup.StrokePattern;
        switch (group)
        {
            case PdfAOperatorGroup.FillSpace or PdfAOperatorGroup.StrokeSpace:
            {
                Set(run, stroke, name.IsNone ? null : PdfAResources.ColourSpace(state, run.Scope, name));
                break;
            }

            case PdfAOperatorGroup.FillPattern or PdfAOperatorGroup.StrokePattern:
            {
                SetPattern(state, run, stroke, name);
                break;
            }

            default:
            {
                Set(run, stroke, PdfAResources.ColourSpace(state, run.Scope, DeviceFamily(group)));
                break;
            }
        }

        PdfAColourChecks.Visit(state, stroke ? run.Paint.Stroke : run.Paint.Fill, new(0, false, run.Frame));
    }

    /// <summary>Visits the colour spaces a path painting operator uses.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="fill">Whether it fills.</param>
    /// <param name="stroke">Whether it strokes.</param>
    internal static void Paint(PdfAColourState state, PdfAContentRun run, bool fill, bool stroke)
    {
        var paint = run.Paint;
        if (!paint.ProcessColour)
        {
            return;
        }

        if (fill)
        {
            PdfAColourChecks.Visit(state, paint.Fill, new(paint.OverprintMode, paint.OverprintFill, run.Frame));
        }

        if (stroke)
        {
            PdfAColourChecks.Visit(state, paint.Stroke, new(paint.OverprintMode, paint.OverprintStroke, run.Frame));
        }
    }

    /// <summary>Visits what a text showing operator uses: a Type 3 font's glyphs and the colour spaces of its rendering mode.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    internal static void ShowText(PdfAColourState state, PdfAContentRun run)
    {
        var paint = run.Paint;
        PdfAFormWalk.VisitType3Font(state, paint.Font, run.Frame);
        var mode = 1 << paint.RenderMode;
        Paint(state, run, (FillModes & mode) != 0, (StrokeModes & mode) != 0);
    }

    /// <summary>Visits the shading <c>sh</c> paints.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="name">The shading name, or none.</param>
    internal static void Shading(PdfAColourState state, PdfAContentRun run, PdfName name)
    {
        if (!run.Paint.ProcessColour || name.IsNone)
        {
            return;
        }

        var shading = PdfAResources.Find(state, run.Scope, KnownName.Shading, name);
        PdfAPatternWalk.VisitShading(state, shading.Raw, shading.Resources);
    }

    /// <summary>Sets the fill or stroke colour space.</summary>
    /// <param name="run">The stream's state.</param>
    /// <param name="stroke">Whether it is the stroke space.</param>
    /// <param name="space">The space, or null.</param>
    private static void Set(PdfAContentRun run, bool stroke, PdfAColourSpace? space) =>
        run.Paint = stroke ? run.Paint with { Stroke = space } : run.Paint with { Fill = space };

    /// <summary>Applies <c>scn</c> or <c>SCN</c>: with a pattern space selected, the named pattern becomes the colour.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="stroke">Whether it is the stroke colour.</param>
    /// <param name="name">The pattern name, or none.</param>
    private static void SetPattern(PdfAColourState state, PdfAContentRun run, bool stroke, PdfName name)
    {
        var current = stroke ? run.Paint.Stroke : run.Paint.Fill;
        if (current is not { } space || PdfAColourSpaceParsing.Family(space) != KnownName.Pattern)
        {
            return;
        }

        var underlying = PdfAColourSpaceParsing.Underlying(state, space);
        var pattern = name.IsNone ? null : PdfAPatternWalk.Find(state, run.Scope, name);
        run.Paint = stroke
            ? run.Paint with { Stroke = pattern, StrokeUnderlying = underlying }
            : run.Paint with { Fill = pattern, FillUnderlying = underlying };
    }

    /// <summary>Gets the device family a device colour operator selects.</summary>
    /// <param name="group">The operator's group.</param>
    /// <returns>The family.</returns>
    private static KnownName DeviceFamily(PdfAOperatorGroup group) => group switch
    {
        PdfAOperatorGroup.FillGray or PdfAOperatorGroup.StrokeGray => KnownName.DeviceGray,
        PdfAOperatorGroup.FillRgb or PdfAOperatorGroup.StrokeRgb => KnownName.DeviceRGB,
        _ => KnownName.DeviceCMYK,
    };
}
