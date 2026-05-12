using ManagedCuda;
using ManagedCuda.BasicTypes;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SparkCU;

public class Event : IDisposable
{
    private CUevent _event0;
    private CUevent _event1;
    private bool disposedValue;

    internal void Record0()
    {
        var res = DriverAPINativeMethods.Events.cuEventRecord(_event0, default);
        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't record an event, code: {res}");
        }
    }

    internal void Record1()
    {
        var res = DriverAPINativeMethods.Events.cuEventRecord(_event1, default);
        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't record an event, code: {res}");
        }
    }

    internal Event()
    {
        _event0 = new();
        var res = DriverAPINativeMethods.Events.cuEventCreate(ref _event0, CUEventFlags.BlockingSync);
        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't create an event, code: {res}");
        }
        _event1 = new();
        res = DriverAPINativeMethods.Events.cuEventCreate(ref _event1, CUEventFlags.BlockingSync);
        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't create an event, code: {res}");
        }
    }

    public ulong ElapsedMilliseconds
    {
        get
        {
            float pMilliseconds = 0f;
            CUResult res = DriverAPINativeMethods.Events.cuEventElapsedTime(
                ref pMilliseconds,
                _event0,
                _event1
            );
            if (res != CUResult.Success)
            {
                throw new Exception($"Couldn't query elapsed time, code: {res}");
            }

            return (ulong)pMilliseconds;
        }
    }

    public void Wait()
    {
        var res = DriverAPINativeMethods.Events.cuEventSynchronize(_event1);

        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't wait for event, code: {res}");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            var res = CUResult.Success;
            res = DriverAPINativeMethods.Events.cuEventDestroy_v2(_event0);
            if (res != CUResult.Success)
            {
                Debug.WriteLine($"Couldn't destroy an event, code: {res}");
            }
            res = DriverAPINativeMethods.Events.cuEventDestroy_v2(_event1);
            if (res != CUResult.Success)
            {
                Debug.WriteLine($"Couldn't destroy an event, code: {res}");
            }

            disposedValue = true;
        }
    }

    ~Event()
    {
        // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}