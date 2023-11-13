using System.Diagnostics;
using System.Runtime.InteropServices;
using MinHook;

// ReSharper disable InconsistentNaming

namespace DX11HookTemplate
{
    
    public sealed class Renderer 
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void VTableFuncDelegate(IntPtr instance);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        public delegate int DxgiSwapChainPresentDelegate(IntPtr swapChainPtr, int syncInterval, int flags);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        public delegate int DxgiSwapChainResizeTargetDelegate(
            IntPtr swapChainPtr, ref ModeDescription newTargetParameters);
        
        
    
    
        private static DxgiSwapChainPresentDelegate _dxgiSwapChainPresentDelegate_org;
        
        int DxgiSwapChainPresentDetour(IntPtr swapChainPtr, int syncInterval, int  flags)
        {
            Console.WriteLine("SwapChainPresent "+swapChainPtr.ToString("X"));
            Console.WriteLine("syncInterval "+syncInterval);
            Console.WriteLine("flags "+flags);
            return _dxgiSwapChainPresentDelegate_org(swapChainPtr, syncInterval, flags);
        }

        IntPtr D3DDevicePtr;
        
        const int DXGI_FORMAT_R8G8B8A8_UNORM = 0x1C;
        const int DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
        const int D3D11_SDK_VERSION = 7;
        const int D3D_DRIVER_TYPE_HARDWARE = 1;
        private IntPtr _device;
        private VTableFuncDelegate _deviceContextRelease;

        private VTableFuncDelegate _deviceRelease;

        IntPtr _myDxgiDll;

        private IntPtr _swapChain;
        private VTableFuncDelegate _swapchainRelease;
        private IntPtr _theirDxgiDll;
        public static IntPtr[] GetVTblAddresses(IntPtr pointer, int numberOfMethods)
        {
            return GetVTblAddresses(pointer, 0, numberOfMethods);
        }

