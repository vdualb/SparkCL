using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SparkCL;

public class Event
{
    internal OCLHelper.Event inner;

    internal Event(OCLHelper.Event inner)
    {
        this.inner = inner;
    }

    public ulong ElapsedMilliseconds {
        get {
            var s = inner.GetProfilingInfo(ProfilingInfo.Start);
            var c = inner.GetProfilingInfo(ProfilingInfo.End);

            return (c - s) / 1_000_000;
        }
    }

    public void Wait()
    {
        inner.Wait();
    }
}
