using ManagedCuda;
using ManagedCuda.BasicTypes;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SparkCU;

[Flags]
public enum BufferFlags
{
    OnHost          = 1,
    OnDevice        = 1<<1,
    OnHostAndDevice = OnHost | OnDevice,
    // DeviceInternal = 1<<2,
}

[Flags]
public enum MemFlags : ulong
{
    None            = 0,
    ReadWrite       = 1,
    HostNoAccess    = 1<<1,
}

public enum MapFlags
{
    Read
}

public unsafe class ComputeBuffer<T> : IDisposable
where T: unmanaged, INumber<T>
{
    private bool disposedValue;
    
    internal T[] _hostBuffer;
    internal CUdeviceptr _deviceBuffer;
    public int Length { get; }
    private BufferFlags _bufferFlags;

    public Dictionary<int, T> FiveFirst { get {
        var len = Math.Min(5, Length);
        var res = new T[len];
        var red = new Dictionary<int, T>(len);
        GCHandle handle = GCHandle.Alloc(res, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            var err = DriverAPINativeMethods.SynchronousMemcpy_v2.cuMemcpyDtoH_v2(
                ptr,
                _deviceBuffer,
                len*sizeof(T)
            );

        }
        finally
        {
            handle.Free();
        }

        for (int i = 0; i < len; i++)
        {
            red.Add(i, res[i]);
        }

        return red;
    }}

    public Dictionary<int, T> FiveLast { get {
        var len = Math.Min(5, Length);
        var res = new T[len];
        var red = new Dictionary<int, T>(len);
        GCHandle handle = GCHandle.Alloc(res, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            var err = DriverAPINativeMethods.SynchronousMemcpy_v2.cuMemcpyDtoH_v2(
                ptr,
                _deviceBuffer + (Length - len) * sizeof(T),
                len * sizeof(T)
            );

        }
        finally
        {
            handle.Free();
        }

        for (int i = 0; i < len; i++)
        {
            red.Add(Length - len + i, res[i]);
        }

        return red;
    }}

    public ComputeBuffer(ReadOnlySpan<T> in_array, BufferFlags bufferFlags, MemFlags flags = MemFlags.ReadWrite)
    {
        _deviceBuffer = new CUdeviceptr();
        _bufferFlags = bufferFlags;
        Length = in_array.Length;
        var _typeSize = (uint)Marshal.SizeOf<T>();

        if (bufferFlags.HasFlag(BufferFlags.OnHost))
        {
            _hostBuffer = in_array.ToArray();
        }
            
        if (bufferFlags.HasFlag(BufferFlags.OnDevice))
        {
            var res = DriverAPINativeMethods.MemoryManagement.cuMemAlloc_v2(ref _deviceBuffer, _typeSize * Length);
            if (res != CUResult.Success)
            {
                throw new Exception($"Couldn't create a buffer, code: {res}");
            }
            fixed (T* ptr = in_array)
            {
                res = DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyHtoDAsync_v2(_deviceBuffer, (IntPtr)ptr, Length * sizeof(T), CUstream.NullStream);
            }
            if (res != CUResult.Success)
            {
                throw new Exception($"Couldn't copy initial values, code: {res}");
            }
        }
    }
    
    public ComputeBuffer(int length, BufferFlags bufferFlags, MemFlags flags = MemFlags.ReadWrite)
    {        
        _deviceBuffer = new CUdeviceptr();
        _bufferFlags = bufferFlags;
        Length = length;
        var _typeSize = (uint)Marshal.SizeOf<T>();

        if (bufferFlags.HasFlag(BufferFlags.OnHost))
        {
            _hostBuffer = new T[Length];
        }
            
        if (bufferFlags.HasFlag(BufferFlags.OnDevice))
        {
            var res = DriverAPINativeMethods.MemoryManagement.cuMemAlloc_v2(ref _deviceBuffer, _typeSize * Length);
            if (res != CUResult.Success)
            {
                throw new Exception($"Couldn't create a buffer, code: {res}");
            }
        }
    }
    

    // TODO: map/accessor, idk how to replicate that in CUDA
    /*
    internal void UnmapAccessor(IReadOnlyMemAccessor<T> accessor)
    {
        Core.queue!.EnqueueUnmapMemObject(_hostBuffer!, accessor._ptr, out var ev);
#if CU_COLLECT_TIME
        Core.IOEvents.Add(ev);
#endif
    }
    */
    public Accessor<T> MapHost(MapFlags flags)
    {
        if (!_bufferFlags.HasFlag(BufferFlags.OnHost))
        {
            throw new Exception($"No host copy to map into. Buffer must be created with {BufferFlags.OnHost}");
        }

        return new Accessor<T>(this, _hostBuffer.AsSpan());
    }

    /*
    T* MapHostPointer(
        MapFlags flags,
        bool blocking = true
    ) {
        var res = (T*)Core.queue!.EnqueueMapBuffer(_hostBuffer!, blocking, flags, 0, (nuint)Length, out var ev);
#if CU_COLLECT_TIME
        Core.IOEvents.Add(ev);
#endif
        if (blocking)
        {
            ev.Wait();
        }
        return res;
    }
    */

    //unsafe public Event Unmap()
    //{
    //    Core.queue!.EnqueueUnmapMemObject(buffer, mappedPtr, out var ev);
    //    return ev;
    //}

    /*
    public Event Read(
        bool blocking = true,
        Event[]? wait_list = null
    )
    {
        Core.queue!.EnqueueReadBuffer(_buffer, blocking, 0, AsSpan(), out var ev);
        #if CU_COLLECT_TIME
            Core.IOEvents.Add(ev);
        #endif
        return ev;
    }

    public Event Write(
        bool blocking = true
    )
    {
        Core.queue!.EnqueueWriteBuffer(_buffer, blocking, 0, AsSpan(), out var ev);
        #if CU_COLLECT_TIME
            Core.IOEvents.Add(ev);
        #endif
        return ev;
    }
    */

    public Event HostReadTo(
        Span<T> destination
    ) {
        var ev = new Event();
        // TODO: idk how events work in such situations to measure host time
        ev.Record0();
        _hostBuffer.CopyTo(destination);
        ev.Record1();

#if CU_COLLECT_TIME
        Core.IOEvents.Add(ev);
#endif
        return ev;
    }
    
    public Event DeviceReadTo(
        Span<T> destination
    ) {
        var ev = new Event();
        ev.Record0();
        fixed (T* ptr = destination)
        {
            var err = DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyDtoHAsync_v2(
                (IntPtr)ptr, _deviceBuffer,
                Length * sizeof(T),
                CUstream.NullStream
            );
            if (err != CUResult.Success)
            {
                throw new Exception($"Couldn't create a buffer, code: {err}");
            }
        }
        ev.Record1();
#if CU_COLLECT_TIME
        Core.IOEvents.Add(ev);
#endif
        return ev;
    }

    public Event? ToDevice(
        bool blocking = true,
        Event[]? waitList = null
    ) {
        if (!_bufferFlags.HasFlag(BufferFlags.OnHostAndDevice))
        {
            throw new Exception($"Buffer must be created with {BufferFlags.OnHostAndDevice}");
        }

        var ev = new Event();
        ev.Record0();
        fixed(T* ptr = _hostBuffer)
        {
            var err = DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyHtoDAsync_v2(_deviceBuffer, (IntPtr)ptr, Length * sizeof(T), CUstream.NullStream);
            if (err != CUResult.Success)
            {
                throw new Exception($"Couldn't copy buffer, code: {err}");
            }
        }
        ev.Record1();
            
#if CU_COLLECT_TIME
            Core.IOEvents.Add(ev);
#endif
        if (blocking)
        {
            ev.Wait();
        }

        return ev;
    }
    
    public Event? ToHost(
        bool blocking = true,
        Event[]? waitList = null
    ) {
        if (!_bufferFlags.HasFlag(BufferFlags.OnHostAndDevice))
        {
            throw new Exception($"Buffer must be created with {BufferFlags.OnHostAndDevice}");
        }

        var ev = new Event();
        ev.Record0();
        fixed(T* ptr = _hostBuffer)
        {
            var err = DriverAPINativeMethods.SynchronousMemcpy_v2.cuMemcpyDtoH_v2(
                (IntPtr)ptr,
                _deviceBuffer,
                Length*sizeof(T)
            );
            if (err != CUResult.Success)
            {
                throw new Exception($"Couldn't copy buffer to host, code: {err}");
            }
        }
        ev.Record1();
#if CU_COLLECT_TIME
            Core.IOEvents.Add(ev);
#endif
        if (blocking)
        {
            ev.Wait();
        }

        return ev;
    }
    
    public Event CopyHostTo(
        ComputeBuffer<T> destination,
        bool blocking = true,
        Event[]? waitList = null
    ) {
        if (!_bufferFlags.HasFlag(BufferFlags.OnHost) || !destination._bufferFlags.HasFlag(BufferFlags.OnHost))
        {
            throw new Exception($"Both Buffers must be created with {BufferFlags.OnHost}");
        }
        if (Length != destination.Length)
        {
            throw new Exception("Source and destination sizes doesn't match");
        }

        var ev = new Event();
        ev.Record0();
        _hostBuffer.CopyTo(destination._hostBuffer, 0);
        ev.Record1();
#if CU_COLLECT_TIME
        Core.KernEvents.Add(ev);
#endif
        if (blocking)
        {
            ev.Wait();
        }
        return ev;
    }
    
    public Event CopyDeviceTo(
        ComputeBuffer<T> destination,
        bool blocking = true,
        Event[]? waitList = null
    ) {
        if (!_bufferFlags.HasFlag(BufferFlags.OnDevice) || !destination._bufferFlags.HasFlag(BufferFlags.OnDevice))
        {
            throw new Exception($"Both Buffers must be created with {BufferFlags.OnDevice}");
        }
        if (Length != destination.Length)
        {
            throw new Exception("Source and destination sizes doesn't match");
        }

        var ev = new Event();
        ev.Record0();
        var err = DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyDtoDAsync_v2(
            destination._deviceBuffer,
            _deviceBuffer,
            Length * sizeof(T),
            CUstream.NullStream
        );
        if (err != CUResult.Success)
        {
            throw new Exception($"Couldn't copy a buffer, code: {err}");
        }
        ev.Record1();

#if CU_COLLECT_TIME
        Core.KernEvents.Add(ev);
#endif
        if (blocking)
        {
            ev.Wait();
        }
        return ev;
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            var err = CUResult.Success;
             err = DriverAPINativeMethods.MemoryManagement.cuMemFree_v2(_deviceBuffer);
            if (err != CUResult.Success)
            {
                throw new Exception($"Couldn't free a buffer, code: {err}");
            }
            disposedValue = true;
        }
    }

    ~ComputeBuffer()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

unsafe interface IReadOnlyMemAccessor<T>
where T: unmanaged, INumber<T>
{
    int Length{ get; }
    T this[int i] { get; }
}

interface IMemAccessor<T> : IReadOnlyMemAccessor<T>
where T: unmanaged, INumber<T>
{
    new T this[int i] { get; set; }
}

public unsafe ref struct Accessor<T> : IMemAccessor<T>, IDisposable
where T : unmanaged, INumber<T>
{
    Span<T> inner_;

    ComputeBuffer<T> master_;

    public int Length { get; private set; }

    public T this[int i]
    {
        // TODO: measure performace
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => inner_[i];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => inner_[i] = value;
    }

    internal Accessor(ComputeBuffer<T> buffer, Span<T> inner)
    {
        master_ = buffer;
        inner_ = inner;
    }

    public Span<T> AsSpan()
    {
        return inner_;
    }

    public void Dispose()
    {
        // nothing to do
    }
}
