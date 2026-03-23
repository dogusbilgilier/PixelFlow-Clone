# PixelFlow Clone - Systems Overview

A mobile puzzle game where players tap color-matched shooters to launch them onto a looping conveyor belt. As each shooter rides the conveyor, it automatically fires bullets at same-color pixel targets arranged in a grid, clearing them layer by layer. The challenge lies in choosing the shooters in right order.

---

## Conveyor Belt System

The conveyor is the central mechanic of the game. A `SplineComputer` (Dreamteck Splines) defines a loop track shaped as a rounded rectangle. `ConveyorFollowerBoard` objects ride this spline using a `SplineFollower` component that is added at runtime.

**Board Lifecycle:**
Boards begin docked inside a dispenser machine, stacked visually with a configurable gap. When a player taps a shooter, the frontmost board is dequeued, tweened from the machine to the spline's start point, and its `SplineFollower` is activated. The board travels the full loop at a constant speed. Once it reaches the end, it resets: the assigned shooter is unparented, the follower halts, and the board is re-enqueued for reuse.

**Board State Machine:**
Each board tracks two flags, `IsBoardReadyForConveyor` and `IsBoardCompletedPath`, which together determine whether it can be dispatched again. The `MainConveyor` manages a `Queue<ConveyorFollowerBoard>` for dispatch ordering and a flat `List` for ownership, keeping allocation-free iteration during gameplay.

**Shooter-Board Binding:**
When a shooter jumps, it is parented directly to the board's transform. A combined DOTween `Sequence` handles the arc jump and rotation simultaneously. The board only begins moving after the jump animation completes, ensuring the shooter is properly seated before travel starts.

---

## Shooter & Lane System

Shooters are organized into vertical lanes. Each `ShooterLane` maintains an ordered list and a current-index pointer. The frontmost shooter is the only one eligible to jump (with one exception: linked pairs). When the front shooter leaves, the index advances, the remaining shooters tween forward to fill the gap, and the new front is revealed if shooter was hidden.

**Jump Eligibility:**
`CheckShooterCanJump` evaluates multiple conditions: the shooter must be in first place, not already on the conveyor, and there must be at least one available board. For linked pairs, two boards must be available simultaneously. The method also checks whether the linked partner is either in first place in another lane or directly behind in the same lane.

**Jumpable Visuals:**
Front shooters that can jump display at full brightness and full text opacity. Non-jumpable shooters are rendered with reduced HSV value (darkened) and reduced text alpha. The visual state refreshes whenever board availability changes: after a shooter completes a path, after a shooter is destroyed, after a board returns to the machine, and after a shooter jumps from storage. This ensures the visual feedback always reflects the current game state.

**Linked Shooters:**
Two shooters can be linked as a pair, visually connected by a `LinkObject` that stretches between them every frame. Linked pairs must jump together: the front one goes first, and after a configurable delay, the second follows automatically via a delayed callback. Once they jump, the link is broken and they operate independently on the conveyor.

---

## Target Detection & Shooting

This is one of the core mechanics. Shooters ride the conveyor around the target grid, and the system must decide, every frame, which target each shooter should fire at based on its current position and approach direction.

### The Detection Loop

Every frame, `GameplayController.CheckTargetsForShooters` iterates all shooters currently on the conveyor (in reverse order for safe removal). For each shooter, it calls `TryFindTargetForShooter`, which performs the full detection pipeline: determine approach side, interpolate missed positions, find the first alive target in the corresponding line, and return it for shooting.

### Step 1: Determining the Approach Side

The target grid occupies a rectangular area in world space with known bounds. The shooter's world position is compared against these bounds to classify which side the shooter is.

### Step 2: Interpolation to Prevent Skipping

At high conveyor speeds, a shooter can move more than one grid cell per frame. Without compensation, it would skip over target columns/rows entirely and never fire at them.

The system tracks each shooter's `LastCheckPosition` (where it was when last scanned). Each frame, it calculates the distance traveled since the last check and divides it by the target cell size. If the distance exceeds one cell, the scan is broken into multiple steps. Intermediate positions are generated via `Vector3.Lerp`, and the detection is run at each interpolated point. This guarantees that every column or row the shooter passes over is checked, regardless of frame rate or conveyor speed.

