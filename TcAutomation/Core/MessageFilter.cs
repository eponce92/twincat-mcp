using System;
using System.Runtime.InteropServices;

namespace TcAutomation.Core
{
    /// <summary>
    /// COM Message Filter to handle "Application is busy" errors.
    /// 
    /// When Visual Studio/TwinCAT is processing a previous COM call, it may reject
    /// new calls with RPC_E_CALL_REJECTED. This filter automatically retries
    /// rejected calls instead of failing immediately.
    /// 
    /// Usage:
    ///   MessageFilter.Register();
    ///   try { /* COM operations */ }
    ///   finally { MessageFilter.Revoke(); }
    /// </summary>
    [ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    public class MessageFilter : IOleMessageFilter
    {
        private const int SERVERCALL_ISHANDLED = 0;
        private const int PENDINGMSG_WAITDEFPROCESS = 2;
        private const int SERVERCALL_RETRYLATER = 2;

        [DllImport("ole32.dll")]
        private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);

        /// <summary>
        /// Register the message filter for the current thread.
        /// Must be called from STA thread before any COM operations.
        /// </summary>
        public static void Register()
        {
            IOleMessageFilter newFilter = new MessageFilter();
            IOleMessageFilter? oldFilter;
            int hr = CoRegisterMessageFilter(newFilter, out oldFilter);
            if (hr != 0)
            {
                Console.Error.WriteLine($"Warning: CoRegisterMessageFilter returned 0x{hr:X8}");
            }
        }

        /// <summary>
        /// Revoke the message filter. Call this when done with COM operations.
        /// </summary>
        public static void Revoke()
        {
            IOleMessageFilter? oldFilter;
            CoRegisterMessageFilter(null, out oldFilter);
        }

        int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
        {
            // Allow all incoming calls
            return SERVERCALL_ISHANDLED;
        }

        int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            if (dwRejectType == SERVERCALL_RETRYLATER)
            {
                // Server is busy, retry after a short delay
                // Return value 0-99 means retry immediately after that many milliseconds
                // Return value >= 100 means retry after 100ms
                // Return value -1 means don't retry
                return 99; // Retry after ~99ms
            }
            return -1; // Don't retry other rejection types
        }

        int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
        {
            // Continue waiting for the call to complete
            return PENDINGMSG_WAITDEFPROCESS;
        }
    }
}
