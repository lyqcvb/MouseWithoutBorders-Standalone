// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.Core;

internal enum ServiceInstallState
{
    /// <summary>No service with our name is registered.</summary>
    NotInstalled,

    /// <summary>The registered service runs the executable shipped next to this application.</summary>
    InstalledForThisPath,

    /// <summary>Another copy of the program (or a PowerToys install) registered the service.</summary>
    InstalledForAnotherPath,

    /// <summary>The query failed; the caller must not draw conclusions from it.</summary>
    Unknown,
}

internal enum ServiceInstallResult
{
    Succeeded,
    Cancelled,
    Failed,
    NotAvailable,
}

/// <summary>
/// Registers the session-0 service on behalf of the user, without a command line.
/// </summary>
/// <remarks>
/// Registering a service needs administrator rights, so the work itself stays in
/// <c>App/Service/ServiceInstaller.cs</c>: this type only queries the current registration
/// (readable without elevation) and runs the service executable's <c>install</c>/<c>uninstall</c>
/// maintenance commands through UAC. Keeping the registration in one place means both entry
/// points produce the same service name, start type and security descriptor.
/// </remarks>
internal static class ServiceRegistration
{
    internal const string ServiceName = "PowerToys.MWB.Service";
    internal const string ServiceExeName = "PowerToys.MouseWithoutBordersService.exe";

    private const int ErrorCancelled = 1223;
    private const int ErrorServiceDoesNotExist = 1060;
    private const int InstallTimeoutMilliseconds = 60_000;

    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SERVICE_QUERY_CONFIG = 0x0001;
    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const uint SERVICE_STOPPED = 0x00000001;

