// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Composition;

/// <summary>Writes a laid out page to the document: its running content and commands, inside marked content when the document is tagged.</summary>
internal static class PageWriter
{
    /// <summary>Writes a page.</summary>
    /// <param name="state">The document being written.</param>
    /// <param name="plan">The page.</param>
    /// <param name="running">The commands for the page's header and footer.</param>
    /// <returns>The page's object.</returns>
    internal static PdfObjectId Write(RenderState state, PagePlan plan, List<DrawCommand> running)
    {
        var setup = plan.Section.Setup;
        var canvas = state.Creator.BeginPage(setup.PageWidth, setup.PageHeight);
        try
        {
            state.PendingLinks.Clear();
            state.PageNumber = plan.Number;
            var marked = new MarkedContentState();
            WriteCommands(ref canvas, state, marked, running);
            WriteCommands(ref canvas, state, marked, plan.Commands);
            marked.Close(ref canvas);
            LinkAnnotations.AddAll(ref canvas, state);
            var id = canvas.Finish();
            state.PageIds.Add(id);
            return id;
        }
        finally
        {
            canvas.Dispose();
        }
    }

    /// <summary>Writes commands, in order.</summary>
    /// <param name="canvas">The page.</param>
    /// <param name="state">The document being written.</param>
    /// <param name="marked">The marked content state of the page.</param>
    /// <param name="commands">The commands.</param>
    private static void WriteCommands(ref PdfPageCanvas canvas, RenderState state, MarkedContentState marked, List<DrawCommand> commands)
    {
        foreach (var command in commands)
        {
            marked.Enter(ref canvas, state, command);
            command.Write(ref canvas, state);
        }
    }
}
