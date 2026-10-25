// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Lays blocks out in a fixed width that never runs out of room, for headers, footers, table cells and the like.</summary>
internal static class StackLayout
{
    /// <summary>A height so large that a stack never fills it.</summary>
    private const float Unlimited = 1_000_000F;

    /// <summary>Lays blocks out.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="blocks">The blocks.</param>
    /// <param name="width">The width of the stack.</param>
    /// <param name="artifact">The kind of artifact content without a structure node is drawn as.</param>
    /// <param name="scope">Where the blocks are: the structure node their nodes go under and the style paragraphs without one take.</param>
    /// <param name="cancellationToken">Cancels the layout between blocks.</param>
    /// <returns>The height and the commands.</returns>
    internal static StackResult Build(LayoutEnv env, IReadOnlyList<PdfBlock> blocks, float width, PdfArtifactKind artifact, FlattenScope scope, CancellationToken cancellationToken)
    {
        var page = new PagePlan(0, new(0, new PdfSection([])), 0, 0);
        var geometry = new PageGeometry(width, Unlimited, 0F, width, 0F, -Unlimited);
        var cursor = new FlowCursor(env, page, geometry, null) { Artifact = artifact };
        var items = new Flattener(env).Flatten(blocks, scope);
        FlowEngine.Run(items, cursor, cancellationToken);
        return new(-cursor.Y, page.Commands);
    }
}
