// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The embedded block coder of ISO 15444-1 annex D written plainly over two-dimensional state, with the bypass, reset,
/// terminate-all, vertically causal and segmentation symbol mode switches. It shares only the context tables with the
/// decoder.
/// </summary>
internal sealed class JpxTestBlockEncoder
{
    /// <summary>The rows of a stripe.</summary>
    private const int StripeRows = 4;

    /// <summary>The last row of a stripe.</summary>
    private const int LastRow = StripeRows - 1;

    /// <summary>The passes of a bit-plane after the first.</summary>
    private const int PassesPerPlane = 3;

    /// <summary>The passes saved by the first bit-plane having only a cleanup pass.</summary>
    private const int FirstPlaneSaving = 2;

    /// <summary>The bit-planes coded with the arithmetic coder before bypass starts.</summary>
    private const int BypassStartPlanes = 4;

    /// <summary>The passes of the first bypass segment.</summary>
    private const int FirstBypassPasses = 10;

    /// <summary>The passes of a raw bypass segment.</summary>
    private const int RawPasses = 2;

    /// <summary>The most passes of a segment without mode switches.</summary>
    private const int UnlimitedPasses = 109;

    /// <summary>The magnitude refinement context of a first refinement with no significant neighbours.</summary>
    private const int FirstRefinement = 14;

    /// <summary>The magnitude refinement context of a first refinement with significant neighbours.</summary>
    private const int FirstRefinementNear = 15;

    /// <summary>The magnitude refinement context of later refinements.</summary>
    private const int LaterRefinement = 16;

    /// <summary>The segmentation symbol, 1010.</summary>
    private const int SegmentationSymbol = 0b1010;

    /// <summary>The bits of the segmentation symbol.</summary>
    private const int SegmentationBits = 4;

    /// <summary>The entries of the sign table per horizontal contribution.</summary>
    private const int SignRow = 3;

    /// <summary>The bit of a sign entry that inverts the sign.</summary>
    private const int SignFlipShift = 7;

    /// <summary>The mask of the context in a sign entry.</summary>
    private const int SignContextMask = 0x7F;

    /// <summary>The shift of the vertical count in a neighbourhood code.</summary>
    private const int VerticalShift = 2;

    /// <summary>The shift of the diagonal count in a neighbourhood code.</summary>
    private const int DiagonalShift = 4;

    /// <summary>The neighbourhood codes per orientation.</summary>
    private const int Neighbourhoods = 128;

    /// <summary>The pass type of a significance pass.</summary>
    private const int SignificancePass = 0;

    /// <summary>The pass type of a refinement pass.</summary>
    private const int RefinementPass = 1;

    /// <summary>The pass type of a cleanup pass.</summary>
    private const int CleanupPass = 2;

    /// <summary>The arithmetic and raw coder.</summary>
    private readonly JpxTestMqEncoder _coder = new();

    /// <summary>The coefficient magnitudes.</summary>
    private int[] _magnitudes = [];

    /// <summary>The coefficient signs.</summary>
    private bool[] _negative = [];

    /// <summary>The significance state.</summary>
    private bool[] _significant = [];

    /// <summary>Whether a coefficient was coded in the current significance pass.</summary>
    private bool[] _visited = [];

    /// <summary>Whether a coefficient has been refined.</summary>
    private bool[] _refined = [];

    /// <summary>The block width.</summary>
    private int _width;

    /// <summary>The block height.</summary>
    private int _height;

    /// <summary>The sub-band orientation.</summary>
    private int _orientation;

    /// <summary>The mode switches.</summary>
    private JpxBlockStyle _style;

    /// <summary>Codes a code-block.</summary>
    /// <param name="coefficients">The coefficients, row by row.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="orientation">The sub-band orientation.</param>
    /// <param name="style">The mode switches.</param>
    /// <returns>The coded block.</returns>
    internal JpxTestBlockCode Encode(ReadOnlySpan<int> coefficients, int width, int height, int orientation, JpxBlockStyle style)
    {
        Load(coefficients, width, height, orientation, style);
        var largest = 0;
        foreach (var magnitude in _magnitudes)
        {
            largest = Math.Max(largest, magnitude);
        }

        var bits = largest == 0 ? 0 : BitOperations.Log2((uint)largest) + 1;
        return new(bits, bits == 0 ? [] : EncodePasses(bits));
    }

