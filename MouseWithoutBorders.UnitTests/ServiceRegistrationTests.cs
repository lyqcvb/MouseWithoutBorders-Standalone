// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Windows.Forms;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests;

/// <summary>
/// Covers the parts of the self-service installation that do not need elevation: recognizing
/// the registration that belongs to this copy, and the wiring that makes the setting drive the
/// service mode. The registration itself is exercised by <c>ServiceSecurityDescriptorTests</c>
/// and by installing once on a real machine.
/// </summary>
[TestClass]
public sealed class ServiceRegistrationTests
{
    private const string ServiceName = "PowerToys.MWB.Service";

    [TestMethod]
    public void ServiceNameMatchesTheServiceProject()
    {
        var installer = ReadRepositorySource(@"App\Service\ServiceInstaller.cs");

        Assert.AreEqual(ServiceName, ServiceRegistration.ServiceName);
        StringAssert.Contains(installer, $"ServiceName = \"{ServiceName}\"");
    }

    [TestMethod]
    public void ServiceExecutableNameMatchesTheBuiltAssembly()
    {
        var serviceProject = ReadRepositorySource(@"App\Service\MouseWithoutBordersService.csproj");

        StringAssert.Contains(serviceProject, "<AssemblyName>PowerToys.MouseWithoutBordersService</AssemblyName>");
        Assert.AreEqual("PowerToys.MouseWithoutBordersService.exe", ServiceRegistration.ServiceExeName);
    }

    [TestMethod]
    public void ServiceExecutableSitsNextToTheApplication()
    {
        var applicationDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        Assert.AreEqual(
            Path.Combine(applicationDirectory!, ServiceRegistration.ServiceExeName),
            ServiceRegistration.ServiceExePath);
    }

    [DataTestMethod]
    [DataRow("\"{0}\" \"C:\\Users\\someone\\AppData\\Local\"")]
    [DataRow("\"{0}\"")]
    public void RegisteredPathForThisCopyIsRecognized(string format)
    {
        string binaryPath = string.Format(CultureInfo.InvariantCulture, format, ServiceRegistration.ServiceExePath);

        Assert.AreEqual(ServiceInstallState.InstalledForThisPath, StateFor(binaryPath));
        Assert.IsTrue(ServiceRegistration.IsThisInstallation(binaryPath));
    }

    [TestMethod]
    public void RegisteredPathIsComparedCaseInsensitively()
    {
        string binaryPath = "\"" + ServiceRegistration.ServiceExePath.ToUpperInvariant() + "\" \"C:\\Users\\someone\\AppData\\Local\"";

        Assert.IsTrue(ServiceRegistration.IsThisInstallation(binaryPath));
    }

    [TestMethod]
    public void RegisteredPathInAnotherDirectoryIsNotThisCopy()
    {
        string otherDirectory = Path.Combine(Path.GetDirectoryName(ServiceRegistration.ServiceExePath)!, "old");
        string binaryPath = "\"" + Path.Combine(otherDirectory, ServiceRegistration.ServiceExeName) + "\" \"C:\\Users\\someone\\AppData\\Local\"";

        Assert.IsFalse(ServiceRegistration.IsThisInstallation(binaryPath));
        Assert.AreEqual(ServiceInstallState.InstalledForAnotherPath, StateFor(binaryPath));
    }

