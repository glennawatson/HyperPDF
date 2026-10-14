// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Interactivity;

namespace HyperPdf.App.Controls;

/// <summary>One step of a focus change on its way through the controls that hold the newly focused one.</summary>
/// <param name="Sender">The control the step reached.</param>
/// <param name="Args">The focus change.</param>
internal readonly record struct FocusArrival(object Sender, RoutedEventArgs Args);
