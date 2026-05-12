using Silk.NET.OpenCL;
using OCLHelper;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;

namespace SparkCL;

[Flags]
public enum BufferFlags
{
    OnHost          = 1,
    OnDevice        = 1<<1,
    OnHostAndDevice = OnHost | OnDevice,
    // DeviceInternal = 1<<2,
}

public enum MapFlags
{
    Read
}

static class MapFlagsImpl
{
    public static Silk.NET.OpenCL.MapFlags ToNative(this MapFlags flags)
    {
        return flags switch
        {
            MapFlags.Read => Silk.NET.OpenCL.MapFlags.Read,

            _ => throw new Exception("Invalid flag to convert")
        };
    }
}

public unsafe class ComputeBuffer<T> : IDisposable
where T: unmanaged, INumber<T>
{
    private bool disposedValue;
    
    internal Buffer<T>? _hostBuffer;
    internal Buffer<T>? _deviceBuffer;
    public int Length { get; }
    private BufferFlags _bufferFlags;

    public Dictionary<int, T> FiveFirst { get {
        var len = Math.Min(5, Length);
        var res = new T[len];
        var red = new Dictionary<int, T>(len);
        Core.queue!.EnqueueReadBuffer(_deviceBuffer!, true, 0, res, out _);

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
        Core.queue!.EnqueueReadBuffer(_deviceBuffer!, true, (uint)(Length - len), res, out _);

        for (int i = 0; i < len; i++)
        {
            red.Add(Length - len + i, res[i]);
        }

        return red;
    }}

    public ComputeBuffer(ReadOnlySpan<T> in_array, BufferFlags bufferFlags, MemFlags flags = MemFlags.ReadWrite)
    {
        if (flags.HasFlag(MemFlags.AllocHostPtr))
        {
            throw new Exception("AllocHostPtr flag is not supported");
        }

        _bufferFlags = bufferFlags;
        Length = in_array.Length;
        
        if (Core.device!.IsHostUnifiedMemory)
        {
            _hostBuffer = Buffer<T>.NewCopy(Core.context!, flags | MemFlags.AllocHostPtr, in_array);
            _deviceBuffer = _hostBuffer;
        }
        else 
        {
            if (bufferFlags.HasFlag(BufferFlags.OnHost))
            {
                _hostBuffer = Buffer<T>.NewCopy(Core.context!, flags | MemFlags.AllocHostPtr, in_array);
            }
            
            if (bufferFlags.HasFlag(BufferFlags.OnDevice))
            {
                _deviceBuffer = Buffer<T>.NewCopy(Core.context!, flags, in_array);
            }
        }
    }
    
    public ComputeBuffer(int length, BufferFlags bufferFlags, MemFlags flags = MemFlags.ReadWrite)
    {
        if (flags.HasFlag(MemFlags.AllocHostPtr))
        {
            throw new Exception("AllocHostPtr flag is not supported");
        }
        
        Length = length;
        _bufferFlags = bufferFlags;
        
        if (Core.device!.IsHostUnifiedMemory)
        {
            _hostBuffer = Buffer<T>.NewAllocHost(Core.context!, flags, (nuint)length);
            _deviceBuffer = _hostBuffer;
        }
        else 
        {
            if (bufferFlags.HasFlag(BufferFlags.OnHost))
            {
                _hostBuffer = Buffer<T>.NewAllocHost(Core.context!, flags, (nuint)length);
            }
            
            if (bufferFlags.HasFlag(BufferFlags.OnDevice))
            {
                _deviceBuffer = new Buffer<T>(Core.context!, flags, (nuint)length);
            }
        }
    }
    

    internal void UnmapAccessor(IReadOnlyMemAccessor<T> accessor)
    {
        Core.queue!.EnqueueUnmapMemObject(_hostBuffer!, accessor._ptr, out var ev);
#if COLLECT_TIME
        Core.IOEvents.Add(new Event(ev));
#endif
    }

    public Accessor<T> MapHost(MapFlags flags)
    {
        if (!_bufferFlags.HasFlag(BufferFlags.OnHost))
        {
            throw new Exception($"No host copy to map into. Buffer must be created with {BufferFlags.OnHost}");
        }
        
        var ptr = MapHostPointer(flags);
        return new Accessor<T>(this, ptr, Length);
    }

    T* MapHostPointer(
        MapFlags flags,
        bool blocking = true
    ) {
        var res = Core.queue!.EnqueueMapBuffer(_hostBuffer!, blocking, flags.ToNative(), 0, (nuint)Length, out var ev);
#if COLLECT_TIME
        Core.IOEvents.Add(new Event(ev));
#endif
        if (blocking)
        {
            ev.Wait();
        }
        return res;
    }

    public Event HostReadTo(
        Span<T> destination
    ) {
        Core.queue!.EnqueueReadBuffer(_hostBuffer!, true, 0, destination, out var ev, null);
        var evcl = new Event(ev);
#if COLLECT_TIME
        Core.IOEvents.Add(evcl);
#endif
        return evcl;
    }
    
    public Event DeviceReadTo(
        Span<T> destination
    ) {
        Core.queue!.EnqueueReadBuffer(_deviceBuffer!, true, 0, destination, out var ev, null);
        var evcl = new Event(ev);
#if COLLECT_TIME
        Core.IOEvents.Add(evcl);
#endif
        return evcl;
    }

    public Event? ToDevice(
        bool blocking = true,
        Event[]? waitList = null
    ) {
        if (!_bufferFlags.HasFlag(BufferFlags.OnHostAndDevice))
        {
            throw new Exception($"Buffer must be created with {BufferFlags.OnHostAndDevice}");
        }
        
        if (_hostBuffer != _deviceBuffer)
        {
            var waitList_ = waitList?.Select(val => val.inner).ToArray();
            Core.queue!.EnqueueCopyBuffer(_hostBuffer!, _deviceBuffer!, 0, 0, (nuint) Length, out var ev, waitList_);
            var evcl = new Event(ev);

#if COLLECT_TIME
            Core.IOEvents.Add(evcl);
#endif
            if (blocking)
            {
                evcl.Wait();
            }
            
            return evcl;
        }
        else
        {
            return null;
        }

    }
    
    public Event? ToHost(
        bool blocking = true,
        Event[]? waitList = null
    ) {
        if (!_bufferFlags.HasFlag(BufferFlags.OnHostAndDevice))
        {
            throw new Exception($"Buffer must be created with {BufferFlags.OnHostAndDevice}");
        }

        if (_hostBuffer != _deviceBuffer)
        {

            var waitList_ = waitList?.Select(val => val.inner).ToArray();
            Core.queue!.EnqueueCopyBuffer(_deviceBuffer!, _hostBuffer!, 0, 0, (nuint)Length, out var ev, waitList_);
            var evcl = new Event(ev);

#if COLLECT_TIME
            Core.IOEvents.Add(evcl);
#endif
            if (blocking)
            {
                evcl.Wait();
            }

            return evcl;
        }
        else
        {
            return null;
        }
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
        
        var waitList_ = waitList?.Select(val => val.inner).ToArray();
        Core.queue!.EnqueueCopyBuffer(_hostBuffer!, destination._hostBuffer!, 0, 0, (nuint) Length, out var ev, waitList_);
        var evcl = new Event(ev);

#if COLLECT_TIME
        Core.KernEvents.Add(evcl);
#endif
        if (blocking)
        {
            evcl.Wait();
        }
        return evcl;
    }
    
    // [Pure]
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

        var waitList_ = waitList?.Select(val => val.inner).ToArray();
        Core.queue!.EnqueueCopyBuffer(_deviceBuffer!, destination._deviceBuffer!, 0, 0, (nuint) Length, out var ev, waitList_);
        var evcl = new Event(ev);

#if COLLECT_TIME
        Core.KernEvents.Add(evcl);
#endif
        if (blocking)
        {
            evcl.Wait();
        }
        return evcl;
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            _hostBuffer?.Dispose();
            _deviceBuffer?.Dispose();
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
    internal T* _ptr { get; }
    int Length{ get; }
    T this[int i] { get; }
}

interface IMemAccessor<T> : IReadOnlyMemAccessor<T>
where T: unmanaged, INumber<T>
{
    new T this[int i] { get; set; }
}

public unsafe class Accessor<T> : IMemAccessor<T>, IDisposable
where T: unmanaged, INumber<T>
{
    private bool disposedValue;
    
    internal T* ptr { get; }
    T* IReadOnlyMemAccessor<T>._ptr => ptr;
    ComputeBuffer<T> _master;

    public int Length { get; private set; }

    public T this[int i]
    {
        // TODO: measure performace
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ptr[i];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => ptr[i] = value;
    }

    internal Accessor(ComputeBuffer<T> buffer, T* ptr, int length)
    {
        _master = buffer;
        this.ptr = ptr;
        Length = length;
    }

    public Span<T> AsSpan()
    {
        return new Span<T>(ptr, Length);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            _master.UnmapAccessor(this);
            disposedValue = true;
        }
    }

    ~Accessor()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
