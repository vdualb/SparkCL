using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

public class Program : IDisposable
{
    private bool disposedValue;
    
    internal nint Handle { get; }

    unsafe public static Program CreateWithSource(
        Context context,
        string[] source
    ) {
        ErrorCodes err;
        var handle = OCL.CreateProgramWithSource(context.Handle, 1, source, null, (int *)&err);

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Failed to create CL program from source, code: ", err));
        }

        return new(handle);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="options">utf8 null terminated string</param>
    unsafe public void Build(
        ReadOnlySpan<byte> options
    )
    {
        ErrorCodes err;
        fixed (byte *o = options)
        {
            err = (ErrorCodes)OCL.BuildProgram(Handle, 0, null, o, null, null);
        }

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("OpenCL build failed, code: ", err));
        }
    }

    unsafe static public Program FromFilename(
        Context context,
        Device device,
        string fileName)
    {
        using var sr = new StreamReader(fileName);
        string clStr = sr.ReadToEnd();

        var program = CreateWithSource(context, [clStr]);

        var options = "-cl-kernel-arg-info"u8;

        try
        {
            program.Build(options);
        } catch (Exception)
        {
            string? build_log = program.GetBuildLog(device);

            //Console.WriteLine("Error in kernel: ");
            Console.WriteLine("=============== OpenCL Program Build Info ================");
            Console.WriteLine(build_log);
            Console.WriteLine("==========================================================");

            throw;
        }

        return program;
    }

    unsafe public string GetBuildLog(
        Device device
    ) {
        GetBuildInfo<byte>(
            device,
            ProgramBuildInfo.BuildLog,
            0, null, out var size_ret
        );

        byte[] logBytes = new byte[size_ret / sizeof(byte)];

        fixed (byte *p_infoBytes = logBytes)
        {
            GetBuildInfo(
                device,
                ProgramBuildInfo.BuildLog,
                size_ret, p_infoBytes,
                out _
            );
        }

        var len = Array.IndexOf(logBytes, (byte)0);
        return Encoding.UTF8.GetString(logBytes, 0, len);
    }

    unsafe private void GetBuildInfo<Y>(
        Device device,
        ProgramBuildInfo build_info,
        nuint info_size,
        Y *info_value,
        out nuint info_size_ret)
    where Y: unmanaged
    {
        var err = (ErrorCodes)OCL.GetProgramBuildInfo(
            Handle,
            device.Handle,
            build_info,
            info_size,
            info_value,
            out info_size_ret
        );

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode(
                $"Failed to get program build info ({build_info}), code: ",
                err
            ));
        }
    }
    
    unsafe private void GetInfo<Y>(
        ProgramInfo param_name,
        nuint param_value_size,
        Y* param_value,
        out nuint param_value_size_ret)
    where Y : unmanaged
    {
        var err = (ErrorCodes)OCL.GetProgramInfo(
            Handle,
            param_name,
            param_value_size,
            param_value,
            out param_value_size_ret
        );

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode(
                $"Failed to get program build info ({param_name}), code: ",
                err
            ));
        }
    }
    
    public unsafe nuint[] GetBinarySizes()
    {
        GetInfo<nuint>(
            ProgramInfo.BinarySizes,
            0, null, out var size_ret
        );

        nuint[] sizes = new nuint[size_ret / (nuint)sizeof(nuint)];

        fixed (nuint* p_sizes = sizes)
        {
            GetInfo(
                ProgramInfo.BinarySizes,
                size_ret, p_sizes,
                out _
            );
        }

        return sizes;
    }
    
    public unsafe byte[][] GetBinaries()
    {
        var sizes = GetBinarySizes();
        var binaries = new byte[sizes.Length][];
        var gch_binaries = new GCHandle[sizes.Length];
        var p_binaries = new byte* [sizes.Length];

        for (int i = 0; i < sizes.Length; i++)
        {
            binaries[i] = new byte[sizes[i]];
            gch_binaries[i] = GCHandle.Alloc(binaries[i], GCHandleType.Pinned);
            p_binaries[i] = (byte*)gch_binaries[i].AddrOfPinnedObject().ToPointer();
        }

        GetInfo<IntPtr>(
            ProgramInfo.Binaries,
            0, null,
            out var size_ret
        );

        fixed (byte** p_p_binaries = p_binaries)
        {
            GetInfo(
                ProgramInfo.Binaries,

                size_ret,
                (nint*)p_p_binaries,
                out _
            );
        }

        for (int i = 0; i < gch_binaries.Length; i++)
        {
            gch_binaries[i].Free();
        }

        return binaries;
    }

    Program(nint h)
    {
        Handle = h;
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            OCL.ReleaseProgram(Handle);
            disposedValue = true;
        }
    }

    ~Program()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
