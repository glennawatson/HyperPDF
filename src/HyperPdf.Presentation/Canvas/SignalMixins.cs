// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Operators every stream can use, shared by every head.</summary>
public static class SignalMixins
{
    /// <summary>Adds the operators every stream can use.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The stream.</param>
    extension<T>(IObservable<T> source)
    {
        /// <summary>Gets the stream as plain signals, the input a command without a parameter takes.</summary>
        public IObservable<RxVoid> AsVoid => source.Select(static _ => RxVoid.Default);
    }
}
