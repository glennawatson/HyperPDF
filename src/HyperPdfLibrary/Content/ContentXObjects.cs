// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's XObjects operations over its owned state.</summary>
internal static class ContentXObjects
{
    /// <summary>The bytes of the <c>&lt; &lt;</c> and <c>&gt; &gt;</c> that wrap an inline image's entries.</summary>
    internal const int DictionaryDelimiters = 4;

    /// <summary>The width of one delimiter pair.</summary>
    internal const int DelimiterWidth = 2;

    /// <summary>Determines whether an object's optional content is shown.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The XObject's or annotation's dictionary.</param>
    /// <returns><see langword="true"/> when it has no /OC or its layers are on.</returns>
    internal static bool IsVisible(ContentInterpreter self, PdfDictionary dictionary)
    {
        var membership = dictionary.GetRaw(KnownName.OC);
        return membership.IsNull || ContentXObjects.IsLayerShown(self, membership);
    }

    /// <summary>Handles <c>Do</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpPaintXObject(ContentInterpreter self, ref ContentReader reader)
    {
        if (ContentExecution.FindResource(self, KnownName.XObject, reader.Operand(0).Name).AsStream() is { } xobject)
        {
            ContentXObjects.PaintXObject(self, xobject);
        }
    }

    /// <summary>Handles <c>BI</c> after the reader has found the image's data.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpInlineImage(ContentInterpreter self, ref ContentReader reader)
    {
        // Text extraction never decodes images.
        if (self.Hidden == 0 && self.TextObjects is null)
        {
            ContentXObjects.DrawInlineImage(self, ref reader);
        }
    }

    /// <summary>Handles <c>sh</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpPaintShading(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.Hidden > 0)
        {
            return;
        }

