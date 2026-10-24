// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;

using ManagedCommon;

namespace MouseWithoutBordersService
{
    /// <summary>
    /// Registers and removes the Mouse Without Borders service for the standalone build.
    /// </summary>
    /// <remarks>
    /// PowerToys registers this service from its native module host
    /// (<c>ModuleInterface/dllmain.cpp</c>: <c>register_service</c>). That host is not part of
    /// the standalone build, so the same registration is done here: create the service as
    /// <c>LocalSystem</c>, demand start, with the current user's local app data as its only
    /// argument, and grant interactive users the right to start it (the logon desktop cannot
    /// ask for elevation). The service name, start type and security descriptor mirror the
    /// upstream implementation so both installs remain interchangeable.
    /// </remarks>
    internal static class ServiceInstaller
    {
        internal const string ServiceName = "PowerToys.MWB.Service";

        private const string ApplicationExeName = "PowerToys.MouseWithoutBorders.exe";
        private const string LogPath = "\\MouseWithoutBorders\\Logs";

        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_QUERY_STATUS = 0x0004;
        private const uint SERVICE_QUERY_CONFIG = 0x0001;
        private const uint SERVICE_CHANGE_CONFIG = 0x0002;
        private const uint SERVICE_STOP = 0x0020;
        private const uint SERVICE_DELETE = 0x10000;

        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_DEMAND_START = 0x00000003;
        private const uint SERVICE_DISABLED = 0x00000004;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;
        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        private const uint SERVICE_CONTROL_STOP = 0x00000001;

        private const uint SERVICE_STOPPED = 0x00000001;
        private const uint SERVICE_STOP_PENDING = 0x00000003;

        private const uint DACL_SECURITY_INFORMATION = 0x00000004;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;
        private const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;

        /// <summary>
        /// Creates the service, or repairs an existing registration whose binary path or start
        /// type no longer matches this installation.
        /// </summary>
        internal static int Install()
        {
            InitializeLogging();

            string serviceExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(serviceExe) || !File.Exists(serviceExe))
            {
                Logger.LogError("Cannot determine the service executable path.");
                return 1;
            }

            string applicationExe = Path.Combine(Path.GetDirectoryName(serviceExe), ApplicationExeName);
            if (!File.Exists(applicationExe))
            {
                Logger.LogError($"Cannot install the service: {applicationExe} was not found next to it.");
                return 1;
            }

            // The service starts the application as LocalSystem, so it has to be told where the
            // interactive user's settings live.
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string binaryPath = $"\"{serviceExe}\" \"{EscapeDoubleQuotes(localAppData)}\"";

