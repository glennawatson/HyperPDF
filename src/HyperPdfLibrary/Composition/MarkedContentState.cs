// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Tracks which marked content is open on a page, so commands for the same structure node share one marked content sequence.</summary>
[DebuggerDisplay("MarkedContentState: open {_open}")]
internal sealed class MarkedContentState
{
    /// <summary>Whether marked content is open.</summary>
    private bool _open;

    /// <summary>The node whose content is open, or <see langword="null"/> when an artifact is open.</summary>
    private StructNode? _node;

    /// <summary>The kind of artifact open when there is no node.</summary>
    private PdfArtifactKind _artifact;

    /// <summary>Opens the marked content a command needs, closing the open sequence first when it is for something else.</summary>
    /// <param name="canvas">The page.</param>
    /// <param name="state">The document being written.</param>
    /// <param name="command">The command about to be written.</param>
    internal void Enter(ref PdfPageCanvas canvas, RenderState state, DrawCommand command)
    {
        if (state.Creator.Tags is null || !command.Paints)
        {
            return;
        }

        if (_open && ReferenceEquals(_node, command.Node) && (command.Node is not null || _artifact == command.Artifact))
        {
            return;
        }

        Close(ref canvas);
        if (command.Node is { } node)
        {
            canvas.BeginContent(state.GetElement(node));
        }
        else
        {
            canvas.BeginArtifact(command.Artifact);
        }

        _open = true;
        _node = command.Node;
        _artifact = command.Artifact;
    }

    /// <summary>Closes the open marked content, if any.</summary>
    /// <param name="canvas">The page.</param>
    internal void Close(ref PdfPageCanvas canvas)
    {
        if (!_open)
        {
            return;
        }

        canvas.EndContent();
        _open = false;
        _node = null;
    }
}