    /// <summary>Loads the coefficients and clears the state.</summary>
    /// <param name="coefficients">The coefficients.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="orientation">The sub-band orientation.</param>
    /// <param name="style">The mode switches.</param>
    private void Load(ReadOnlySpan<int> coefficients, int width, int height, int orientation, JpxBlockStyle style)
    {
        _width = width;
        _height = height;
        _orientation = orientation;
        _style = style;
        var count = width * height;
        _magnitudes = new int[count];
        _negative = new bool[count];
        _significant = new bool[count];
        _visited = new bool[count];
        _refined = new bool[count];
        for (var i = 0; i < count; i++)
        {
            _magnitudes[i] = Math.Abs(coefficients[i]);
            _negative[i] = coefficients[i] < 0;
        }

        _coder.ResetContexts();
    }

    /// <summary>Codes every pass, terminating segments as the mode switches require.</summary>
    /// <param name="bits">The magnitude bit-planes.</param>
    /// <returns>The segments.</returns>
    private List<JpxTestSegment> EncodePasses(int bits)
    {
        var passes = new List<JpxTestPass>((PassesPerPlane * bits) - FirstPlaneSaving);
        for (var plane = bits - 1; plane >= 0; plane--)
        {
            for (var type = plane == bits - 1 ? CleanupPass : SignificancePass; type <= CleanupPass; type++)
            {
                passes.Add(new(plane, type));
            }
        }

        var segments = new List<JpxTestSegment>();
        var limit = 0;
        for (var start = 0; start < passes.Count; start += limit)
        {
            var (plane, type) = passes[start];
            var raw = (_style & JpxBlockStyle.Bypass) != 0 && type != CleanupPass && plane < bits - BypassStartPlanes;
            limit = Math.Min(NextLimit(limit, segments.Count == 0), passes.Count - start);
            StartSegment(raw);
            foreach (var pass in passes.GetRange(start, limit))
            {
                RunPass(pass.Type, pass.Plane, raw);
            }

            segments.Add(new(limit, raw ? _coder.FinishRaw() : _coder.Finish()));
        }

        return segments;
    }

    /// <summary>Gets the most passes of the next segment, by the rule the packet headers follow.</summary>
    /// <param name="previous">The previous segment's limit.</param>
    /// <param name="first">Whether it is the first segment.</param>
    /// <returns>The limit.</returns>
    private int NextLimit(int previous, bool first)
    {
        if ((_style & JpxBlockStyle.TerminateAll) != 0)
        {
            return 1;
        }

        if ((_style & JpxBlockStyle.Bypass) == 0)
        {
            return UnlimitedPasses;
        }

        if (first)
        {
            return FirstBypassPasses;
        }

        return previous is 1 or FirstBypassPasses ? RawPasses : 1;
    }

    /// <summary>Starts a segment.</summary>
    /// <param name="raw">Whether it is raw bypass data.</param>
    private void StartSegment(bool raw)
    {
        if (raw)
        {
            _coder.StartRaw();
        }
        else
        {
            _coder.Start();
        }
    }

    /// <summary>Runs one pass and the context reset the mode may ask for.</summary>
    /// <param name="type">The pass type.</param>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass is raw.</param>
    private void RunPass(int type, int plane, bool raw)
    {
        switch (type)
        {
            case SignificancePass:
            {
                Significance(plane, raw);
                break;
            }

            case RefinementPass:
            {
                Refinement(plane, raw);
                break;
            }

            default:
            {
                Cleanup(plane);
                break;
            }
        }

        if (!raw && (_style & JpxBlockStyle.Reset) != 0)
        {
            _coder.ResetContexts();
        }
    }

