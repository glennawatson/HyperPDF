// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// The working objects of shaping one run: the glyph buffer and the engines that run the substitution and positioning
/// lookups over it. A session is rented for a run and returned after it, so shaping many runs makes these objects once.
/// </summary>
[DebuggerDisplay("ShapingSession: {Buffer.Count} glyphs")]
internal sealed class ShapingSession
{
    /// <summary>The glyphs a returned session may hold on to; a larger buffer is left to the collector.</summary>
    private const int MaxRetainedGlyphs = 4096;

    /// <summary>The engine for the substitution table, made on first use.</summary>
    private LayoutEngine? _substitution;

    /// <summary>The engine for the positioning table, made on first use.</summary>
    private LayoutEngine? _positioning;

    /// <summary>Initializes a new instance of the <see cref="ShapingSession"/> class.</summary>
    internal ShapingSession() => Buffer = new(1);

    /// <summary>Gets the glyphs of the run.</summary>
    internal GlyphBuffer Buffer { get; }

    /// <summary>Takes an owner's spare session, or makes one.</summary>
    /// <param name="spare">The owner's spare slot.</param>
    /// <returns>A session no other run holds.</returns>
    internal static ShapingSession Rent(ref ShapingSession? spare) => Interlocked.Exchange(ref spare, null) ?? new();

    /// <summary>Hands the session back to its owner for the next run.</summary>
    /// <param name="spare">The owner's spare slot.</param>
    internal void Return(ref ShapingSession? spare)
    {
        if (Buffer.Capacity <= MaxRetainedGlyphs && Interlocked.CompareExchange(ref spare, this, null) is null)
        {
            return;
        }

        Buffer.Dispose();
    }

    /// <summary>Gets the engine for a substitution table, set up for a run.</summary>
    /// <param name="layout">The GSUB table.</param>
    /// <param name="gdef">The font's glyph definitions.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <returns>The engine.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LayoutEngine Substitute(OtLayoutTable layout, OtGdef gdef, bool rightToLeft) => Prepare(ref _substitution, layout, gdef, false, rightToLeft);

    /// <summary>Gets the engine for a positioning table, set up for a run.</summary>
    /// <param name="layout">The GPOS table.</param>
    /// <param name="gdef">The font's glyph definitions.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <returns>The engine.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LayoutEngine Position(OtLayoutTable layout, OtGdef gdef, bool rightToLeft) => Prepare(ref _positioning, layout, gdef, true, rightToLeft);

    /// <summary>Makes an engine on first use and points it at a table.</summary>
    /// <param name="engine">The session's engine slot.</param>
    /// <param name="layout">The table.</param>
    /// <param name="gdef">The font's glyph definitions.</param>
    /// <param name="positioning">Whether the table is GPOS.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <returns>The engine.</returns>
    private LayoutEngine Prepare(ref LayoutEngine? engine, OtLayoutTable layout, OtGdef gdef, bool positioning, bool rightToLeft)
    {
        if (engine is null)
        {
            engine = new(layout, gdef, Buffer, positioning, rightToLeft);
        }
        else
        {
            engine.Begin(layout, gdef, positioning, rightToLeft);
        }

        return engine;
    }
}
