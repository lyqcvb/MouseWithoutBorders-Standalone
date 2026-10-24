// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Principal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseWithoutBordersService;

namespace MouseWithoutBorders.UnitTests;

/// <summary>
/// Exercises the service security descriptor the installer applies. Registering the service
/// itself needs elevation, but building the descriptor does not.
/// </summary>
[TestClass]
public sealed class ServiceSecurityDescriptorTests
{
    [TestMethod]
    public void DescriptorForCurrentUserParses()
    {
        // Regression check: the conversion failed with ERROR_INVALID_PARAMETER because the
        // P/Invoke declaration omitted CharSet.Unicode, so the wide-character entry point
        // received ANSI bytes.
        using var identity = WindowsIdentity.GetCurrent();
        var sddl = ServiceInstaller.BuildSecurityDescriptor(identity.User!.Value);

        Assert.IsTrue(ServiceInstaller.TryCreateSecurityDescriptor(sddl, out var descriptor), "The service security descriptor must be convertible.");

        try
        {
            Assert.AreNotEqual(IntPtr.Zero, descriptor);
        }
        finally
        {
            if (descriptor != IntPtr.Zero)
            {
                _ = LocalFree(descriptor);
            }
        }
    }

    [TestMethod]
    public void DescriptorGrantsTheUserStartRightsAndKeepsUpstreamEntries()
    {
        const string userSid = "S-1-5-21-1-2-3-1001";
        var sddl = ServiceInstaller.BuildSecurityDescriptor(userSid);

        // Same trustee list as PowerToys' generateSecurityDescriptor.h.
        foreach (var trustee in new[] { ";;;SY)", ";;;BA)", ";;;IU)", ";;;SU)", ";;;AU)", ";;;PU)" })
        {
            StringAssert.Contains(sddl, trustee);
        }

        StringAssert.Contains(sddl, "RPWPDTLO;;;" + userSid + ")");
        StringAssert.Contains(sddl, "S:(AU;FA;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;WD)");
        StringAssert.StartsWith(sddl, "D:");
    }

    [TestMethod]
    public void InvalidSddlIsRejected()
    {
        Assert.IsFalse(ServiceInstaller.TryCreateSecurityDescriptor("not an sddl", out _));
    }

    private static IntPtr NativeLocalFree(IntPtr handle)
    {
        return LocalFree(handle);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);
}
