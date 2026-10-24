// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;

// The event classes keep the PowerToys type names so their source does not need to change,
// but this standalone build has no telemetry pipeline: events are traced locally and dropped.
namespace Microsoft.PowerToys.Telemetry
{
    /// <summary>
    /// Privacy tag values carried by every event.
    /// </summary>
    public enum PartA_PrivTags : ulong
    {
        None = 0,
        ProductAndServicePerformance = 1,
        ProductAndServiceUsage = 2,
    }
}

namespace Microsoft.PowerToys.Telemetry.Events
{
    /// <summary>
    /// Marker implemented by every telemetry event.
    /// </summary>
    public interface IEvent
    {
        PartA_PrivTags PartA_PrivTags { get; }
    }

    /// <summary>
    /// Properties common to all telemetry events.
    /// </summary>
    public class EventBase
    {
        public bool UTCReplace_AppSessionGuid => true;

        public string EventName { get; set; }

        public string Version => $"v{Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? new Version(0, 0, 0).ToString()}";
    }
}

namespace Microsoft.PowerToys.Telemetry
{
    /// <summary>
    /// Standalone replacement for the PowerToys telemetry sink.
    /// </summary>
    public static class PowerToysTelemetry
    {
        public static TelemetryLogger Log { get; } = new();

        public sealed class TelemetryLogger
        {
            public void WriteEvent<T>(T telemetryEvent)
                where T : Events.EventBase, Events.IEvent
            {
                Debug.WriteLine($"[telemetry] {telemetryEvent?.GetType().Name ?? "null"} ({telemetryEvent?.PartA_PrivTags})");
            }
        }
    }

    /// <summary>
    /// Standalone replacement for the PowerToys ETW trace helper.
    /// </summary>
    public sealed class ETWTrace : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