        public static IntPtr[] GetVTblAddresses(IntPtr pointer, int startIndex, int numberOfMethods)
        {
            List<IntPtr> vtblAddresses = new List<IntPtr>();

            IntPtr vTable = Marshal.ReadIntPtr(pointer);
            for (int i = startIndex; i < startIndex + numberOfMethods; i++)
                vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size));

            return vtblAddresses.ToArray();
        }


        private CustomWindow window = new CustomWindow("D3D11HookTemplate");
        public const int DXGI_SWAPCHAIN_METHOD_COUNT = 18;
        private HookEngine Engine = new HookEngine();
        public void Init()
        {
            bool ihook = false;
            do
            {
                InitD3D(out D3DDevicePtr);
                if (D3DDevicePtr != IntPtr.Zero)
                {
                    ihook= true;
                    var hookAddress = GetSwapVTableFuncAbsoluteAddress(VTableIndexes.DXGISwapChainPresent);
                    // DXGISwapChainVTbl vtbl = DXGISwapChainVTbl.Present;
                    //
                    // var hookAddress= GetVTblAddresses(_swapChain, DXGI_SWAPCHAIN_METHOD_COUNT)[(int)vtbl];
                    if (hookAddress != IntPtr.Zero)
                    {
                        _dxgiSwapChainPresentDelegate_org = Engine.CreateHook(hookAddress, new DxgiSwapChainPresentDelegate(DxgiSwapChainPresentDetour));
                        Engine.EnableHooks();
                        Console.WriteLine("Hooked");
                    }else
                    {
                        Console.WriteLine("Hook failed");
                    }
                }
            }while (!ihook);

            while (true)
            {
                Thread.Sleep(10);
            }
        }
        
        private bool InitD3D(out IntPtr d3DDevicePtr)
        {
            LoadDxgiDll();
            var scd = new SwapChainDescription
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription { Format = DXGI_FORMAT_R8G8B8A8_UNORM },
                Usage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                OutputHandle = window.m_hwnd,
                SampleDescription = new SampleDescription { Count = 1 },
                IsWindowed = true
            };

            unsafe
            {
                var pSwapChain = IntPtr.Zero;
                var pDevice = IntPtr.Zero;
                var pImmediateContext = IntPtr.Zero;

                var ret = D3D11CreateDeviceAndSwapChain((void*)IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE,
                    (void*)IntPtr.Zero, 0, (void*)IntPtr.Zero, 0, D3D11_SDK_VERSION, &scd, &pSwapChain, &pDevice,
                    (void*)IntPtr.Zero, &pImmediateContext);

                _swapChain = pSwapChain;
                _device = pDevice;
                d3DDevicePtr = pImmediateContext;

                if (ret >= 0)
                {
                    var vTableFuncAddress = GetVTableFuncAddress(_swapChain, VTableIndexes.DXGISwapChainRelease);
                    _swapchainRelease = Marshal.GetDelegateForFunctionPointer<VTableFuncDelegate>(vTableFuncAddress);

                    var deviceptr = GetVTableFuncAddress(_device, VTableIndexes.D3D11DeviceRelease);
                    _deviceRelease = Marshal.GetDelegateForFunctionPointer<VTableFuncDelegate>(deviceptr);

                    var contex = GetVTableFuncAddress(d3DDevicePtr, VTableIndexes.D3D11DeviceContextRelease);
                    _deviceContextRelease = Marshal.GetDelegateForFunctionPointer<VTableFuncDelegate>(contex);
                }
            }
            window.Dispose();
            return true;
        }

        void LoadDxgiDll()
        {
            _myDxgiDll = WinApi.GetModuleHandle("dxgi.dll");
            if (_myDxgiDll == IntPtr.Zero)
            {
                throw new Exception("Could not load dxgi.dll");
            }

            _theirDxgiDll =
                Client.Self.Modules.Cast<ProcessModule>().First(m => m.ModuleName == "dxgi.dll").BaseAddress;
        }

        public unsafe IntPtr GetSwapVTableFuncAbsoluteAddress(int funcIndex)
        {
            var pointer = *(IntPtr*)(void*)_swapChain;
            pointer = *(IntPtr*)(void*)(pointer + funcIndex * IntPtr.Size);

            var offset = new IntPtr(pointer.ToInt64() - _myDxgiDll.ToInt64());
            return new IntPtr(_theirDxgiDll.ToInt64() + offset.ToInt64());
        }
        

        [DllImport("d3d11.dll")]
        public static extern unsafe int D3D11CreateDeviceAndSwapChain(void* pAdapter, int driverType, void* Software,
            int flags, void* pFeatureLevels,
            int FeatureLevels, int SDKVersion,
            void* pSwapChainDesc, void* ppSwapChain,
            void* ppDevice, void* pFeatureLevel,
            void* ppImmediateContext);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rational
        {
            readonly int Numerator;
            readonly int Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ModeDescription
        {
            readonly int Width;
            readonly int Height;
            readonly Rational RefreshRate;
            public int Format;
            readonly int ScanlineOrdering;
            readonly int Scaling;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SampleDescription
        {
            public int Count;
            readonly int Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SwapChainDescription
        {
            public ModeDescription ModeDescription;
            public SampleDescription SampleDescription;
            public int Usage;
            public int BufferCount;
            public IntPtr OutputHandle;
            [MarshalAs(UnmanagedType.Bool)] public bool IsWindowed;

            readonly int SwapEffect;
            readonly int Flags;
        }

        public struct VTableIndexes
        {
            public const int DXGISwapChainRelease = 2;
            public const int D3D11DeviceRelease = 2;
            public const int D3D11DeviceContextRelease = 2;
            public const int DXGISwapChainPresent = 8;
            public const int D3D11DeviceContextBegin = 0x1B;
            public const int D3D11DeviceContextEnd = 0x1C;
        }
        unsafe IntPtr GetVTableFuncAddress(IntPtr obj, int funcIndex)
        {
            var pointer = *(IntPtr*)(void*)obj;
            return *(IntPtr*)(void*)(pointer + funcIndex * IntPtr.Size);
        }
    }
    
}

// ReSharper restore InconsistentNaming