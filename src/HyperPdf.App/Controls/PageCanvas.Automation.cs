// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;

namespace HyperPdf.App.Controls;

/// <summary>Gives screen readers the page the reader is on and its text.</summary>
public sealed partial class PageCanvas
{
    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new PageCanvasAutomationPeer(this);
}
