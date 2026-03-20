#if UNITY_EDITOR
using System.Collections.Generic;
using Game;
using UnityEditor;
using UnityEngine;

public partial class LevelCreatorEditor
{
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
        shooter.SetData(shooterData);
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
            shooter.SetData(shooterData);

        if (isNew || isDestroyed)
            RecalculateShooterGridHeight();

        MarkLevelDataDirty();
        UpdateBulletAndTargetsCounts();
    }

    #endregion
}

#endif
