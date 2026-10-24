// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Security.Principal;

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    /// <summary>
    /// Settings path helpers used by Mouse Without Borders.
    /// </summary>
    /// <remarks>
    /// Only the members the standalone build needs are kept; the PowerToys runner and
    /// custom-action helpers were removed.
    /// </remarks>
    public static class Helper
    {
        public static readonly IFileSystem FileSystem = new FileSystem();

        public static string UserLocalAppDataPath { get; set; } = string.Empty;

        public static IFileSystemWatcher GetFileWatcher(string path, Action onChangedCallback, IFileSystem fileSystem = null)
        {
            fileSystem ??= FileSystem;

            var dirPath = Path.GetDirectoryName(path);
            if (!fileSystem.Directory.Exists(dirPath))
            {
                return null;
            }

            var watcher = fileSystem.FileSystemWatcher.New();
            watcher.Path = dirPath;
            watcher.Filter = Path.GetFileName(path);
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;

            watcher.Changed += (o, e) => onChangedCallback();

            return watcher;
        }

        public static IFileSystemWatcher GetFileWatcher(string moduleName, string fileName, Action onChangedCallback, IFileSystem fileSystem = null)
        {
            fileSystem ??= FileSystem;

            var path = fileSystem.Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{moduleName}");

            if (!fileSystem.Directory.Exists(path))
            {
                fileSystem.Directory.CreateDirectory(path);
            }

            var watcher = fileSystem.FileSystemWatcher.New();
            watcher.Path = path;
            watcher.Filter = fileName;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;

            watcher.Changed += (o, e) => onChangedCallback();

            return watcher;
        }

        public static string LocalApplicationDataFolder()
        {
            using WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            SecurityIdentifier currentUserSID = currentUser.User;

            SecurityIdentifier localSystemSID = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            if (currentUserSID.Equals(localSystemSID) && UserLocalAppDataPath != string.Empty)
            {
                return UserLocalAppDataPath;
            }
            else
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
        }

        public static string GetProductVersion()
        {
            return typeof(Helper).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        }

        // Local replacement for the native LayoutMapManaged key-name mapping.
        public static string GetKeyName(uint key)
        {
            if (key == 0)
            {
                return string.Empty;
            }

            if (key is >= 'A' and <= 'Z')
            {
                return ((char)key).ToString();
            }

            if (key is >= '0' and <= '9')
            {
                return ((char)key).ToString();
            }

            if (key is >= 0x70 and <= 0x87)
            {
                return "F" + (key - 0x70 + 1);
            }

            if (key is >= 0x60 and <= 0x69)
            {
                return "NumPad" + (key - 0x60);
            }

            var scanCode = MapVirtualKey(key, MapVirtualKeyTranslation.VkToChar);
            if (scanCode != 0)
            {
                var buffer = new char[8];
                var length = GetKeyNameText((int)(scanCode << 16), buffer, buffer.Length);
                if (length > 0)
                {
                    return new string(buffer, 0, length);
                }
            }

            return GetFallbackKeyName(key);
        }

        public static uint GetKeyValue(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0;
            }

            if (key.Length == 1)
            {
                var ch = char.ToUpperInvariant(key[0]);
                if (char.IsLetterOrDigit(ch))
                {
                    return ch;
                }
            }

            if (key.StartsWith("F", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(key.AsSpan(1), out var functionNumber)
                && functionNumber is >= 1 and <= 24)
            {
                return (uint)(0x70 + functionNumber - 1);
            }

            if (key.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(key.AsSpan(6), out var numPadNumber)
                && numPadNumber is >= 0 and <= 9)
            {
                return (uint)(0x60 + numPadNumber);
            }

            return GetFallbackKeyValue(key);
        }

        private static string GetFallbackKeyName(uint key)
        {
            // Virtual-key codes without a printable name are rendered as their code, which is
            // what the settings UI shows for keys the layout cannot map.
            return string.Empty;
        }

        private static uint GetFallbackKeyValue(string key)
        {
            return 0;
        }

        private enum MapVirtualKeyTranslation : uint
        {
            VkToChar = 2,
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "MapVirtualKeyW")]
        private static extern uint MapVirtualKey(uint code, MapVirtualKeyTranslation mapType);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "GetKeyNameTextW")]
        private static extern int GetKeyNameText(int lParam, [System.Runtime.InteropServices.Out] char[] buffer, int size);
    }
}
