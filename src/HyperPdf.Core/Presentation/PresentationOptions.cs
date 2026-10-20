// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Presentation;

/// <summary>What the reader allows a presentation to do by itself.</summary>
/// <param name="FollowTransitions">Whether pages may appear with the effects they ask for.</param>
/// <param name="FollowTimings">Whether pages may move on after the time they ask for.</param>
/// <param name="ReduceMotion">Whether motion is reduced, which stops both.</param>
[DebuggerDisplay("PresentationOptions: transitions {FollowTransitions}, timings {FollowTimings}, reduced motion {ReduceMotion}")]
public readonly record struct PresentationOptions(bool FollowTransitions, bool FollowTimings, bool ReduceMotion);
