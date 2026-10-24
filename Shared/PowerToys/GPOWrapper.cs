// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using Microsoft.Win32;

namespace PowerToys.GPOWrapper
{
    /// <summary>
    /// Result of reading a PowerToys group policy value.
    /// Mirrors the native <c>GpoRuleConfigured</c> enum in <c>src/common/GPOWrapper/GPOWrapper.idl</c>.
    /// </summary>
    public enum GpoRuleConfigured
    {
        WrongValue = -3,
        Unavailable = -2,
        NotConfigured = -1,
        Disabled = 0,
        Enabled = 1,
    }

    /// <summary>
    /// Managed replacement for the native GPOWrapper component.
    /// </summary>
    /// <remarks>
    /// The registry locations, value names and machine-before-user precedence match
    /// <c>src/common/utils/gpo.h</c>, so an existing PowerToys group policy configuration
    /// keeps applying to this build.
    /// </remarks>
    public static class GPOWrapper
    {
        private const string PoliciesPath = @"Software\Policies\Microsoft\PowerToys";

        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;

        public static GpoRuleConfigured GetConfiguredMouseWithoutBordersEnabledValue()
            => GetConfiguredValue("ConfigureEnabledUtilityMouseWithoutBorders");

        public static GpoRuleConfigured GetConfiguredMwbAllowServiceModeValue()
            => GetConfiguredValue("MwbAllowServiceMode");

        public static GpoRuleConfigured GetConfiguredMwbClipboardSharingEnabledValue()
            => GetConfiguredValue("MwbClipboardSharingEnabled");

        public static GpoRuleConfigured GetConfiguredMwbFileTransferEnabledValue()
            => GetConfiguredValue("MwbFileTransferEnabled");

        public static GpoRuleConfigured GetConfiguredMwbDisallowBlockingScreensaverValue()
            => GetConfiguredValue("MwbDisallowBlockingScreensaver");

        public static GpoRuleConfigured GetConfiguredMwbUseOriginalUserInterfaceValue()
            => GetConfiguredValue("MwbUseOriginalUserInterface");

        public static GpoRuleConfigured GetConfiguredMwbValidateRemoteIpValue()
            => GetConfiguredValue("MwbValidateRemoteIp");

        public static GpoRuleConfigured GetConfiguredMwbSameSubnetOnlyValue()
            => GetConfiguredValue("MwbSameSubnetOnly");

        public static GpoRuleConfigured GetConfiguredMwbDisableUserDefinedIpMappingRulesValue()
            => GetConfiguredValue("MwbDisableUserDefinedIpMappingRules");

        public static string GetConfiguredMwbPolicyDefinedIpMappingRules()
            => GetPolicyListValue("MwbPolicyDefinedIpMappingRules");

        // getConfiguredValue in gpo.h: machine scope first; if the machine key is missing or the
        // value is absent there, fall back to the user scope. A missing user key (or value) means
        // "not configured"; any other registry failure means "unavailable".
        private static GpoRuleConfigured GetConfiguredValue(string valueName)
        {
            try
            {
                object raw = null;
                if (TryReadDword(RegistryHive.LocalMachine, valueName, out var machineValue))
                {
                    raw = machineValue;
                }
                else if (!TryReadDword(RegistryHive.CurrentUser, valueName, out var userValue, out var userKeyMissing))
                {
                    return userKeyMissing ? GpoRuleConfigured.NotConfigured : GpoRuleConfigured.Unavailable;
                }
                else
                {
                    raw = userValue;
                }

                return Convert.ToInt64(raw, System.Globalization.CultureInfo.InvariantCulture) switch
                {
                    0 => GpoRuleConfigured.Disabled,
                    1 => GpoRuleConfigured.Enabled,
                    _ => GpoRuleConfigured.WrongValue,
                };
            }
            catch (Exception)
            {
                return GpoRuleConfigured.Unavailable;
            }
        }

        private static bool TryReadDword(RegistryHive hive, string valueName, out int value)
            => TryReadDword(hive, valueName, out value, out _);

        private static bool TryReadDword(RegistryHive hive, string valueName, out int value, out bool keyMissing)
        {
            value = 0;
            keyMissing = false;

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(PoliciesPath, writable: false);
            if (key == null)
            {
                keyMissing = true;
                return false;
            }

            object raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw == null)
            {
                return false;
            }

            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }

            if (!int.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out value))
            {
                throw new Win32Exception();
            }

            return true;
        }

        // getPolicyListValue in gpo.h: the machine list, when enabled, wins; otherwise the user list.
        private static string GetPolicyListValue(string valueName)
        {
            try
            {
                var machine = ReadStringList(RegistryHive.LocalMachine, valueName);
                if (!string.IsNullOrEmpty(machine))
                {
                    return machine;
                }

                return ReadStringList(RegistryHive.CurrentUser, valueName);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string ReadStringList(RegistryHive hive, string valueName)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(PoliciesPath, writable: false);
            if (key == null)
            {
                return string.Empty;
            }

            return key.GetValue(valueName) switch
            {
                string[] lines => string.Join("\r\n", lines),
                string single => single,
                _ => string.Empty,
            };
        }
    }
}
