#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Freya;
using Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomEditor(typeof(LevelCreator))]
public class LevelCreatorEditor : Editor
{
    private static LevelCreatorEditor s_active;
    private LevelCreator _levelCreator;

    // --- Conveyor ---
    private MainConveyor _mainConveyor;
    private bool _hasMainConveyorBounds;
    private Bounds _mainConveyorBounds;

    // --- Mouse Hover ---
    private Vector3 _hoverCellCenter;
    private bool _isMouseInShooterGrid;
    private bool _isMouseInTargetGrid;
    private Vector2Int _currentHoverCellCoords;

    // --- Linking ---
    private bool _isLinking;
    private Shooter _currentlyLinkingShooter;
    private EditTool _editTool = EditTool.Paint;

    // --- Scene GUI Tool Window ---
    private Rect _toolWindowRect = new Rect(10, 10, 280, 120);
    private bool _isMouseOverToolWindow;
    private readonly int _toolWindowId = "LevelCreatorEditor.ToolWindow".GetHashCode();
    private bool _showToolWindow = true;
    private float _toolContentHeight;

    // --- Brush ---
    private GameColor _brushColor = GameColor.Green;
    private bool _overrideColor;
    private const string PrefKey_BrushColor = "LevelCreatorEditor.BrushColor";
    private const string PrefKey_EditTool = "LevelCreatorEditor.EditTool";

    // --- Bullet Count --
    private int _bulletCount;
    private bool _overrideBulletCount;
    private const string PrefKey_BulletCount = "LevelCreatorEditor.BulletCount";

    // --- Level Data Shooter Grid ---
    private const string PrefKey_Width = "LevelCreatorEditor.Width";
    private const string PrefKey_ContainerSlot = "LevelCreatorEditor.ContainerSlot";
    private GameGrid _shooterAreaGrid;
    private readonly Plane _gridPlane = new Plane(Vector3.up, Vector3.zero);
    private int _lastInitializedLaneCount;

    // --- Target Area ---
    private GameGrid _targetAreaGrid;
    private const string PrefKey_TargetBrushRadius = "LevelCreatorEditor.TargetBrushRadius";
    private int _targetBrushSize = 1;

    // --- Validation ---
    private readonly Dictionary<GameColor, int> _bulletsPerColor = new();
    private readonly Dictionary<GameColor, int> _targetsPerColor = new();
    private readonly List<ValidationMessage> _validationMessages = new();

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

        InitializeShooterGrid();
        InitializeTargetAreaGrid();

        CreateVisualsFromLevelData();
        UpdateBulletAndTargetsCounts();

        SceneView.duringSceneGui -= OnSceneGUI;

        if (s_active != null && s_active != this)
            SceneView.duringSceneGui -= s_active.OnSceneGUI;

        s_active = this;

        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

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

        _isMouseOverToolWindow = _toolWindowRect.Contains(e.mousePosition);

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
                DeleteCurrentlyHooveringObject();

