#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Freya;
using Game;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelCreator))]
public class LevelCreatorEditor : Editor
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

    #region TOOL WINDOW

    private void DrawToolWindow(int id)
    {
        _toolScrollPosition = GUILayout.BeginScrollView(_toolScrollPosition);

        DrawEditToolArea();

        InsertGUISeparator();
        DrawBoundsArea();

        InsertGUISeparator();
        DrawShooterAreaGridOptions();

        InsertGUISeparator();
        DrawTargetAreaGridOptions();


        InsertGUISeparator();
        DrawAutoShooterSection();

        if (_isLinking)
        {
            InsertGUISeparator();
            DrawLinkingArea();
        }

        InsertGUISeparator();
        DrawColorArea();

        InsertGUISeparator();
        DrawBulletCountArea();

        InsertGUISeparator();
        DrawValidationArea();

        if (Event.current.type == EventType.Repaint)
            _toolContentHeight = GUILayoutUtility.GetLastRect().yMax;

        GUILayout.EndScrollView();

        HandleResizeGrip();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private void HandleResizeGrip()
    {
        Rect resizeRect = new Rect(
            _toolWindowRect.width - ResizeHandleSize,
            _toolWindowRect.height - ResizeHandleSize,
            ResizeHandleSize,
            ResizeHandleSize);

        // Draw diagonal grip dots
        Color gripColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        float dotSize = 2f;
        float spacing = 4f;
        // 3 diagonal lines of dots: ·  ··  ···
        for (int diag = 0; diag < 3; diag++)
        {
            for (int d = 0; d <= diag; d++)
            {
                float x = resizeRect.xMax - (3 - diag) * spacing;
                float y = resizeRect.yMax - (d + 1) * spacing;
                EditorGUI.DrawRect(new Rect(x, y, dotSize, dotSize), gripColor);
            }
        }

        EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeUpLeft);

        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && resizeRect.Contains(e.mousePosition))
        {
            _isResizingToolWindow = true;
            e.Use();
        }
    }

    private void DrawEditorToolsWindow()
    {
        Handles.BeginGUI();

        // Handle resize drag/release events (works even when mouse is outside the window)
        if (_isResizingToolWindow)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDrag)
            {
                _toolWindowRect.width = Mathf.Clamp(
                    _toolWindowRect.width + e.delta.x,
                    MinToolWindowWidth,
                    MaxToolWindowWidth);
                _toolWindowRect.height = Mathf.Clamp(
                    _toolWindowRect.height + e.delta.y,
                    MinToolWindowHeight,
                    MaxToolWindowHeight);
                EditorPrefs.SetFloat(PrefKey_ToolWindowWidth, _toolWindowRect.width);
                EditorPrefs.SetFloat(PrefKey_ToolWindowHeight, _toolWindowRect.height);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isResizingToolWindow = false;
                e.Use();
            }
        }

        _toolWindowRect = GUILayout.Window(_toolWindowId, _toolWindowRect, DrawToolWindow, "Level Creator Tools");
        Handles.EndGUI();
    }

    private void DrawBoundsArea()
    {
        _drawConveyorBounds = EditorGUILayout.Toggle("Draw Conveyor Bounds", _drawConveyorBounds);
        EditorPrefs.SetBool(PrefKey_DrawConveyorBounds, _drawConveyorBounds);

        _drawGameAreaBounds = EditorGUILayout.Toggle("Draw Game Area Bounds", _drawGameAreaBounds);
        EditorPrefs.SetBool(PrefKey_DrawGameAreaBounds, _drawGameAreaBounds);
    }

    private void DrawEditToolArea()
    {
        string[] toolNames = Enum.GetNames(typeof(EditTool));
        int newIndex = GUILayout.Toolbar((int)_editTool, toolNames);

        if (newIndex != (int)_editTool)
        {
            _editTool = (EditTool)newIndex;
            EditorPrefs.SetInt(PrefKey_EditTool, (int)_editTool);
            _isLinking = false;
            _currentlyLinkingShooter = null;

            SceneView.RepaintAll();
        }

        GUILayout.Label("1 = Paint, 2 = Remove, ESC = Cancel Link, Ctrl+Z/Y = Undo/Redo", EditorStyles.miniLabel);
    }

    private void DrawShooterAreaGridOptions()
    {
        EditorGUILayout.LabelField("Shooter Grid Options", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newLaneCount = EditorGUILayout.IntSlider("Shooter Grid Width", _levelCreator.LevelData.shooterLaneCount, 1, 5);
        float minShooterSize = _levelCreator.shooterPrefab.ShooterVisual.ShooterRenderer.bounds.size.x;
        float maxShooterSize = minShooterSize * 2f;
        float newGridSize = EditorGUILayout.Slider("Shooter Grid Size", _levelCreator.LevelData.shooterGridSize, minShooterSize, maxShooterSize);
        int newStorageCount = EditorGUILayout.IntSlider("Shooter Storage Count", _levelCreator.LevelData.storageCount, 1, 5);

        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Modify Shooter Grid");

            _levelCreator.LevelData.shooterLaneCount = newLaneCount;
            _levelCreator.LevelData.shooterGridSize = newGridSize;
            _levelCreator.LevelData.storageCount = newStorageCount;

            MarkLevelDataDirty();

            bool isWidthChanged = _lastInitializedLaneCount != newLaneCount;
            bool isSizeChanged = !Mathf.Approximately(_lastInitializedSize, newGridSize);

            if (isWidthChanged || isSizeChanged)
            {
                RecalculateShooterGridHeight();
                OnShooterGridChanged();

                _lastInitializedLaneCount = newLaneCount;
                _lastInitializedSize = newGridSize;
            }
        }

        bool newCompact = EditorGUILayout.Toggle("Auto Compact", _autoCompactShooters);
        if (newCompact != _autoCompactShooters)
        {
            _autoCompactShooters = newCompact;
            EditorPrefs.SetBool(PrefKey_AutoCompact, _autoCompactShooters);
        }
    }

    private void DrawTargetAreaGridOptions()
    {
        EditorGUILayout.LabelField("Target Grid Options", EditorStyles.boldLabel);

        // --- Texture Import ---
        EditorGUI.BeginChangeCheck();
        var newTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture", _levelCreator.LevelData.sourceTexture, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Set Source Texture");
            _levelCreator.LevelData.sourceTexture = newTexture;
            MarkLevelDataDirty();
        }

        // --- Texture Info + Preview ---
        if (_levelCreator.LevelData.sourceTexture != null)
        {
            var tex = _levelCreator.LevelData.sourceTexture;
            EditorGUILayout.LabelField($"Texture: {tex.width} x {tex.height} px", EditorStyles.miniLabel);
        }

        // --- Color Tolerance ---
        int newTolerance = EditorGUILayout.IntSlider("Color Tolerance", _colorTolerance, 0, 128);
        if (newTolerance != _colorTolerance)
        {
            _colorTolerance = newTolerance;
            EditorPrefs.SetInt(PrefKey_ColorTolerance, _colorTolerance);
        }

        // --- Buttons Row ---
        if (_levelCreator.LevelData.sourceTexture != null)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate From Texture"))
                    GenerateFromTexture();

                if (GUILayout.Button("Auto Aspect"))
                    ApplyAutoAspect();
            }
        }

        GUILayout.Space(4);

        // --- Grid Dimensions ---
        EditorGUI.BeginChangeCheck();
        int newWidth = EditorGUILayout.IntSlider("Target Grid Width", _levelCreator.LevelData.targetAreaWidth, 1, 100);
        int newHeight = EditorGUILayout.IntSlider("Target Grid Height", _levelCreator.LevelData.targetAreaHeight, 1, 100);

        if (EditorGUI.EndChangeCheck())
        {
            RecordUndo("Modify Target Grid");

            _levelCreator.LevelData.targetAreaWidth = newWidth;
            _levelCreator.LevelData.targetAreaHeight = newHeight;
            _levelCreator.LevelData.targetAreaSize = CalculateTargetAreaSize();

            MarkLevelDataDirty();

            bool isWidthChanged = _lastInitializedTargetWidth != newWidth;
            bool isHeightChanged = _lastInitializedTargetHeight != newHeight;

            if (isWidthChanged || isHeightChanged)
            {
                _targetAreaGrid = GridHelper.CreateTargetAreaGrid(_levelCreator.LevelData, _mainConveyorBounds.center);
                OnTargetGridChanged();

                _lastInitializedTargetWidth = newWidth;
                _lastInitializedTargetHeight = newHeight;
                _lastInitializedTargetSize = _levelCreator.LevelData.targetAreaSize;
            }
        }

        // --- Offset ---
        EditorGUI.BeginChangeCheck();
        float minOffset = _mainConveyor.Spline.GetPointSize(0) + 0.1f;
        float maxOffset = 5f;
        float newOffset = EditorGUILayout.Slider("Target Area Offset", _targetAreaOffset, minOffset, maxOffset);
        if (EditorGUI.EndChangeCheck())
        {
            _targetAreaOffset = Mathf.Max(0f, newOffset);
            EditorPrefs.SetFloat(PrefKey_TargetAreaOffset, _targetAreaOffset);

            RecordUndo("Modify Target Offset");
            _levelCreator.LevelData.targetAreaSize = CalculateTargetAreaSize();
            MarkLevelDataDirty();

            _targetAreaGrid = GridHelper.CreateTargetAreaGrid(_levelCreator.LevelData, _mainConveyorBounds.center);
            _lastInitializedTargetSize = _levelCreator.LevelData.targetAreaSize;

            CreateVisualsFromLevelData();
        }

        // --- Cell Size (auto, disabled) ---
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.FloatField("Cell Size (auto)", _levelCreator.LevelData.targetAreaSize);
        EditorGUI.EndDisabledGroup();
    }

    private float CalculateTargetAreaSize()
    {
        float availableW = _mainConveyorBounds.size.x - 2f * _targetAreaOffset;
        float availableH = _mainConveyorBounds.size.z - 2f * _targetAreaOffset;
        int gridW = _levelCreator.LevelData.targetAreaWidth;
        int gridH = _levelCreator.LevelData.targetAreaHeight;

        if (gridW <= 0 || gridH <= 0)
            return 0.5f;

        float size = Mathf.Min(availableW / gridW, availableH / gridH);
        return Mathf.Max(0.01f, size);
    }

    private void ApplyAutoAspect()
    {
        var tex = _levelCreator.LevelData.sourceTexture;
        if (tex == null)
            return;

        RecordUndo("Auto Aspect");

        int w = _levelCreator.LevelData.targetAreaWidth;
        int h = Mathf.Max(1, Mathf.RoundToInt((float)w * tex.height / tex.width));
        _levelCreator.LevelData.targetAreaHeight = h;
        _levelCreator.LevelData.targetAreaSize = CalculateTargetAreaSize();

        MarkLevelDataDirty();

        _targetAreaGrid = GridHelper.CreateTargetAreaGrid(_levelCreator.LevelData, _mainConveyorBounds.center);

        _lastInitializedTargetWidth = w;
        _lastInitializedTargetHeight = h;
        _lastInitializedTargetSize = _levelCreator.LevelData.targetAreaSize;

        OnTargetGridChanged();
        CreateVisualsFromLevelData();
        UpdateBulletAndTargetsCounts();
    }

    private void DrawColorArea()
    {
        EditorGUILayout.LabelField("Brush Color", EditorStyles.boldLabel);
        _overrideColor = EditorGUILayout.Toggle("Override Color", _overrideColor);
        DrawBrushColorRow();
        _targetBrushSize = EditorGUILayout.IntSlider("Brush Size For Target Area", _targetBrushSize, 1, 8);
        _targetBrushSize = Mathf.Max(1, _targetBrushSize);

        EditorPrefs.SetInt(PrefKey_TargetBrushRadius, _targetBrushSize);
    }

    private void DrawBrushColorRow()
    {
        var palette = _levelCreator.LevelData.colorPalette;

        if (palette == null || palette.Count == 0)
        {
            EditorGUILayout.LabelField("No colors in palette. Import a texture.", EditorStyles.miniLabel);
            return;
        }

        const int colsPerRow = 5;

        for (int i = 0; i < palette.Count; i++)
        {
            if (i % colsPerRow == 0)
            {
                if (i > 0)
                    GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }

            var levelColor = palette[i];
            Color bg = levelColor.Color;

            var prevBg = GUI.backgroundColor;
            var prevContent = GUI.contentColor;

            GUI.backgroundColor = bg;
            GUI.contentColor = (bg.grayscale > 0.6f) ? Color.black : Color.white;

            string label = (levelColor.Id == _brushColorId) ? "\u2713" : "";
            if (GUILayout.Button(new GUIContent(label, $"Color {levelColor.Id}"), GUILayout.Width(26), GUILayout.Height(20)))
                SetBrushColor(levelColor.Id);

            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
        }

        if (palette.Count > 0)
            GUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Selected Color ID: {_brushColorId}", EditorStyles.miniLabel);
    }

    private void DrawLinkingArea()
    {
        EditorGUILayout.HelpBox("Linking mode active. (Press Esc to exit)", MessageType.Info);
        if (GUILayout.Button("Cancel Link"))
        {
            _isLinking = false;
            _currentlyLinkingShooter = null;
        }
    }

    private void DrawBulletCountArea()
    {
        EditorGUILayout.LabelField("Set Bullet Count", EditorStyles.boldLabel);
        _overrideBulletCount = EditorGUILayout.Toggle("Override Bullet Count", _overrideBulletCount);
        _bulletCount = EditorGUILayout.IntField("Count", _bulletCount);
        _bulletCount = Mathf.Max(0, _bulletCount);
        EditorPrefs.SetInt(PrefKey_BulletCount, _bulletCount);
    }

    private void DrawValidationArea()
    {
        GUIContent warningIcon = EditorGUIUtility.IconContent("d_ProfilerColumn.WarningCount", "");
        GUIContent circleIcon = EditorGUIUtility.IconContent("d_CircleCollider2D Icon", "");

        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        var palette = _levelCreator.LevelData.colorPalette;
        if (palette == null || palette.Count == 0)
        {
            EditorGUILayout.LabelField("No palette loaded.", EditorStyles.miniLabel);
            return;
        }

        using (new EditorGUILayout.VerticalScope())
        {
            foreach (var levelColor in palette)
            {
                int bulletCount = _bulletsPerColor.GetValueOrDefault(levelColor.Id, 0);
                int targetObjectCount = _targetsPerColor.GetValueOrDefault(levelColor.Id, 0);
                bool isMet = bulletCount == targetObjectCount;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(6);

                    var prev = GUI.color;
                    GUI.color = levelColor.Color;
                    GUILayout.Label(isMet ? circleIcon : warningIcon, GUILayout.Width(20), GUILayout.Height(20));
                    GUI.color = prev;

                    GUILayout.Label($"Bullets: {bulletCount}", GUILayout.Width(90));
                    GUILayout.Label($"Targets: {targetObjectCount}", GUILayout.Width(90));
                }
            }
        }
    }

    private void DrawAutoShooterSection()
    {
        EditorGUILayout.LabelField("Auto Shooter Generation", EditorStyles.boldLabel);

        // Display bullet count list
        EditorGUILayout.LabelField("Bullet Counts:", EditorStyles.miniLabel);

        using (new GUILayout.HorizontalScope())
        {
            for (int i = 0; i < _shooterBulletCounts.Count; i++)
            {
                if (GUILayout.Button($"{_shooterBulletCounts[i]} \u2715", GUILayout.Height(20)))
                {
                    _shooterBulletCounts.RemoveAt(i);
                    SaveBulletCountList();
                    GUIUtility.ExitGUI();
                }
            }
        }

        using (new GUILayout.HorizontalScope())
        {
            _newBulletCountInput = EditorGUILayout.TextField(_newBulletCountInput, GUILayout.Width(50));
            if (GUILayout.Button("Add", GUILayout.Width(40)))
            {
                if (int.TryParse(_newBulletCountInput, out int val) && val > 0 && !_shooterBulletCounts.Contains(val))
                {
                    _shooterBulletCounts.Add(val);
                    _shooterBulletCounts.Sort();
                    SaveBulletCountList();
                }

                _newBulletCountInput = "";
            }
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Generate Shooters", GUILayout.Height(30)))
            AutoGenerateShooters();
    }

    private void SaveBulletCountList()
    {
        EditorPrefs.SetString(PrefKey_ShooterBulletCounts, string.Join(",", _shooterBulletCounts));
    }

    private void LoadBulletCountList()
    {
        string saved = EditorPrefs.GetString(PrefKey_ShooterBulletCounts, "");
        if (string.IsNullOrEmpty(saved))
        {
            _shooterBulletCounts = new List<int> { 5, 10, 20 };
            return;
        }

        _shooterBulletCounts = new List<int>();
        foreach (var s in saved.Split(','))
        {
            if (int.TryParse(s.Trim(), out int val) && val > 0)
                _shooterBulletCounts.Add(val);
        }

        if (_shooterBulletCounts.Count == 0)
            _shooterBulletCounts = new List<int> { 5, 10, 20 };
    }

    #endregion

    #region TEXTURE IMPORT

    private void GenerateFromTexture()
    {
        var texture = _levelCreator.LevelData.sourceTexture;
        if (texture == null)
            return;

        // Ensure texture is readable
        string texturePath = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            _levelCreator.LevelData.sourceTexture = texture;
        }

        RecordUndo("Generate From Texture");

        int texWidth = texture.width;
        int texHeight = texture.height;

        // Clear existing targets and palette
        _levelCreator.LevelData.targetDataList.Clear();
        _levelCreator.LevelData.colorPalette.Clear();

        // Read all pixels
        Color32[] pixels = texture.GetPixels32();

        // Grid dimensions stay as user-set values (no auto-override)
        int gridW = _levelCreator.LevelData.targetAreaWidth;
        int gridH = _levelCreator.LevelData.targetAreaHeight;

        // Resample: nearest-neighbor mapping from grid coords to texture coords
        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                int texX = Mathf.Clamp(Mathf.FloorToInt((float)gx / gridW * texWidth), 0, texWidth - 1);
                int texY = Mathf.Clamp(Mathf.FloorToInt((float)gy / gridH * texHeight), 0, texHeight - 1);

                // Texture Y=0 is bottom, grid Y=0 is top → flip for sampling
                int flippedTexY = (texHeight - 1) - texY;
                Color32 pixel = pixels[flippedTexY * texWidth + texX];

                // Skip transparent pixels
                if (pixel.a == 0)
                    continue;

                int colorId = _levelCreator.LevelData.GetOrAddColorId(pixel, _colorTolerance);
                _levelCreator.LevelData.targetDataList.Add(new TargetData(new Vector2Int(gx, gy), colorId));
            }
        }

        // Auto-calculate cell size to fit within conveyor bounds
        _levelCreator.LevelData.targetAreaSize = CalculateTargetAreaSize();

        _targetAreaGrid = GridHelper.CreateTargetAreaGrid(_levelCreator.LevelData, _mainConveyorBounds.center);

        _lastInitializedTargetWidth = gridW;
        _lastInitializedTargetHeight = gridH;
        _lastInitializedTargetSize = _levelCreator.LevelData.targetAreaSize;

        // Set brush to first color if palette not empty
        if (_levelCreator.LevelData.colorPalette.Count > 0)
        {
            _brushColorId = _levelCreator.LevelData.colorPalette[0].Id;
            EditorPrefs.SetInt(PrefKey_BrushColorId, _brushColorId);
        }

        MarkLevelDataDirty();
        CreateVisualsFromLevelData();
        UpdateBulletAndTargetsCounts();

        Debug.Log($"Generated {_levelCreator.LevelData.targetDataList.Count} targets with {_levelCreator.LevelData.colorPalette.Count} unique colors (tolerance: {_colorTolerance}) — grid: {gridW}x{gridH}, texture: {texWidth}x{texHeight}");
    }

    #endregion

    #region AUTO SHOOTER GENERATION

    /// <summary>
    /// Layer-by-layer (outside-in) shooter generation.
    /// Each target's "layer" = min distance to any grid edge.
    /// Shooters are created per-layer per-color so that outer targets
    /// are cleared first, guaranteeing level completion.
    /// </summary>
    private void AutoGenerateShooters()
    {
        var palette = _levelCreator.LevelData.colorPalette;
        if (palette == null || palette.Count == 0)
        {
            Debug.LogWarning("No color palette. Import a texture first.");
            return;
        }

        if (_levelCreator.LevelData.targetDataList == null || _levelCreator.LevelData.targetDataList.Count == 0)
        {
            Debug.LogWarning("No target data. Generate targets from texture first.");
            return;
        }

        RecordUndo("Auto Generate Shooters");

        int gridW = _levelCreator.LevelData.targetAreaWidth;
        int gridH = _levelCreator.LevelData.targetAreaHeight;

        // Group targets by (layer, colorId)
        var layerColorCounts = new SortedDictionary<int, Dictionary<int, int>>();

        foreach (var td in _levelCreator.LevelData.targetDataList)
        {
            int layer = Mathf.Min(
                Mathf.Min(td.Coordinates.x, td.Coordinates.y),
                Mathf.Min(gridW - 1 - td.Coordinates.x, gridH - 1 - td.Coordinates.y));

            if (!layerColorCounts.ContainsKey(layer))
                layerColorCounts[layer] = new Dictionary<int, int>();

            if (!layerColorCounts[layer].ContainsKey(td.ColorId))
                layerColorCounts[layer][td.ColorId] = 0;

            layerColorCounts[layer][td.ColorId]++;
        }

        // Create shooter specs in layer order with carry-forward.
        // Only the last layer per color uses greedy remainder;
        // all earlier layers carry leftover to the next layer
        // so that shooters always use counts from the configured list.
        var shooterSpecs = new List<(int colorId, int bulletCount)>();
        var carry = new Dictionary<int, int>();
        var layers = new List<int>(layerColorCounts.Keys);

        // Sort bullet counts descending for greedy allocation
        var sortedCounts = new List<int>(_shooterBulletCounts);
        sortedCounts.Sort((a, b) => b.CompareTo(a));

        for (int li = 0; li < layers.Count; li++)
        {
            int layer = layers[li];
            var colorCounts = layerColorCounts[layer];

            foreach (var colorKvp in colorCounts)
            {
                int colorId = colorKvp.Key;
                int layerCount = colorKvp.Value;
                carry.TryGetValue(colorId, out int carryCount);
                int accumulated = carryCount + layerCount;

                // Check if this color has targets in any future layer
                bool hasMoreTargets = false;
                for (int lj = li + 1; lj < layers.Count; lj++)
                {
                    if (layerColorCounts[layers[lj]].ContainsKey(colorId))
                    {
                        hasMoreTargets = true;
                        break;
                    }
                }

                if (!hasMoreTargets)
                {
                    // Last layer for this color → greedy with remainder
                    var counts = DecomposeIntoBulletCounts(accumulated, _shooterBulletCounts);
                    foreach (int c in counts)
                        shooterSpecs.Add((colorId, c));
                    carry[colorId] = 0;
                }
                else
                {
                    // Create only full shooters from the list, carry the rest
                    int remaining = accumulated;
                    while (remaining > 0)
                    {
                        bool found = false;
                        foreach (int count in sortedCounts)
                        {
                            if (count <= remaining)
                            {
                                shooterSpecs.Add((colorId, count));
                                remaining -= count;
                                found = true;
                                break;
                            }
                        }

                        if (!found) break;
                    }

                    carry[colorId] = remaining;
                }
            }
        }

        // Clear existing shooters
        _levelCreator.shooterParent.DestroyAllChildrenImmediate();
        _levelCreator.LevelData.shooterLaneDataList.Clear();

        // Initialize lanes
        int laneCount = _levelCreator.LevelData.shooterLaneCount;
        for (int i = 0; i < laneCount; i++)
            _levelCreator.LevelData.shooterLaneDataList.Add(new ShooterLaneData { ShooterDataList = new List<ShooterData>() });

        // Place shooters round-robin across lanes
        int[] laneY = new int[laneCount];
        int currentLane = 0;

        foreach (var (colorId, bulletCount) in shooterSpecs)
        {
            int x = currentLane;
            int y = laneY[x];
            int id = int.Parse((x + 1) + "" + y);

            var data = new ShooterData(id, bulletCount, colorId, -1, new Vector2Int(x, y), false);
            _levelCreator.LevelData.shooterLaneDataList[x].ShooterDataList.Add(data);

            laneY[x]++;
            currentLane = (currentLane + 1) % laneCount;
        }

        MarkLevelDataDirty();
        RecalculateShooterGridHeight();
        CreateVisualsFromLevelData();
        UpdateBulletAndTargetsCounts();

        int totalLayers = layerColorCounts.Count;
        Debug.Log($"Auto-generated {shooterSpecs.Count} shooters across {laneCount} lanes, {totalLayers} layers.");
    }

    private List<int> DecomposeIntoBulletCounts(int total, List<int> availableCounts)
    {
        var result = new List<int>();
        if (total <= 0 || availableCounts == null || availableCounts.Count == 0)
            return result;

        // Sort descending for greedy allocation
        var sorted = new List<int>(availableCounts);
        sorted.Sort((a, b) => b.CompareTo(a));

        int remaining = total;

        while (remaining > 0)
        {
            bool found = false;
            foreach (int count in sorted)
            {
                if (count <= remaining)
                {
                    result.Add(count);
                    remaining -= count;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Remainder is less than smallest available count — use as-is
                result.Add(remaining);
                remaining = 0;
            }
        }

        return result;
    }

    #endregion

    #region MOUSE EVENTS

    private void OnRightMouseClick(Event e)
    {
        var menu = new GenericMenu();

        if (_isMouseInShooterGrid)
        {
            menu.AddItem(new GUIContent("Delete All Shooters"), false, DeleteAllShooters);

            if (IsShooterExist(out var shooter))
            {
                menu.AddItem(new GUIContent("Delete Shooter"), false, () => DeleteShooter(shooter));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Is Hidden"), shooter.Data.IsHidden, () => SetShooterHidden(shooter));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(shooter.Data.LinkedShooterID == -1 ? "Create Link" : $"Break Link With {shooter.Data.LinkedShooterID}"), shooter.Data.LinkedShooterID != -1, () => HandleLinkOperation(shooter));
                menu.AddSeparator("");

                AddSetColorSubMenu(menu, shooter);
            }
            else
            {
                menu.AddItem(new GUIContent("Create Shooter"), false, CreateShooter);
                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("Set Color (create first)"));
            }

            menu.DropDown(new Rect(e.mousePosition, Vector2.zero));
        }
        else if (_isMouseInTargetGrid)
        {
            menu.AddItem(new GUIContent("Delete All Target Objects"), false, DeleteAllTargetObjects);
            menu.DropDown(new Rect(e.mousePosition, Vector2.zero));
        }
    }

    private void AddSetColorSubMenu(GenericMenu menu, Shooter shooter)
    {
        var palette = _levelCreator.LevelData.colorPalette;
        if (palette == null || palette.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("Set Color (no palette)"));
            return;
        }

        foreach (var levelColor in palette)
        {
            int colorId = levelColor.Id;
            bool isCurrent = shooter.Data != null && shooter.Data.ColorId == colorId;

            menu.AddItem(new GUIContent($"Set Color/Color {colorId}"), isCurrent, () => { SetShooterColor(shooter, colorId); });
        }
    }

    private void OnLeftMouseClick(Event e)
    {
        if (_editTool == EditTool.Remove)
        {
            DeleteCurrentlyHoveringObject();
            return;
        }

        if (_isLinking && IsShooterExist(out var shooter) && _currentlyLinkingShooter != null && shooter.Data.LinkedShooterID == -1)
        {
            CreateLinkBetween(_currentlyLinkingShooter, shooter);
            return;
        }

        CreateShooter();
        CreateTargetObject();
    }

    private void OnMouseMove(Event e, SceneView sceneView)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (!_gridPlane.Raycast(ray, out float enter))
            return;

        Vector3 worldPos = ray.GetPoint(enter);

        if (GridHelper.TryGetGridFromPosition(_shooterAreaGrid, worldPos, out var shooterAreaCoords, out var shooterGridCellCenter))
        {
            bool changed = !_isMouseInShooterGrid || shooterAreaCoords != _currentHoverCellCoords;

            _isMouseInShooterGrid = true;
            _currentHoverCellCoords = shooterAreaCoords;
            _hoverCellCenter = shooterGridCellCenter;

            if (changed)
                sceneView.Repaint();
        }
        else
        {
            if (_isMouseInShooterGrid)
            {
                _isMouseInShooterGrid = false;
                sceneView.Repaint();
            }
        }

        if (GridHelper.TryGetGridFromPosition(_targetAreaGrid, worldPos, out var targetAreaCoords, out var targetAreaCellCenter))
        {
            bool changed = !_isMouseInTargetGrid || targetAreaCoords != _currentHoverCellCoords;

            _isMouseInTargetGrid = true;
            _currentHoverCellCoords = targetAreaCoords;
            _hoverCellCenter = targetAreaCellCenter;

            if (changed)
                sceneView.Repaint();
        }
        else
        {
            if (_isMouseInTargetGrid)
            {
                _isMouseInTargetGrid = false;
                sceneView.Repaint();
            }
        }
    }

    #endregion

    #region TARGET OBJECT OPERATIONS

    private void CreateTargetObject()
    {
        if (!_isMouseInTargetGrid)
            return;

        int size = Mathf.Max(1, _targetBrushSize);
        Vector2Int center = _currentHoverCellCoords;

        int half = (size - 1) / 2;
        Vector2Int start = center - new Vector2Int(half, half);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int coords = start + new Vector2Int(x, y);

                if (!GridHelper.TryGetPositionFromCoords(_targetAreaGrid, coords, out var cellCenter))
                    continue;

                if (TryGetTargetObjectAtCoords(coords, out var existingTarget))
                {
                    if (_overrideColor && existingTarget.Data.ColorId != _brushColorId)
                    {
                        RecordUndo("Change Target Color");
                        existingTarget.Data.ColorId = _brushColorId;
                        existingTarget.SetData(existingTarget.Data, _levelCreator.LevelData);
                        OnTargetObjectDataUpdated(existingTarget.Data);
                    }
                }
                else
                {
                    RecordUndo("Create Target Object");

                    TargetObject targetObject = PrefabUtility.InstantiatePrefab(_levelCreator.targetObjectPrefab, _levelCreator.targetObjectParent) as TargetObject;
                    targetObject.transform.position = cellCenter;
                    targetObject.transform.localScale = new Vector3(_targetAreaGrid.Size, 1, _targetAreaGrid.Size);

                    TargetData targetData = new TargetData(coords, _brushColorId);
                    targetObject.SetData(targetData, _levelCreator.LevelData);
                    OnTargetObjectDataUpdated(targetData, isNew: true);
                }
            }
        }
    }

    private void DeleteAllTargetObjects()
    {
        RecordUndo("Delete All Target Objects");

        for (int i = _levelCreator.targetObjectParent.transform.childCount - 1; i >= 0; i--)
        {
            var targetGameObject = _levelCreator.targetObjectParent.transform.GetChild(i).gameObject;
            DestroyImmediate(targetGameObject);
        }

        _levelCreator.LevelData.targetDataList.Clear();
        MarkLevelDataDirty();
        UpdateBulletAndTargetsCounts();
    }

    private void DeleteTargetObjectsInBrushArea()
    {
        if (!_isMouseInTargetGrid)
            return;

        int size = Mathf.Max(1, _targetBrushSize);
        Vector2Int center = _currentHoverCellCoords;

        int half = (size - 1) / 2;
        Vector2Int start = center - new Vector2Int(half, half);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int coords = start + new Vector2Int(x, y);

                if (!GridHelper.TryGetPositionFromCoords(_targetAreaGrid, coords, out _))
                    continue;

                if (TryGetTargetObjectAtCoords(coords, out var existingTarget))
                {
                    RecordUndo("Delete Target Object");
                    var data = existingTarget.Data;
                    OnTargetObjectDataUpdated(data, isDestroyed: true);
                    DestroyImmediate(existingTarget.gameObject);
                }
            }
        }
    }

    private void OnTargetObjectDataUpdated(TargetData targetData, bool isDestroyed = false, bool isNew = false)
    {
        if (!isNew)
        {
            for (var i = 0; i < _levelCreator.LevelData.targetDataList.Count; i++)
            {
                var data = _levelCreator.LevelData.targetDataList[i];

                if (data.Coordinates != targetData.Coordinates)
                    continue;

                if (isDestroyed)
                {
                    _levelCreator.LevelData.targetDataList.Remove(data);
                    break;
                }

                _levelCreator.LevelData.targetDataList[i] = targetData;
                break;
            }
        }
        else
            _levelCreator.LevelData.targetDataList.Add(targetData);

        MarkLevelDataDirty();
        UpdateBulletAndTargetsCounts();
    }

    #endregion

    #region SHOOTER OPERATIONS

    private void SetShooterBulletCount(Shooter shooter, int count)
    {
        if (shooter == null || shooter.Data == null)
            return;

        RecordUndo("Set Bullet Count");
        shooter.Data.BulletCount = count;
        OnShooterUpdated(shooter);
    }

    private void SetShooterColor(Shooter shooter, int colorId)
    {
        if (shooter == null || shooter.Data == null)
            return;

        RecordUndo("Set Shooter Color");
        shooter.Data.ColorId = colorId;
        OnShooterUpdated(shooter);
    }

    private void DeleteAllShooters()
    {
        RecordUndo("Delete All Shooters");
        _levelCreator.shooterParent.DestroyAllChildrenImmediate();
        _levelCreator.LevelData.shooterLaneDataList.Clear();
        RecalculateShooterGridHeight();
        MarkLevelDataDirty();
        UpdateBulletAndTargetsCounts();
    }

    private void CreateShooter()
    {
        if (!_isMouseInShooterGrid)
            return;

        if (!GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, _currentHoverCellCoords, out var cellCenter))
            return;

        bool existingShooter = IsShooterExist(out var currentlyHoveringShooter);

        if (existingShooter)
        {
            if (_overrideColor)
                SetShooterColor(currentlyHoveringShooter, _brushColorId);
            if (_overrideBulletCount)
                SetShooterBulletCount(currentlyHoveringShooter, _bulletCount);

            return;
        }

        RecordUndo("Create Shooter");

        var shooter = PrefabUtility.InstantiatePrefab(_levelCreator.shooterPrefab, _levelCreator.shooterParent) as Shooter;
        shooter.transform.position = cellCenter;

        int id = int.Parse((_currentHoverCellCoords.x + 1) + "" + _currentHoverCellCoords.y);
        ShooterData shooterData = new ShooterData(id, _bulletCount, _brushColorId, -1, _currentHoverCellCoords, false);
        shooter.SetData(shooterData, _levelCreator.LevelData);
        OnShooterUpdated(shooter, isNew: true);
    }

    private void DeleteShooter(Shooter shooter)
    {
        RecordUndo("Delete Shooter");
        DestroyImmediate(shooter.gameObject);
        OnShooterUpdated(shooter, isDestroyed: true);
    }

    private void SetShooterHidden(Shooter shooter)
    {
        RecordUndo("Toggle Shooter Hidden");
        bool isHidden = shooter.Data != null && shooter.Data.IsHidden;
        shooter.Data.IsHidden = !isHidden;
        OnShooterUpdated(shooter);
    }

    private void BreakLinkBetween(Shooter shooter, Shooter linkedShooter)
    {
        RecordUndo("Break Shooter Link");
        shooter.Data.LinkedShooterID = -1;
        linkedShooter.Data.LinkedShooterID = -1;
        OnShooterUpdated(shooter);
    }

    private void HandleLinkOperation(Shooter shooter)
    {
        if (shooter.Data.LinkedShooterID == -1)
        {
            _currentlyLinkingShooter = shooter;
            _isLinking = true;
        }
        else
        {
            if (TryGetShooterFromId(shooter.Data.LinkedShooterID, out var linkedShooter))
            {
                BreakLinkBetween(shooter, linkedShooter);
            }
        }
    }

    private void CreateLinkBetween(Shooter shooter, Shooter linkedShooter)
    {
        RecordUndo("Link Shooters");
        shooter.Data.LinkedShooterID = linkedShooter.Data.ID;
        linkedShooter.Data.LinkedShooterID = shooter.Data.ID;
        OnShooterUpdated(shooter);
        _currentlyLinkingShooter = null;
        _isLinking = false;
    }

    private void OnShooterUpdated(Shooter shooter, bool isDestroyed = false, bool isNew = false)
    {
        ShooterData shooterData = shooter.Data;
        if (!isNew)
        {
            foreach (var laneData in _levelCreator.LevelData.shooterLaneDataList)
            {
                for (var j = 0; j < laneData.ShooterDataList.Count; j++)
                {
                    var data = laneData.ShooterDataList[j];

                    if (data.Coordinates != shooterData.Coordinates)
                        continue;

                    if (isDestroyed)
                    {
                        laneData.ShooterDataList.Remove(data);
                        break;
                    }

                    laneData.ShooterDataList[j] = shooterData;
                    break;
                }
            }
        }
        else
        {
            if (_levelCreator.LevelData.shooterLaneDataList.Count - 1 < shooterData.Coordinates.x)
            {
                _levelCreator.LevelData.shooterLaneDataList.Add(new ShooterLaneData());
                _levelCreator.LevelData.shooterLaneDataList[shooterData.Coordinates.x].ShooterDataList = new List<ShooterData>();
            }

            _levelCreator.LevelData.shooterLaneDataList[shooterData.Coordinates.x].ShooterDataList.Add(shooterData);
        }

        if (!isDestroyed)
            shooter.SetData(shooterData, _levelCreator.LevelData);

        if (isNew || isDestroyed)
            RecalculateShooterGridHeight();

        MarkLevelDataDirty();
        UpdateBulletAndTargetsCounts();
    }

    #endregion

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

    #region HELPERS

    private bool TryGetTargetBrushPreviewBounds(out Bounds bounds, out Vector3[] rect)
    {
        bounds = default;
        rect = null;

        if (!_isMouseInTargetGrid)
            return false;

        int size = Mathf.Max(1, _targetBrushSize);
        Vector2Int center = _currentHoverCellCoords;

        int half = (size - 1) / 2;
        Vector2Int start = center - new Vector2Int(half, half);

        bool hasAny = false;
        float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int coords = start + new Vector2Int(x, y);

                if (!GridHelper.TryGetPositionFromCoords(_targetAreaGrid, coords, out var cellCenter))
                    continue;

                if (!hasAny)
                {
                    minX = maxX = cellCenter.x;
                    minZ = maxZ = cellCenter.z;
                    hasAny = true;
                }
                else
                {
                    minX = Mathf.Min(minX, cellCenter.x);
                    maxX = Mathf.Max(maxX, cellCenter.x);
                    minZ = Mathf.Min(minZ, cellCenter.z);
                    maxZ = Mathf.Max(maxZ, cellCenter.z);
                }
            }
        }

        if (!hasAny)
            return false;

        float cellSize = _targetAreaGrid.Size;
        float width = (maxX - minX) + cellSize;
        float depth = (maxZ - minZ) + cellSize;

        Vector3 centerPos = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        bounds = new Bounds(centerPos, new Vector3(width, 0.01f, depth));

        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;

        Vector3 topLeft = new Vector3(centerPos.x - halfW, 0f, centerPos.z + halfD);
        Vector3 topRight = new Vector3(centerPos.x + halfW, 0f, centerPos.z + halfD);
        Vector3 bottomRight = new Vector3(centerPos.x + halfW, 0f, centerPos.z - halfD);
        Vector3 bottomLeft = new Vector3(centerPos.x - halfW, 0f, centerPos.z - halfD);

        rect = new[] { topLeft, topRight, bottomRight, bottomLeft };
        return true;
    }

    private void DrawTargetBrushPreview()
    {
        if (!TryGetTargetBrushPreviewBounds(out Bounds bounds, out Vector3[] rect))
            return;

        const float y = 0.001f;
        for (int i = 0; i < rect.Length; i++)
            rect[i].y = y;

        Handles.DrawSolidRectangleWithOutline(rect, Color.white.WithAlpha(0.06f), Color.white.WithAlpha(0.9f));
        Handles.color = Color.white;
        Handles.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 0.01f, bounds.size.z));
        Handles.DrawWireDisc(_hoverCellCenter, Vector3.up, _targetAreaGrid.Size * 0.3f);
    }

    private void InsertGUISeparator()
    {
        GUILayout.Space(7);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(7);
    }

    private void SetBrushColor(int colorId)
    {
        _brushColorId = colorId;
        EditorPrefs.SetInt(PrefKey_BrushColorId, _brushColorId);
        SceneView.RepaintAll();
    }

    private void HandleShortcuts(Event e)
    {
        if (e.type != EventType.KeyDown) return;

        if (e.keyCode == KeyCode.Alpha1)
        {
            _editTool = EditTool.Paint;
            EditorPrefs.SetInt(PrefKey_EditTool, (int)_editTool);
            e.Use();
            SceneView.RepaintAll();
        }
        else if (e.keyCode == KeyCode.Alpha2)
        {
            _editTool = EditTool.Remove;
            EditorPrefs.SetInt(PrefKey_EditTool, (int)_editTool);
            e.Use();
            SceneView.RepaintAll();
        }
        else if (e.keyCode == KeyCode.Escape && _isLinking)
        {
            _isLinking = false;
            _currentlyLinkingShooter = null;
            e.Use();
            SceneView.RepaintAll();
        }
    }

    #endregion

    #region HANDLES VISUALIZATION

    private void TryVisualizeGetMainConveyorsBounds()
    {
        if (!_drawConveyorBounds)
            return;
        Handles.color = Color.aquamarine;
        Handles.DrawWireCube(_mainConveyorBounds.center, _mainConveyorBounds.size);

        var pointSize = _mainConveyor.Spline.GetPointSize(0);
        Vector3 maxPoint = _mainConveyorBounds.max - new Vector3(pointSize, 0, pointSize);
        Vector3 minPoint = _mainConveyorBounds.min + new Vector3(pointSize, 0, pointSize);

        Bounds bounds = new Bounds(minPoint, Vector3.zero);
        bounds.Encapsulate(maxPoint);

        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private void VisualizeShooterGrid()
    {
        Handles.color = Color.aquamarine;
        DrawGridLines(_shooterAreaGrid);

        Handles.color = Color.darkKhaki;
        var positions = GridHelper.GetStoragePositions(_levelCreator.LevelData, _shooterAreaGrid);

        foreach (var position in positions)
            Handles.DrawWireCube(position, (Vector3.one * _shooterAreaGrid.Size).FlattenY());

        var bounds = GridHelper.GetGridBounds(_shooterAreaGrid);
        
        Handles.color = Color.blueViolet;
        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private void DrawGridLines(GameGrid grid)
    {
        float size = grid.Size;
        int width = grid.Width;
        int height = grid.Height;

        float startZ = grid.CenterPosition.z + (height * 0.5f * size);
        float startX = -((width - 1) * size * 0.5f);
        Vector3 firstCellCenter = new Vector3(startX, 0f, startZ);

        float half = size * 0.5f;

        Vector3 topLeft = firstCellCenter + new Vector3(-half, 0f, half);
        Vector3 topRight = firstCellCenter + new Vector3((width - 1) * size + half, 0f, half);
        Vector3 bottomLeft = firstCellCenter + new Vector3(-half, 0f, -((height - 1) * size + half));

        for (int x = 0; x <= width; x++)
        {
            Vector3 a = topLeft + Vector3.right * (x * size);
            Vector3 b = bottomLeft + Vector3.right * (x * size);
            Handles.DrawLine(a, b);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 a = topLeft + Vector3.back * (y * size);
            Vector3 b = topRight + Vector3.back * (y * size);
            Handles.DrawLine(a, b);
        }
    }

    private void VisualizeTargetAreaGrid()
    {
        Handles.color = Color.aquamarine;
        DrawGridLines(_targetAreaGrid);

        var bounds = GridHelper.GetGridBounds(_targetAreaGrid);
        Handles.color = Color.blueViolet;
        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private void VisualizeShooterLinks()
    {
        var shooters = _levelCreator.shooterParent.transform.GetComponentsInChildren<Shooter>();
        foreach (var shooter in shooters)
        {
            if (shooter.Data.LinkedShooterID != -1)
            {
                if (TryGetShooterFromId(shooter.Data.LinkedShooterID, out Shooter linkedShooter))
                {
                    Handles.color = Color.white;
                    Handles.DrawLine(shooter.transform.position, linkedShooter.transform.position, 5);
                }
            }
        }
    }

    #region GAME AREA BOUND VISUALIZATION

    private Bounds _gameAreaBounds;
    private Camera _cam;

    private Camera MainCamera
    {
        get
        {
            if (_cam != null)
                return _cam;

            _cam = Camera.main;
            return _cam;
        }
    }

    private void TryVisualizeGameScene()
    {
        if (MainCamera == null || !_drawGameAreaBounds)
            return;

        float aspect = GetGameViewAspect(MainCamera);

        Plane plane = new Plane(Vector3.up, 0f);

        if (!TryGetViewportQuadOnPlane(MainCamera, aspect, plane, out Vector3[] quad))
            return;

        _gameAreaBounds = new Bounds(quad[0], Vector3.zero);
        for (int i = 1; i < quad.Length; i++)
            _gameAreaBounds.Encapsulate(quad[i]);

        Handles.color = Color.white.WithAlpha(0.5f);
        Handles.DrawWireCube(_gameAreaBounds.center, _gameAreaBounds.size);
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
        Handles.Label(_gameAreaBounds.max.z * Vector3.forward, "Visible Game Area", style);
    }

    private static float GetGameViewAspect(Camera cam)
    {
        if (Application.isPlaying && Screen.height > 0)
            return (float)Screen.width / Screen.height;

        Vector2 gv = Handles.GetMainGameViewSize();
        if (gv.y > 0.0001f)
            return gv.x / gv.y;

        return cam.aspect > 0f ? cam.aspect : (16f / 9f);
    }

    private static bool TryGetViewportQuadOnPlane(Camera cam, float desiredAspect, Plane plane, out Vector3[] quad)
    {
        quad = new Vector3[4];

        Vector2[] viewPorts = { new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f) };

        float prevAspect = cam.aspect;
        cam.aspect = desiredAspect;

        for (int i = 0; i < 4; i++)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(viewPorts[i].x, viewPorts[i].y, 0f));
            if (!plane.Raycast(ray, out float enter) || enter < 0f)
            {
                cam.aspect = prevAspect;
                return false;
            }

            quad[i] = ray.GetPoint(enter);
        }

        cam.aspect = prevAspect;
        return true;
    }

    #endregion

    private void OnHandlesDraw()
    {
        TryVisualizeGetMainConveyorsBounds();
        TryVisualizeGameScene();
        VisualizeShooterGrid();
        VisualizeTargetAreaGrid();
        VisualizeShooterLinks();
    }

    #endregion
}

#endif