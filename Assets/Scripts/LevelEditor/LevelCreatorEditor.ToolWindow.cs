#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class LevelCreatorEditor
{
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

    #region HELPERS

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
}

#endif
