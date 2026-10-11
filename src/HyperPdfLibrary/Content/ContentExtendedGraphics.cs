// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's ExtendedGraphics operations over its owned state.</summary>
internal static class ContentExtendedGraphics
{
    /// <summary>The entries of a transfer function table.</summary>
    internal const int TransferEntries = 256;

    /// <summary>The largest byte as a float.</summary>
    internal const float ByteMax = 255;

    /// <summary>Half the width and height of the area assumed for a soft mask group that gives no bounding box.</summary>
    internal const float UnboundedExtent = 1_000_000;

    /// <summary>The width and height of the area assumed for a soft mask group that gives no bounding box.</summary>
    internal const float UnboundedSize = 2 * ContentExtendedGraphics.UnboundedExtent;

    /// <summary>The numbers in a dash array entry of /D plus its phase.</summary>
    internal const int DashEntryCount = 2;

    /// <summary>The numbers in an ExtGState /Font entry: the font and its size.</summary>
    internal const int FontEntryCount = 2;

    /// <summary>Reads a /BM value: a name, or an array of names of which the first known one applies.</summary>
    /// <param name = "value">The value.</param>
    /// <returns>The blend mode.</returns>
    internal static PdfBlendMode ReadBlendMode(PdfValue value)
    {
        if (value.AsArray() is not { } names)
        {
            return ContentExtendedGraphics.BlendModeFor(value.AsName()) ?? PdfBlendMode.Normal;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (ContentExtendedGraphics.BlendModeFor(names.GetName(i)) is { } mode)
            {
                return mode;
            }
        }

        return PdfBlendMode.Normal;
    }

    /// <summary>Maps a blend mode name.</summary>
    /// <param name = "name">The name.</param>
    /// <returns>The mode, or null for an unknown name.</returns>
    internal static PdfBlendMode? BlendModeFor(PdfName name) => name.ToKnownName() switch
    {
        KnownName.Normal or KnownName.Compatible => PdfBlendMode.Normal,
        KnownName.Multiply => PdfBlendMode.Multiply,
        KnownName.Screen => PdfBlendMode.Screen,
        KnownName.Overlay => PdfBlendMode.Overlay,
        KnownName.Darken => PdfBlendMode.Darken,
        KnownName.Lighten => PdfBlendMode.Lighten,
        KnownName.ColorDodge => PdfBlendMode.ColorDodge,
        KnownName.ColorBurn => PdfBlendMode.ColorBurn,
        KnownName.HardLight => PdfBlendMode.HardLight,
        KnownName.SoftLight => PdfBlendMode.SoftLight,
        KnownName.Difference => PdfBlendMode.Difference,
        KnownName.Exclusion => PdfBlendMode.Exclusion,
        KnownName.Hue => PdfBlendMode.Hue,
        KnownName.Saturation => PdfBlendMode.Saturation,
        KnownName.Color => PdfBlendMode.Color,
        KnownName.Luminosity => PdfBlendMode.Luminosity,
        _ => null,
    };

    /// <summary>Reads a boolean entry the way PDFium does, which also accepts a non-zero number.</summary>
    /// <param name = "value">The value.</param>
    /// <returns><see langword="true"/> for <c>true</c> or a non-zero number.</returns>
    internal static bool IsTrue(PdfValue value) => value.AsBoolean() || value.AsInt32() != 0;

    /// <summary>Reads the /TR transfer function of a luminosity mask into a lookup table.</summary>
    /// <param name = "mask">The soft mask dictionary.</param>
    /// <returns>The table, or null for the identity.</returns>
    internal static byte[]? ReadTransfer(PdfDictionary mask)
    {
        var value = mask.Get(KnownName.TR);
        if (value.IsNull || value.IsName(KnownName.Identity) || PdfFunction.Parse(value) is not { } function)
        {
            return null;
        }

        var table = new byte[ContentExtendedGraphics.TransferEntries];
        Span<float> input = stackalloc float[1];
        Span<float> output = stackalloc float[PdfFunction.MaxComponents];
        for (var i = 0; i < table.Length; i++)
        {
            input[0] = i / ContentExtendedGraphics.ByteMax;
            function.Evaluate(input, output);
            table[i] = (byte)MathF.Round(Math.Clamp(output[0], 0, 1) * ContentExtendedGraphics.ByteMax);
        }

        return table;
    }

