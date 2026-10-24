// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// JSON serialization context for the Mouse Without Borders settings types.
    /// </summary>
    /// <remarks>
    /// Only the types this standalone build uses are registered; every settings class that
    /// derives from <see cref="BasePTModuleSettings"/> must be listed here for
    /// <see cref="BasePTModuleSettings.ToJsonString"/> to serialize it.
    /// </remarks>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true)]
    [JsonSerializable(typeof(MouseWithoutBordersSettings))]
    [JsonSerializable(typeof(MouseWithoutBordersProperties))]
    [JsonSerializable(typeof(ConnectionRequest))]
    [JsonSerializable(typeof(NewKeyGenerationRequest))]
    [JsonSerializable(typeof(BoolProperty))]
    [JsonSerializable(typeof(StringProperty))]
    [JsonSerializable(typeof(IntProperty))]
    [JsonSerializable(typeof(GenericProperty<HashSet<string>>))]
    [JsonSerializable(typeof(HotkeySettings))]
    [JsonSerializable(typeof(List<string>))]
    public partial class SettingsSerializationContext : JsonSerializerContext
    {
    }
}
