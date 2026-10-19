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
using HyperPdfLibrary.Rendering;

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

    /// <summary>Reads a boolean entry leniently: a non-zero number also counts as true, because damaged files write 1 for true.</summary>
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

    /// <summary>
    /// Applies the entries of an ExtGState dictionary (ISO 32000-2 Table 53). Flatness, smoothness, stroke adjustment,
    /// halftone, black generation and undercolour removal are kept in the state and change no output; the transfer
    /// function changes the final colours.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The dictionary.</param>
    internal static void ApplyExtGState(ContentInterpreter self, PdfDictionary dictionary)
    {
        var rendering = self.State.ColorRendering;
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            var value = dictionary.Get(key);
            if (key == self.Cache.LowerCa)
            {
                self.State.FillAlpha = Math.Clamp(value.AsSingle(1), 0, 1);
                continue;
            }

            if (key == self.Cache.UseBlackPointCompensation)
            {
                ContentColorRendering.ApplyBlackPointMode(self, value);
                continue;
            }

            var known = key.ToKnownName();
            if (!ContentDeviceFunctions.Apply(self, dictionary, known, value))
            {
                ContentExtendedGraphics.ApplyExtGStateEntry(self, known, value);
            }
        }

        ContentExtendedGraphics.ApplyOverprint(self, dictionary);
        ContentColorRendering.Refresh(self, rendering);
    }

    /// <summary>
    /// Applies /OP, /op, /OPM and /TK: /OP sets stroke overprint, and fill overprint too unless /op
    /// is present (ISO 32000-2 Table 57). A value of the wrong type is ignored, and /OPM keeps only 0 or 1.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The ExtGState dictionary.</param>
    internal static void ApplyOverprint(ContentInterpreter self, PdfDictionary dictionary)
    {
        var fillUsable = ContentDeviceFunctions.TryReadBoolean(dictionary.Get(KnownName.FillOverprint), out var fill);
        if (ContentDeviceFunctions.TryReadBoolean(dictionary.Get(KnownName.OP), out var stroke))
        {
            self.State.StrokeOverprint = stroke;
            if (!fillUsable)
            {
                self.State.FillOverprint = stroke;
            }
        }

        if (fillUsable)
        {
            self.State.FillOverprint = fill;
        }

        var mode = dictionary.Get(KnownName.OPM);
        if (mode.IsNumber && mode.AsInt32() is 0 or 1)
        {
            self.State.OverprintMode = mode.AsInt32();
        }

        if (ContentDeviceFunctions.TryReadBoolean(dictionary.Get(KnownName.TK), out var knockout))
        {
            self.State.TextKnockout = knockout;
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

    /// <summary>Applies the entries that name other objects or values: font, blend mode, soft mask and rendering intent.</summary>
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

            case KnownName.RI:
                {
                    ContentColorRendering.ApplyIntent(self, value);
                    break;
                }

            default:
                {
                    // Overprint and text knockout are read by ApplyOverprint; the device-dependent keys by ContentDeviceFunctions.
                    break;
                }
        }
    }

    /// <summary>Applies a /D entry: <c>[dashArray phase]</c>. The lengths of each dash array are made once and reused.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "array">The array.</param>
    internal static void ApplyDash(ContentInterpreter self, PdfArray? array)
    {
        if (array is not { Count: >= ContentExtendedGraphics.DashEntryCount } || array.GetArray(0) is not { } lengths)
        {
            return;
        }

        var pattern = self.Cache.Dashes.GetOrCreate(lengths, 0, static (key, _) => ContentExtendedGraphics.ReadDash(key));
        self.State.Dash = pattern is null ? default : pattern.Lengths;
        self.State.DashPhase = array.GetSingle(1);
    }

    /// <summary>Reads the lengths of a dash array.</summary>
    /// <param name = "lengths">The array.</param>
    /// <returns>The pattern; no lengths when the array is empty or its lengths add to nothing.</returns>
    internal static DashPattern ReadDash(PdfArray lengths)
    {
        Span<float> buffer = stackalloc float[ContentGraphicsState.MaxDashEntries];
        var count = Math.Min(lengths.Count, buffer.Length);
        var total = 0F;
        for (var i = 0; i < count; i++)
        {
            buffer[i] = lengths.GetSingle(i);
            total += Math.Abs(buffer[i]);
        }

        return new(count == 0 || total <= 0 ? [] : [.. buffer[..count]]);
    }

    /// <summary>Gets a soft mask recorded with the current transform.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "mask">The soft mask dictionary.</param>
    /// <returns>The mask, or null when it is damaged or no drawing backend is registered.</returns>
    internal static PdfSoftMask? GetSoftMask(ContentInterpreter self, PdfDictionary mask)
    {
        if (self.Cache.Document.DrawingServices.ConfiguredBackend is null || mask.GetStream(KnownName.G) is null)
        {
            return null;
        }

        var mode = self.Mode;
        self.Cache.ObserveLayerVersion(mode.LayerVersion);
        return self.Cache.SoftMasks.GetOrCreate(
            new(mask, self.State.Ctm, ContentExecution.CurrentResources(self), mode),
            new SoftMaskRequest(self, mask),
            static (key, request) => ContentExtendedGraphics.RecordSoftMask(request.Owner, request.Mask, key.Ctm));
    }

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
        using var child = ContentInterpreter.Rent(self.Cache, recorder, self.Depth + 1, self.CancellationToken);
        child.Printing = self.Printing;
        child.ZoomClass = self.ZoomClass;
        child.Colorants = self.Colorants;
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
