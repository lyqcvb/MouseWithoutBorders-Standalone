// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MouseWithoutBordersService
{
    public delegate void StopService();

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
    public static class CmdArgs
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
    {
        public static string[] Value { get; set; }

        public static StopService StopServiceDelegate { get; set; }
    }

    internal sealed class Program
    {
        [STAThread]
        private static int Main()
        {
            string[] args = Environment.GetCommandLineArgs();

            // The standalone build registers the service itself; PowerToys normally does this
            // from its module host. The service control manager starts this executable with the
            // user's local app data as its only argument, so these commands cannot collide.
            // They run before the policy check so the service can always be removed.
            if (args.Length > 1 && TryRunMaintenanceCommand(args[1], out int exitCode))
            {
                return exitCode;
            }

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled
                || PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMwbAllowServiceModeValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // TODO: Add logging.
                // Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return 0;
            }

            CmdArgs.Value = args;
            var builder = Host.CreateDefaultBuilder(args);

            var host = builder
            .UseWindowsService(options =>
            {
                options.ServiceName = ServiceInstaller.ServiceName;
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<Worker>();
            })
            .Build();

            // These warning are disabled because StreamJsonRpc brings in thread analyzers to the project. Enable warnings once we adopt thread analyzers globally.
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
#pragma warning disable VSTHRD110 // Observe result of async calls
            CmdArgs.StopServiceDelegate = async () => { await host.StopAsync(); };
            host.Run();
            host.StopAsync();
#pragma warning restore VSTHRD110 // Observe result of async calls
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

            return 0;
        }

        private static bool TryRunMaintenanceCommand(string command, out int exitCode)
        {
            exitCode = 0;

            if (command.Equals("install", StringComparison.OrdinalIgnoreCase))
            {
                exitCode = ServiceInstaller.Install();
                return true;
            }

            if (command.Equals("uninstall", StringComparison.OrdinalIgnoreCase)
                || command.Equals("remove", StringComparison.OrdinalIgnoreCase))
            {
                exitCode = ServiceInstaller.Uninstall();
                return true;
            }

            if (command.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                exitCode = ServiceInstaller.StartOrStop(true);
                return true;
            }

            if (command.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                exitCode = ServiceInstaller.StartOrStop(false);
                return true;
            }

            return false;
        }
    }
}
