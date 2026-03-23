# PixelFlow Clone — Systems Brief

## Conveyor Belt
Boards queue in a dispenser machine and travel a closed spline loop one at a time. Each board re-queues automatically after completing the path, ready for the next shooter.

## Shooter & Lane System
Shooters stack in vertical lanes; only the front one can jump. When it leaves, the rest advance and the new front is evaluated for jump eligibility based on available board count and conveyor state.

## Linked Shooters
Two shooters can be paired with a stretching rope visual. Tapping either one sends the front first, then auto-dispatches the second after a short delay. The link breaks once both are airborne.

## Target Detection & Shooting
Each frame, the system determines which side of the target grid the shooter is approaching and scans inward from the edge to find the first alive target in that column or row. Outer targets naturally shield inner ones. Duplicate shots are prevented via a per-side coordinate log that resets each loop.

## Bullet Pool
Bullets are recycled with Unity's `ObjectPool`. On fire, a bullet tweens to the target at a fixed speed, triggers the hit callback on arrival, and returns to the pool.

## Storage System
Shooters with bullets remaining after a full loop are held in a storage rack instead of being discarded. If all slots are full when another shooter returns, the game ends in failure.

## Level Data
All level configuration lives in a `LevelData` ScriptableObject — color palette, target coordinates, shooter definitions, grid dimensions, and board count. Similar colors can share a `ShooterColorId` so targets keep visual variety while shooters treat them as one group.

## Level Creator Editor
A custom Scene View editor with a floating tool window. Designers paint targets and shooters directly onto the 3D grid, import a source texture, auto-generate shooters from target data layer by layer, and view a live validation panel showing bullet-to-target balance per color.

## Event Bus
A generic, type-safe pub-sub system using struct events for zero heap allocation. Components subscribe and unsubscribe explicitly; the bus is pre-warmed at startup by scanning all assemblies.

## UI Panel System
`PanelBase` wraps `CanvasGroup` behind `ShowPanel` / `HidePanel` calls. `UIManager` listens to gameplay state changes via the Event Bus and switches panels automatically.

## Singleton & Config Pattern
`Singleton<T>` handles MonoBehaviour managers. ScriptableObject configs use an explicit static instance initialized at startup, with an `AssetDatabase` fallback for editor access outside Play mode.

## DOTween Conventions
Related tweens are combined into a single `Sequence` so they share one lifecycle. Every tween is bound to its owner via `SetLink`, and existing tweens are always killed before new ones start.

## Grid System
`GameGrid` stores size, dimensions, and center position. `GridHelper` converts between world positions and `(column, row)` coordinates in both directions, used by both the shooter grid and the target grid.
