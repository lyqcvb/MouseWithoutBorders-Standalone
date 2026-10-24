// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

/// <summary>
/// The helper and the service are started by name from the application, so their assembly
/// names and the service name have to stay in sync across projects. These checks read the
/// sources because the processes are never started by the tests.
/// </summary>
[TestClass]
public sealed class ProcessWiringTests
{
    private const string ServiceName = "PowerToys.MWB.Service";

    [TestMethod]
    public void HelperIsStartedByItsAssemblyName()
    {
        var helperProject = ReadRepositorySource(@"App\Helper\MouseWithoutBordersHelper.csproj");
        var helper = ReadRepositorySource(@"App\Core\Helper.cs");

        StringAssert.Contains(helperProject, "<AssemblyName>PowerToys.MouseWithoutBordersHelper</AssemblyName>");
        StringAssert.Contains(helper, "HelperProcessName = \"PowerToys.MouseWithoutBordersHelper\"");
        StringAssert.Contains(helper, "{HelperProcessName}.exe");
    }

    [TestMethod]
    public void HelperLaunchesTheApplicationByItsAssemblyName()
    {
        var applicationProject = ReadRepositorySource(@"App\MouseWithoutBorders.csproj");
        var helperProgram = ReadRepositorySource(@"App\Helper\Program.cs");

        StringAssert.Contains(applicationProject, "<AssemblyName>PowerToys.MouseWithoutBorders</AssemblyName>");
        StringAssert.Contains(helperProgram, "\\PowerToys.MouseWithoutBorders.exe");
        Assert.IsFalse(helperProgram.Contains("\\MouseWithoutBorders.exe", StringComparison.Ordinal), "The application executable is PowerToys.MouseWithoutBorders.exe.");
    }

    [TestMethod]
    public void ServiceIsStartedByItsRegisteredName()
    {
        var serviceProject = ReadRepositorySource(@"App\Service\MouseWithoutBordersService.csproj");
        var installer = ReadRepositorySource(@"App\Service\ServiceInstaller.cs");
        var registration = ReadRepositorySource(@"App\Core\ServiceRegistration.cs");
        var serviceStarter = ReadRepositorySource(@"App\Core\Service.cs");
        var program = ReadRepositorySource(@"App\Class\Program.cs");
        var inputSimulation = ReadRepositorySource(@"App\Class\InputSimulation.cs");

        StringAssert.Contains(serviceProject, "<AssemblyName>PowerToys.MouseWithoutBordersService</AssemblyName>");
        StringAssert.Contains(installer, $"ServiceName = \"{ServiceName}\"");
        StringAssert.Contains(registration, $"ServiceName = \"{ServiceName}\"");

        // The application cannot reference the service assembly, so the name lives in one
        // constant there and everything else has to use it.
        StringAssert.Contains(serviceStarter, "serviceName = ServiceRegistration.ServiceName");
        StringAssert.Contains(program, "ServiceName = Core.ServiceRegistration.ServiceName");
        StringAssert.Contains(inputSimulation, "new ServiceController(ServiceRegistration.ServiceName)");

        // The hotkey path used to start a service name that nothing registers.
        Assert.IsFalse(inputSimulation.Contains("MouseWithoutBordersSvc", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("\"PowerToys.MWB.Service\"", StringComparison.Ordinal), "Use ServiceRegistration.ServiceName instead of repeating the literal.");
    }

    [TestMethod]
    public void ServiceStartsTheApplicationFromItsOwnDirectory()
    {
        var worker = ReadRepositorySource(@"App\Service\Worker.cs");
        var installer = ReadRepositorySource(@"App\Service\ServiceInstaller.cs");

        StringAssert.Contains(worker, "processName = \"PowerToys.MouseWithoutBorders\"");
        StringAssert.Contains(installer, "ApplicationExeName = \"PowerToys.MouseWithoutBorders.exe\"");
    }

    [TestMethod]
    public void MaintenanceCommandsRunBeforeTheHost()
    {
        var program = ReadRepositorySource(@"App\Service\Program.cs");

        var command = program.IndexOf("TryRunMaintenanceCommand(args[1], out int exitCode)", StringComparison.Ordinal);
        var host = program.IndexOf("Host.CreateDefaultBuilder(args)", StringComparison.Ordinal);

        Assert.IsTrue(command >= 0 && host > command, "Service install/uninstall commands must be handled before the host starts.");
        foreach (var verb in new[] { "install", "uninstall", "start", "stop" })
        {
            StringAssert.Contains(program, $"command.Equals(\"{verb}\", StringComparison.OrdinalIgnoreCase)");
        }
    }

    [TestMethod]
    public void ServiceIsRegisteredInTheServiceNameUpstreamUses()
    {
        // PowerToys registers this name from its native module host; keeping it means an
        // existing PowerToys installation and the standalone build stay interchangeable.
        Assert.AreEqual("PowerToys.MWB.Service", ServiceName);
    }

    private static string ReadRepositorySource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Repository source must be available for the wiring check.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
