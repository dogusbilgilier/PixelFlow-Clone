using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

[Searchable]
[CreateAssetMenu(fileName = "NewGameConfigs.asset", menuName = "Game Configs", order = 1)]
public class GameConfigs : ScriptableObject
{
    private static GameConfigs s_Instance;

    public static GameConfigs Instance
    {
        get
        {
#if UNITY_EDITOR
            return Application.isPlaying ? s_Instance : EditorInstance;
#else
            return s_Instance;
#endif
        }
    }

    public void Initialize()
    {
        Debug.Assert(s_Instance == null, "A GameConfigs Instance already exist!");
        s_Instance = this;
    }
    
    // CONFIGS
    [Button(ButtonSizes.Gigantic, ButtonStyle.FoldoutButton)]
    [FoldoutGroup("GENERAL", Expanded = false)]
    [TitleGroup("GENERAL/Settings", alignment: TitleAlignments.Centered)]
    public float minShooterRequestInterval = 0.2f;
    [Button(ButtonSizes.Gigantic, ButtonStyle.FoldoutButton)]
    [FoldoutGroup("CONVEYOR", Expanded = false)]
    [TitleGroup("CONVEYOR/Settings", alignment: TitleAlignments.Centered)]
    public int conveyorBoardCount = 5;
    [TitleGroup("CONVEYOR/Settings", alignment: TitleAlignments.Centered)]
    public float gapBetweenBoards = 0.05f;
    [TitleGroup("CONVEYOR/Settings", alignment: TitleAlignments.Centered)]
    public float boardConveyorToMachineTweenDuration = 0.2f;
    [TitleGroup("CONVEYOR/Settings", alignment: TitleAlignments.Centered)]
    public float boardMachineToConveyorTweenDuration = 0.2f;
    [TitleGroup("CONVEYOR/Settings", alignment: TitleAlignments.Centered)]
    public float boardFollowSpeed = 15f;
    
    [FoldoutGroup("SHOOTER", Expanded = false)]
    [TitleGroup("SHOOTER/Settings", alignment: TitleAlignments.Centered)]
    public float shooterGridZOffsetToMainConveyorByGridSize = 3f;
    [TitleGroup("SHOOTER/Settings", alignment: TitleAlignments.Centered)]
    public float shooterBulletSpeed = 20f;
    [TitleGroup("SHOOTER/JumpToConveyor", alignment: TitleAlignments.Centered)]
    public float shooterJumpToConveyorDuration = 0.5f;
    [TitleGroup("SHOOTER/JumpToConveyor", alignment: TitleAlignments.Centered)]
    public float shooterJumpToConveyorPower = 1f;


    //-----


#if UNITY_EDITOR
    private static GameConfigs s_EditorInstance;

    private static GameConfigs EditorInstance
    {
        get
        {
            if (s_EditorInstance == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:GameConfigs");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    s_EditorInstance = AssetDatabase.LoadAssetAtPath<GameConfigs>(path);
                }
            }

            return s_EditorInstance;
        }
    }
#endif
}