        var shading = ContentColors.GetShading(self, ContentExecution.FindResource(self, KnownName.Shading, reader.Operand(0).Name));
        if (shading is not null)
        {
            self.Device.PaintShading(shading, ref self.State);
        }
    }

    /// <summary>
    /// Determines whether optional content is shown: by the viewer's layer switches, or by print usage when printing.
    /// Text extraction sees every layer, as PDFium extracts text from hidden layers too.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "membership">The /OC value.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    internal static bool IsLayerShown(
        ContentInterpreter self,
        PdfValue membership) =>
        self.TextObjects is not null
        || (self.Printing ? self.Layers.IsPrinted(membership) : self.Layers.IsVisible(membership));

    /// <summary>Draws an XObject.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "xobject">The image or form stream.</param>
    internal static void PaintXObject(ContentInterpreter self, PdfStream xobject)
    {
        var dictionary = xobject.Dictionary;
        if (self.Hidden > 0 || !ContentXObjects.IsVisible(self, dictionary))
        {
            return;
        }

        switch (dictionary.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Form:
                {
                    ContentXObjects.DrawForm(self, xobject);
                    break;
                }

            case KnownName.Image:
                {
                    ContentXObjects.DrawImageXObject(self, xobject);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Draws an image XObject into the unit square.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "image">The image stream.</param>
    internal static void DrawImageXObject(ContentInterpreter self, PdfStream image)
    {
        if (self.TextObjects is not null)
        {
            // Text extraction never decodes images.
            return;
        }

        var reduction = ImageReduction.Select(image.Dictionary, self.State.Ctm, self.ImageScale);
        var entry = self.Cache.AcquireImage(image, reduction);
        if (entry is null)
        {
            return;
        }

        try
        {
            if (!(entry.IsMask && self.State.Fill.PaintsNothing))
            {
                ContentXObjects.DrawDecodedImage(self, entry.Image, entry.IsMask, entry.Interpolate, image.Dictionary);
            }
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>Draws a decoded image, simulating overprint the way PDFium does.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "image">The image.</param>
    /// <param name = "isMask">Whether it is a stencil mask.</param>
    /// <param name = "smooth">Whether it asks for smoothing.</param>
    /// <param name = "dictionary">The image dictionary.</param>
    internal static void DrawDecodedImage(ContentInterpreter self, IPdfRenderImage image, bool isMask, bool smooth, PdfDictionary dictionary)
    {
        if (self.ColorLocked)
        {
            ContentXObjects.DrawImageAsShape(self, image, isMask, smooth);
            return;
        }

        if (isMask || !ContentXObjects.OverprintsImage(self, dictionary))
        {
            self.Device.DrawImage(image, isMask, smooth, ref self.State);
            return;
        }

        var state = self.State;
        state.BlendMode = PdfBlendMode.Darken;
        self.Device.DrawImage(image, false, smooth, ref state);
    }

    /// <summary>
    /// Determines whether an image overprints. PDFium's source simulates overprint only here: an opaque, unmasked image
    /// in DeviceCMYK, Separation or DeviceN drawn with fill overprint on, overprint mode 0, the Normal blend and both
    /// alphas at 1 is darkened into the page instead of covering it. Paths, text and shadings never overprint. The PDFium
    /// build the app ships shows no darkening, so the rule applies only when the document's options turn it on.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The image dictionary.</param>
    /// <returns><see langword="true"/> when the image is drawn with the Darken blend.</returns>
    internal static bool OverprintsImage(ContentInterpreter self, PdfDictionary dictionary)
    {
        if (!self.Cache.SimulateOverprint
        || !self.State.FillOverprint
        || self.State.OverprintMode != 0
        || self.State.BlendMode != PdfBlendMode.Normal
        || self.State.FillAlpha < 1
        || self.State.StrokeAlpha < 1)
        {
            return false;
        }

        if (dictionary.ContainsKey(KnownName.SMask) || dictionary.ContainsKey(KnownName.Mask))
        {
            return false;
        }

        var space = dictionary.Get(KnownName.ColorSpace);
        return ContentXObjects.IsSubtractive(self, space.IsNull ? dictionary.Get(KnownName.CS) : space);
    }

    /// <summary>Determines whether a colour space is DeviceCMYK, Separation or DeviceN, following one resource name.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "space">The colour space value.</param>
    /// <returns><see langword="true"/> for those families.</returns>
    internal static bool IsSubtractive(ContentInterpreter self, PdfValue space)
    {
        if (space.AsArray() is { Count: > 0 } array)
        {
            return array.GetName(0).ToKnownName() is KnownName.Separation or KnownName.DeviceN or KnownName.DeviceCMYK;
        }

        var name = space.AsName();
        if (name.IsNone)
        {
            return false;
        }

        var known = name.ToKnownName();
        if (known is KnownName.DeviceCMYK or KnownName.CMYK)
        {
            return true;
        }

        var resource = ContentExecution.FindResource(self, KnownName.ColorSpace, name);
        return resource.AsArray() is { Count: > 0 } named ? named.GetName(0).ToKnownName() is KnownName.Separation or
        KnownName.DeviceN or
        KnownName.DeviceCMYK : resource.AsName().ToKnownName() == KnownName.DeviceCMYK;
    }

    /// <summary>Decodes and draws an inline image whose ranges the reader has found.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "reader">The reader.</param>
    internal static void DrawInlineImage(ContentInterpreter self, ref ContentReader reader)
    {
        var content = reader.Content;
        var range = reader.InlineImageDictionary;
        var offset = range.Start.GetOffset(content.Length);
        var length = range.End.GetOffset(content.Length) - offset;
        var text = new byte[length + ContentXObjects.DictionaryDelimiters];
        text[0] = (byte)'<';
        text[1] = (byte)'<';
        content.Slice(offset, length).CopyTo(text.AsSpan(ContentXObjects.DelimiterWidth));
        text[length + ContentXObjects.DelimiterWidth] = (byte)'>';
        text[length + ContentXObjects.DelimiterWidth + 1] = (byte)'>';
        var parser = new PdfParser(text, 0, self.Cache.Document.Objects, self.Cache.Names);
        if (parser.ParseValue().AsDictionary() is not { } dictionary)
        {
            return;
        }

        using var scope = Graphics.Colors.OutputIntentColors.Enter(self.Cache.DeviceColors);
        var data = PdfImageDecoder.DecodeInline(dictionary, content[reader.InlineImageData], ContentExecution.CurrentResources(self));
        if (data is null || data.UnsupportedCodec != PdfImageCodec.None)
        {
            return;
        }

        using var image = PdfRenderCache.ToRenderImage(data);
        if (image is not null && !(data.IsStencilMask && self.State.Fill.PaintsNothing))
        {
            ContentXObjects.DrawDecodedImage(self, image, data.IsStencilMask, data.Interpolate, dictionary);
        }
    }

    /// <summary>
    /// Draws an image inside a Type 3 glyph that gives only a shape (<c>d1</c>) or an uncoloured pattern. PDFium renders
    /// those as a mask painted with one colour, so a stencil mask keeps its shape and any other image covers its unit square.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "image">The image.</param>
    /// <param name = "isMask">Whether it is a stencil mask.</param>
    /// <param name = "smooth">Whether it asks for smoothing.</param>
    internal static void DrawImageAsShape(ContentInterpreter self, IPdfRenderImage image, bool isMask, bool smooth)
    {
        if (isMask)
        {
            self.Device.DrawImage(image, true, smooth, ref self.State);
            return;
        }

        ContentPaths.PathMove(self, 0, 0);
        ContentPaths.PathLine(self, 1, 0);
        ContentPaths.PathLine(self, 1, 1);
        ContentPaths.PathLine(self, 0, 1);
        self.Path.Close();
        var square = self.Path.Detach();
        self.PathPoints = 0;
        self.Device.Fill(square, false, ref self.State);
    }

    /// <summary>Draws a form XObject: its matrix, bounding box, resources and, when needed, its transparency group.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "form">The form stream.</param>
    internal static void DrawForm(ContentInterpreter self, PdfStream form)
    {
        if (self.Depth >= PdfLimits.MaxDrawDepth)
        {
            return;
        }

        var dictionary = form.Dictionary;
        var tm = self.Tm;
        var tlm = self.Tlm;
        var patternBase = self.PatternBase;
        self.Path.Reset();
        self.PathPoints = 0;
        self.PendingClip = false;
        ContentGraphicsState.SaveState(self);
        self.State.Ctm = ContentOperands.ReadMatrix(dictionary, KnownName.Matrix) * self.State.Ctm;
        var bounds = ContentXObjects.ClipToBox(self, dictionary);
        var group = ContentXObjects.BeginFormGroup(self, dictionary, bounds);
        self.PatternBase = self.State.Ctm;
        self.Resources.Add(dictionary.GetDictionary(KnownName.Resources) ?? ContentExecution.CurrentResources(self));
        self.Depth++;
        try
        {
            ContentExecution.RunStreamBody(self, form);
        }
        finally
        {
            self.Depth--;
            self.Resources.RemoveAt(self.Resources.Count - 1);
            if (group is { } info)
            {
                self.Device.EndGroup(info);
            }

            ContentGraphicsState.RestoreState(self);
            self.Tm = tm;
            self.Tlm = tlm;
            self.PatternBase = patternBase;
        }
    }

    /// <summary>Clips to a form's /BBox.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The form dictionary.</param>
    /// <returns>The box in page space, or an empty rectangle when the form has none.</returns>
    internal static PdfRect ClipToBox(ContentInterpreter self, PdfDictionary dictionary)
    {
        if (!dictionary.TryGetRectangle(KnownName.BBox, out var box))
        {
            return PdfRect.Empty;
        }

        ContentPaths.PathMove(self, box.Left, box.Bottom);
        ContentPaths.PathLine(self, box.Right, box.Bottom);
        ContentPaths.PathLine(self, box.Right, box.Top);
        ContentPaths.PathLine(self, box.Left, box.Top);
        self.Path.Close();
        var clip = self.Path.Detach();
        self.Device.Clip(clip, false, self.State.Ctm);

        self.PathPoints = 0;
        return self.State.Ctm.MapRect(new(box.Left, box.Bottom, box.Right, box.Top));
    }

    /// <summary>Determines whether the state composites objects with constant alpha, a blend mode or a soft mask.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <returns><see langword="true"/> when something other than opaque Normal painting applies.</returns>
    internal static bool HasTransparency(ContentInterpreter self) => self.State.FillAlpha < 1 || self.State.BlendMode != PdfBlendMode.Normal || self.State.SoftMask is not null;

    /// <summary>Starts a transparency group when the form is one and is drawn with transparency.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "dictionary">The form dictionary.</param>
    /// <param name = "bounds">The form's box in page space.</param>
    /// <returns>The group, or null when the form is drawn directly.</returns>
    internal static GroupInfo? BeginFormGroup(ContentInterpreter self, PdfDictionary dictionary, PdfRect bounds)
    {
        var group = dictionary.GetDictionary(KnownName.Group);
        if (group is null || !group.IsName(KnownName.S, KnownName.Transparency))
        {
            return null;
        }

        // PDFium composites a group separately when it is isolated or drawn with transparency; a knockout group needs its
        // own layer too, so its objects replace each other rather than the page beneath.
        var isolated = group.GetBoolean(KnownName.I);
        var knockout = group.GetBoolean(KnownName.K);
        if (!isolated && !knockout && !ContentXObjects.HasTransparency(self))
        {
            return null;
        }

        var info = new GroupInfo(isolated, knockout, self.State.FillAlpha, self.State.BlendMode, self.State.SoftMask, bounds);
        self.Device.BeginGroup(info);
        self.State.FillAlpha = 1;
        self.State.StrokeAlpha = 1;
        self.State.BlendMode = PdfBlendMode.Normal;
        self.State.SoftMask = null;
        return info;
    }
}
