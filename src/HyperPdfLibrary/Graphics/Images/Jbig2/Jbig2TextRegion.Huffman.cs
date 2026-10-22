// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <content>The Huffman-coded text region decoding procedure.</content>
internal static partial class Jbig2TextRegion
{
    /// <summary>The bytes after a refinement's arithmetic data that its size includes.</summary>
    private const int RefinementTrailer = 2;

    /// <summary>Reads the refinement deltas and the size of the refinement data.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="refinement">The deltas.</param>
    /// <param name="size">The bytes of refinement data.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool ReadRefinement(ref Jbig2Reader reader, Jbig2TextRegionSettings settings, out Refinement refinement, out long size)
    {
        refinement = default;
        size = 0;
        if (settings.RefineWidth.Decode(ref reader, out var deltaWidth) != Jbig2HuffmanResult.Value
            || settings.RefineHeight.Decode(ref reader, out var deltaHeight) != Jbig2HuffmanResult.Value
            || settings.RefineX.Decode(ref reader, out var offsetX) != Jbig2HuffmanResult.Value
            || settings.RefineY.Decode(ref reader, out var offsetY) != Jbig2HuffmanResult.Value
            || settings.RefineSize.Decode(ref reader, out var refinementSize) != Jbig2HuffmanResult.Value)
        {
            return false;
        }

        refinement = new(deltaWidth, deltaHeight, offsetX, offsetY);
        size = (uint)refinementSize;
        return true;
    }

    /// <summary>Reads a symbol ID with the symbol ID table or as fixed-length bits.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="id">The symbol ID.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool TryReadSymbolId(ref Jbig2Reader reader, Jbig2TextRegionSettings settings, out long id)
    {
        if (settings.SymbolCodes is { } table)
        {
            var result = table.Decode(ref reader, out var value);
            id = value;
            return result == Jbig2HuffmanResult.Value;
        }

        var read = reader.TryReadBits(settings.SymbolCodeLength, out var bits);
        id = bits;
        return read;
    }

    /// <summary>Decodes a refined bitmap with an arithmetic decoder started at the reader's position.</summary>
    /// <param name="reader">The reader, moved past the data the decoder read.</param>
    /// <param name="contexts">The refinement contexts.</param>
    /// <param name="parameters">The refinement parameters.</param>
    /// <param name="reference">The symbol being refined.</param>
    /// <param name="refined">The bitmap receiving the refinement.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    private static bool DecodeRefinement(ref Jbig2Reader reader, Span<byte> contexts, in Jbig2RefinementParameters parameters, Jbig2BitmapView reference, Jbig2Bitmap refined)
    {
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        var decoded = Jbig2RefinementRegion.Decode(ref decoder, contexts, parameters, reference, refined);
        reader.Offset = decoder.Position;
        return decoded;
    }

    /// <content>The Huffman-coded path of the session.</content>
    private ref partial struct Session
    {
        /// <summary>Decodes the region's Huffman-coded strips.</summary>
        /// <param name="reader">The reader, at the coded instances.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        internal bool DecodeHuffman(ref Jbig2Reader reader)
        {
            _region.Fill(_settings.DefaultPixel);
            if (_settings.DeltaT.Decode(ref reader, out var initial) != Jbig2HuffmanResult.Value)
            {
                return false;
            }

            _stripT = -(long)initial * _settings.Strips;
            while (_instances < _settings.InstanceCount)
            {
                if (_settings.DeltaT.Decode(ref reader, out var delta) != Jbig2HuffmanResult.Value)
                {
                    return false;
                }

                _stripT += (long)delta * _settings.Strips;
                if (!DecodeHuffmanStrip(ref reader))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Decodes the instances of one strip, until the out-of-band S delta.</summary>
        /// <param name="reader">The reader.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool DecodeHuffmanStrip(ref Jbig2Reader reader)
        {
            if (_settings.FirstS.Decode(ref reader, out var firstDelta) != Jbig2HuffmanResult.Value)
            {
                return false;
            }

            _firstS += firstDelta;
            _currentS = _firstS;
            var valid = true;
            var more = true;
            while (valid && more)
            {
                if (!ReadHuffmanInstance(ref reader, out var instance) || !PlaceHuffman(ref reader, instance))
                {
                    valid = false;
                    continue;
                }

                var result = _settings.DeltaS.Decode(ref reader, out var sDelta);
                if (result != Jbig2HuffmanResult.Value)
                {
                    valid = result == Jbig2HuffmanResult.OutOfBand;
                    more = false;
                    continue;
                }

                _currentS += (long)sDelta + _settings.SOffset;
            }

            return valid;
        }

        /// <summary>Reads an instance's T offset, symbol ID and refinement flag.</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="instance">The instance.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private readonly bool ReadHuffmanInstance(ref Jbig2Reader reader, out Instance instance)
        {
            instance = default;
            uint currentT = 0;
            if (_settings.Strips != 1 && !reader.TryReadBits(_settings.StripBits, out currentT))
            {
                return false;
            }

            if (!TryFitInt(_stripT + currentT, out var t) || !TryReadSymbolId(ref reader, _settings, out var id))
            {
                return false;
            }

            var refine = 0;
            if (_settings.Refine && !reader.TryReadBit(out refine))
            {
                return false;
            }

            instance = new(id, t, refine != 0);
            return true;
        }

        /// <summary>Gets an instance's bitmap, refining it when asked, and places it.</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="instance">The instance.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool PlaceHuffman(ref Jbig2Reader reader, Instance instance)
        {
            if (!_symbols.TryFind(instance.Id, out var store, out var index))
            {
                return false;
            }

            if (instance.Refine)
            {
                return PlaceRefinedHuffman(ref reader, store, index, instance.T);
            }

            // A symbol without a bitmap is an empty bitmap (T.88 section 6.4.5): it is counted and draws nothing.
            _instances++;
            return Place(store.Get(index), instance.T);
        }

        /// <summary>Reads a refinement's deltas and size, decodes the refined bitmap and places it.</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="store">The store holding the symbol.</param>
        /// <param name="index">The symbol in the store.</param>
        /// <param name="t">The instance's T coordinate.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool PlaceRefinedHuffman(ref Jbig2Reader reader, Jbig2SymbolStore store, int index, int t)
        {
            if (!ReadRefinement(ref reader, _settings, out var refinement, out var size) || !store.IsPresent(index))
            {
                return false;
            }

            reader.AlignByte();
            var start = reader.Offset;
            var reference = store.Get(index);
            if (!refinement.TryGetParameters(reference, _settings, out var refinedSize, out var parameters))
            {
                return false;
            }

            var refined = _workspace.Refinement(refinedSize.Width, refinedSize.Height);
            if (refined is not null
                && (!_workspace.TryCharge((long)refined.Width * refined.Height) || !DecodeRefinement(ref reader, _refinement, parameters, reference, refined)))
            {
                return false;
            }

            reader.AlignByte();
            reader.Skip(RefinementTrailer);
            if (reader.Offset - start != size)
            {
                return false;
            }

            _instances++;
            return Place(refined is null ? default : refined.View, t);
        }
    }
}
