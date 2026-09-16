# Changelog

## Unreleased

- Use a consistent tooltip panel in both the main menu and a loaded world.
- Fit tooltips to short device names while keeping long names wrapped at the native maximum width.
- Show full device names in native hover tooltips on the dropdown and its options.
- Keep long output names inside the dropdown with bounded font sizing and ellipsis.
- Wrap status notices and hide them while the device menu is open.
- Record a successful Windows user test while retaining the experimental support label.

## 1.0.0 (2026-09-14)

- Show the selected output only in the dropdown, with actionable notices below it.
- Support host audio commands when running inside Steam's Linux runtime.
- Align the output dropdown with native audio controls and remove the copied dropdown label.
- Add a native Output device dropdown to Valheim's Audio settings.
- Preview selections immediately, save with OK, and restore the saved selection with Back.
- Route only Valheim's audio on Linux using PulseAudio or PipeWire.
- Add experimental, untested Windows per-app output selection.
- Remember disconnected devices and retry when they return.
