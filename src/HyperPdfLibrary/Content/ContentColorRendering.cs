// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Reads the rendering intent (<c>ri</c>, /RI, image /Intent) and /UseBlackPtComp into the interpreter's graphics state,
/// and converts the current colours again when the ICC conversion they select changes.
/// </summary>
internal static class ContentColorRendering
{
    /// <summary>Handles <c>ri</c>; an operand that is not a name is ignored.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    internal static void OpSetRenderingIntent(ContentInterpreter self, ref ContentReader reader)
    {
        var operand = reader.Operand(0);
        if (operand.Kind != ContentOperandKind.Name)
        {
            return;
        }

        var previous = self.State.ColorRendering;
        self.State.RenderingIntent = ColorRenderingNames.ReadIntent(self.Cache.Names.GetSpelling(operand.Name));
        Refresh(self, previous);
    }

    /// <summary>Applies an ExtGState /RI entry; a value that is not a name is ignored.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value, resolved.</param>
    internal static void ApplyIntent(ContentInterpreter self, PdfValue value)
    {
        var name = value.AsName();
        if (!name.IsNone)
        {
            self.State.RenderingIntent = ColorRenderingNames.ReadIntent(self.Cache.Names.GetSpelling(name));
        }
    }

    /// <summary>Applies an ExtGState /UseBlackPtComp entry; a value other than the names ON, OFF and Default is ignored.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value, resolved.</param>
    internal static void ApplyBlackPointMode(ContentInterpreter self, PdfValue value)
    {
        var name = value.AsName();
        if (!name.IsNone && ColorRenderingNames.TryReadBlackPointMode(self.Cache.Names.GetSpelling(name), out var mode))
        {
            self.State.BlackPointMode = mode;
        }
    }

    /// <summary>
    /// Converts the fill and stroke colours again when the rendering changed since they were set, because the rendering
    /// in force when an object is painted is the one that applies (ISO 32000-2 11.7.5.3).
    /// </summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="previous">The rendering before the change.</param>
    internal static void Refresh(ContentInterpreter self, IccRendering previous)
    {
        var rendering = self.State.ColorRendering;
        if (rendering == previous || self.ColorLocked)
        {
            return;
        }

        self.State.Fill = self.State.Fill.Rerender(previous, rendering);
        self.State.Stroke = self.State.Stroke.Rerender(previous, rendering);
    }

    /// <summary>Gets the rendering an image converts with: its own /Intent when it has one (ISO 32000-2 8.9.5), else the graphics state's.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="image">The image dictionary.</param>
    /// <returns>The rendering, with the graphics state's black point choice.</returns>
    internal static IccRendering ImageRendering(ContentInterpreter self, PdfDictionary image)
    {
        var name = image.GetName(KnownName.Intent);
        var intent = name.IsNone ? self.State.RenderingIntent : ColorRenderingNames.ReadIntent(self.Cache.Names.GetSpelling(name));
        return IccRendering.From(intent, self.State.BlackPointMode);
    }
}
