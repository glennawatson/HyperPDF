// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>The names the optimiser writes and tests for, all known names with the same id in every document.</summary>
internal static class OptimizerNames
{
    /// <summary>The first heading level.</summary>
    private const int FirstHeading = 1;

    /// <summary>The last heading level.</summary>
    private const int LastHeading = 6;

    /// <summary>Gets <c>/Matte</c>.</summary>
    internal static PdfName Matte => KnownName.Matte;

    /// <summary>Gets <c>/Thumb</c>.</summary>
    internal static PdfName Thumb => KnownName.Thumb;

    /// <summary>Gets <c>/PieceInfo</c>.</summary>
    internal static PdfName PieceInfo => KnownName.PieceInfo;

    /// <summary>Gets <c>/Marked</c>.</summary>
    internal static PdfName Marked => KnownName.Marked;

    /// <summary>Gets <c>/DisplayDocTitle</c>.</summary>
    internal static PdfName DisplayDocTitle => KnownName.DisplayDocTitle;

    /// <summary>Gets <c>/ParentTreeNextKey</c>.</summary>
    internal static PdfName ParentTreeNextKey => KnownName.ParentTreeNextKey;

    /// <summary>Gets <c>/Length1</c>.</summary>
    internal static PdfName Length1 => KnownName.Length1;

    /// <summary>Gets <c>/OutputIntents</c>.</summary>
    internal static PdfName OutputIntents => KnownName.OutputIntents;

    /// <summary>Gets <c>/Figure</c>.</summary>
    internal static PdfName Figure => KnownName.Figure;

    /// <summary>Gets <c>/Sect</c>.</summary>
    internal static PdfName Sect => KnownName.Sect;

    /// <summary>Gets <c>/Document</c>.</summary>
    internal static PdfName Document => KnownName.Document;

    /// <summary>Gets <c>/Span</c>.</summary>
    internal static PdfName Span => KnownName.Span;

    /// <summary>Gets <c>/LI</c>.</summary>
    internal static PdfName ListItem => KnownName.LI;

    /// <summary>Gets <c>/LBody</c>.</summary>
    internal static PdfName ListBody => KnownName.LBody;

    /// <summary>Gets <c>/L</c>.</summary>
    internal static PdfName List => KnownName.L;

    /// <summary>Gets <c>/Lbl</c>.</summary>
    internal static PdfName ListLabel => KnownName.Lbl;

    /// <summary>Gets <c>/Table</c>.</summary>
    internal static PdfName Table => KnownName.Table;

    /// <summary>Gets <c>/TR</c>.</summary>
    internal static PdfName TableRow => KnownName.TR;

    /// <summary>Gets <c>/TH</c>.</summary>
    internal static PdfName TableHeader => KnownName.TH;

    /// <summary>Gets <c>/TD</c>.</summary>
    internal static PdfName TableData => KnownName.TD;

    /// <summary>Gets <c>/Caption</c>.</summary>
    internal static PdfName Caption => KnownName.Caption;

    /// <summary>Gets <c>/Div</c>.</summary>
    internal static PdfName Division => KnownName.Div;

    /// <summary>Gets <c>/O</c>, the owner of an attribute object.</summary>
    internal static PdfName Owner => KnownName.O;

    /// <summary>Gets <c>/Scope</c>.</summary>
    internal static PdfName Scope => KnownName.Scope;

    /// <summary>Gets <c>/RowSpan</c>.</summary>
    internal static PdfName RowSpan => KnownName.RowSpan;

    /// <summary>Gets <c>/ColSpan</c>.</summary>
    internal static PdfName ColSpan => KnownName.ColSpan;

    /// <summary>Gets <c>/Column</c>.</summary>
    internal static PdfName ScopeColumn => KnownName.Column;

    /// <summary>Gets <c>/Row</c>.</summary>
    internal static PdfName ScopeRow => KnownName.Row;

    /// <summary>Gets <c>/Both</c>.</summary>
    internal static PdfName ScopeBoth => KnownName.Both;

    /// <summary>Gets the heading name for a level.</summary>
    /// <param name="level">The level, clamped to 1 through 6.</param>
    /// <returns><c>/H1</c> to <c>/H6</c>.</returns>
    internal static PdfName Heading(int level) => (KnownName)((int)KnownName.H1 + Math.Clamp(level, FirstHeading, LastHeading) - FirstHeading);
}
