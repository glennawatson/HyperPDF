// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Media;

namespace HyperPdf.Platform.Windows.Audio;

/// <summary>
/// Plays a continuous stream of sound through WASAPI on the default output device in shared mode. Windows converts the samples to the
/// device's format. A write waits while the device buffer is full, so the caller stays about a fifth of a second ahead of what is
/// heard, and the latency is the sound still in the device buffer. The COM interfaces are called through their function tables, and
/// every call that may come from a different thread joins the multithreaded apartment first.
/// </summary>
[DebuggerDisplay("WasapiStreamingAudioOutput: WASAPI")]
public sealed unsafe class WasapiStreamingAudioOutput : IStreamingAudioOutput
{
    /// <summary>Shared mode.</summary>
    private const int SharedMode = 0;

    /// <summary>Lets Windows convert the sample rate and format: AUTOCONVERTPCM | SRC_DEFAULT_QUALITY.</summary>
    private const uint ConvertFlags = 0x8000_0000 | 0x0800_0000;

    /// <summary>The buffer length, 200 ms in 100 ns units.</summary>
    private const long BufferDuration = 2_000_000;

    /// <summary>All COM server contexts.</summary>
    private const uint AllContexts = 0x17;

    /// <summary>The multithreaded COM apartment.</summary>
    private const uint MultiThreaded = 0;

    /// <summary>The render (playback) data flow.</summary>
    private const int Render = 0;

    /// <summary>The console device role.</summary>
    private const int Console = 0;

    /// <summary>The IUnknown::Release slot.</summary>
    private const int ReleaseSlot = 2;

    /// <summary>The IMMDeviceEnumerator::GetDefaultAudioEndpoint slot.</summary>
    private const int GetDefaultEndpointSlot = 4;

    /// <summary>The IMMDevice::Activate slot.</summary>
    private const int ActivateSlot = 3;

    /// <summary>The IAudioClient::Initialize slot.</summary>
    private const int InitializeSlot = 3;

    /// <summary>The IAudioClient::GetBufferSize slot.</summary>
    private const int GetBufferSizeSlot = 4;

    /// <summary>The IAudioClient::GetCurrentPadding slot.</summary>
    private const int GetCurrentPaddingSlot = 6;

    /// <summary>The IAudioClient::Start slot.</summary>
    private const int StartSlot = 10;

    /// <summary>The IAudioClient::Stop slot.</summary>
    private const int StopSlot = 11;

    /// <summary>The IAudioClient::Reset slot.</summary>
    private const int ResetSlot = 12;

    /// <summary>The IAudioClient::GetService slot.</summary>
    private const int GetServiceSlot = 14;

    /// <summary>The IAudioRenderClient::GetBuffer slot.</summary>
    private const int GetBufferSlot = 3;

    /// <summary>The IAudioRenderClient::ReleaseBuffer slot.</summary>
    private const int ReleaseBufferSlot = 4;

    /// <summary>How long to wait for the device to want more audio.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>The class id of the device enumerator, CLSID_MMDeviceEnumerator.</summary>
    private static readonly Guid DeviceEnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    /// <summary>The interface id of IMMDeviceEnumerator.</summary>
    private static readonly Guid DeviceEnumeratorInterface = new("A95664D2-9614-4F35-A746-DE8DB63617E6");

    /// <summary>The interface id of IAudioClient.</summary>
    private static readonly Guid AudioClientInterface = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");

    /// <summary>The interface id of IAudioRenderClient.</summary>
    private static readonly Guid RenderClientInterface = new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");

    /// <summary>Serialises calls to the device.</summary>
    private readonly Lock _gate = new();

    /// <summary>The IAudioClient, or null.</summary>
    private void* _client;

    /// <summary>The IAudioRenderClient, or null.</summary>
    private void* _render;

    /// <summary>The device buffer in frames.</summary>
    private uint _frames;

    /// <summary>The samples each second of each channel.</summary>
    private int _rate = 1;

    /// <summary>The channels.</summary>
    private int _channels = 1;

    /// <summary>Gets a value indicating whether there is a default output device.</summary>
    public bool IsAvailable
    {
        get
        {
            var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
            try
            {
                var device = OpenDefaultDevice();
                Release(device);
                return device is not null;
            }
            finally
            {
                Uninitialize(initialized);
            }
        }
    }