    /// <summary>Runs a significance propagation pass.</summary>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass is raw.</param>
    private void Significance(int plane, bool raw)
    {
        for (var top = 0; top < _height; top += StripeRows)
        {
            for (var x = 0; x < _width; x++)
            {
                for (var y = top; y < Math.Min(top + StripeRows, _height); y++)
                {
                    SignificanceStep(x, y, plane, raw);
                }
            }
        }
    }

    /// <summary>Codes one coefficient in a significance pass.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass is raw.</param>
    private void SignificanceStep(int x, int y, int plane, bool raw)
    {
        var i = (y * _width) + x;
        var code = Neighbourhood(x, y);
        if (_significant[i] || code == 0)
        {
            return;
        }

        var bit = (_magnitudes[i] >> plane) & 1;
        Code(bit, ZeroContext(code), raw);
        if (bit != 0)
        {
            EncodeSign(x, y, raw);
            _significant[i] = true;
        }

        _visited[i] = true;
    }

    /// <summary>Runs a magnitude refinement pass.</summary>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass is raw.</param>
    private void Refinement(int plane, bool raw)
    {
        for (var top = 0; top < _height; top += StripeRows)
        {
            for (var x = 0; x < _width; x++)
            {
                for (var y = top; y < Math.Min(top + StripeRows, _height); y++)
                {
                    RefinementStep(x, y, plane, raw);
                }
            }
        }
    }

    /// <summary>Codes one coefficient in a refinement pass.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass is raw.</param>
    private void RefinementStep(int x, int y, int plane, bool raw)
    {
        var i = (y * _width) + x;
        if (!_significant[i] || _visited[i])
        {
            return;
        }

        var first = Neighbourhood(x, y) != 0 ? FirstRefinementNear : FirstRefinement;
        var context = _refined[i] ? LaterRefinement : first;
        Code((_magnitudes[i] >> plane) & 1, context, raw);
        _refined[i] = true;
    }

    /// <summary>Runs a cleanup pass and any segmentation symbol.</summary>
    /// <param name="plane">The bit-plane.</param>
    private void Cleanup(int plane)
    {
        for (var top = 0; top < _height; top += StripeRows)
        {
            for (var x = 0; x < _width; x++)
            {
                CleanupColumn(x, top, plane);
            }
        }

        if ((_style & JpxBlockStyle.SegmentationSymbols) == 0)
        {
            return;
        }

        for (var i = SegmentationBits - 1; i >= 0; i--)
        {
            _coder.Encode((SegmentationSymbol >> i) & 1, JpxTestMqEncoder.Uniform);
        }
    }

    /// <summary>Codes one stripe column of a cleanup pass.</summary>
    /// <param name="x">The column.</param>
    /// <param name="top">The stripe's first row.</param>
    /// <param name="plane">The bit-plane.</param>
    private void CleanupColumn(int x, int top, int plane)
    {
        var rows = Math.Min(StripeRows, _height - top);
        var start = 0;
        if (rows == StripeRows && QuietColumn(x, top))
        {
            var run = 0;
            while (run < StripeRows && ((_magnitudes[((top + run) * _width) + x] >> plane) & 1) == 0)
            {
                run++;
            }

            _coder.Encode(run < StripeRows ? 1 : 0, JpxTestMqEncoder.RunLength);
            if (run == StripeRows)
            {
                return;
            }

            _coder.Encode(run >> 1, JpxTestMqEncoder.Uniform);
            _coder.Encode(run & 1, JpxTestMqEncoder.Uniform);
            EncodeSign(x, top + run, false);
            _significant[((top + run) * _width) + x] = true;
            start = run + 1;
        }

        for (var row = start; row < rows; row++)
        {
            CleanupStep(x, top + row, plane);
        }

        for (var row = 0; row < rows; row++)
        {
            _visited[((top + row) * _width) + x] = false;
        }
    }

