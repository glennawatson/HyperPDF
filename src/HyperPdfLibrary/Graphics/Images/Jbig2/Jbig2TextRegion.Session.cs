// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <content>The decoding session and its arithmetic-coded path.</content>
internal static partial class Jbig2TextRegion
{
    /// <summary>The inputs and coordinates of one text region decode.</summary>
    private ref partial struct Session
    {
        /// <summary>The region parameters.</summary>
        private readonly Jbig2TextRegionSettings _settings;

        /// <summary>The symbols.</summary>
        private readonly Jbig2SymbolSet _symbols;

        /// <summary>The refinement contexts.</summary>
        private readonly Span<byte> _refinement;

        /// <summary>The region bitmap.</summary>
        private readonly Jbig2Bitmap _region;

        /// <summary>The work workspace.</summary>
        private readonly Jbig2Workspace _workspace;

        /// <summary>The T coordinate of the current strip, STRIPT.</summary>
        private long _stripT;

        /// <summary>The S coordinate of the first instance of the current strip, FIRSTS.</summary>
        private long _firstS;

        /// <summary>The S coordinate of the current instance, CURS.</summary>
        private long _currentS;

        /// <summary>The number of instances decoded, NINSTANCES.</summary>
        private long _instances;

        /// <summary>Initializes a new instance of the <see cref="Session"/> struct.</summary>
        /// <param name="settings">The region parameters.</param>
        /// <param name="symbols">The symbols.</param>
        /// <param name="refinement">The refinement contexts.</param>
        /// <param name="region">The region bitmap.</param>
        /// <param name="workspace">The work budget and scratch bitmaps.</param>
        internal Session(Jbig2TextRegionSettings settings, Jbig2SymbolSet symbols, Span<byte> refinement, Jbig2Bitmap region, Jbig2Workspace workspace)
        {
            _settings = settings;
            _symbols = symbols;
            _refinement = refinement;
            _region = region;
            _workspace = workspace;
        }

        /// <summary>Decodes the region's arithmetic-coded strips.</summary>
        /// <param name="decoder">The arithmetic decoder.</param>
        /// <param name="integers">The integer and symbol ID contexts.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        internal bool DecodeArithmetic(ref Jbig2ArithmeticDecoder decoder, Jbig2IntegerContexts integers)
        {
            _region.Fill(_settings.DefaultPixel);
            if (!DecodeInteger(ref decoder, integers, Jbig2IntegerKind.StripDelta, out var initial))
            {
                return false;
            }

            _stripT = -(long)initial * _settings.Strips;
            while (_instances < _settings.InstanceCount)
            {
                if (!DecodeInteger(ref decoder, integers, Jbig2IntegerKind.StripDelta, out var delta))
                {
                    return false;
                }

                _stripT += (long)delta * _settings.Strips;
                if (!DecodeArithmeticStrip(ref decoder, integers))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Decodes one arithmetic-coded integer.</summary>
        /// <param name="decoder">The arithmetic decoder.</param>
        /// <param name="integers">The integer contexts.</param>
        /// <param name="kind">The integer decoder.</param>
        /// <param name="value">The value.</param>
        /// <returns><see langword="false"/> for out-of-band.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool DecodeInteger(ref Jbig2ArithmeticDecoder decoder, Jbig2IntegerContexts integers, Jbig2IntegerKind kind, out int value) =>
            Jbig2IntegerDecoder.Decode(ref decoder, integers.Get(kind), out value);

        /// <summary>Decodes the instances of one strip.</summary>
        /// <param name="decoder">The arithmetic decoder.</param>
        /// <param name="integers">The integer contexts.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool DecodeArithmeticStrip(ref Jbig2ArithmeticDecoder decoder, Jbig2IntegerContexts integers)
        {
            _ = DecodeInteger(ref decoder, integers, Jbig2IntegerKind.FirstS, out var firstDelta);
            _firstS += firstDelta;
            _currentS = _firstS;
            while (_instances < _settings.InstanceCount)
            {
                if (!DecodeArithmeticInstance(ref decoder, integers))
                {
                    return false;
                }

                _instances++;
                if (!DecodeInteger(ref decoder, integers, Jbig2IntegerKind.SDelta, out var sDelta))
                {
                    return true;
                }

                _currentS += (long)sDelta + _settings.SOffset;
            }

            return true;
        }

        /// <summary>Decodes one instance's T offset, symbol ID and refinement flag, and places it.</summary>
        /// <param name="decoder">The arithmetic decoder.</param>
        /// <param name="integers">The integer contexts.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool DecodeArithmeticInstance(ref Jbig2ArithmeticDecoder decoder, Jbig2IntegerContexts integers)
        {
            var currentT = 0;
            if (_settings.Strips != 1)
            {
                _ = DecodeInteger(ref decoder, integers, Jbig2IntegerKind.InstanceT, out currentT);
            }

            var id = Jbig2IntegerDecoder.DecodeId(ref decoder, integers.Id, integers.IdCodeLength);
            var refine = 0;
            if (_settings.Refine)
            {
                _ = DecodeInteger(ref decoder, integers, Jbig2IntegerKind.Refine, out refine);
            }

            return TryFitInt(_stripT + currentT, out var t) && PlaceArithmetic(ref decoder, integers, new(id, t, refine != 0));
        }

        /// <summary>Gets an instance's bitmap, refining it when asked, and places it.</summary>
        /// <param name="decoder">The arithmetic decoder.</param>
        /// <param name="integers">The integer contexts.</param>
        /// <param name="instance">The instance.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool PlaceArithmetic(ref Jbig2ArithmeticDecoder decoder, Jbig2IntegerContexts integers, Instance instance)
        {
            if (!_symbols.TryFind(instance.Id, out var store, out var index))
            {
                return false;
            }

            // A symbol without a bitmap is an empty bitmap (T.88 section 6.4.5): it is counted and placed, and draws nothing.
            if (!instance.Refine)
            {
                return Place(store.Get(index), instance.T);
            }

            if (!store.IsPresent(index))
            {
                return false;
            }

            _ = DecodeInteger(ref decoder, integers, Jbig2IntegerKind.RefineWidth, out var deltaWidth);
            _ = DecodeInteger(ref decoder, integers, Jbig2IntegerKind.RefineHeight, out var deltaHeight);
            _ = DecodeInteger(ref decoder, integers, Jbig2IntegerKind.RefineX, out var offsetX);
            _ = DecodeInteger(ref decoder, integers, Jbig2IntegerKind.RefineY, out var offsetY);
            var reference = store.Get(index);
            if (!new Refinement(deltaWidth, deltaHeight, offsetX, offsetY).TryGetParameters(reference, _settings, out var size, out var parameters))
            {
                return false;
            }

            var refined = _workspace.Refinement(size.Width, size.Height);
            return refined is null
                ? Place(default, instance.T)
                : _workspace.TryCharge((long)size.Width * size.Height)
                && Jbig2RefinementRegion.Decode(ref decoder, _refinement, parameters, reference, refined)
                && Place(refined.View, instance.T);
        }

        /// <summary>Places an instance's bitmap in the region and moves S past it.</summary>
        /// <param name="symbol">The instance's bitmap; empty when a refinement had no valid size.</param>
        /// <param name="t">The instance's T coordinate.</param>
        /// <returns><see langword="false"/> when S overflows or the budget is spent.</returns>
        private bool Place(Jbig2BitmapView symbol, int t)
        {
            var width = Math.Max(symbol.Width, 0);
            var height = Math.Max(symbol.Height, 0);
            _currentS += LeadingAdvance(_settings, width, height);
            if (!TryFitInt(_currentS, out var s))
            {
                return false;
            }

            var origin = GetOrigin(_settings, s, t, width, height);
            _currentS += origin.Advance;
            return _workspace.TryCharge(Jbig2Composer.Compose(_region, symbol, origin.X, origin.Y, _settings.Operator));
        }
    }
}
