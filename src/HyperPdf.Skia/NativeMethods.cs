// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace HyperPdf.Skia;

/// <summary>Creates gradient shaders directly from spans of stops.</summary>
internal static partial class NativeMethods
{
    /// <summary>The native Skia library.</summary>
    private const string NativeLibrary = "libSkiaSharp";

    /// <summary>The angle of a complete circle.</summary>
    private const float FullCircleDegrees = 360;

    /// <summary>Resolves the native library shipped beside the executable for Native AOT.</summary>
    static NativeMethods() =>
        System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, static (name, _, _) =>
        {
            if (name != NativeLibrary)
            {
                return IntPtr.Zero;
            }

            var fileName = "libSkiaSharp.so";
            if (OperatingSystem.IsWindows())
            {
                fileName = "libSkiaSharp.dll";
            }
            else if (OperatingSystem.IsMacOS())
            {
                fileName = "libSkiaSharp.dylib";
            }

            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            return File.Exists(path) ? System.Runtime.InteropServices.NativeLibrary.Load(path) : IntPtr.Zero;
        });

    /// <summary>Transfers a native shader reference to a paint.</summary>
    /// <param name="paint">The paint that retains the shader.</param>
    /// <param name="shader">The owned shader handle.</param>
    internal static void AssignShader(SKPaint paint, IntPtr shader)
    {
        SetPaintShader(paint.Handle, shader);
        UnrefShader(shader);
    }

    /// <summary>Creates a linear gradient without copying its stops.</summary>
    /// <param name="start">The gradient start.</param>
    /// <param name="end">The gradient end.</param>
    /// <param name="colors">The stop colors.</param>
    /// <param name="offsets">The stop positions.</param>
    /// <param name="mode">The spread mode.</param>
    /// <param name="matrix">The local transform.</param>
    /// <returns>The owned shader.</returns>
    internal static unsafe IntPtr Linear(SKPoint start, SKPoint end, ReadOnlySpan<SKColor> colors, ReadOnlySpan<float> offsets, SKShaderTileMode mode, ref SKMatrix matrix)
    {
        SKPoint* points = stackalloc SKPoint[2] { start, end };
        fixed (SKColor* colorPointer = colors)
        {
            fixed (float* offsetPointer = offsets)
            {
                fixed (SKMatrix* matrixPointer = &matrix)
                {
                    return CreateLinear(points, (uint*)colorPointer, offsetPointer, colors.Length, mode, matrixPointer);
                }
            }
        }
    }

    /// <summary>Creates a radial gradient without copying its stops.</summary>
    /// <param name="center">The gradient center.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="colors">The stop colors.</param>
    /// <param name="offsets">The stop positions.</param>
    /// <param name="mode">The spread mode.</param>
    /// <param name="matrix">The local transform.</param>
    /// <returns>The owned shader.</returns>
    internal static unsafe IntPtr Radial(SKPoint center, float radius, ReadOnlySpan<SKColor> colors, ReadOnlySpan<float> offsets, SKShaderTileMode mode, ref SKMatrix matrix)
    {
        fixed (SKColor* colorPointer = colors)
        {
            fixed (float* offsetPointer = offsets)
            {
                fixed (SKMatrix* matrixPointer = &matrix)
                {
                    return CreateRadial(&center, radius, (uint*)colorPointer, offsetPointer, colors.Length, mode, matrixPointer);
                }
            }
        }
    }

    /// <summary>Creates a sweep gradient without copying its stops.</summary>
    /// <param name="center">The gradient center.</param>
    /// <param name="colors">The stop colors.</param>
    /// <param name="offsets">The stop positions.</param>
    /// <param name="mode">The spread mode.</param>
    /// <param name="matrix">The local transform.</param>
    /// <returns>The owned shader.</returns>
    internal static unsafe IntPtr Sweep(SKPoint center, ReadOnlySpan<SKColor> colors, ReadOnlySpan<float> offsets, SKShaderTileMode mode, ref SKMatrix matrix)
    {
        fixed (SKColor* colorPointer = colors)
        {
            fixed (float* offsetPointer = offsets)
            {
                fixed (SKMatrix* matrixPointer = &matrix)
                {
                    return CreateSweep(&center, (uint*)colorPointer, offsetPointer, colors.Length, mode, 0, FullCircleDegrees, matrixPointer);
                }
            }
        }
    }

    /// <summary>Retains a shader on a native paint.</summary>
    /// <param name="paint">The paint handle.</param>
    /// <param name="shader">The shader handle.</param>
    [LibraryImport(NativeLibrary, EntryPoint = "sk_paint_set_shader")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void SetPaintShader(IntPtr paint, IntPtr shader);

    /// <summary>Releases an owned shader reference after the paint retains it.</summary>
    /// <param name="shader">The shader handle.</param>
    [LibraryImport(NativeLibrary, EntryPoint = "sk_shader_unref")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void UnrefShader(IntPtr shader);

    /// <summary>Creates a native linear gradient.</summary>
    /// <param name="points">The two endpoints.</param>
    /// <param name="colors">The color buffer.</param>
    /// <param name="offsets">The offset buffer.</param>
    /// <param name="count">The stop count.</param>
    /// <param name="mode">The spread mode.</param>
    /// <param name="matrix">The local transform.</param>
    /// <returns>The owned handle.</returns>
    [LibraryImport(NativeLibrary, EntryPoint = "sk_shader_new_linear_gradient")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial IntPtr CreateLinear(SKPoint* points, uint* colors, float* offsets, int count, SKShaderTileMode mode, SKMatrix* matrix);

    /// <summary>Creates a native radial gradient.</summary>
    /// <param name="center">The center.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="colors">The color buffer.</param>
    /// <param name="offsets">The offset buffer.</param>
    /// <param name="count">The stop count.</param>
    /// <param name="mode">The spread mode.</param>
    /// <param name="matrix">The local transform.</param>
    /// <returns>The owned handle.</returns>
    [LibraryImport(NativeLibrary, EntryPoint = "sk_shader_new_radial_gradient")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial IntPtr CreateRadial(SKPoint* center, float radius, uint* colors, float* offsets, int count, SKShaderTileMode mode, SKMatrix* matrix);

    /// <summary>Creates a native sweep gradient.</summary>
    /// <param name="center">The center.</param>
    /// <param name="colors">The color buffer.</param>
    /// <param name="offsets">The offset buffer.</param>
    /// <param name="count">The stop count.</param>
    /// <param name="mode">The spread mode.</param>
    /// <param name="start">The start angle.</param>
    /// <param name="end">The end angle.</param>
    /// <param name="matrix">The local transform.</param>
    /// <returns>The owned handle.</returns>
    [LibraryImport(NativeLibrary, EntryPoint = "sk_shader_new_sweep_gradient")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial IntPtr CreateSweep(SKPoint* center, uint* colors, float* offsets, int count, SKShaderTileMode mode, float start, float end, SKMatrix* matrix);
}
