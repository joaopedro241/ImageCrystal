# Optional local memory diagnostics

Diagnostics are disabled during normal ImageCrystal launches. They do not keep pixel buffers alive: resource tracking uses weak references. Native-resource sizes are estimates and aliases must not be added together as independent allocations.

From a portable application folder:

```powershell
.\Start-Memory-Diagnostics.ps1 -ImagePath 'C:\Images\sample.jpg'
# Explicit testing option: force a managed GC after the diagnostic settling period:
.\Start-Memory-Diagnostics.ps1 -ImagePath 'C:\Images\sample.jpg' -CollectAfterLoad
```

Logs are local JSONL files under `memory-logs/`. They include full image paths, working set/private commit, managed allocations/GC data, timed decode/ICC/conversion/upload stages and weak resource/disposal snapshots. Review logs before sharing. `IMAGECRYSTAL_MEMORY_LOG` enables probes directly; `IMAGECRYSTAL_MEMORY_GC=1` is a diagnostic-only option. Finite post-load snapshots stop after settling; there is no recurring normal-viewer cleanup timer.

## Interpreting results

Working set includes shared resident pages; private working set and private commit are different measurements. GPU texture estimates do not directly add to Task Manager's private RAM figure. Managed allocation traffic is not the same as simultaneously retained memory, and GC is pressure-driven rather than a periodic few-second cleanup promise.

A 4000x6000 RGB24 image is about 68.7 MiB of decoded CPU pixels; a full RGBA/BGR32 GPU representation is about 91.6 MiB. Encoded JPEG size and DPI do not determine those costs. Full-resolution CPU/GPU sources remain for zooming and renderer lifetime/device recovery.

## What changed

Bounded WIC export avoids the full-image managed array/WPF bridge. Explicit native disposal fixes replaced photos, previews, frames and utility conversions. JPEG motion scanning uses a bounded buffer. Header reuse stores only small facts and a detected-format value, not copied profiles or pixels. Toolbar reuse keeps one current bitmap per button rather than old DPI variants. Forced normal-viewer GC/polling has been removed.

Earlier prototype measurements showed a substantial improvement after bounded export and direct-file ownership fixes. They were not a controlled upstream-v9 baseline and do not support a universal percentage claim. The later three-trial comparison showed broadly similar or slightly lower memory and no consistent empty-startup/large-JPEG speedup. Different images, settings and metrics cannot be compared by simple subtraction.

Private historical measurements, sample files and experimental harnesses live under ignored local/artifacts folders and are not distributed. The current supported verification is the tracked smoke suite plus TESTING.md.
