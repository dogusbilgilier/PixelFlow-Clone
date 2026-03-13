#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Freya;
using Game;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelCreator))]
public partial class LevelCreatorEditor : Editor
{
    private static LevelCreatorEditor s_active;
    private LevelCreator _levelCreator;

    // --- Conveyor ---
    private MainConveyor _mainConveyor;
    private Bounds _mainConveyorBounds;

    // --- Mouse Hover ---
    private Vector3 _hoverCellCenter;
    private bool _isMouseInShooterGrid;
    private bool _isMouseInTargetGrid;
    private Vector2Int _currentHoverCellCoords;

    // --- Scene GUI Tool Window ---
    private Rect _toolWindowRect = new Rect(10, 10, 280, 120);
    private readonly int _toolWindowId = "LevelCreatorEditor.ToolWindow".GetHashCode();
    private bool _isMouseOverToolWindow;
    private float _toolContentHeight;
    private EditTool _editTool = EditTool.Paint;
    private const string PrefKey_EditTool = "LevelCreatorEditor.EditTool";

    // --- Resize ---
    private bool _isResizingToolWindow;
    private const float MinToolWindowWidth = 220f;
    private const float MaxToolWindowWidth = 600f;
    private const float MinToolWindowHeight = 200f;
    private const float ResizeHandleSize = 16f;
    private const string PrefKey_ToolWindowWidth = "LevelCreatorEditor.ToolWindowWidth";
    private const string PrefKey_ToolWindowHeight = "LevelCreatorEditor.ToolWindowHeight";

    // --- Linking ---
    private bool _isLinking;
    private Shooter _currentlyLinkingShooter;

    // --- Brush ---
    private bool _overrideColor;
    private int _brushColorId;
    private const string PrefKey_BrushColorId = "LevelCreatorEditor.BrushColorId";

    // --- Bullet Count ---
    private bool _overrideBulletCount;
    private int _bulletCount;
    private const string PrefKey_BulletCount = "LevelCreatorEditor.BulletCount";

    // --- Shooter Grid ---
    private GameGrid _shooterAreaGrid;
    private readonly Plane _gridPlane = new Plane(Vector3.up, Vector3.zero);
    private int _lastInitializedLaneCount;
    private int _lastInitializedHeight;
    private bool _autoCompactShooters;
    private const string PrefKey_AutoCompact = "LevelCreatorEditor.AutoCompact";
    private float _lastInitializedSize;

    // --- Target Area ---
    private GameGrid _targetAreaGrid;
    private const string PrefKey_TargetBrushRadius = "LevelCreatorEditor.TargetBrushRadius";
    private int _targetBrushSize = 1;
    private int _lastInitializedTargetWidth;
    private int _lastInitializedTargetHeight;
    private float _lastInitializedTargetSize;

    // --- Texture Import ---
    private int _colorTolerance;
    private const string PrefKey_ColorTolerance = "LevelCreatorEditor.ColorTolerance";
    private float _targetAreaOffset;
    private const string PrefKey_TargetAreaOffset = "LevelCreatorEditor.TargetAreaOffset";

    // --- Scroll ---
    private Vector2 _toolScrollPosition;
    private const float MaxToolWindowHeight = 6000f;

    // --- Auto Shooter Generation ---
    private List<int> _shooterBulletCounts = new List<int> { 5, 10, 20 };
    private string _newBulletCountInput = "";
    private const string PrefKey_ShooterBulletCounts = "LevelCreatorEditor.ShooterBulletCounts";

    // --- Bound Preferences ---
    private bool _drawGameAreaBounds;
    private const string PrefKey_DrawGameAreaBounds = "LevelCreatorEditor.DrawGameAreaBounds";
    private bool _drawConveyorBounds;
    private const string PrefKey_DrawConveyorBounds = "LevelCreatorEditor.DrawConveyorBounds";

    // --- Validation ---
    private readonly Dictionary<int, int> _bulletsPerColor = new();
    private readonly Dictionary<int, int> _targetsPerColor = new();

    // --- Undo ---
    private int _dragUndoGroup = -1;

