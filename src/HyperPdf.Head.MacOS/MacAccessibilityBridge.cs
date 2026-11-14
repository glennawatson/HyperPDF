// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Head.MacOS.Cocoa;
using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.MacOS;

/// <summary>
/// Publishes the viewer to VoiceOver and other macOS assistive technology. The window's content view becomes the document, with
/// the page text as its value, and each block of the page becomes an accessibility element under it, so VoiceOver can read the page
/// and move by heading, list item, table cell and figure. Everything but <see cref="IsAssistiveTechnologyActive"/> runs on the
/// window thread.
/// </summary>
[DebuggerDisplay("MacAccessibilityBridge: VoiceOver {IsAssistiveTechnologyActive}")]
public sealed class MacAccessibilityBridge : IAccessibilityBridge
{
    /// <summary>The tree being served.</summary>
    private IAccessibleTree? _tree;

    /// <summary>The subscription to the tree's changes.</summary>
    private IDisposable? _changes;

    /// <summary>The window's content view, which stands for the document.</summary>
    private ulong _view;

    /// <summary>Whether the tree has been asked to build itself, which it does only for a reader.</summary>
    private bool _reading;

    /// <inheritdoc/>
    public bool IsAssistiveTechnologyActive =>
        NativeMethods.SendFlag(NativeMethods.Send(NativeMethods.GetClass("NSWorkspace"), Selectors.SharedWorkspace), Selectors.IsVoiceOverEnabled) != 0;

    /// <inheritdoc/>
    public void Attach(IAccessibleTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        _changes?.Dispose();
        _tree = tree;
        _reading = false;
        _changes = tree.Changes.Subscribe(new ChangeObserver(this));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _changes?.Dispose();
        _changes = null;
        _tree = null;
        _view = 0;
    }

    /// <summary>Tells the bridge which view stands for the document. Called once, right after the window is created.</summary>
    /// <param name="view">The window's content view.</param>
    internal void AttachView(nint view)
    {
        _view = (ulong)view;
        NativeMethods.SendVoidFlag(view, Selectors.SetAccessibilityElement, 1);
        NativeMethods.SendVoid(view, Selectors.SetAccessibilityRole, Cocoa.Cocoa.Text("AXTextArea"));
    }

    /// <summary>Asks the tree to build itself once VoiceOver is on. The tree builds nothing until a reader asks, and VoiceOver reads what is published here instead of asking the tree.</summary>
    internal void FollowVoiceOver()
    {
        if (_reading || _tree is not { } tree || !IsAssistiveTechnologyActive)
        {
            return;
        }

        _reading = true;
        _ = tree.Get(tree.RootId);
    }

    /// <summary>Finds the document element: the first child of the first window.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The document, or <see langword="null"/> before the tree has one.</returns>
    private static AccessibleNode? FindDocument(IAccessibleTree tree)
    {
        var application = tree.Get(tree.RootId);
        var window = application is { Children.Count: > 0 } ? tree.Get(application.Children[0]) : null;
        return window is { Children.Count: > 0 } ? tree.Get(window.Children[0]) : null;
    }

    /// <summary>Finds the Cocoa accessibility role of an element.</summary>
    /// <param name="role">The element's role.</param>
    /// <returns>The role name.</returns>
    private static string RoleOf(AccessibleRole role) => role switch
    {
        AccessibleRole.Heading => "AXHeading",
        AccessibleRole.Image => "AXImage",
        AccessibleRole.TableCell => "AXCell",
        _ => "AXStaticText",
    };

    /// <summary>Publishes the document and the blocks of its page again after the tree changed.</summary>
    private void Publish()
    {
        if (_tree is not { } tree || _view == 0 || FindDocument(tree) is not { } document)
        {
            return;
        }

        var view = (nint)_view;
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            NativeMethods.SendVoid(view, Selectors.SetAccessibilityLabel, Cocoa.Cocoa.Text(document.Name));
            NativeMethods.SendVoid(view, Selectors.SetAccessibilityHelp, Cocoa.Cocoa.Text(document.Description));
            NativeMethods.SendVoid(view, Selectors.SetAccessibilityValue, Cocoa.Cocoa.Text(document.Text?.Value ?? string.Empty));
            var frame = ScreenFrame.Of(view);
            var children = NativeMethods.Send(NativeMethods.GetClass("NSMutableArray"), Selectors.Array);
            var elements = NativeMethods.GetClass("NSAccessibilityElement");
            foreach (var id in document.Children)
            {
                if (tree.Get(id) is not { } block)
                {
                    continue;
                }

                var role = Cocoa.Cocoa.Text(RoleOf(block.Role));
                var element = NativeMethods.SendElement(elements, Selectors.AccessibilityElement, role, frame.ToCocoa(block.Bounds), Cocoa.Cocoa.Text(block.Name), view);
                NativeMethods.SendVoid(children, Selectors.AddObject, element);
            }

            NativeMethods.SendVoid(view, Selectors.SetAccessibilityChildren, children);
            NativeMethods.AccessibilityPostNotification(view, Cocoa.Cocoa.Text("AXLayoutChanged"));
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Passes the tree's changes to the bridge.</summary>
    /// <param name="bridge">The bridge.</param>
    private sealed class ChangeObserver(MacAccessibilityBridge bridge) : IObserver<AccessibleChange>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNext(AccessibleChange value) => bridge.Publish();

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            // A failed change stream leaves VoiceOver with what was last published.
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            // A finished change stream leaves VoiceOver with what was last published.
        }
    }
}
