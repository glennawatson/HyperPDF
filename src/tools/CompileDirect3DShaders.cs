#:property TargetFramework=net11.0
#:property AllowUnsafeBlocks=true
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Compiles the Direct3D compositor shaders with the HLSL compiler that ships with Windows and writes them as byte tables,
// so the viewer needs no shader compiler at run time. Run on Windows after changing the HLSL below:
//   dotnet run --file src/tools/CompileDirect3DShaders.cs -- src/HyperPdf.Rendering.Direct3D/Direct3DShaderCode.cs

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

const string VertexSource = """
    cbuffer Frame : register(b0)
    {
        float2 scale;
        float2 unused;
    };

    struct Input
    {
        float2 position : POSITION;
        float2 uv : TEXCOORD0;
        float4 color : COLOR0;
    };

    struct Output
    {
        float4 position : SV_Position;
        float2 uv : TEXCOORD0;
        float4 color : COLOR0;
    };

    Output main(Input input)
    {
        Output output;
        output.position = float4((input.position.x * scale.x) - 1.0, 1.0 - (input.position.y * scale.y), 0.0, 1.0);
        output.uv = input.uv;
        output.color = input.color;
        return output;
    }
    """;

const string PixelSource = """
    Texture2D tile : register(t0);
    SamplerState tileSampler : register(s0);

    struct Input
    {
        float4 position : SV_Position;
        float2 uv : TEXCOORD0;
        float4 color : COLOR0;
    };

    float4 main(Input input) : SV_Target
    {
        return tile.Sample(tileSampler, input.uv) * input.color;
    }
    """;

const int BytesPerLine = 16;
const uint OptimizationLevel3 = 1 << 15;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: CompileDirect3DShaders <output .cs file>");
    return 1;
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The HLSL compiler ships with Windows, so this tool runs on Windows.");
    return 1;
}

var vertex = Compile(VertexSource, "vs_4_0");
var pixel = Compile(PixelSource, "ps_4_0");
var text = new StringBuilder();
text.Append(
    """
    // Copyright (c) 2026 Glenn Watson. All rights reserved.
    // Glenn Watson licenses this file to you under the MIT license.
    // See the LICENSE file in the project root for full license information.

    namespace HyperPdf.Rendering.Direct3D;

    /// <summary>The compositor's shaders as Direct3D bytecode, compiled once from the HLSL in src/tools/CompileDirect3DShaders.cs, so no shader compiler is needed at run time.</summary>
    internal static class Direct3DShaderCode
    {

    """);
AppendTable(text, "Vertex", "Gets the vertex shader: turns a position in pixels into clip space and passes the texture coordinate and colour on.", vertex);
text.Append('\n');
AppendTable(text, "Pixel", "Gets the pixel shader: the tile's colour multiplied by the vertex colour.", pixel);
text.Append("}\n");
await File.WriteAllTextAsync(args[0], text.ToString().ReplaceLineEndings("\n"));
Console.WriteLine($"vertex {vertex.Length} bytes, pixel {pixel.Length} bytes -> {args[0]}");
return 0;

static void AppendTable(StringBuilder text, string name, string summary, byte[] code)
{
    text.Append(CultureInfo.InvariantCulture, $"    /// <summary>{summary}</summary>\n");
    text.Append(CultureInfo.InvariantCulture, $"    internal static ReadOnlySpan<byte> {name} =>\n    [\n");
    for (var i = 0; i < code.Length; i += BytesPerLine)
    {
        text.Append("        ");
        var end = Math.Min(i + BytesPerLine, code.Length);
        for (var b = i; b < end; b++)
        {
            text.Append(CultureInfo.InvariantCulture, $"0x{code[b]:X2},");
            if (b + 1 < end)
            {
                text.Append(' ');
            }
        }

        text.Append('\n');
    }

    text.Append("    ];\n");
}

static unsafe byte[] Compile(string source, string target)
{
    var bytes = Encoding.ASCII.GetBytes(source);
    var entry = "main\0"u8;
    var profile = Encoding.ASCII.GetBytes(target + "\0");
    void* code = null;
    void* errors = null;
    int result;
    fixed (byte* sourcePointer = bytes)
    fixed (byte* entryPointer = entry)
    fixed (byte* profilePointer = profile)
    {
        result = Compiler.D3DCompile(sourcePointer, (nuint)bytes.Length, null, null, null, entryPointer, profilePointer, OptimizationLevel3, 0, &code, &errors);
    }

    if (result < 0)
    {
        var message = errors is null ? "no message" : Marshal.PtrToStringAnsi((nint)Blob.Pointer(errors));
        throw new InvalidOperationException($"The {target} shader did not compile: {message}");
    }

    var compiled = new ReadOnlySpan<byte>(Blob.Pointer(code), (int)Blob.Size(code)).ToArray();
    Blob.Release(code);
    Blob.Release(errors);
    return compiled;
}

internal static unsafe partial class Compiler
{
    [LibraryImport("d3dcompiler_47", EntryPoint = "D3DCompile")]
    internal static partial int D3DCompile(
        byte* source,
        nuint sourceLength,
        byte* sourceName,
        void* defines,
        void* include,
        byte* entryPoint,
        byte* target,
        uint flags1,
        uint flags2,
        void** code,
        void** errors);
}

internal static unsafe class Blob
{
    internal static void* Pointer(void* blob) => ((delegate* unmanaged[Stdcall]<void*, void*>)(*(void***)blob)[3])(blob);

    internal static nuint Size(void* blob) => ((delegate* unmanaged[Stdcall]<void*, nuint>)(*(void***)blob)[4])(blob);

    internal static void Release(void* blob)
    {
        if (blob is not null)
        {
            _ = ((delegate* unmanaged[Stdcall]<void*, uint>)(*(void***)blob)[2])(blob);
        }
    }
}
