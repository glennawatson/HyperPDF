// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// The commands the tool bar holds for tools that are made on first use. The tool bar is laid out for every tab shown, so a
/// button that held the tool's own command would make the tool for a tab that never uses it.
/// </summary>
public sealed partial class DocumentTabViewModel
{
    /// <summary>Turns Focus Mode on or off.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleFocusMode() => Signal.Return(RxVoid.Default).InvokeCommand(FocusMode.ToggleCommand).Dispose();

    /// <summary>Starts or ends Fill and Sign.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleFillAndSign() => Signal.Return(RxVoid.Default).InvokeCommand(FillAndSign.ToggleCommand).Dispose();

    /// <summary>Starts or ends editing the page's own content.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleContentEditing() => Signal.Return(RxVoid.Default).InvokeCommand(ContentEdit.ToggleEditingCommand).Dispose();

    /// <summary>Starts or stops organising the pages. Starting shows the pages in the side panel, where they are chosen.</summary>
    [ReactiveCommand]
    private void ToggleOrganisePages()
    {
        Signal.Return(RxVoid.Default).InvokeCommand(Pages.ToggleOrganisingCommand).Dispose();
        if (!Pages.IsOrganising)
        {
            return;
        }

        SidebarMode = SidebarMode.Thumbnails;
        SidebarVisible = true;
    }

    /// <summary>Prints the document.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Print() => Signal.Return(RxVoid.Default).InvokeCommand(Printing.PrintCommand).Dispose();

    /// <summary>Opens or closes Read Aloud.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleReadAloud() => Signal.Return(RxVoid.Default).InvokeCommand(ReadAloud.ToggleCommand).Dispose();
}