            e.Use();
        }
        else if (e.type == EventType.MouseDown)
        {
            if (e.button == 0)
            {
                OnLeftMouseClick(e);
                e.Use();
            }
            else if (e.button == 1)
            {
                OnRightMouseClick(e);
                e.Use();
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

    #region TOOL WINDOW

    private void DrawToolWindow(int id)
    {
        DrawEditToolArea();

        InsertGUISeperator();
        DrawShooterAreaGridOptions();

        if (_isLinking)
        {
            InsertGUISeperator();
            DrawLinkingArea();
        }

        InsertGUISeperator();
        DrawColorArea();

        InsertGUISeperator();
        DrawBulletCountArea();

        InsertGUISeperator();
        DrawValidationArea();

        if (Event.current.type == EventType.Repaint)
            _toolContentHeight = GUILayoutUtility.GetLastRect().yMax;

        GUI.DragWindow(new Rect(0, 0, 10000, 200));
    }

    private void DrawEditorToolsWindow()
    {
        Handles.BeginGUI();
        _toolWindowRect = GUILayout.Window(_toolWindowId, _toolWindowRect, DrawToolWindow, "Level Creator Tools");
        Handles.EndGUI();
        if (Event.current.type == EventType.Repaint)
            _toolWindowRect.height = Mathf.Max(200f, _toolContentHeight + 35f);
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

        GUILayout.Label("Shortcuts: 1 = Paint, 2 = Remove, ESC = Cancel Link", EditorStyles.miniLabel);
    }

    private void DrawShooterAreaGridOptions()
    {
        EditorGUILayout.LabelField("Shooter Grid Options", EditorStyles.boldLabel);
        _levelCreator.LevelData.laneCount = EditorGUILayout.IntSlider("Shooter Grid Width", _levelCreator.LevelData.laneCount, 1, 5);
        if (_lastInitializedLaneCount != _levelCreator.LevelData.laneCount)
        {
            _shooterAreaGrid.Width = _levelCreator.LevelData.laneCount;
            OnLevelLaneCountChanged();
            _lastInitializedLaneCount = _levelCreator.LevelData.laneCount;
        }

        _levelCreator.LevelData.storageCount = EditorGUILayout.IntSlider("Shooter Storage Count", _levelCreator.LevelData.storageCount, 1, 5);
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

    private void OnLevelLaneCountChanged()
    {
        var shooters = _levelCreator.shooterParent.GetComponentsInChildren<Shooter>();
        foreach (var shooter in shooters)
        {
            var coords = shooter.Data.Coordinates;
            if (coords.x >= _levelCreator.LevelData.laneCount)
            {
                DeleteShooter(shooter);
            }
            else if (GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, coords, out var cellCenter))
            {
                shooter.transform.position = cellCenter;
            }
        }
    }

    private void DrawBrushColorRow()
    {
        var colorEnumValues = (GameColor[])Enum.GetValues(typeof(GameColor));

        using (new GUILayout.HorizontalScope())
        {
            foreach (var color in colorEnumValues)
            {
                var bg = GetGuiColor(color);

                var prevBg = GUI.backgroundColor;
                var prevContent = GUI.contentColor;

                GUI.backgroundColor = bg;
                GUI.contentColor = (bg.grayscale > 0.6f) ? Color.black : Color.white;

                string label = (color.Equals(_brushColor)) ? "✓" : "";
                if (GUILayout.Button(new GUIContent(label, color.ToString()), GUILayout.Width(26), GUILayout.Height(20)))
                    SetBrushColor(color);

                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
            }
        }

        EditorGUILayout.LabelField($"Selected: {_brushColor}", EditorStyles.miniLabel);
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
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        if (_validationMessages.Count == 0)
        {
            EditorGUILayout.HelpBox("Bullets / Targets counts match for all colors.", MessageType.Info);
        }
        else
        {
            foreach (var msg in _validationMessages)
                EditorGUILayout.HelpBox(msg.Text, msg.Type);
        }

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            foreach (GameColor color in Enum.GetValues(typeof(GameColor)))
            {
                int bulletCount = _bulletsPerColor.GetValueOrDefault(color, 0);
                int targetObjectCount = _targetsPerColor.GetValueOrDefault(color, 0);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var prev = GUI.color;
                    GUI.color = GetGuiColor(color);
                    GUILayout.Box(GUIContent.none, GUILayout.Width(12), GUILayout.Height(12));
                    GUI.color = prev;

                    GUILayout.Space(6);
                    GUILayout.Label($"{color}", GUILayout.Width(70));
                    GUILayout.Label($"Bullets: {bulletCount}", GUILayout.Width(90));
                    GUILayout.Label($"Targets: {targetObjectCount}", GUILayout.Width(90));
                }
            }
        }
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
        foreach (GameColor c in System.Enum.GetValues(typeof(GameColor)))
        {
            var color = c;
            bool isCurrent = shooter.Data != null && shooter.Data.Color == color;

            menu.AddItem(new GUIContent($"Set Color/{color}"), isCurrent, () => { SetShooterColor(shooter, color); });
        }
    }

    private void OnLeftMouseClick(Event e)
    {
        if (_editTool == EditTool.Remove)
        {
            DeleteCurrentlyHooveringObject();
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
                    if (_overrideColor && existingTarget.Data.Color != _brushColor)
                    {
                        existingTarget.Data.Color = _brushColor;
                        existingTarget.SetData(existingTarget.Data);
                        OnTargetObjectDataUpdated(existingTarget.Data);
                    }
                }
                else
                {
                    TargetObject targetObject = PrefabUtility.InstantiatePrefab(_levelCreator.targetObjectPrefab, _levelCreator.targetObjectParent) as TargetObject;
                    targetObject.transform.position = cellCenter;
                    targetObject.transform.localScale = new Vector3(_targetAreaGrid.Size, 1, _targetAreaGrid.Size);

                    TargetData targetData = new TargetData(coords, _brushColor);
                    targetObject.SetData(targetData);
                    OnTargetObjectDataUpdated(targetData, isNew: true);
                }
            }
        }
    }

    private void DeleteAllTargetObjects()
    {
        for (int i = _levelCreator.targetObjectParent.transform.childCount - 1; i >= 0; i--)
        {
            var targetGameObject = _levelCreator.targetObjectParent.transform.GetChild(i).gameObject;
            DestroyImmediate(targetGameObject);
        }

        _levelCreator.LevelData.targetDataList.Clear();
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

        UpdateBulletAndTargetsCounts();
    }

    #endregion

    #region SHOOTER OPERATIONS

    // Bullet Count
    private void SetShooterBulletCount(Shooter shooter, int count)
    {
        if (shooter == null || shooter.Data == null)
            return;

        shooter.Data.BulletCount = count;
        shooter.SetData(shooter.Data);
        OnShooterUpdated(shooter.Data);
    }

    //Color
    private void SetShooterColor(Shooter shooter, GameColor color)
    {
        if (shooter == null || shooter.Data == null)
            return;

        shooter.Data.Color = color;
        shooter.SetData(shooter.Data);

        OnShooterUpdated(shooter.Data);
    }

    //Create & Delete
    private void DeleteAllShooters()
    {
        _levelCreator.shooterParent.DestroyAllChildrenImmediate();
        _levelCreator.LevelData.targetDataList.Clear();
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
                SetShooterColor(currentlyHoveringShooter, _brushColor);
            if (_overrideBulletCount)
                SetShooterBulletCount(currentlyHoveringShooter, _bulletCount);

            return;
        }

        var shooter = PrefabUtility.InstantiatePrefab(_levelCreator.shooterPrefab, _levelCreator.shooterParent) as Shooter;
        shooter.transform.position = cellCenter;

        int id = int.Parse((_currentHoverCellCoords.x + 1) + "" + _currentHoverCellCoords.y);
        ShooterData shooterData = new ShooterData(id, _bulletCount, _brushColor, -1, _currentHoverCellCoords, false);
        shooter.SetData(shooterData);
        OnShooterUpdated(shooterData, isNew: true);
    }

    private void DeleteShooter(Shooter shooter)
    {
        DestroyImmediate(shooter.gameObject);
        OnShooterUpdated(shooter.Data, isDestroyed: true);
    }

    //Hidden
    private void SetShooterHidden(Shooter shooter)
    {
        bool isHidden = shooter.Data != null && shooter.Data.IsHidden;
        shooter.Data.IsHidden = !isHidden;
        OnShooterUpdated(shooter.Data);
    }

    //Linking
    private void BreakLinkBetween(Shooter shooter, Shooter linkedShooter)
    {
        shooter.Data.LinkedShooterID = -1;
        linkedShooter.Data.LinkedShooterID = -1;
        OnShooterUpdated(shooter.Data);
    }

    private void CreateLinkBetween(Shooter shooter, Shooter linkedShooter)
    {
        shooter.Data.LinkedShooterID = linkedShooter.Data.ID;
        linkedShooter.Data.LinkedShooterID = shooter.Data.ID;
        OnShooterUpdated(shooter.Data);
        _currentlyLinkingShooter = null;
        _isLinking = false;
    }

    private void OnShooterUpdated(ShooterData shooterData, bool isDestroyed = false, bool isNew = false)
    {
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

        UpdateBulletAndTargetsCounts();
    }

    #endregion

    #region HELPERS

    private void UpdateBulletAndTargetsCounts()
    {
        _bulletsPerColor.Clear();
        _targetsPerColor.Clear();
        _validationMessages.Clear();

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
                    if (!_bulletsPerColor.TryAdd(s.Color, bc)) _bulletsPerColor[s.Color] += bc;
                }
            }
        }

        if (levelData.targetDataList != null)
        {
            foreach (var t in levelData.targetDataList)
            {
                if (t == null)
                    continue;

                if (!_targetsPerColor.TryAdd(t.Color, 1)) _targetsPerColor[t.Color] += 1;
            }
        }

        foreach (GameColor c in Enum.GetValues(typeof(GameColor)))
        {
            int b = _bulletsPerColor.GetValueOrDefault(c, 0);
            int t = _targetsPerColor.GetValueOrDefault(c, 0);

            if (b == 0 && t == 0)
                continue;

            if (b != t)
                _validationMessages.Add(new ValidationMessage(MessageType.Warning, $"{c}: Bullets={b}, Targets={t}"));
        }

        SceneView.RepaintAll();
    }

    private void InitializeShooterGrid()
    {
        float size = GameConfigs.Instance.gridSCellSize;
        int width = _levelCreator.LevelData.laneCount;
        int height = 40;
        float centerZ = _mainConveyorBounds.min.z - GameConfigs.Instance.gridZOffsetToMainConveyor - GameConfigs.Instance.StorageSlotSize - (height * 0.5f * size);
        _shooterAreaGrid = new GameGrid(size, width, height, Vector3.forward * centerZ);
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

    private void InitializeMainConveyor()
    {
        _hasMainConveyorBounds = false;
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

        _hasGameAreaBounds = true;
    }

    private void InitializeTargetAreaGrid()
    {
        float size = 0.5f;
        int width = 20;
        int height = 20;
        Vector3 centerPosition = _mainConveyorBounds.center.FlattenY();
        _targetAreaGrid = new GameGrid(size, width, height, centerPosition);
    }

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

    private void InsertGUISeperator()
    {
        GUILayout.Space(7);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(7);
    }

    private void SetBrushColor(GameColor gameColor)
    {
        _brushColor = gameColor;
        EditorPrefs.SetInt(PrefKey_BrushColor, (int)_brushColor);
        SceneView.RepaintAll();
    }

    private static Color GetGuiColor(GameColor color)
    {
        switch (color.ToString())
        {
            case "Yellow": return new Color(1f, 0.9f, 0.2f);
            case "Orange": return new Color(1f, 0.55f, 0.15f);
            case "Green": return new Color(0.35f, 0.9f, 0.4f);
            case "Blue": return new Color(0.3f, 0.55f, 1f);
        }

        float h = (Mathf.Abs(color.GetHashCode()) % 1000) / 1000f;
        return Color.HSVToRGB(h, 0.75f, 0.95f);
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

    private void InitializePreferences()
    {
        var values = (GameColor[])Enum.GetValues(typeof(GameColor));
        var defaultColor = values.Length > 0 ? values[0] : default;

        _brushColor = (GameColor)EditorPrefs.GetInt(PrefKey_BrushColor, (int)defaultColor);
        _editTool = (EditTool)EditorPrefs.GetInt(PrefKey_EditTool, (int)EditTool.Paint);
        _bulletCount = EditorPrefs.GetInt(PrefKey_BulletCount, 10);
        _lastInitializedLaneCount = _levelCreator.LevelData.laneCount;

        _targetBrushSize = EditorPrefs.GetInt(PrefKey_TargetBrushRadius, 0);
        _targetBrushSize = Mathf.Max(1, _targetBrushSize);

        EditorPrefs.SetInt(PrefKey_Width, _levelCreator.LevelData.laneCount);
        EditorPrefs.SetInt(PrefKey_ContainerSlot, _levelCreator.LevelData.storageCount);
    }

    private void DeleteCurrentlyHooveringObject()
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
            targetObject.transform.localScale = new Vector3(_targetAreaGrid.Size, 1, _targetAreaGrid.Size);
            targetObject.SetData(targetObjectData);
        }
    }

    private void CreateShooterByData(ShooterData shooterData)
    {
        if (GridHelper.TryGetPositionFromCoords(_shooterAreaGrid, shooterData.Coordinates, out Vector3 position))
        {
            var shooter = PrefabUtility.InstantiatePrefab(_levelCreator.shooterPrefab, _levelCreator.shooterParent) as Shooter;
            shooter.transform.position = position;
            shooter.SetData(shooterData);
        }
    }

    #endregion

    #region HANDLES VISUALIZATION

    private void TryVisualizeGetMainConveyorsBounds()
    {
        Handles.color = Color.aquamarine;
        Handles.DrawWireCube(_mainConveyorBounds.center, _mainConveyorBounds.size);
    }

    private void VisualizeShooterGrid()
    {
        Handles.color = Color.aquamarine;
        DrawGridLines(_shooterAreaGrid);
        int storageCount = _levelCreator.LevelData.storageCount;
        float storageSize = GameConfigs.Instance.gridSCellSize;
        float storageXPos = -((storageSize / 2f) * storageCount);
        float startZ = _shooterAreaGrid.CenterPosition.z + (_shooterAreaGrid.Height * 0.5f * _shooterAreaGrid.Size);
        Vector3 storageStartPos = new Vector3(storageXPos, 0f, startZ + (storageSize));

        Handles.color = Color.darkKhaki;
        for (int x = 0; x < storageCount; x++)
        {
            Vector3 position = storageStartPos + (Vector3.right * (storageSize * x)) + (Vector3.right * storageSize / 2f);
            Handles.DrawWireCube(position, (Vector3.one * _shooterAreaGrid.Size).FlattenY());
        }
    }

    private void DrawGridLines(GameGrid g)
    {
        float size = g.Size;
        int w = g.Width;
        int h = g.Height;

        float startZ = g.CenterPosition.z + (h * 0.5f * size);
        float startX = -((w - 1) * size * 0.5f);
        Vector3 firstCellCenter = new Vector3(startX, 0f, startZ);

        float half = size * 0.5f;

        Vector3 topLeft = firstCellCenter + new Vector3(-half, 0f, +half);
        Vector3 topRight = firstCellCenter + new Vector3((w - 1) * size + half, 0f, +half);
        Vector3 bottomLeft = firstCellCenter + new Vector3(-half, 0f, -((h - 1) * size + half));

        for (int x = 0; x <= w; x++)
        {
            Vector3 a = topLeft + Vector3.right * (x * size);
            Vector3 b = bottomLeft + Vector3.right * (x * size);
            Handles.DrawLine(a, b);
        }

        for (int y = 0; y <= h; y++)
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

    private bool _hasGameAreaBounds;
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
        if (MainCamera == null)
            return;

        float aspect = GetGameViewAspect(MainCamera);

        Plane plane = new Plane(Vector3.up, 0f);

        if (!TryGetViewportQuadOnPlane(MainCamera, aspect, plane, out Vector3[] quad))
            return;

        _gameAreaBounds = new Bounds(quad[0], Vector3.zero);
        _hasGameAreaBounds = true;
        for (int i = 1; i < quad.Length; i++)
            _gameAreaBounds.Encapsulate(quad[i]);

        Handles.color = Color.white.WithAlpha(0.5f);
        Handles.DrawWireCube(_gameAreaBounds.center, _gameAreaBounds.size);
    }

    private static float GetGameViewAspect(Camera cam)
    {
        if (Application.isPlaying && Screen.height > 0)
            return (float)Screen.width / Screen.height;

        Vector2 gv = Handles.GetMainGameViewSize();
        if (gv.y > 0.0001f)
            return gv.x / gv.y;

        // Fallback
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

    private struct ValidationMessage
    {
        public MessageType Type;
        public string Text;

        public ValidationMessage(MessageType type, string text)
        {
            Type = type;
            Text = text;
        }
    }
}


#endif