using UnityEditor;
using UnityEngine;

public static class GameUtilities
{
    public static void DestroyAllChildrenImmediate(this Transform transform)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            Object.DestroyImmediate(child.gameObject);
        }
    }
}