    /// <summary>Applies the entries of an ExtGState dictionary that affect rendering; rendering intent, flatness and the like are ignored.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The dictionary.</param>
    internal static void ApplyExtGState(ContentInterpreter self, PdfDictionary dictionary)
    {
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            var value = dictionary.Get(key);
            if (key == self.Cache.LowerCa)
            {
                self.State.FillAlpha = Math.Clamp(value.AsSingle(1), 0, 1);
                continue;
            }

            ContentExtendedGraphics.ApplyExtGStateEntry(self, key.ToKnownName(), value);
        }

        ContentExtendedGraphics.ApplyOverprint(self, dictionary);
    }

    /// <summary>
    /// Applies /OP, /op, /OPM and /TK as PDFium reads them: /OP sets stroke overprint, and fill overprint too unless /op
    /// is present. Rendering intent (/RI), flatness (/FL), smoothness (/SM), transfer and halftones are parsed and ignored.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The ExtGState dictionary.</param>
    internal static void ApplyOverprint(ContentInterpreter self, PdfDictionary dictionary)
    {
        var stroke = dictionary.Get(KnownName.OP);
        var fill = dictionary.Get(KnownName.FillOverprint);
        if (!stroke.IsNull)
        {
            self.State.StrokeOverprint = ContentExtendedGraphics.IsTrue(stroke);
            if (fill.IsNull)
            {
                self.State.FillOverprint = self.State.StrokeOverprint;
            }
        }

        if (!fill.IsNull)
        {
            self.State.FillOverprint = ContentExtendedGraphics.IsTrue(fill);
        }

        var mode = dictionary.Get(KnownName.OPM);
        if (!mode.IsNull)
        {
            self.State.OverprintMode = mode.AsInt32();
        }

        var knockout = dictionary.Get(KnownName.TK);
        if (!knockout.IsNull)
        {
            self.State.TextKnockout = ContentExtendedGraphics.IsTrue(knockout);
        }
    }

    /// <summary>Applies one ExtGState entry.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "key">The key.</param>
    /// <param name = "value">The value, resolved.</param>
    internal static void ApplyExtGStateEntry(ContentInterpreter self, KnownName key, PdfValue value)
    {
        switch (key)
        {
            case KnownName.LW:
                {
                    self.State.LineWidth = Math.Max(0, value.AsSingle(1));
                    break;
                }

            case KnownName.LC:
                {
                    self.State.LineCap = value.AsInt32();
                    break;
                }

            case KnownName.LJ:
                {
                    self.State.LineJoin = value.AsInt32();
                    break;
                }

            case KnownName.ML:
                {
                    self.State.MiterLimit = value.AsSingle(self.State.MiterLimit);
                    break;
                }

            case KnownName.D:
                {
                    ContentExtendedGraphics.ApplyDash(self, value.AsArray());
                    break;
                }

            case KnownName.CA:
                {
                    self.State.StrokeAlpha = Math.Clamp(value.AsSingle(1), 0, 1);
                    break;
                }

            default:
                {
                    ContentExtendedGraphics.ApplyExtGStateObject(self, key, value);
                    break;
                }
        }
    }

    /// <summary>Applies the entries that name other objects: font, blend mode and soft mask.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "key">The key.</param>
    /// <param name = "value">The value, resolved.</param>
    internal static void ApplyExtGStateObject(ContentInterpreter self, KnownName key, PdfValue value)
    {
        switch (key)
        {
            case KnownName.Font:
                {
                    if (value.AsArray() is { Count: >= ContentExtendedGraphics.FontEntryCount } font && font.GetDictionary(0) is { } dictionary)
                    {
                        self.State.Font = self.Cache.Fonts.Get(dictionary);
                        self.State.FontSize = font.GetSingle(1);
                    }

                    break;
                }

            case KnownName.BM:
                {
                    self.State.BlendMode = ContentExtendedGraphics.ReadBlendMode(value);
                    break;
                }

            case KnownName.SMask:
                {
                    self.State.SoftMask = value.AsDictionary() is { } mask ? ContentExtendedGraphics.GetSoftMask(self, mask) : null;
                    break;
                }

            default:
                {
                    // Overprint and text knockout are read by ApplyOverprint; transfer, halftone, flatness and rendering intent change nothing drawn.
                    break;
                }
        }
    }

    /// <summary>Applies a /D entry: <c>[dashArray phase]</c>.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "array">The array.</param>
    internal static void ApplyDash(ContentInterpreter self, PdfArray? array)
    {
        if (array is not { Count: >= ContentExtendedGraphics.DashEntryCount } || array.GetArray(0) is not { } lengths)
        {
            return;
        }

        Span<float> buffer = stackalloc float[ContentGraphicsState.MaxDashEntries];
        var count = Math.Min(lengths.Count, buffer.Length);
        var total = 0F;
        for (var i = 0; i < count; i++)
        {
            buffer[i] = lengths.GetSingle(i);
            total += Math.Abs(buffer[i]);
        }

        self.State.Dash = count == 0 || total <= 0 ? [] : [.. buffer[..count]];
        self.State.DashPhase = array.GetSingle(1);
    }

    /// <summary>Gets a soft mask recorded with the current transform.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "mask">The soft mask dictionary.</param>
    /// <returns>The mask, or null when it is damaged or no drawing backend is registered.</returns>
    internal static PdfSoftMask? GetSoftMask(ContentInterpreter self, PdfDictionary mask) =>
        PdfDrawingServices.ConfiguredBackend is null || mask.GetStream(KnownName.G) is null ? null : self.Cache.SoftMasks.GetOrCreate(
            new(mask, self.State.Ctm),
            new SoftMaskRequest(self, mask),
            static (key, request) => ContentExtendedGraphics.RecordSoftMask(request.Owner, request.Mask, key.Ctm));

    /// <summary>Records a soft mask's group to a picture.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "mask">The soft mask dictionary.</param>
    /// <param name = "ctm">The transform in force when the mask was set.</param>
    /// <returns>The mask.</returns>
    internal static PdfSoftMask RecordSoftMask(ContentInterpreter self, PdfDictionary mask, Matrix3x2 ctm)
    {
        var group = mask.GetStream(KnownName.G)!;
        var bounds = group.Dictionary.TryGetRectangle(KnownName.BBox, out var box)
            ? ctm.MapRect(new(box.Left, box.Bottom, box.Right, box.Top))
            : PdfRect.Create(-UnboundedExtent, -UnboundedExtent, UnboundedSize, UnboundedSize);
        using var recorder = self.Device.CreatePictureDevice(bounds);
        using var child = new ContentInterpreter(self.Cache, recorder, self.Depth + 1, cancellationToken: self.CancellationToken) { Printing = self.Printing };
        ContentExecution.RunForm(child, group, ctm, ContentExecution.CurrentResources(self));
        var picture = recorder.Finish();
        var luminosity = mask.IsName(KnownName.S, KnownName.Luminosity);
        return new(picture, luminosity, ContentExtendedGraphics.ReadBackdrop(self, mask, group), luminosity ? ContentExtendedGraphics.ReadTransfer(mask) : null);
    }

    /// <summary>Reads the /BC backdrop colour of a luminosity mask.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "mask">The soft mask dictionary.</param>
    /// <param name = "group">The mask group.</param>
    /// <returns>The backdrop; black when none is given.</returns>
    internal static PdfColor ReadBackdrop(ContentInterpreter self, PdfDictionary mask, PdfStream group)
    {
        var colors = mask.GetArray(KnownName.BC);
        if (colors is null)
        {
            return PdfColor.Black;
        }

        var groupDictionary = group.Dictionary.GetDictionary(KnownName.Group);
        var space = groupDictionary is null
        || groupDictionary.Get(KnownName.CS).IsNull ? PdfColorSpace.FromComponents(colors.Count) : self.Cache.GetColorSpace(
            groupDictionary.Get(KnownName.CS),
            ContentExecution.CurrentResources(self)?.GetDictionary(KnownName.ColorSpace));
        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        _ = colors.ReadNumbers(components);
        var colour = ColorState.Resolve(space, components);
        return new(colour.Red, colour.Green, colour.Blue);
    }
}