    [TestMethod]
    public void RegisteredPowerToysInstallIsNotThisCopy()
    {
        string binaryPath = "\"C:\\Program Files\\PowerToys\\PowerToys.MouseWithoutBordersService.exe\" \"C:\\Users\\someone\\AppData\\Local\"";

        Assert.IsFalse(ServiceRegistration.IsThisInstallation(binaryPath));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\"")]
    [DataRow("\"\"")]
    public void MissingOrEmptyRegisteredPathIsNotThisCopy(string? binaryPath)
    {
        Assert.IsFalse(ServiceRegistration.IsThisInstallation(binaryPath));
    }

    [TestMethod]
    public void ExecutableIsReadFromAQuotedCommandLine()
    {
        Assert.AreEqual(
            @"C:\Program Files\Mouse Without Borders\PowerToys.MouseWithoutBordersService.exe",
            ServiceRegistration.GetExecutableFromCommandLine("\"C:\\Program Files\\Mouse Without Borders\\PowerToys.MouseWithoutBordersService.exe\" \"C:\\Users\\someone\\AppData\\Local\""));
    }

    [TestMethod]
    public void ExecutableIsReadFromAnUnquotedCommandLine()
    {
        Assert.AreEqual(
            @"C:\MWB\PowerToys.MouseWithoutBordersService.exe",
            ServiceRegistration.GetExecutableFromCommandLine(@"C:\MWB\PowerToys.MouseWithoutBordersService.exe -k"));
    }

    [TestMethod]
    public void ServiceModeFollowsTheSettingForPlainInteractiveLaunchesOnly()
    {
        var program = ReadRepositorySource(@"App\Class\Program.cs");

        // A SYSTEM instance started by the service must not try to start the service again,
        // or the program would recurse instead of running; and a launch that names a desktop
        // (the restart watchdog, the helper) must not consume the desktop argument into a
        // service start either.
        StringAssert.Contains(program, "firstArg == ServiceModeArg");
        StringAssert.Contains(program, "!runningAsSystem && firstArg.Length == 0 && Setting.Values.UseService");
    }

    [TestMethod]
    public void StartupPromptSkipsServiceModeAndLogonDesktop()
    {
        var registration = ReadRepositorySource(@"App\Core\ServiceRegistration.cs");
        var prompt = Section(registration, "internal static void PromptOnStartupIfNeeded()", "internal static void OfferRestart()");

        StringAssert.Contains(prompt, "if (!Common.RunWithNoAdminRight)");
        StringAssert.Contains(prompt, "if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)");
        StringAssert.Contains(prompt, "if (Setting.Values.UseService || Setting.Values.ServicePromptDismissed)");
        StringAssert.Contains(prompt, "if (Setting.Values.FirstRun)");
    }

    [TestMethod]
    public void StartupPromptIsSkippedWhenPolicyForbidsServiceMode()
    {
        var registration = ReadRepositorySource(@"App\Core\ServiceRegistration.cs");
        var prompt = Section(registration, "internal static void PromptOnStartupIfNeeded()", "internal static void OfferRestart()");

        // Under the policy the UseService getter always returns false, so an accepted prompt
        // would install a service the program still would not use.
        int policy = prompt.IndexOf("Setting.Values.AllowServiceModeIsGpoConfigured", System.StringComparison.Ordinal);
        int dialog = prompt.IndexOf("MessageBox.Show", System.StringComparison.Ordinal);

        Assert.IsTrue(policy >= 0 && dialog > policy, "The policy check must run before the prompt.");
    }

    [TestMethod]
    public void AlreadyRunningServiceCountsAsStarted()
    {
        // ServiceController wraps the Win32 error, so a check on the outer exception alone would
        // treat "already running" as a failure and switch service mode off.
        Assert.IsTrue(MouseWithoutBorders.Class.Program.IsServiceAlreadyRunning(
            new InvalidOperationException("x", new System.ComponentModel.Win32Exception(1056))));
        Assert.IsTrue(MouseWithoutBorders.Class.Program.IsServiceAlreadyRunning(new System.ComponentModel.Win32Exception(1056)));
        Assert.IsFalse(MouseWithoutBorders.Class.Program.IsServiceAlreadyRunning(new InvalidOperationException("Cannot open service")));
        Assert.IsFalse(MouseWithoutBorders.Class.Program.IsServiceAlreadyRunning(new System.ComponentModel.Win32Exception(5)));
    }

    [TestMethod]
    public void RestartReleasesTheSingleInstanceEventBeforeTheServiceCanStartACopy()
    {
        var registration = ReadRepositorySource(@"App\Core\ServiceRegistration.cs");
        var restart = Section(registration, "internal static void RestartApplication()", "internal static string DescribeFailure(");

        // The SYSTEM copy the service starts for the default desktop asserts the same
        // per-desktop event as this instance (same desktop, same session); leaving it taken
        // would make that copy kill itself and the restart would leave nothing running.
        int release = restart.IndexOf("MachineStuff.oneInstanceCheck?.Close();", StringComparison.Ordinal);
        int start = restart.IndexOf("Process.Start(Application.ExecutablePath)", StringComparison.Ordinal);
        int quit = restart.IndexOf("Common.MainForm.Quit(", StringComparison.Ordinal);

        Assert.IsTrue(release >= 0 && start > release && quit > start, "The event must be released before the replacement starts and before this instance quits.");
        StringAssert.Contains(restart, "MachineStuff.AssertOneInstancePerDesktopSession();");
    }

    [TestMethod]
    public void StartupPromptIsShownOnceFromTheMainForm()
    {
        var screen = ReadRepositorySource(@"App\Form\frmScreen.cs");
        var shown = screen[screen.IndexOf("private void FrmScreen_Shown(", System.StringComparison.Ordinal)..];

        StringAssert.Contains(shown, "ServiceRegistration.PromptOnStartupIfNeeded();");
    }

    [TestMethod]
    public void ServiceIsInstalledThroughTheServiceExecutable()
    {
        var registration = ReadRepositorySource(@"App\Core\ServiceRegistration.cs");

        // The registration itself stays in the service project; the application only runs its
        // maintenance commands through UAC, off the UI thread.
        StringAssert.Contains(registration, "Verb = \"runas\"");
        StringAssert.Contains(registration, "UseShellExecute = true");
        StringAssert.Contains(registration, "internal static void Install(Action<ServiceInstallResult> onCompleted) => MaintenanceCommandBlocking(\"install\", onCompleted);");
        StringAssert.Contains(registration, "internal static void Uninstall(Action<ServiceInstallResult> onCompleted) => MaintenanceCommandBlocking(\"uninstall\", onCompleted);");

        // No write API: the application must not be able to create, change or delete services
        // on its own, even if it is later run elevated for some other reason.
        foreach (var writeApi in new[] { "CreateServiceW", "ChangeServiceConfigW", "DeleteService", "ControlService", "SetServiceObjectSecurity" })
        {
            Assert.IsFalse(registration.Contains(writeApi, System.StringComparison.Ordinal), $"{writeApi} must stay in the service project.");
        }
    }

    private static ServiceInstallState StateFor(string binaryPath)
    {
        return ServiceRegistration.IsThisInstallation(binaryPath)
            ? ServiceInstallState.InstalledForThisPath
            : ServiceInstallState.InstalledForAnotherPath;
    }

    /// <summary>
    /// The source between two markers, so an assertion cannot accidentally match a different
    /// method further down the file.
    /// </summary>
    private static string Section(string source, string start, string end)
    {
        int first = source.IndexOf(start, System.StringComparison.Ordinal);
        Assert.IsTrue(first >= 0, $"Cannot find '{start}'.");

        int next = source.IndexOf(end, first, System.StringComparison.Ordinal);
        Assert.IsTrue(next > first, $"Cannot find '{end}' after '{start}'.");

        return source[first..next];
    }

    private static string ReadRepositorySource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, $"Cannot find {relativePath} above {AppContext.BaseDirectory}.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
