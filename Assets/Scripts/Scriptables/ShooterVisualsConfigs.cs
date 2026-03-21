using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Game
{
[CreateAssetMenu(fileName = "ShooterVisualsConfigs", menuName = "Scriptable Objects/ShooterVisualsConfigs")]
public class ShooterVisualsConfigs : ScriptableObject
{
    private static ShooterVisualsConfigs s_Instance;

    public static ShooterVisualsConfigs Instance
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
        Debug.Assert(s_Instance == null, "A ShooterVisualsConfigs Instance already exist!");
        s_Instance = this;
    }

    [TitleGroup("Materials")]
    public Material BaseMaterial;

    [TitleGroup("Materials")]
    public Material Hidden;


#if UNITY_EDITOR
    private static ShooterVisualsConfigs s_EditorInstance;

    private static ShooterVisualsConfigs EditorInstance
    {
        get
        {
            if (s_EditorInstance == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ShooterVisualsConfigs");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    s_EditorInstance = AssetDatabase.LoadAssetAtPath<ShooterVisualsConfigs>(path);
                }
            }

            return s_EditorInstance;
        }
    }
#endif
}
}
