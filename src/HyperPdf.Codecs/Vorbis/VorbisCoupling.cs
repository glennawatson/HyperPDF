// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Vorbis;

/// <summary>Square polar coupling: two channels are coded as a magnitude and an angle, and are turned back into the two channels.</summary>
internal static class VorbisCoupling
{
    /// <summary>Undoes the coupling of one pair of channels.</summary>
    /// <param name="magnitudes">The magnitude channel; becomes the first channel of the pair.</param>
    /// <param name="angles">The angle channel; becomes the second channel of the pair.</param>
    /// <param name="count">The number of values.</param>
    internal static void Undo(float[] magnitudes, float[] angles, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var magnitude = magnitudes[index];
            var angle = angles[index];
            var difference = magnitude > 0 ? magnitude - angle : magnitude + angle;
            var sum = magnitude > 0 ? magnitude + angle : magnitude - angle;
            if (angle > 0)
            {
                angles[index] = difference;
            }
            else
            {
                magnitudes[index] = sum;
                angles[index] = magnitude;
            }
        }
    }
}
