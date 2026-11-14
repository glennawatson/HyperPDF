// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Windows;

/// <summary>Receives an event while a head pumps its window's queue.</summary>
/// <param name="windowEvent">The event.</param>
public delegate void WindowEventHandler(in WindowEvent windowEvent);