### Step 3: Grid Coordinate Projection

For each scan position, the system projects the shooter's world position onto the target grid to determine which column (for Bottom/Top) or row (for Right/Left) the shooter is aligned with.

This projection converts a continuous world-space position into a discrete grid coordinate that can be used to index into the target array.

### Step 4: Line-of-Sight Target Selection

Once the column or row is determined, the system scans along the perpendicular axis to find the **first alive target** that the shooter would hit. The scan direction depends on the approach side:

- **Bottom:** Start at the last row (closest to the shooter) and scan upward (dy = -1). The shooter fires at the outermost alive target in that column.
- **Top:** Start at row 0 (closest to the shooter from above) and scan downward (dy = +1).
- **Right:** Start at the last column and scan leftward (dx = -1).
- **Left:** Start at column 0 and scan rightward (dx = +1).

The first non-null, non-destroyed target found in this scan is selected. This means inner targets are naturally shielded by outer targets: a shooter approaching from the bottom cannot hit a target in row 0 if there is still an alive target in row 5 of the same column. This is the fundamental mechanic that makes target clearing order matter.

### Step 5: One Shot Per Line Per Side

Each shooter maintains a `ShooterTargetData` object that records which columns and rows it has already fired upon, tracked independently per side. When a shooter fires at column 3 from the Bottom side, column 3 is added to `_checkedColsForBottom`. On subsequent frames, even if the shooter is still aligned with column 3, the system checks this list and skips it. This prevents a single shooter from firing multiple bullets at the same column from the same side.

The tracking is per-side, not global. A shooter that passes column 3 on the Bottom side and later passes it again on the Top side (after completing the loop) can fire at the same column again from the other direction. This is intentional: the outermost alive target is different depending on the approach direction.

When the shooter completes a full conveyor loop and its board reaches the end of the spline, `ShooterTargetData.Reset()` is called, clearing all tracked columns and rows. If the shooter is re-dispatched from storage, it starts fresh.

### Step 6: Color Matching and Shooting

After a target is selected, the system verifies color compatibility. The target's `ColorId` is resolved to a `ShooterColorId` (which may differ when color grouping is enabled), and compared against the shooter's own `ColorId`. Only matching targets receive a bullet.

If the color matches and the shooter hasn't already fired at that line, a bullet is pulled from the pool, the shooter performs its recoil animation, and the target is immediately marked as destroyed (`MarketForHit`) to prevent other shooters from targeting it in the same frame. The actual visual destruction happens asynchronously when the bullet arrives.

---

## Bullet Pool

Bullets are managed through Unity's `ObjectPool<Bullet>`. When a shooter fires, a bullet is retrieved from the pool, positioned at the shooter's muzzle, and tweened toward the target using speed-based `DOMove`. On arrival, the bullet notifies the target via an event, then clears its trail renderer and returns to the pool. The pool starts with a default capacity of 30 and grows on demand. On level reset, all active bullets are force-released with their tweens killed.

---

## Storage System

When a shooter completes the conveyor loop without exhausting all its bullets, it needs somewhere to go. The `ShooterStorageController` manages a fixed array of `StoragePiece` slots. The shooter is placed into the first empty slot and can be re-dispatched by tapping it again.

**Overflow Failure:**
If all storage slots are occupied when a shooter returns, the game enters the Fail state. The shooter's conveyor flag is cleared and it is re-parented to its original transform to prevent orphaned objects.

