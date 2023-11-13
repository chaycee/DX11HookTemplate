using System.Diagnostics;

namespace DX11HookTemplate.DirextX;

internal class Dirext3D
{
    internal Dirext3D(System.Diagnostics.Process targetProc)
    {
        TargetProcess = targetProc;

        UsingDirectX11 = TargetProcess.Modules.Cast<ProcessModule>().Any(m => m.ModuleName == "d3d11.dll");
        //TODO:throw exception if not found
        if (UsingDirectX11)
        {
            Device = (D3DDevice)new D3D11Device(targetProc);

            HookAddress = ((D3D11Device) Device).GetSwapVTableFuncAbsoluteAddress(Device.PresentVtableIndex);
        }

        else
        {
            throw new Exception("DirectX 11 not found!");
        }

    }

    internal System.Diagnostics.Process TargetProcess { get; }

    internal bool UsingDirectX11 { get; }

    internal IntPtr HookAddress { get; private set; }

    internal D3DDevice Device { get; }
}
