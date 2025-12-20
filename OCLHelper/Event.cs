using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

public class Event: IDisposable
{
    private bool disposedValue;

    public nint Handle { get; }

    unsafe public ulong GetProfilingInfo(
        ProfilingInfo info
    )
    {
        ulong time;
        var err = (ErrorCodes)OCL.GetEventProfilingInfo(Handle, info, 8, &time, null);

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't get profiling info, code: ", err));
        }

        return time;
    }

    unsafe public void Wait()
    {
        var handle = Handle;
        var err = (ErrorCodes)OCL.WaitForEvents(1, &handle);

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't wait for event, code: ", err));
        }
    }

    internal Event(nint h)
    {
        Handle = h;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            OCL.ReleaseEvent(Handle);
            disposedValue = true;
        }
    }

    ~Event()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
