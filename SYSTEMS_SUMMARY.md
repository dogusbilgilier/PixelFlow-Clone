# PixelFlow Clone — Systems Summary

A mobile puzzle game where players tap color-matched shooters to send them onto a looping conveyor belt. Each shooter automatically fires at same-color pixel targets arranged inside the conveyor's bounds. Targets shield the ones behind them, so clearing order matters — outer targets must go first.

---

## Conveyor Belt System
A spline (Dreamteck Splines) defines the conveyor track. `ConveyorFollowerBoard` objects are queued inside a dispenser machine and dispatched one at a time when a shooter jumps. Each board travels the full loop via a `SplineFollower`, then resets and re-queues for reuse.

---

## Shooter & Lane System
Shooters queue in vertical lanes behind each other. Only the front shooter in a lane is eligible to jump. When it leaves, the remaining shooters slide forward and the new front is revealed.

---

## Linked Shooters
Two shooters can be paired. A `LinkObject` stretches between them each frame as a visual rope. When the pair is tapped, the front one jumps first, and the second follows automatically after a short delay. The link breaks once both are airborne and they operate independently from that point on.

---

## Target Detection & Shooting
Every frame, each shooter on the conveyor is checked against the target grid. The system first determines which side of the grid the shooter is approaching (Bottom, Top, Left, or Right) based on its world position relative to the grid's bounds. Shooters only fire along the straight edges of the loop — never at corners.

For each side, the system finds the column or row the shooter is aligned with and scans inward from the edge to find the first alive target in that line. This means outer targets naturally shield inner ones. If the conveyor speed is high enough that the shooter moves more than one cell per frame, the scan is interpolated between the last known position and the current one so no column is ever skipped.

Each shooter tracks which columns and rows it has already fired at, per side, preventing duplicate shots on the same pass. This record resets when the shooter completes a full loop. Color matching is applied last — only targets whose `ShooterColorId` matches the shooter's color are fired upon.

---

## Bullet Pool
Bullets are managed with Unity's `ObjectPool<Bullet>`. On fire, a bullet is positioned at the shooter's muzzle and sent toward the target via a speed-based DOTween. On arrival it triggers the target's hit callback, clears its trail renderer, and returns to the pool.

---

## Storage System
Shooters that complete the conveyor loop with bullets remaining are placed into storage slots rather than being discarded. From storage they can be re-dispatched by tapping again. If all slots are full when a shooter returns, the game enters the Fail state. When a shooter leaves storage the remaining ones compact left to fill the gap.

---

## Level Data Architecture
All level configuration lives in a `LevelData` ScriptableObject: a color palette, a flat list of target coordinates with color IDs, per-lane shooter lists with bullet counts and link references, grid dimensions, storage count, and conveyor board count. The palette supports a dual-ID system where each exact color has its own ID but similar colors within a tolerance share a `ShooterColorId`, letting targets keep visual variety while shooters treat them as one group.

---

## Level Creator Editor
A fully custom Unity editor that runs inside the Scene View. A floating, draggable, resizable tool window provides all design controls. Designers can paint shooters and targets directly onto the 3D grid with the mouse, set brush size, switch between Paint and Remove modes with keyboard shortcuts, and link shooter pairs by right-clicking.

A texture can be imported and resampled onto the target grid at any resolution via nearest-neighbor sampling. The color tolerance slider merges similar shades into one palette entry, or — with the grouping toggle — keeps them visually distinct while still routing them to the same shooter type.

Auto Shooter Generation reads the target grid layer by layer (outside-in), counts targets per color per layer, and decomposes those counts into shooters using a configurable set of bullet-count denominations (e.g. 5, 10, 20) with carry-forward between layers. This guarantees the generated shooters exactly cover every target in the correct order. Shooters are distributed round-robin across lanes.

A live validation panel shows bullets vs. targets per color with a checkmark or warning icon, giving instant solvability feedback without entering Play mode. All operations support full Undo/Redo; drag strokes are collapsed into a single undo step.

---

## Event Bus
A generic, type-safe publish-subscribe system. Events are structs (zero heap allocation). The bus pre-warms at startup by scanning all loaded assemblies for `IEvent` implementations, making runtime dispatch allocation-free. Components subscribe on initialization and unsubscribe in `OnDestroy`.

---

## UI Panel System
An abstract `PanelBase` class wraps `CanvasGroup` visibility behind `ShowPanel` / `HidePanel` with virtual lifecycle hooks. `UIManager` listens to gameplay state changes via the Event Bus and switches between the Gameplay, Level Completed, and Level Failed panels automatically.

---

## Singleton Pattern
A generic `Singleton<T>` base for `MonoBehaviour` managers handles instance creation and duplicate destruction. `ScriptableObject` configs (`GameConfigs`, `ShooterVisualsConfigs`) use a separate static-field pattern initialized explicitly at startup, with an `AssetDatabase` fallback for editor-time access outside Play mode.

---

## DOTween Usage
All animations use DOTween with consistent conventions: related tweens are grouped into a single `Sequence` to share one lifecycle, every tween is bound to its owning GameObject via `SetLink` for automatic cleanup, and existing tweens are always killed before new ones are created to handle rapid input safely.

---

## Grid Coordinate System
A shared `GameGrid` struct represents both the shooter grid and the target grid. `GridHelper` converts between world positions and `(column, row)` coordinates in both directions. The target grid auto-sizes its cells to fill the conveyor's physical bounds, keeping the pixel art at the correct scale regardless of grid resolution.
