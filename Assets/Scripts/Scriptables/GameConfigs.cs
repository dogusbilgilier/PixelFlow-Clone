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
    public int ConveyorBoardCount = 5;
    [TitleGroup("GENERAL/Settings", alignment: TitleAlignments.Centered)]
    public float gapBetweenBoards = 0.5f;
    [TitleGroup("GENERAL/Settings", alignment: TitleAlignments.Centered)]
    public float boardConveyorToMachineTweenDuration = 0.2f;
    [TitleGroup("GENERAL/Settings", alignment: TitleAlignments.Centered)]
    public float boardMachineToConveyorTweenDuration = 0.2f;
    
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