    #region UNITY FUNCTIONS

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    private void OnEnable()
    {
        _levelCreator = (LevelCreator)target;

        InitializeMainConveyor();
        InitializePreferences();

        _shooterAreaGrid = GridHelper.CreateShooterGrid(_levelCreator.LevelData, _mainConveyorBounds.min.z);

        // Recalculate target area size based on conveyor bounds
        _levelCreator.LevelData.targetAreaSize = CalculateTargetAreaSize();
        _targetAreaGrid = GridHelper.CreateTargetAreaGrid(_levelCreator.LevelData, _mainConveyorBounds.center);

        CreateVisualsFromLevelData();
        RecalculateShooterGridHeight();
        UpdateBulletAndTargetsCounts();

        SceneView.duringSceneGui -= OnSceneGUI;

        if (s_active != null && s_active != this)
            SceneView.duringSceneGui -= s_active.OnSceneGUI;

        s_active = this;

        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;

        if (s_active == this)
            s_active = null;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (s_active != this)
            return;

        if (_levelCreator.LevelData == null)
            return;

        Event e = Event.current;

        HandleShortcuts(e);
        DrawEditorToolsWindow();

        _isMouseOverToolWindow = _toolWindowRect.Contains(e.mousePosition) || _isResizingToolWindow;

        if (_isMouseOverToolWindow)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (e.type != EventType.Layout && e.type != EventType.Repaint)
                return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            OnMouseMove(e, sceneView);

            if (_editTool == EditTool.Paint)
            {
                CreateShooter();
                CreateTargetObject();
            }
            else if (_editTool == EditTool.Remove)
                DeleteCurrentlyHoveringObject();

            e.Use();
        }
        else if (e.type == EventType.MouseDown)
        {
            if (e.button == 0)
            {
                _dragUndoGroup = Undo.GetCurrentGroup();
                OnLeftMouseClick(e);
                e.Use();
            }
            else if (e.button == 1)
            {
                OnRightMouseClick(e);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (_autoCompactShooters)
            {
                RecordUndo("Compact Shooters");
                CompactShooterLanes();
            }

            if (_dragUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_dragUndoGroup);
                _dragUndoGroup = -1;
            }
        }
        else if (e.type == EventType.MouseMove && e.button != 2)
        {
            OnMouseMove(e, sceneView);
            e.Use();
        }
        else if (e.type == EventType.Repaint)
        {
            OnHandlesDraw();

            if (_isMouseInShooterGrid)
            {
                float size = _shooterAreaGrid.Size;

                Handles.color = Color.white;
                Handles.DrawWireCube(_hoverCellCenter, new Vector3(size, 0.01f, size));
                Handles.DrawWireDisc(_hoverCellCenter, Vector3.up, size * 0.3f);
            }

            if (_isMouseInTargetGrid)
            {
                DrawTargetBrushPreview();
            }
        }
    }

    #endregion

    #region INITIALIZATION

    private void UpdateBulletAndTargetsCounts()
    {
        _bulletsPerColor.Clear();
        _targetsPerColor.Clear();

        if (_levelCreator == null || _levelCreator.LevelData == null)
            return;

        var levelData = _levelCreator.LevelData;

        if (levelData.shooterLaneDataList != null)
        {
            foreach (var lane in levelData.shooterLaneDataList)
            {
                if (lane == null || lane.ShooterDataList == null)
                    continue;

                foreach (var s in lane.ShooterDataList)
                {
                    if (s == null)
                        continue;

                    int bc = Mathf.Max(0, s.BulletCount);
                    if (!_bulletsPerColor.TryAdd(s.ColorId, bc)) _bulletsPerColor[s.ColorId] += bc;
                }
            }
        }

        if (levelData.targetDataList != null)
        {
            foreach (var t in levelData.targetDataList)
            {
                if (t == null)
                    continue;

                if (!_targetsPerColor.TryAdd(t.ColorId, 1)) _targetsPerColor[t.ColorId] += 1;
            }
        }

        SceneView.RepaintAll();
    }

    private void InitializeMainConveyor()
    {
        _mainConveyor = FindFirstObjectByType<MainConveyor>();

        if (_mainConveyor == null)
        {
            if (_levelCreator.mainConveyorPrefab == null)
                return;

            _mainConveyor = Instantiate(_levelCreator.mainConveyorPrefab);
        }

        if (_mainConveyor == null)
            return;

        if (_mainConveyor.Spline.TryGetComponent(out Renderer renderer))
            _mainConveyorBounds = renderer.bounds;
    }

    private void InitializePreferences()
    {
        _brushColorId = EditorPrefs.GetInt(PrefKey_BrushColorId, 0);

        _editTool = (EditTool)EditorPrefs.GetInt(PrefKey_EditTool, (int)EditTool.Paint);
        _bulletCount = EditorPrefs.GetInt(PrefKey_BulletCount, 10);

        _drawConveyorBounds = EditorPrefs.GetBool(PrefKey_DrawConveyorBounds, true);
        _drawGameAreaBounds = EditorPrefs.GetBool(PrefKey_DrawGameAreaBounds, true);
        _autoCompactShooters = EditorPrefs.GetBool(PrefKey_AutoCompact, false);

        _targetBrushSize = EditorPrefs.GetInt(PrefKey_TargetBrushRadius, 0);
        _targetBrushSize = Mathf.Max(1, _targetBrushSize);

        _lastInitializedLaneCount = _levelCreator.LevelData.shooterLaneCount;
        _lastInitializedHeight = _levelCreator.LevelData.shooterLaneHeight;
        _lastInitializedSize = _levelCreator.LevelData.shooterGridSize;

        _lastInitializedTargetWidth = _levelCreator.LevelData.targetAreaWidth;
        _lastInitializedTargetHeight = _levelCreator.LevelData.targetAreaHeight;
        _lastInitializedTargetSize = _levelCreator.LevelData.targetAreaSize;

        _colorTolerance = EditorPrefs.GetInt(PrefKey_ColorTolerance, 0);
        _targetAreaOffset = EditorPrefs.GetFloat(PrefKey_TargetAreaOffset, 0f);

        _toolWindowRect.width = EditorPrefs.GetFloat(PrefKey_ToolWindowWidth, 280f);
        _toolWindowRect.height = EditorPrefs.GetFloat(PrefKey_ToolWindowHeight, 400f);

        LoadBulletCountList();
    }

    #endregion

    #region UNDO

    private void RecordUndo(string operationName)
    {
        Undo.RecordObject(_levelCreator.LevelData, operationName);
    }

    private void MarkLevelDataDirty()
    {
        EditorUtility.SetDirty(_levelCreator.LevelData);
    }

    private void OnUndoRedoPerformed()
    {
        if (_levelCreator == null || _levelCreator.LevelData == null)
            return;

        _levelCreator.LevelData.targetAreaSize = CalculateTargetAreaSize();
        _targetAreaGrid = GridHelper.CreateTargetAreaGrid(_levelCreator.LevelData, _mainConveyorBounds.center);

        RecalculateShooterGridHeight();

        _lastInitializedLaneCount = _levelCreator.LevelData.shooterLaneCount;
        _lastInitializedSize = _levelCreator.LevelData.shooterGridSize;

        _lastInitializedTargetWidth = _levelCreator.LevelData.targetAreaWidth;
        _lastInitializedTargetHeight = _levelCreator.LevelData.targetAreaHeight;
        _lastInitializedTargetSize = _levelCreator.LevelData.targetAreaSize;

        CreateVisualsFromLevelData();
        UpdateBulletAndTargetsCounts();

        SceneView.RepaintAll();
    }

    #endregion

    #region SHARED UTILITIES

    private void DeleteCurrentlyHoveringObject()
    {
        if (_isMouseInShooterGrid && GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, _currentHoverCellCoords, out _) && IsShooterExist(out Shooter shooter))
        {
            DeleteShooter(shooter);
            return;
        }

        if (_isMouseInTargetGrid && GridHelper.TryGetPositionFromCoords(_targetAreaGrid, _currentHoverCellCoords, out _))
        {
            DeleteTargetObjectsInBrushArea();
        }
    }

    private bool IsShooterExist(out Shooter shooter)
    {
        shooter = null;

        foreach (var shooterInScene in _levelCreator.shooterParent.transform.GetComponentsInChildren<Shooter>())
        {
            if (shooterInScene.Data.Coordinates == _currentHoverCellCoords)
            {
                shooter = shooterInScene;
                return true;
            }
        }

        return false;
    }

    private bool TryGetShooterFromId(int id, out Shooter shooter)
    {
        shooter = null;

        var shooters = _levelCreator.shooterParent.GetComponentsInChildren<Shooter>();
        foreach (var shooterObject in shooters)
        {
            if (shooterObject.Data.ID == id)
            {
                shooter = shooterObject;
                return true;
            }
        }

        return false;
    }

    private bool TryGetTargetObjectAtCoords(Vector2Int coords, out TargetObject targetObject)
    {
        targetObject = null;

        foreach (var t in _levelCreator.targetObjectParent.transform.GetComponentsInChildren<TargetObject>())
        {
            if (t.Data.Coordinates == coords)
            {
                targetObject = t;
                return true;
            }
        }

        return false;
    }

    private void CreateVisualsFromLevelData()
    {
        _levelCreator.targetObjectParent.DestroyAllChildrenImmediate();
        _levelCreator.shooterParent.DestroyAllChildrenImmediate();

        foreach (TargetData targetObjectData in _levelCreator.LevelData.targetDataList)
        {
            CreateTargetObjectByData(targetObjectData);
        }

        foreach (var shooterLaneData in _levelCreator.LevelData.shooterLaneDataList)
        {
            foreach (ShooterData shooterData in shooterLaneData.ShooterDataList)
            {
                CreateShooterByData(shooterData);
            }
        }
    }

    private void CreateTargetObjectByData(TargetData targetObjectData)
    {
        if (GridHelper.TryGetPositionFromCoords(_targetAreaGrid, targetObjectData.Coordinates, out Vector3 position))
        {
            var targetObject = PrefabUtility.InstantiatePrefab(_levelCreator.targetObjectPrefab, _levelCreator.targetObjectParent) as TargetObject;
            targetObject.transform.position = position;
            targetObject.transform.localScale = new Vector3(_targetAreaGrid.Size, 1f, _targetAreaGrid.Size);
            targetObject.SetData(targetObjectData, _levelCreator.LevelData);
        }
    }

    private void CreateShooterByData(ShooterData shooterData)
    {
        if (GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, shooterData.Coordinates, out Vector3 position))
        {
            var shooter = PrefabUtility.InstantiatePrefab(_levelCreator.shooterPrefab, _levelCreator.shooterParent) as Shooter;
            shooter.transform.position = position;
            shooter.SetData(shooterData, _levelCreator.LevelData);
        }
    }

    private void RecalculateShooterGridHeight()
    {
        int maxY = -1;

        foreach (var laneData in _levelCreator.LevelData.shooterLaneDataList)
        {
            if (laneData?.ShooterDataList == null)
                continue;

            foreach (var shooterData in laneData.ShooterDataList)
            {
                if (shooterData != null && shooterData.Coordinates.y > maxY)
                    maxY = shooterData.Coordinates.y;
            }
        }

        int newHeight = Mathf.Max(1, maxY + 2);

        _levelCreator.LevelData.shooterLaneHeight = newHeight;
        _shooterAreaGrid = GridHelper.CreateShooterGrid(_levelCreator.LevelData, _mainConveyorBounds.min.z);
        _lastInitializedHeight = newHeight;
    }

    private void CompactShooterLanes()
    {
        bool anyChanged = false;
        var idMapping = new Dictionary<int, int>();

        foreach (var laneData in _levelCreator.LevelData.shooterLaneDataList)
        {
            if (laneData?.ShooterDataList == null || laneData.ShooterDataList.Count == 0)
                continue;

            laneData.ShooterDataList.Sort((a, b) => a.Coordinates.y.CompareTo(b.Coordinates.y));

            for (int i = 0; i < laneData.ShooterDataList.Count; i++)
            {
                var data = laneData.ShooterDataList[i];

                if (data.Coordinates.y == i)
                    continue;

                anyChanged = true;

                int oldId = data.ID;
                var newCoords = new Vector2Int(data.Coordinates.x, i);
                int newId = int.Parse((newCoords.x + 1) + "" + i);

                idMapping[oldId] = newId;

                data.Coordinates = newCoords;
                data.ID = newId;
            }
        }

        if (!anyChanged)
            return;

        // Update linked shooter references
        foreach (var laneData in _levelCreator.LevelData.shooterLaneDataList)
        {
            if (laneData?.ShooterDataList == null)
                continue;

            foreach (var data in laneData.ShooterDataList)
            {
                if (data.LinkedShooterID != -1 && idMapping.TryGetValue(data.LinkedShooterID, out int newLinkedId))
                    data.LinkedShooterID = newLinkedId;
            }
        }

        RecalculateShooterGridHeight();
        CreateVisualsFromLevelData();
        MarkLevelDataDirty();
        UpdateBulletAndTargetsCounts();
    }

    private void OnShooterGridChanged()
    {
        var shooters = _levelCreator.shooterParent.GetComponentsInChildren<Shooter>();
        foreach (var shooter in shooters)
        {
            var coords = shooter.Data.Coordinates;
            if (coords.x >= _levelCreator.LevelData.shooterLaneCount || coords.y >= _levelCreator.LevelData.shooterLaneHeight)
            {
                DeleteShooter(shooter);
            }
            else if (GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, coords, out var cellCenter))
            {
                shooter.transform.position = cellCenter;
            }
        }
    }

    private void OnTargetGridChanged()
    {
        var targets = _levelCreator.targetObjectParent.GetComponentsInChildren<TargetObject>();
        foreach (var target in targets)
        {
            var coords = target.Data.Coordinates;
            if (coords.x >= _levelCreator.LevelData.targetAreaWidth || coords.y >= _levelCreator.LevelData.targetAreaHeight)
            {
                RecordUndo("Remove Out-of-Bounds Target");
                OnTargetObjectDataUpdated(target.Data, isDestroyed: true);
                DestroyImmediate(target.gameObject);
            }
            else if (GridHelper.TryGetPositionFromCoords(_targetAreaGrid, coords, out var cellCenter))
            {
                target.transform.position = cellCenter;
                target.transform.localScale = new Vector3(_targetAreaGrid.Size, 1f, _targetAreaGrid.Size);
            }
        }
    }

    #endregion
}

#endif