    /// <summary>Codes one coefficient in a cleanup pass.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="plane">The bit-plane.</param>
    private void CleanupStep(int x, int y, int plane)
    {
        var i = (y * _width) + x;
        if (_significant[i] || _visited[i])
        {
            return;
        }

        var bit = (_magnitudes[i] >> plane) & 1;
        _coder.Encode(bit, ZeroContext(Neighbourhood(x, y)));
        if (bit == 0)
        {
            return;
        }

        EncodeSign(x, y, false);
        _significant[i] = true;
    }

    /// <summary>Determines whether a whole stripe column is insignificant, unvisited and has no significant neighbours.</summary>
    /// <param name="x">The column.</param>
    /// <param name="top">The stripe's first row.</param>
    /// <returns><see langword="true"/> when run-length coding applies.</returns>
    private bool QuietColumn(int x, int top)
    {
        for (var row = 0; row < StripeRows; row++)
        {
            var i = ((top + row) * _width) + x;
            if (_significant[i] || _visited[i] || Neighbourhood(x, top + row) != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Codes a decision with the arithmetic coder, or as a raw bit.</summary>
    /// <param name="bit">The decision.</param>
    /// <param name="context">The context.</param>
    /// <param name="raw">Whether the pass is raw.</param>
    private void Code(int bit, int context, bool raw)
    {
        if (raw)
        {
            _coder.EncodeRaw(bit);
        }
        else
        {
            _coder.Encode(bit, context);
        }
    }

    /// <summary>Codes the sign of a coefficient that has just become significant.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="raw">Whether the pass is raw.</param>
    private void EncodeSign(int x, int y, bool raw)
    {
        var negative = _negative[(y * _width) + x] ? 1 : 0;
        if (raw)
        {
            _coder.EncodeRaw(negative);
            return;
        }

        var horizontal = Math.Clamp(Contribution(x - 1, y) + Contribution(x + 1, y), -1, 1);
        var below = Hidden(y) ? 0 : Contribution(x, y + 1);
        var vertical = Math.Clamp(Contribution(x, y - 1) + below, -1, 1);
        var entry = JpxContexts.Sign[((horizontal + 1) * SignRow) + vertical + 1];
        _coder.Encode(negative ^ (entry >> SignFlipShift), entry & SignContextMask);
    }

    /// <summary>Gets a neighbour's sign contribution.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>1 when positive and significant, -1 when negative and significant, else 0.</returns>
    private int Contribution(int x, int y)
    {
        if (!Significant(x, y))
        {
            return 0;
        }

        return _negative[(y * _width) + x] ? -1 : 1;
    }

    /// <summary>Gets a coefficient's neighbourhood code: horizontal, vertical and diagonal significant neighbours.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The code.</returns>
    private int Neighbourhood(int x, int y)
    {
        var hidden = Hidden(y);
        var horizontal = Count(x - 1, y) + Count(x + 1, y);
        var vertical = Count(x, y - 1) + (hidden ? 0 : Count(x, y + 1));
        var diagonal = Count(x - 1, y - 1) + Count(x + 1, y - 1) + (hidden ? 0 : Count(x - 1, y + 1) + Count(x + 1, y + 1));
        return horizontal | (vertical << VerticalShift) | (diagonal << DiagonalShift);
    }

    /// <summary>Determines whether the row below a coefficient is hidden by the vertically causal mode.</summary>
    /// <param name="y">The coefficient's row.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool Hidden(int y) => (_style & JpxBlockStyle.VerticallyCausal) != 0 && (y & LastRow) == LastRow;

    /// <summary>Gets the zero-coding context of a neighbourhood code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The context.</returns>
    private int ZeroContext(int code) => JpxContexts.ZeroCoding[(_orientation * Neighbourhoods) + code];

    /// <summary>Counts a neighbour when it is inside the block and significant.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>1 or 0.</returns>
    private int Count(int x, int y) => Significant(x, y) ? 1 : 0;

    /// <summary>Determines whether a position is inside the block and significant.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool Significant(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height && _significant[(y * _width) + x];
}