**Arrangement:**
When a shooter leaves storage (re-dispatched to the conveyor), the remaining shooters are compacted: all are unassigned, collected into a temporary list (from Unity's `ListPool`), and reassigned sequentially to fill gaps from left to right.

---

## Level Data Architecture

All level configuration is stored in `LevelData`, a `ScriptableObject` containing:

- **Color Palette:** A list of `LevelColor` entries, each with a unique `Id`, an RGBA `Color`, and a `ShooterColorId` for grouping.
- **Target Data:** A flat list of `TargetData` entries (grid coordinates + color ID).
- **Shooter Lane Data:** A list of `ShooterLaneData`, each containing an ordered list of `ShooterData` (ID, bullet count, color, linked partner ID, coordinates, hidden flag).
- **Grid Parameters:** Target area dimensions, shooter lane count/height, grid cell sizes, storage slot count, and conveyor board count.

Color IDs are auto-assigned incrementally. When color grouping is enabled, `GetOrAddColorIdGrouped` creates a unique palette entry for each exact color but assigns the same `ShooterColorId` to colors within a configurable Euclidean distance tolerance. This allows targets to retain their visual diversity while shooters treat similar colors as one group.

---

## Level Creator Editor (Custom Inspector Tooling)

The entire level design workflow happens directly in the Unity Scene View through a custom editor. The editor is implemented as a `CustomEditor` for the `LevelCreator` MonoBehaviour and is split across five partial class files, each responsible for a distinct concern: core logic, tool window UI, visualization/handles, painting operations, and texture import with auto shooter generation.

### Architecture and Scene View Integration

The editor hooks into `SceneView.duringSceneGui` on `OnEnable` and unregisters on `OnDisable`. A static `s_active` reference ensures only one instance processes events at a time, preventing duplicate handling when multiple inspector windows exist. All scene interaction (mouse clicks, drags, keyboard shortcuts) is intercepted through Unity's `Event` system inside the `OnSceneGUI` callback.

The editor maintains two independent `GameGrid` instances: one for the shooter area (positioned below the conveyor) and one for the target area (centered within the conveyor bounds). Both grids are rebuilt whenever their dimensions change, and all visual objects in the scene are regenerated from the underlying `LevelData` ScriptableObject.

### The Floating Tool Window

A custom GUI window is rendered inside the Scene View using `Handles.BeginGUI` and `GUILayout.Window`. This window is:

- **Draggable** via the title bar (handled by `GUI.DragWindow`).
- **Resizable** via a bottom-right grip handle.
- **Scrollable** via `GUILayout.BeginScrollView` for content that exceeds the window height.
- **Persistent** across sessions: window dimensions, position, selected tool, brush color, bullet count, and all other preferences are saved to `EditorPrefs` with unique keys.

When the mouse hovers over the tool window, a default control ID is registered via `HandleUtility.AddDefaultControl` to prevent Scene View mouse events from passing through to the grid below. Only Layout and Repaint events are allowed through, which prevents accidental painting while adjusting sliders.

### Grid-Based Painting System

Mouse input is projected onto a horizontal `Plane(Vector3.up, Vector3.zero)` to determine the world position of the cursor. This position is then converted to grid coordinates using `GridHelper.TryGetGridFromPosition`, which tests whether the point falls within the shooter grid or the target grid.

**Paint Mode:** Left-clicking or dragging over the shooter grid creates a new shooter at the hovered cell (if one doesn't already exist). The shooter's color and bullet count can be overridden via toggles in the tool window; otherwise, they default to the current brush color and a standard count. The same gesture over the target grid creates target objects. The target brush supports a configurable radius (1-8 cells), allowing batch painting of rectangular areas in a single drag.

**Remove Mode:** The same click/drag gesture deletes shooters or targets at the cursor position. For targets, deletion respects the brush radius.

**Right-Click Actions:** Right-clicking a shooter initiates linking mode. The first right-click selects the shooter; the second right-click on a different shooter creates a bidirectional link between them. Pressing Escape cancels the operation.

**Keyboard Shortcuts:** `1` switches to Paint mode, `2` switches to Remove mode, and `Escape` cancels an active linking operation.

**Scene View Feedback:** During the Repaint event, the editor draws a white wireframe cube and disc at the hovered shooter cell, and a brush-radius preview rectangle at the hovered target cell using `Handles.DrawWireCube` and `Handles.DrawWireDisc`.

### Texture Import Pipeline

A source `Texture2D` can be assigned in the tool window. When "Generate From Texture" is clicked, the system:

1. **Ensures readability:** Checks the texture's import settings. If `isReadable` is false, the importer is programmatically modified, the asset is re-imported, and the texture reference is refreshed. This allows designers to drag in any texture without worrying about import configuration.

2. **Reads all pixels** in a single `GetPixels32` call (one allocation for the entire texture).

3. **Resamples via nearest-neighbor:** The grid dimensions (width x height) are user-defined and independent of the texture resolution. For each grid cell `(gx, gy)`, the corresponding texture pixel is calculated as `texX = floor(gx / gridW * texWidth)`, with Y-axis flipping to convert between texture space (Y=0 at bottom) and grid space (Y=0 at top). This means a 1024x1024 texture can be mapped onto a 20x20 grid or a 100x100 grid with identical logic.

4. **Builds the color palette:** Each pixel color is registered via `GetOrAddColorId` (which merges similar colors within a tolerance using Euclidean distance in RGB space) or `GetOrAddColorIdGrouped` (which creates a unique palette entry per exact color but groups similar colors under the same `ShooterColorId`). Transparent pixels (alpha = 0) are skipped entirely.

5. **Creates target data:** For every non-transparent pixel, a `TargetData` entry is added with the grid coordinates and the resolved color ID.

6. **Auto-calculates cell size:** The target cell size is computed to fit the entire grid within the conveyor bounds minus a configurable offset margin: `size = min(availableWidth / gridWidth, availableHeight / gridHeight)`. This ensures the pixel art fills the conveyor area regardless of grid resolution.

**Auto Aspect:** A convenience button that automatically adjusts the grid height to match the source texture's aspect ratio while keeping the width fixed.

### Color Grouping System

The tool window exposes a "Group Colors for Shooters" toggle. When enabled, the texture import uses a dual-ID system:

- Each target retains its **exact** color as a unique `LevelColor` entry in the palette, preserving visual fidelity.
- Colors within the tolerance distance share the same `ShooterColorId`, meaning they are treated as one color for shooter generation and bullet matching.

This is critical for pixel art with subtle gradients: a face texture might have 12 slightly different skin tones that should all be handled by a single "skin color" shooter. Without grouping, 12 separate shooter colors would be needed. With grouping, a single shooter type clears them all while the target grid retains the original gradient.

The brush palette in the tool window adapts to this mode: when grouping is enabled, only one swatch per `ShooterColorId` group is shown (using a `HashSet` to skip duplicates), keeping the palette clean. A "+" button opens a color picker to manually add new colors to the palette.

### Layer-by-Layer Auto Shooter Generation

It analyzes the target grid and automatically generates the optimal set of shooters to clear all targets.

**Layer calculation:** Each target's layer is defined as its minimum distance to any grid edge: `min(x, y, gridWidth - 1 - x, gridHeight - 1 - y)`. Layer 0 contains all edge targets, layer 1 contains targets one cell inward, and so on. A `SortedDictionary<int, Dictionary<int, int>>` maps each layer to a dictionary of (shooterColorId -> target count).

**Bullet allocation with carry-forward:** The algorithm processes layers from outermost (0) to innermost. For each layer and color:

1. It adds any **carried-over** bullets from previous layers to the current layer's target count.
2. If this color has targets in **future layers**, it creates only "full" shooters using the configured denominations (e.g., {5, 10, 20}), picking the largest denomination that fits via greedy allocation. Any remainder is carried forward to the next layer. This ensures shooters use clean, predictable bullet counts.
3. If this is the **last layer** for this color, it performs a final greedy decomposition with remainder: e.g., 27 bullets with denominations {5, 10, 20} becomes [20, 5, 2]. The smallest remainder gets its own shooter to ensure every target is covered.

**Lane placement:** The generated shooter specifications (color + bullet count) are distributed across lanes in round-robin order. Each shooter is assigned sequential Y coordinates within its lane. The grid height auto-expands to fit all generated shooters.

**Configurable denominations:** The bullet count list is editable in the tool window. Designers can add or remove denominations (displayed as removable tag buttons), and the list persists across sessions via `EditorPrefs`.

### Real-Time Validation

The tool window displays a validation panel that compares total bullets vs. total targets for each color group. Two dictionaries (`_bulletsPerColor` and `_targetsPerColor`) are rebuilt whenever the level data changes.

- Bullet counts are summed from all shooter lane data, grouped by `ColorId`.
- Target counts are summed from all target data, grouped by `ShooterColorId` (respecting color grouping).

Each color row displays a icon: a circle (checkmark) when `bullets == targets`, or a warning icon when they don't match. This gives designers instant visual feedback on whether a level is solvable without entering Play mode.

### Undo/Redo Integration

Every data mutation follows a strict protocol:

1. `Undo.RecordObject(levelData, operationName)` is called **before** any field changes, capturing the ScriptableObject's pre-modification state.
2. The modification is performed.
3. `EditorUtility.SetDirty(levelData)` marks the asset as changed.

For drag operations (painting multiple cells in one stroke), the system opens an undo group on `MouseDown` via `Undo.GetCurrentGroup()` and collapses all operations within the drag into a single undo step on `MouseUp` via `Undo.CollapseUndoOperations`. This means Ctrl+Z after a paint stroke undoes the entire stroke, not individual cells.

When undo or redo is performed, `OnUndoRedoPerformed` triggers a full rebuild: grids are recalculated, all scene objects (shooters and targets) are destroyed and recreated from the reverted data, and validation counts are refreshed.

### Auto Compact

An optional "Auto Compact" toggle automatically removes gaps in shooter lanes after each editing operation. When a shooter is deleted from the middle of a lane, the remaining shooters below it have non-sequential Y coordinates (e.g., 0, 1, 3 instead of 0, 1, 2). With auto compact enabled, on `MouseUp` the editor sorts each lane's shooter list by Y coordinate, reassigns sequential coordinates starting from 0, regenerates IDs, and updates any linked-shooter references that may have changed. The grid height is then recalculated to fit the compacted data.

---

## Event Bus

A lightweight, type-safe publish-subscribe system using generics. `EventBus<T>` maintains a static list of `EventBinding<T>` subscribers. Events are value types (structs) to avoid allocation. The system uses `PredefinedAssemblyUtil` to scan loaded assemblies at startup and pre-warm the bus for all `IEvent` implementations, ensuring zero-allocation dispatch at runtime. Components subscribe during initialization and unsubscribe in `OnDestroy` to prevent stale references.

---

## UI Panel System

UI is managed through an abstract `PanelBase` class that wraps `CanvasGroup` operations (alpha, interactability, raycast blocking) behind `ShowPanel`/`HidePanel` methods with virtual hooks (`OnBeforeShow`, `OnShown`, `OnBeforeHide`, `OnHidden`). The `UIManager` listens to gameplay state changes via the Event Bus and shows/hides the appropriate panel (Gameplay, Level Completed, Level Failed). This decouples UI transitions from game logic entirely.

---

## Singleton Pattern

A generic `Singleton<T>` base class for `MonoBehaviour` components. On first access, it searches the scene via `FindFirstObjectByType<T>`. If none exists, it creates a new GameObject. The `Awake` method handles duplicate prevention: if an instance already exists, the newcomer destroys itself. `ScriptableObject` singletons (`GameConfigs`, `ShooterVisualsConfigs`) use a different pattern: a static field set during explicit `Initialize()` calls at startup, with an editor-time fallback that uses `AssetDatabase.FindAssets` for inspector access outside Play mode.

---

## DOTween Integration Patterns

Animation throughout the project uses DOTween with several deliberate patterns:

- **Sequence Grouping:** Related tweens (jump arc + rotation) are combined into a single `Sequence` to share lifecycle. This prevents one tween from completing independently while the other is killed.
- **SetLink:** All gameplay tweens are bound to their GameObject via `.SetLink()`, ensuring automatic cleanup when objects are destroyed or deactivated.
- **Kill Before Create:** Every tween-creating method kills any existing tween on the same target first, preventing tween stacking from rapid inputs.
- **Recoil & Reject Animations:** Shooters use `DOShakeScale` for firing recoil and `DOShakeRotation` for rejection feedback, both with harmonic randomness for natural-feeling motion.
- **Target Disappear:** Targets chain `DOShakeScale` into `DOScale(0)` for a satisfying pop-and-shrink destruction effect.

---

## Grid Coordinate System

The game uses a unified grid system (`GameGrid`) for both shooter lanes and target areas. `GridHelper` provides bidirectional conversion between world positions and grid coordinates.

Grid coordinates use (x, y) where x is the column and y is the row (top-to-bottom). World positions are calculated from a center point with configurable cell size. The target grid auto-calculates its cell size to fit within the conveyor bounds with an optional offset margin, ensuring the pixel art always fills the available space regardless of resolution.

The system supports independent grids for shooters and targets with different cell sizes, dimensions, and world positions, all computed relative to the conveyor's physical bounds.
