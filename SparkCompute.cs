using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SparkCompute
{
    public class NDRange
    {
        public static nuint PaddedTo(int initial, int multiplier)
        {
            if (initial % multiplier == 0)
            {
                return (nuint)initial;
            }
            else
            {
                return ((nuint)(initial / multiplier) + 1) * (nuint)multiplier;
            }
        }

        public NDRange PadTo(int multiplier)
        {
            for (int i = 0; i < 3; i++)
            {
                Sizes[i] = (Sizes[i] / (nuint)multiplier + 1) * (nuint)multiplier;
            }

            return this;
        }

        public NDRange PadTo(nuint multiplier)
        {
            for (int i = 0; i < 3; i++)
            {
                Sizes[i] = (Sizes[i] / multiplier + 1) * multiplier;
            }

            return this;
        }

        public uint Dimensions { get; }
        public nuint[] Sizes { get; } = new nuint[3];

        public NDRange()
        {
            Dimensions = 0;
            Sizes[0] = 0;
            Sizes[1] = 0;
            Sizes[2] = 0;
        }
        public NDRange(nuint size0)
        {
            Dimensions = 1;
            Sizes[0] = size0;
            Sizes[1] = 1;
            Sizes[2] = 1;
        }
        public NDRange(
            nuint size0,
            nuint size1)
        {
            Dimensions = 2;
            Sizes[0] = size0;
            Sizes[1] = size1;
            Sizes[2] = 1;
        }
        public NDRange(
            nuint size0,
            nuint size1,
            nuint size2)
        {
            Dimensions = 3;
            Sizes[0] = size0;
            Sizes[1] = size1;
            Sizes[2] = size2;
        }

        public nuint this[int i]
        {
            get => Sizes[i];
        }
    }

    interface IEvent : IDisposable
    {
        void Wait();
        ulong ElapsedMilliseconds { get; }
    }

    interface IKernel : IDisposable
    {
        IEvent Execute (bool blocking = true, IEvent[]? waitList = null);

        void SetArg<T> (uint idx, T arg)
            where T : unmanaged, INumber<T>;
    }

    interface IComputeProgram : IDisposable
    {
        IKernel GetKernel(string kernelName, NDRange globalWork, NDRange localWork);
        byte[][] GetBinaries();
    }

    interface IComputeBuffer<T> : IDisposable
        where T : unmanaged, INumber<T>
    {
        IEvent HostReadTo(Span<T> destination);
        IEvent DeviceReadTo(Span<T> destination);
        IEvent? ToDevice(bool blocking = true, IEvent[]? waitList = null);
        IEvent? ToHost(bool blocking = true, IEvent[]? waitList = null);
        IEvent CopyHostTo(IComputeBuffer<T> destination, bool blocking = true, IEvent[]? waitList = null);
        IEvent CopyDeviceTo(IComputeBuffer<T> destination, bool blocking = true, IEvent[]? waitList = null);
    }
}