    internal static string ServiceExePath =>
        Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty, ServiceExeName);

    /// <summary>
    /// Reads the registration without elevation. The service security descriptor grants
    /// interactive users SERVICE_QUERY_CONFIG and SERVICE_QUERY_STATUS for exactly this.
    /// </summary>
    internal static ServiceInstallState Query(out string registeredBinaryPath, out bool running)
    {
        registeredBinaryPath = null;
        running = false;

        IntPtr scm = OpenSCManagerW(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
        {
            Logger.Log($"Cannot open the service control manager: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            return ServiceInstallState.Unknown;
        }

        try
        {
            IntPtr service = OpenServiceW(scm, ServiceName, SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS);
            if (service == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorServiceDoesNotExist)
                {
                    return ServiceInstallState.NotInstalled;
                }

                Logger.Log($"Cannot open the service: {new Win32Exception(error).Message}");
                return ServiceInstallState.Unknown;
            }

            try
            {
                running = QueryRunning(service);
                if (!QueryBinaryPath(service, out registeredBinaryPath))
                {
                    return ServiceInstallState.Unknown;
                }

                return IsThisInstallation(registeredBinaryPath)
                    ? ServiceInstallState.InstalledForThisPath
                    : ServiceInstallState.InstalledForAnotherPath;
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
    /// Whether a registered binary path (which carries the user's local app data as its only
    /// argument) points at the service executable next to this application.
    /// </summary>
    internal static bool IsThisInstallation(string registeredBinaryPath)
    {
        string registeredExe = GetExecutableFromCommandLine(registeredBinaryPath);
        if (string.IsNullOrEmpty(registeredExe))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(registeredExe).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(ServiceExePath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the executable from a service command line. The first token is quoted because
    /// the installation path may contain spaces.
    /// </summary>
    internal static string GetExecutableFromCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        string command = commandLine.Trim();
        if (command[0] == '"')
        {
            int closing = command.IndexOf('"', 1);
            return closing > 1 ? command.Substring(1, closing - 1) : null;
        }

        // Unquoted paths cannot contain spaces, so the first space ends the executable.
        int space = command.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? command[..space] : command;
    }

    /// <summary>
    /// Registers the service through the service executable under UAC, then reports the outcome
    /// on the UI thread.
    /// </summary>
    internal static void Install(Action<ServiceInstallResult> onCompleted) => MaintenanceCommandBlocking("install", onCompleted);

    /// <summary>
    /// Removes the registration, stopping the service first. See <see cref="Install"/>.
    /// </summary>
    internal static void Uninstall(Action<ServiceInstallResult> onCompleted) => MaintenanceCommandBlocking("uninstall", onCompleted);

    /// <summary>
    /// Starts the service executable under UAC and waits for it to finish. Never call this on
    /// the UI thread; <see cref="MaintenanceCommandBlocking"/> is the background wrapper.
    /// </summary>
    private static ServiceInstallResult RunMaintenanceCommand(string command)
    {
        string serviceExe = ServiceExePath;
        if (!File.Exists(serviceExe))
        {
            Logger.Log($"Cannot run the service '{command}' command: {serviceExe} was not found.");
            return ServiceInstallResult.NotAvailable;
        }

        var startInfo = new ProcessStartInfo(serviceExe, command)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Logger.Log($"The service '{command}' command did not start.");
                return ServiceInstallResult.Failed;
            }

            if (!process.WaitForExit(InstallTimeoutMilliseconds))
            {
                Logger.Log($"The service '{command}' command timed out.");
                return ServiceInstallResult.Failed;
            }

            if (process.ExitCode == 0)
            {
                return ServiceInstallResult.Succeeded;
            }

            Logger.Log($"The service '{command}' command failed with exit code {process.ExitCode}.");
            return ServiceInstallResult.Failed;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            Logger.Log($"The service '{command}' command was cancelled at the elevation prompt.");
            return ServiceInstallResult.Cancelled;
        }
        catch (Exception e)
        {
            Logger.Log(e);
            return ServiceInstallResult.Failed;
        }
    }

    /// <summary>
    /// Runs a maintenance command from a background thread and delivers the result on the UI
    /// thread. Both prompts the user can trigger (startup and the settings check box) go
    /// through here so neither blocks the message loop while UAC is on screen.
    /// </summary>
    private static void MaintenanceCommandBlocking(string command, Action<ServiceInstallResult> onCompleted)
    {
        var thread = new System.Threading.Thread(() =>
        {
            ServiceInstallResult result = RunMaintenanceCommand(command);
            Common.DoSomethingInUIThread(() => onCompleted?.Invoke(result));
        })
        {
            IsBackground = true,
            Name = $"Service {command}",
        };

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
    }

    /// <summary>
    /// Asks once whether to install the service. Runs from <c>FrmScreen_Shown</c>, so the
    /// dialogs are modal to the hidden main form and the timer already running there keeps
    /// the rest of the program idle until the user answers.
    /// </summary>
    internal static void PromptOnStartupIfNeeded()
    {
        // The processes the service starts run as SYSTEM and are already in service mode.
        if (!Common.RunWithNoAdminRight)
        {
            return;
        }

        if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)
        {
            return;
        }

        if (Setting.Values.UseService || Setting.Values.ServicePromptDismissed)
        {
            return;
        }

        // A policy that forbids service mode makes the setting read back as off, so installing
        // the service here would only produce a prompt whose answer changes nothing.
        if (Setting.Values.AllowServiceModeIsGpoConfigured)
        {
            return;
        }

        // The pairing wizard owns the screen; ask on a later start instead.
        if (Setting.Values.FirstRun)
        {
            return;
        }

        ServiceInstallState state = Query(out string registeredPath, out _);

        // A service registered by this copy (for example by an earlier start whose settings
        // were reset) only has to be switched on, no elevation needed.
        if (state == ServiceInstallState.InstalledForThisPath)
        {
            Setting.Values.UseService = true;
            return;
        }

        if (state == ServiceInstallState.Unknown || !File.Exists(ServiceExePath))
        {
            return;
        }

        // The service name is shared with PowerToys and with other copies of this build, so a
        // foreign registration is shown before the install is allowed to re-point it.
        string message = state == ServiceInstallState.InstalledForAnotherPath
            ? "A " + Application.ProductName + " service is installed for another copy:\r\n\r\n" +
              (registeredPath ?? string.Empty) + "\r\n\r\n" +
              "Point it at this copy and use it? Installing needs administrator approval; Windows will ask once."
            : "Run " + Application.ProductName + " as a service?\r\n\r\n" +
              "The service keeps the mouse and keyboard shared on the logon and lock screens. " +
              "Installing it needs administrator approval; Windows will ask once.\r\n\r\n" +
              "You can change this later in Settings > Other Options.";

        if (MessageBox.Show(message, Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) != DialogResult.Yes)
        {
            Setting.Values.ServicePromptDismissed = true;
            return;
        }

        Install(result =>
        {
            if (result == ServiceInstallResult.Succeeded)
            {
                Setting.Values.UseService = true;
                OfferRestart();
                return;
            }

            // A failure may be transient (the previous service still being deleted), so it is
            // reported but not remembered; a refusal is remembered so we stop asking.
            if (result == ServiceInstallResult.Cancelled)
            {
                Setting.Values.ServicePromptDismissed = true;
            }

            Common.ShowToolTip(DescribeFailure(result, "installed"), 10000, ToolTipIcon.Warning, forceEvenIfHidingOldUI: true);
        });
    }

    /// <summary>
    /// Restarts the application so the service takes over. The window title is changed first
    /// because <c>MachineStuff.CheckSecondInstance</c> looks the old instance up by that title.
    /// </summary>
    internal static void OfferRestart()
    {
        string message =
            "The service is ready. Restart " + Application.ProductName + " now to use it?\r\n\r\n" +
            "Until you restart, this window keeps running as before.";

        if (MessageBox.Show(message, Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) != DialogResult.Yes)
        {
            Common.ShowToolTip("The service mode will be used the next time " + Application.ProductName + " starts.", 8000, forceEvenIfHidingOldUI: true);
            return;
        }

        RestartApplication();
    }

    internal static void RestartApplication()
    {
        if (Common.MainForm == null)
        {
            return;
        }

        // Let the replacement start before this instance starts listening for a second one: the
        // SYSTEM copy the service starts looks the running instance up by window title.
        Common.MainForm.Text = Setting.Values.MyIdEx;

        // The SYSTEM copy the service starts lands on this desktop and asserts the same
        // per-desktop single-instance event, so it has to be free before the service is asked to
        // start anything. The restart watchdog in Common.ExecuteAndTrace does the same.
        MachineStuff.oneInstanceCheck?.Close();

        try
        {
            _ = Process.Start(Application.ExecutablePath);
        }
        catch (Exception e)
        {
            Logger.Log(e);
            Common.MainForm.Text = Setting.Values.MyID;
            MachineStuff.AssertOneInstancePerDesktopSession();
            _ = MessageBox.Show("Cannot restart " + Application.ProductName + ": " + e.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Common.MainForm.Quit(true, false);
    }

    internal static string DescribeFailure(ServiceInstallResult result, string action)
    {
        switch (result)
        {
            case ServiceInstallResult.Cancelled:
                return "Administrator approval was cancelled, so the service was not " + action + ".";
            case ServiceInstallResult.NotAvailable:
                return "The service executable was not found next to " + Application.ProductName + ", so the service was not " + action + ".";
            default:
                return "The service could not be " + action + ". See the " + Application.ProductName + " log for details.";
        }
    }

    private static bool QueryRunning(IntPtr service)
    {
        if (!QueryServiceStatus(service, out SERVICE_STATUS status))
        {
            return false;
        }

        return status.dwCurrentState != SERVICE_STOPPED;
    }

    private static bool QueryBinaryPath(IntPtr service, out string binaryPath)
    {
        binaryPath = null;

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
            return !string.IsNullOrEmpty(binaryPath);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
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
    private static extern bool QueryServiceConfigW(IntPtr service, IntPtr config, uint bufferSize, out uint bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatus(IntPtr service, out SERVICE_STATUS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
