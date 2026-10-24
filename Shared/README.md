# Shared

This folder holds a narrow, local subset of the PowerToys shared libraries that Mouse Without Borders
compiles against, so the repository can be built without a PowerToys checkout.

Every file here was copied from the same PowerToys revision the module was extracted from and keeps its
original MIT copyright header. The PowerToys origin is noted per area:

- `Settings/` - `src/settings-ui/Settings.UI.Library` (Mouse Without Borders settings model, its
  JSON property wrappers, `SettingsUtils`/`SettingPath` persistence and the source-generated
  serialization context reduced to the Mouse Without Borders types).
- `PowerToys/Utilities/` - the IPC pieces the settings channel needs
  (`MouseWithoutBordersIpc`, `RestrictedNamedPipeServer`, `NamedPipePeerVerification`) plus the
  subset of `Helper` used for settings paths.
- `PowerToys/GPOWrapper.cs` - managed replacement for the native `src/common/GPOWrapper` component.
  It reads the same registry policies as `src/common/utils/gpo.h`.
- `Common/Logger.cs` - `src/common/ManagedCommon/Logger.cs` without the native interop dependency.
- `Common/RunnerHelper.cs` - replacement for the PowerToys runner wait helper.
- `Interop/Constants.cs` - event names taken from `src/common/interop/shared_constants.h`.
- `Telemetry/Telemetry.cs` - compile-time stubs for the PowerToys telemetry types. Events are
  traced and dropped; no telemetry is sent.

Deliberate differences from PowerToys are limited to: the GPO wrapper being managed instead of a
CsWinRT projection, telemetry being a no-op, and the runner wait observing a local `PowerToys.exe`
instead of the PowerToys runner IPC. Protocol, pairing and pipe ACL/identity checks are unchanged.
