// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>An accessibility bridge that also answers the window messages UI Automation sends, which only a Win32 window receives.</summary>
public interface IWin32AccessibilityBridge : IAccessibilityBridge
{
    /// <summary>Tells the bridge which window it publishes. Called once, right after the window is created.</summary>
    /// <param name="window">The window handle.</param>
    void Attach(long window);

    /// <summary>Answers WM_GETOBJECT when UI Automation asks for the root of the window.</summary>
    /// <param name="window">The window handle.</param>
    /// <param name="primary">The wParam of the message.</param>
    /// <param name="secondary">The lParam of the message.</param>
    /// <param name="result">Receives the result the window procedure returns.</param>
    /// <returns><see langword="true"/> when the message was a UI Automation request and <paramref name="result"/> holds the answer.</returns>
    bool TryAnswerGetObject(long window, nuint primary, nint secondary, out nint result);
}
