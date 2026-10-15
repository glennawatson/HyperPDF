// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The parameters of the text region decoding procedure (T.88 table 9).</summary>
[DebuggerDisplay("Jbig2TextRegionSettings: {Width}x{Height}, {InstanceCount} instances")]
internal sealed class Jbig2TextRegionSettings
{
    /// <summary>Gets or sets a value indicating whether the region is Huffman coded, SBHUFF.</summary>
    internal bool Huffman { get; set; }

    /// <summary>Gets or sets a value indicating whether instances may be refined, SBREFINE.</summary>
    internal bool Refine { get; set; }

    /// <summary>Gets or sets the region width, SBW.</summary>
    internal int Width { get; set; }

    /// <summary>Gets or sets the region height, SBH.</summary>
    internal int Height { get; set; }

    /// <summary>Gets or sets the number of instances, SBNUMINSTANCES.</summary>
    internal long InstanceCount { get; set; }

    /// <summary>Gets or sets the strip size, SBSTRIPS: 1, 2, 4 or 8.</summary>
    internal int Strips { get; set; } = 1;

    /// <summary>Gets or sets the bits of a Huffman-coded strip T offset.</summary>
    internal int StripBits { get; set; }

    /// <summary>Gets or sets the corner that instance coordinates locate, REFCORNER.</summary>
    internal Jbig2Corner Corner { get; set; } = Jbig2Corner.TopLeft;

    /// <summary>Gets or sets a value indicating whether S runs down and T across, TRANSPOSED.</summary>
    internal bool Transposed { get; set; }

    /// <summary>Gets or sets how instances combine with the region, SBCOMBOP.</summary>
    internal Jbig2ComposeOperator Operator { get; set; }

    /// <summary>Gets or sets a value indicating whether the region starts black, SBDEFPIXEL.</summary>
    internal bool DefaultPixel { get; set; }

    /// <summary>Gets or sets the S offset added between instances, SBDSOFFSET.</summary>
    internal int SOffset { get; set; }

    /// <summary>Gets or sets the refinement template, SBRTEMPLATE.</summary>
    internal int RefinementTemplate { get; set; }

    /// <summary>Gets or sets the refinement adaptive template pixels, SBRAT.</summary>
    internal Jbig2AtPixels RefinementAt { get; set; }

    /// <summary>Gets or sets the Huffman table of symbol IDs, or <see langword="null"/> to read <see cref="SymbolCodeLength"/> bits.</summary>
    internal Jbig2HuffmanTable? SymbolCodes { get; set; }

    /// <summary>Gets or sets the bits of a symbol ID: the fixed Huffman length, or the arithmetic SBSYMCODELEN.</summary>
    internal int SymbolCodeLength { get; set; }

    /// <summary>Gets or sets the first S table, SBHUFFFS.</summary>
    internal Jbig2HuffmanTable FirstS { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B6);

    /// <summary>Gets or sets the S delta table, SBHUFFDS.</summary>
    internal Jbig2HuffmanTable DeltaS { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B8);

    /// <summary>Gets or sets the strip T delta table, SBHUFFDT.</summary>
    internal Jbig2HuffmanTable DeltaT { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B11);

    /// <summary>Gets or sets the refinement width table, SBHUFFRDW.</summary>
    internal Jbig2HuffmanTable RefineWidth { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);

    /// <summary>Gets or sets the refinement height table, SBHUFFRDH.</summary>
    internal Jbig2HuffmanTable RefineHeight { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);

    /// <summary>Gets or sets the refinement X table, SBHUFFRDX.</summary>
    internal Jbig2HuffmanTable RefineX { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);

    /// <summary>Gets or sets the refinement Y table, SBHUFFRDY.</summary>
    internal Jbig2HuffmanTable RefineY { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);

    /// <summary>Gets or sets the refinement size table, SBHUFFRSIZE.</summary>
    internal Jbig2HuffmanTable RefineSize { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);

    /// <summary>Sets every parameter to the value a new text region starts with.</summary>
    internal void Reset()
    {
        Huffman = false;
        Refine = false;
        Width = 0;
        Height = 0;
        InstanceCount = 0;
        Strips = 1;
        StripBits = 0;
        Corner = Jbig2Corner.TopLeft;
        Transposed = false;
        Operator = default;
        DefaultPixel = false;
        SOffset = 0;
        RefinementTemplate = 0;
        RefinementAt = default;
        SymbolCodes = null;
        SymbolCodeLength = 0;
        FirstS = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B6);
        DeltaS = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B8);
        DeltaT = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B11);
        RefineWidth = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);
        RefineHeight = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);
        RefineX = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);
        RefineY = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B14);
        RefineSize = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);
    }

    /// <summary>
    /// Sets the parameters of the refinement/aggregate symbols of a symbol dictionary (T.88 table 17): a refined,
    /// top-left, OR-combined region with one strip and, when Huffman coded, the standard tables. The caller sets each
    /// symbol's width, height and instance count.
    /// </summary>
    /// <param name="huffman">Whether the dictionary is Huffman coded.</param>
    /// <param name="codeLength">The bits of a symbol ID.</param>
    /// <param name="template">The refinement template.</param>
    /// <param name="at">The refinement adaptive pixels.</param>
    internal void ConfigureAggregate(bool huffman, int codeLength, int template, Jbig2AtPixels at)
    {
        Reset();
        Huffman = huffman;
        Refine = true;
        SymbolCodeLength = codeLength;
        RefinementTemplate = template;
        RefinementAt = at;
        RefineWidth = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B15);
        RefineHeight = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B15);
        RefineX = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B15);
        RefineY = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B15);
    }
}
