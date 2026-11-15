// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays a menu, a dialog or the search bar out. The window calls it each time what is shown changed.</summary>
/// <param name="items">Receives the controls, back to front.</param>
/// <param name="place">What to lay them out with.</param>
internal delegate void OverlayLayout(List<ChromeItem> items, in OverlayPlace place);