    /// <inheritdoc/>
    public TimeSpan Latency
    {
        get
        {
            lock (_gate)
            {
                if (_client is null)
                {
                    return TimeSpan.Zero;
                }

                var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
                try
                {
                    return TimeSpan.FromSeconds((double)Padding(_client) / _rate);
                }
                finally
                {
                    Uninitialize(initialized);
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool Start(int sampleRate, int channels)
    {
        lock (_gate)
        {
            CloseLocked();
            var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
            try
            {
                _rate = sampleRate;
                _channels = channels;
                _client = OpenClient(sampleRate, channels, out _frames);
                return _client is not null && OpenRender() && Call(_client, StartSlot);
            }
            finally
            {
                Uninitialize(initialized);
            }
        }
    }

    /// <inheritdoc/>
    public bool Write(ReadOnlySpan<float> samples, CancellationToken cancellationToken)
    {
        var written = 0;
        while (written < samples.Length)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var step = Fill(samples[written..]);
            if (step < 0)
            {
                return false;
            }

            written += step;
            if (step == 0 && cancellationToken.WaitHandle.WaitOne(PollInterval))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public void Flush()
    {
        lock (_gate)
        {
            if (_client is null)
            {
                return;
            }

            var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
            try
            {
                _ = Call(_client, StopSlot);
                _ = Call(_client, ResetSlot);
                _ = Call(_client, StartSlot);
            }
            finally
            {
                Uninitialize(initialized);
            }
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_gate)
        {
            CloseLocked();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Close();

    /// <summary>Gets a function from a COM object's table.</summary>
    /// <param name="instance">The object.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* Slot(void* instance, int slot) => (*(void***)instance)[slot];

    /// <summary>Releases a COM object.</summary>
    /// <param name="instance">The object, or null.</param>
    private static void Release(void* instance)
    {
        if (instance is not null)
        {
            _ = ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(instance, ReleaseSlot))(instance);
        }
    }

    /// <summary>Balances a successful <c>CoInitializeEx</c>.</summary>
    /// <param name="result">Its result.</param>
    private static void Uninitialize(int result)
    {
        if (result >= 0)
        {
            NativeMethods.CoUninitialize();
        }
    }

    /// <summary>Opens the default output device. Callers have initialised COM.</summary>
    /// <returns>The IMMDevice, or null when there is none.</returns>
    private static void* OpenDefaultDevice()
    {
        void* enumerator = null;
        var classId = DeviceEnumeratorClass;
        var interfaceId = DeviceEnumeratorInterface;
        _ = &interfaceId;
        if (NativeMethods.CoCreateInstance(&classId, null, AllContexts, &interfaceId, &enumerator) < 0)
        {
            return null;
        }

        try
        {
            void* device = null;
            var result = ((delegate* unmanaged[Stdcall]<void*, int, int, void**, int>)Slot(enumerator, GetDefaultEndpointSlot))(enumerator, Render, Console, &device);
            return result < 0 ? null : device;
        }
        finally
        {
            Release(enumerator);
        }
    }

    /// <summary>Opens an audio client for float samples. Callers have initialised COM.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="frames">Receives the buffer size in frames.</param>
    /// <returns>The IAudioClient, or null.</returns>
    private static void* OpenClient(int rate, int channels, out uint frames)
    {
        frames = 0;
        var device = OpenDefaultDevice();
        if (device is null)
        {
            return null;
        }

        void* client = null;
        try
        {
            var interfaceId = AudioClientInterface;
            if (((delegate* unmanaged[Stdcall]<void*, Guid*, uint, void*, void**, int>)Slot(device, ActivateSlot))(device, &interfaceId, AllContexts, null, &client) < 0)
            {
                return null;
            }

            var format = WaveFormat.Float(rate, channels);
            var initialize = (delegate* unmanaged[Stdcall]<void*, int, uint, long, long, WaveFormat*, Guid*, int>)Slot(client, InitializeSlot);
            uint size = 0;
            if (initialize(client, SharedMode, ConvertFlags, BufferDuration, 0, &format, null) < 0
                || ((delegate* unmanaged[Stdcall]<void*, uint*, int>)Slot(client, GetBufferSizeSlot))(client, &size) < 0)
            {
                Release(client);
                return null;
            }

            frames = size;
            return client;
        }
        finally
        {
            Release(device);
        }
    }

    /// <summary>Gets the frames queued and not yet played.</summary>
    /// <param name="client">The IAudioClient.</param>
    /// <returns>The frames, or zero on error.</returns>
    private static uint Padding(void* client)
    {
        uint padding = 0;
        return ((delegate* unmanaged[Stdcall]<void*, uint*, int>)Slot(client, GetCurrentPaddingSlot))(client, &padding) < 0 ? 0 : padding;
    }

    /// <summary>Calls a method taking only the object, such as Start, Stop or Reset.</summary>
    /// <param name="client">The IAudioClient.</param>
    /// <param name="slot">The slot.</param>
    /// <returns><see langword="true"/> when the call succeeded.</returns>
    private static bool Call(void* client, int slot) => ((delegate* unmanaged[Stdcall]<void*, int>)Slot(client, slot))(client) >= 0;

    /// <summary>Gets the render client of the open audio client. Callers hold the gate.</summary>
    /// <returns><see langword="true"/> when it was found.</returns>
    private bool OpenRender()
    {
        void* render = null;
        var interfaceId = RenderClientInterface;
        if (((delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)Slot(_client, GetServiceSlot))(_client, &interfaceId, &render) < 0)
        {
            return false;
        }

        _render = render;
        return true;
    }

    /// <summary>Writes as many samples as the device has room for.</summary>
    /// <param name="samples">The samples left.</param>
    /// <returns>The samples written, which is zero when the device is full, or -1 when the output is closed or failed.</returns>
    private int Fill(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            if (_client is null || _render is null)
            {
                return -1;
            }

            var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
            try
            {
                var frames = (int)Math.Min(_frames - Padding(_client), (uint)(samples.Length / _channels));
                if (frames <= 0)
                {
                    return 0;
                }

                byte* buffer = null;
                if (((delegate* unmanaged[Stdcall]<void*, uint, byte**, int>)Slot(_render, GetBufferSlot))(_render, (uint)frames, &buffer) < 0)
                {
                    return -1;
                }

                var count = frames * _channels;
                samples[..count].CopyTo(new(buffer, count));
                _ = ((delegate* unmanaged[Stdcall]<void*, uint, uint, int>)Slot(_render, ReleaseBufferSlot))(_render, (uint)frames, 0);
                return count;
            }
            finally
            {
                Uninitialize(initialized);
            }
        }
    }

    /// <summary>Stops and releases the device. Callers hold the gate.</summary>
    private void CloseLocked()
    {
        if (_client is null)
        {
            return;
        }

        var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
        try
        {
            _ = Call(_client, StopSlot);
            Release(_render);
            Release(_client);
        }
        finally
        {
            Uninitialize(initialized);
            _render = null;
            _client = null;
        }
    }
}
