using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DX11HookTemplate.DirextX;
using MinHook;

namespace DX11HookTemplate;

public class Main
{

    #region Interop

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr hWnd, String text, String caption, uint type);

    #endregion
    
   
    

    
    

   
    
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
        var renderer = new Renderer();
        renderer.Init();
        while (_isRunning)
        {
            Console.WriteLine("Please Enter Message");
            var message = Console.ReadLine();
            if (message is not null)
                MessageBoxW(IntPtr.Zero, message, "This is a hook test", 0);

            Thread.Sleep(100);
        }
    }
    
    
    
}