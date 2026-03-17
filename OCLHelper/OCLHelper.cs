using System.Net.Http.Headers;
using Silk.NET.OpenCL;

// обёртка над Silk.NET.OpenCL для удобного использования в csharp
namespace OCLHelper;

internal static class CLHandle
{
    static public CL OCL = CL.GetApi();
    static public string AppendErrCode(string description, ErrorCodes code)
    {
        return description + $"{code}({(int)code})";
    }
}

public interface IMemObject<T>
{
    public nint Handle { get; }
}
