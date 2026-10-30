// Read-only, current-process DXGI budget accounting. Diagnostic builds only. MIT.
#if CREST_RESOURCE_PROBE && UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class DriverMemory
        {
            public string status, adapter, luid;
            public int vendor, device, localHresult, nonLocalHresult;
            public SegmentMemory local, nonLocal;
        }
        [Serializable, StructLayout(LayoutKind.Sequential)] struct SegmentMemory
        {
            public ulong budget, currentUsage, availableForReservation, currentReservation;
        }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct AdapterDescription
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string description;
            public uint vendor, device, subsystem, revision;
            public UIntPtr dedicatedVideo, dedicatedSystem, sharedSystem;
            public uint luidLow; public int luidHigh;
            public uint flags;
        }
        [DllImport("dxgi.dll", CallingConvention=CallingConvention.StdCall)]
        static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumAdapters(IntPtr self, uint index, out IntPtr adapter);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDescription(IntPtr self, out AdapterDescription description);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QueryMemory(IntPtr self, uint node, uint segment, out SegmentMemory memory);
        static T ComMethod<T>(IntPtr instance, int slot) where T:class
            => Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot*IntPtr.Size), typeof(T)) as T;

        static DriverMemory ReadDriverMemory()
        {
            var result=new DriverMemory {status="adapter not found"};
            IntPtr factory=IntPtr.Zero;
            try
            {
                // Layouts and vtable slots checked against Windows SDK 10.0.22621.0.
                if(IntPtr.Size!=8||Marshal.SizeOf(typeof(AdapterDescription))!=312||Marshal.SizeOf(typeof(SegmentMemory))!=32)
                    throw new NotSupportedException("DXGI layout mismatch");
                var factoryId=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
                Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref factoryId,out factory));
                var enumerate=ComMethod<EnumAdapters>(factory,12);
                for(uint index=0;index<32;index++)
                {
                    IntPtr adapter=IntPtr.Zero, adapter3=IntPtr.Zero;
                    try
                    {
                        int hr=enumerate(factory,index,out adapter);
                        if(hr==unchecked((int)0x887A0002))break; // DXGI_ERROR_NOT_FOUND
                        Marshal.ThrowExceptionForHR(hr);
                        Marshal.ThrowExceptionForHR(ComMethod<GetDescription>(adapter,10)(adapter,out var description));
                        if(description.vendor!=(uint)SystemInfo.graphicsDeviceVendorID||description.device!=(uint)SystemInfo.graphicsDeviceID)continue;
                        result.adapter=description.description;result.vendor=(int)description.vendor;result.device=(int)description.device;
                        result.luid=description.luidHigh.ToString("X8")+description.luidLow.ToString("X8");
                        var adapterId=new Guid("645967A4-1392-4310-A798-8053CE3E93FD");
                        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(adapter,ref adapterId,out adapter3));
                        var query=ComMethod<QueryMemory>(adapter3,14);
                        result.localHresult=query(adapter3,0,0,out result.local);
                        result.nonLocalHresult=query(adapter3,0,1,out result.nonLocal);
                        result.status=result.localHresult==0&&result.nonLocalHresult==0?"ok":"query failed";
                        return result;
                    }
                    finally {if(adapter3!=IntPtr.Zero)Marshal.Release(adapter3);if(adapter!=IntPtr.Zero)Marshal.Release(adapter);}
                }
            }
            catch(Exception e){result.status=e.GetType().Name+": "+e.Message;}
            finally {if(factory!=IntPtr.Zero)Marshal.Release(factory);}
            return result;
        }
    }
}
#endif
