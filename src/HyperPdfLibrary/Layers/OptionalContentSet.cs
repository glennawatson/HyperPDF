// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>The layers read from a document under one configuration, and their state.</summary>
/// <param name="Layers">The layers in document order.</param>
/// <param name="Hidden">The object numbers hidden by default.</param>
/// <param name="NonView">The object numbers of groups that are not for viewing, and so are always shown.</param>
/// <param name="RadioGroups">The sets of groups of which only one is shown.</param>
/// <param name="PrintHidden">The object numbers hidden when printing.</param>
/// <param name="ExportHidden">The object numbers hidden when the document is saved to a format without optional content.</param>
/// <param name="Locked">The object numbers of the locked groups.</param>
/// <param name="ZoomRanges">The zoom classes in which each zoom-managed group is shown, by object number.</param>
/// <param name="ZoomLimits">The distinct magnifications at which a zoom-managed group changes state, ascending.</param>
/// <param name="Tree">The layer order as a tree.</param>
/// <param name="ListMode">Which layers of the order a panel lists.</param>
/// <param name="Configurations">The default configuration and the alternates.</param>
/// <param name="ActiveConfiguration">The <see cref="PdfLayerConfiguration.Index"/> of the configuration the set was read with.</param>
[DebuggerDisplay("OptionalContentSet: {Layers.Length} layers")]
internal sealed record OptionalContentSet(
    PdfLayer[] Layers,
    HashSet<int> Hidden,
    HashSet<int> NonView,
    int[][] RadioGroups,
    HashSet<int> PrintHidden,
    HashSet<int> ExportHidden,
    HashSet<int> Locked,
    Dictionary<int, ZoomClassRange> ZoomRanges,
    double[] ZoomLimits,
    PdfLayerNode[] Tree,
    PdfLayerListMode ListMode,
    PdfLayerConfiguration[] Configurations,
    int ActiveConfiguration)
{
    /// <summary>Gets the set of a document without layers.</summary>
    internal static OptionalContentSet Empty { get; } = new([], [], [], [], [], [], [], [], [], [], PdfLayerListMode.AllPages, [], PdfLayerConfiguration.DefaultIndex);
}
