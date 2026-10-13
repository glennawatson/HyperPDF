// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Controls;

/// <summary>A value from an event stream with the state that was current when it arrived.</summary>
/// <typeparam name="TValue">The value, for example the event arguments.</typeparam>
/// <typeparam name="TState">The state, for example the view model the view shows.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="State">The state.</param>
internal readonly record struct Injected<TValue, TState>(TValue Value, TState State);
