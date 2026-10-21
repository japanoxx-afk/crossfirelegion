# Unity native replay integration

## Architecture and evidence

Crossfire Legion contains a native deterministic command replay implementation. No Unreal/UE4SS components are used. Tempest Rising was consulted only for feature requirements and failure cases.

`GameplayFlowState.OnEnter` obtains a replay from `ReplayService.GetNewReplay` and calls `Replay.BeginWrite` for a recording session with a non-observer game session. `SimController.TickSimOnAdvanceInteractive` sorts accepted commands, then records the frame number, previous simulation checksum and commands before advancing the simulation. `SimController.EndSimulation` closes the writer. Existing user `.rply` files confirm this recording path has executed before this modification.

The native header stores level ID, RNG seed, ordered player loadouts, game configuration, build, random start locations and game mode. Playback uses `ApplicationFrontEndDataSource.EnterReplayMode(path, true)` → `MainMenuFlowState` → `ReplayModeLayer` → `ReplaySelectFlowState` → `ReplaySessionLoadingState` → the native simulation. Stable simulation entities, movement, production, construction and combat are reconstructed by commands rather than position overrides.

The native playback loop discarded recorded checksums in this build. The patch compares each available recorded checksum with the previous computed simulation checksum, using native IL and integer comparisons. A mismatch pauses and latches playback, logs expected/actual sums and the preceding frame, and blocks further advances. No comparison or additional per-frame logging runs in an ordinary match. The native file does not contain a checksum after its final simulated frame; reaching EOF is not proof of every presentation detail or an independent final-state comparison.

## Files and lifecycle

- Native recordings: `%USERPROFILE%\AppData\LocalLow\Blackbird Interactive\Crossfire Legion\Replays\*.rply`.
- Launcher metadata, status and diagnostics: `%LOCALAPPDATA%\CrossfireLegionLauncher\replays`.
- Metadata is stored outside the native recording directory because the native browser enumerates all files in that directory.
- Recording, finalizing, closed, failed and process-ended-without-confirmed-close are distinct states. A closed native stream is not automatically labelled a verified replay.
- Native frame writes update the last-recorded-frame property. SHA256 and final metadata run on a worker after close, without Unity objects crossing threads. A process exit can interrupt this final worker; the native recording remains preserved and is then classified as unconfirmed.
- Native write failures are caught, the stream is closed/disabled, and a failure is logged without failing the match. There is no retry loop in the simulation tick.
- No historical file is modified. There is no automatic truncation of crash tails. An unconfirmed file can be explicitly tried; a malformed native command stream is rejected before requesting a mode transition.
- Export/import bundles contain the original byte-exact `replay.rply` and metadata. Local paths, process IDs and exception paths are omitted from exported metadata. Native replay headers themselves contain player names and network identifiers.

Compatibility uses source-derived rules identity plus hashes of simulation-related installed DLLs/data/scripts. The rules identity is generated identically for launcher/runtime and normalizes source BOM/line endings. It does not depend on a compiler-generated runtime DLL MVID. Different known fingerprints are rejected; legacy files without a fingerprint require explicit trial playback. There is no claim of general compatibility across game/mod versions.

Attribute ZIP rewrites preserve original entry timestamps so applying identical rules does not acquire a different fingerprint merely because it was installed at a different time. Independent local builds and separate patch applications produced matching fingerprints. Metadata includes the launcher/mod version and source rules hash.

## Actual runtime checks, 2026-10-22

Testing uses a separate game copy. Original Steam installation patch files are unchanged. Immutable assets are linked, while patchable managed DLLs, data and scripts are independent copies.

An initial Korean-path copy failed during native `ScriptHelper.LoadExtensions` / Lua initialization with an illegal byte-sequence conversion error. Normal close did not exit that stuck copy; the user explicitly authorized terminating that test process only. An ASCII-path copy subsequently entered actual native spectator playback.

The existing 2026-22-10 05-45-00 recording loaded 9,083 frames (native UI duration 7:34.15). Sampled playback confirmed resource/population changes, buildings under construction and completed, units, global ability state, full/player fog toggle, minimap camera movement, 1x/2x/4x speed, pause/resume. Pause held at 1:57.30 across separate observations. Playback reached its end and returned to the main menu without a checksum mismatch. The original live-match video was not available for a frame-by-frame presentation comparison.

Observed render-interval summaries in this playback included approximately 19 ms p95 and one interval above 50 ms in a sampled minute, with other sampled minutes showing none. Startup/loading stalls are separate. This is not a controlled recording-overhead benchmark or proof of performance on other machines.

A new offline skirmish on Mountain was recorded in the test copy: 2,314 ticks, 1:55.70, 37,025 bytes. Two worker production commands completed and resource gathering continued. Surrender closed the native writer and finalized its SHA256/metadata. A fresh game process replayed the file to the end, with 0.5x/1x/2x/4x speeds and switching to the AI perspective observed. Final exit diagnostics were then verified using an imported byte-exact copy: `playback_eof last_frame=2314 checksum_mismatch=False`, followed by menu return. Surrender can trigger native victory flow before the replay loop's EOF branch, so simulation teardown also verifies the processed frame reached the final recorded frame; an early exit is not labelled complete. The original recording and metadata remain preserved.

The launcher dialog was visually inspected with the actual native recording list, state, duration and six controls. The native serializer still performs its existing synchronous writes/flushes; this patch adds no synchronous per-tick IO, but does not replace that writer with an unverified asynchronous pipeline. Controlled recording-on/off overhead and long-run memory measurements remain outstanding.

## Automated checks

- Bounded native-header parsing, Unicode/atomic metadata, header-only/truncated files, bundled import/export exact bytes, ZIP traversal rejection.
- Actual patched native write methods executed against a throwing stream: write returns false, stream is disabled, failure is persisted, ending a null stream is safe.
- Actual patched replay tick executed with a deliberately wrong checksum: failure latches and later simulation advances are blocked.
- Existing roster/HUD/cheat/energy/movement/startup and reversible-patch regressions pass on copied original game DLLs.
- Steam routing and updater recovery checks pass.

## Limits

No timeline seeking is provided. The native replay controls are used rather than implementing a separate presentation simulation. New internet multiplayer recording, all maps/factions, repeated hours-long playback, garrison/unload, all effects/audio and exact original-vs-replay state snapshots require further runtime coverage. Campaign/observer modes retain the native recording eligibility rules. Files above 64 MB are outside the current playback preflight limit. Original files without captured commands/state cannot be repaired by inventing events.
