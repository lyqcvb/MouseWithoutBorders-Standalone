// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace ManagedCommon
{
    /// <summary>
    /// Standalone replacement for the PowerToys runner lifecycle helper.
    /// </summary>
    /// <remarks>
    /// PowerToys is started and stopped by its own runner; this build has no runner, so the
    /// exit request is observed on the local PowerToys process when one happens to be running.
    /// </remarks>
    public static class RunnerHelper
    {
        public static void WaitForPowerToysRunnerExitFallback(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            Process runner = null;
            try
            {
                var candidates = Process.GetProcessesByName("PowerToys");
                runner = candidates.Length > 0 ? candidates[0] : null;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[runner] cannot enumerate processes: {e.Message}");
            }

            if (runner == null)
            {
                return;
            }

            using (runner)
            {
                runner.WaitForExit();
            }

            action();
        }
    }
}