            IntPtr scm = OpenSCManagerW(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
            {
                Logger.LogError($"Cannot open the service control manager: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                return 1;
            }

            try
            {
                IntPtr service = OpenServiceW(scm, ServiceName, SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG);
                if (service != IntPtr.Zero)
                {
                    try
                    {
                        return UpdateExistingService(service, binaryPath);
                    }
                    finally
                    {
                        _ = CloseServiceHandle(service);
                    }
                }

                return CreateService(scm, binaryPath);
            }
            finally
            {
                _ = CloseServiceHandle(scm);
            }
        }

        /// <summary>
        /// Stops the service (when it is running) and removes its registration.
        /// </summary>
        internal static int Uninstall()
        {
            InitializeLogging();

            IntPtr scm = OpenSCManagerW(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
            {
                Logger.LogError($"Cannot open the service control manager: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                return 1;
            }

            try
            {
                IntPtr service = OpenServiceW(scm, ServiceName, SERVICE_STOP | SERVICE_DELETE | SERVICE_QUERY_STATUS);
                if (service == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == ERROR_SERVICE_DOES_NOT_EXIST)
                    {
                        Logger.LogInfo("The service is not installed.");
                        return 0;
                    }

                    Logger.LogError($"Cannot open the service: {new Win32Exception(error).Message}");
                    return 1;
                }

                try
                {
                    StopService(service);

                    if (!DeleteService(service))
                    {
                        Logger.LogError($"Cannot delete the service: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                        return 1;
                    }

                    Logger.LogInfo("The service was removed.");
                    return 0;
                }
                finally
                {
                    _ = CloseServiceHandle(service);
                }
            }
            finally
            {
                _ = CloseServiceHandle(scm);
            }
        }

        /// <summary>
        /// Starts or stops the registered service without requiring the caller to know the
        /// service control manager API.
        /// </summary>
        internal static int StartOrStop(bool start)
        {
            InitializeLogging();

            try
            {
                using var controller = new ServiceController(ServiceName);

                if (start)
                {
                    controller.Refresh();
                    if (controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                    {
                        Logger.LogInfo("The service is already running.");
                        return 0;
                    }

                    controller.Start();
                    Logger.LogInfo("The service was started.");
                }
                else
                {
                    controller.Refresh();
                    if (controller.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                    {
                        Logger.LogInfo("The service is already stopped.");
                        return 0;
                    }

                    controller.Stop();
                    Logger.LogInfo("The service was stopped.");
                }

                return 0;
            }
            catch (InvalidOperationException e)
            {
                Logger.LogError($"Cannot reach the service: {e.Message}");
                return 1;
            }
        }

        private static int UpdateExistingService(IntPtr service, string binaryPath)
        {
            uint startType = 0;

            if (QueryServiceConfig(service, out string existingPath, out startType))
            {
                bool pathMatches = string.Equals(existingPath, binaryPath, StringComparison.OrdinalIgnoreCase);

                if (!pathMatches)
                {
                    if (!ChangeServiceConfigW(service, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, binaryPath, null, IntPtr.Zero, null, null, null, null))
                    {
                        Logger.LogError($"Cannot update the service path: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                        return 1;
                    }

                    Logger.LogInfo("The service path was updated.");
                }

                if (startType == SERVICE_DISABLED)
                {
                    // A service marked for deletion cannot be re-enabled in place; report it so
                    // the caller can retry once Windows has finished deleting it.
                    if (!ChangeServiceConfigW(service, SERVICE_NO_CHANGE, SERVICE_DEMAND_START, SERVICE_NO_CHANGE, null, null, IntPtr.Zero, null, null, null, null))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ERROR_SERVICE_MARKED_FOR_DELETE)
                        {
                            Logger.LogError("The previous service is still being deleted. Restart the computer and run install again.");
                        }
                        else
                        {
                            Logger.LogError($"Cannot enable the service: {new Win32Exception(error).Message}");
                        }

                        return 1;
                    }

                    Logger.LogInfo("The service was re-enabled.");
                }

                if (pathMatches && startType != SERVICE_DISABLED)
                {
                    Logger.LogInfo("The service is already installed.");
                }

                return 0;
            }

            int queryError = Marshal.GetLastWin32Error();
            if (queryError == ERROR_SERVICE_MARKED_FOR_DELETE)
            {
                Logger.LogError("The previous service is still being deleted. Restart the computer and run install again.");
            }
            else
            {
                Logger.LogError($"Cannot read the service configuration: {new Win32Exception(queryError).Message}");
            }

            return 1;
        }

        private static int CreateService(IntPtr scm, string binaryPath)
        {
            IntPtr service = CreateServiceW(
                scm,
                ServiceName,
                ServiceName,
                SERVICE_ALL_ACCESS,
                SERVICE_WIN32_OWN_PROCESS,
                SERVICE_DEMAND_START,
                SERVICE_ERROR_NORMAL,
                binaryPath,
                null,
                IntPtr.Zero,
                null,
                null,
                null);

            if (service == IntPtr.Zero)
            {
                Logger.LogError($"Cannot create the service: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                return 1;
            }

            try
            {
                ApplyServiceSecurity(service);
                Logger.LogInfo($"The service was installed as \"{binaryPath}\".");
                return 0;
            }
            finally
            {
                _ = CloseServiceHandle(service);
            }
        }

        /// <summary>
        /// Grants interactive users the right to start the service. The logon desktop has no
        /// way to elevate, so without this the application could not start service mode there.
        /// </summary>
        private static void ApplyServiceSecurity(IntPtr service)
        {
            string sid = WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrEmpty(sid))
            {
                Logger.LogWarning("Cannot determine the current user's SID; the service will only be startable by administrators.");
                return;
            }

            if (!TryCreateSecurityDescriptor(BuildSecurityDescriptor(sid), out IntPtr descriptor))
            {
                Logger.LogError($"Cannot create the service security descriptor: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                return;
            }

            try
            {
                if (!SetServiceObjectSecurity(service, DACL_SECURITY_INFORMATION, descriptor))
                {
                    Logger.LogError($"Cannot set the service security: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                }
            }
            finally
            {
                _ = LocalFree(descriptor);
            }
        }

        /// <summary>
        /// The service DACL/SACL upstream applies in <c>generateSecurityDescriptor.h</c>, with the
        /// current user's SID so that user can start the service without elevation.
        /// </summary>
        internal static string BuildSecurityDescriptor(string userSid)
        {
            return "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)(A;;CR;;;AU)(A;;CCLCSWRPWPDTLOCRRC;;;PU)(A;;RPWPDTLO;;;" + userSid + ")S:(AU;FA;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;WD)";
        }

        /// <summary>
        /// Converts an SDDL string to a self-relative descriptor. Internal so the descriptor the
        /// installer actually uses can be exercised without a service handle.
        /// </summary>
        internal static bool TryCreateSecurityDescriptor(string sddl, out IntPtr descriptor)
        {
            return ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out descriptor, out _);
        }

        private static bool QueryServiceConfig(IntPtr service, out string binaryPath, out uint startType)
        {
            binaryPath = null;
            startType = 0;

            _ = QueryServiceConfigW(service, IntPtr.Zero, 0, out uint needed);
            if (needed == 0)
            {
                return false;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)needed);

            try
            {
                if (!QueryServiceConfigW(service, buffer, needed, out _))
                {
                    return false;
                }

                var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIGW>(buffer);
                binaryPath = Marshal.PtrToStringUni(config.lpBinaryPathName);
                startType = config.dwStartType;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void StopService(IntPtr service)
        {
            if (!ControlService(service, SERVICE_CONTROL_STOP, out SERVICE_STATUS status))
            {
                // Already stopped is fine; anything else is reported but does not prevent the
                // registration from being deleted.
                int error = Marshal.GetLastWin32Error();
                if (error != 1062 /* ERROR_SERVICE_NOT_ACTIVE */)
                {
                    Logger.LogWarning($"Cannot stop the service: {new Win32Exception(error).Message}");
                }

                return;
            }

            for (int i = 0; i < 10; i++)
            {
                if (!QueryServiceStatus(service, out status) || status.dwCurrentState != SERVICE_STOP_PENDING)
                {
                    break;
                }

                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void InitializeLogging()
        {
            try
            {
                ManagedCommon.Logger.InitializeLogger(LogPath);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[service] cannot initialize logging: {e.Message}");
            }
        }

        private static string EscapeDoubleQuotes(string value)
        {
            return value?.Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct QUERY_SERVICE_CONFIGW
        {
            public uint dwServiceType;
            public uint dwStartType;
            public uint dwErrorControl;
            public IntPtr lpBinaryPathName;
            public IntPtr lpLoadOrderGroup;
            public uint dwTagId;
            public IntPtr lpDependencies;
            public IntPtr lpServiceStartName;
            public IntPtr lpDisplayName;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManagerW(string machineName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenServiceW(IntPtr scManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateServiceW(
            IntPtr scManager,
            string serviceName,
            string displayName,
            uint desiredAccess,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPathName,
            string loadOrderGroup,
            IntPtr tagId,
            string dependencies,
            string serviceStartName,
            string password);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ChangeServiceConfigW(
            IntPtr service,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPathName,
            string loadOrderGroup,
            IntPtr tagId,
            string dependencies,
            string serviceStartName,
            string password,
            string displayName);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryServiceConfigW(IntPtr service, IntPtr config, uint bufferSize, out uint bytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool QueryServiceStatus(IntPtr service, out SERVICE_STATUS status);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ControlService(IntPtr service, uint control, out SERVICE_STATUS status);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DeleteService(IntPtr service);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceObjectSecurity(IntPtr service, uint securityInformation, IntPtr descriptor);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string stringSecurityDescriptor, uint stringSecurityDescriptorLength, out IntPtr securityDescriptor, out uint securityDescriptorSize);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr handle);
    }
}
