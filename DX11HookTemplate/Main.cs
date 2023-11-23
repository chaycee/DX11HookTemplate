using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DX11HookTemplate;

public class Main
{
     
    private static bool _isRunning = true;
    
    
    [UnmanagedCallersOnly(EntryPoint = "DllMain", CallConvs = new[] { typeof(CallConvStdcall) })]
    private static bool DllMain(IntPtr hModule, uint ulReasonForCall, IntPtr lpReserved)
    {
        switch (ulReasonForCall)
        {
            case 1:
                WinApi.AllocConsole();
            
                Task.Run(WorkerThread);
                break;
            default:
                break;
        }

        return true;
    }

    private static void WorkerThread()
    {
        
        while (_isRunning)
        {
            var message = Console.ReadLine();
            Thread.Sleep(100);
        }
    }
    
    
    
}
