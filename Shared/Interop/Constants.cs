// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.Interop
{
    /// <summary>
    /// Named-event names the module listens on.
    /// </summary>
    /// <remarks>
    /// Values are copied verbatim from <c>src/common/interop/shared_constants.h</c> so the
    /// commands keep reaching this build the same way they reach PowerToys.
    /// </remarks>
    public static class Constants
    {
        public static string MWBToggleEasyMouseEvent()
            => "Local\\PowerToysMWB-ToggleEasyMouseEvent-a9c8d7b6-e5f4-3c2a-1b0d-9e8f7a6b5c4d";

        public static string MWBReconnectEvent()
            => "Local\\PowerToysMWB-ReconnectEvent-b8d7c6a5-f4e3-2b1c-0a9d-8e7f6a5b4c3d";

        // VK_WIN_BOTH from the same header; reported by the native constant of that name.
        public const uint VK_WIN_BOTH = 0x5B;

        public static string AppDataPath()
            => System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys");
    }
}
