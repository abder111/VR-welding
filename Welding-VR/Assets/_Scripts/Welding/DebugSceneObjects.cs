using UnityEngine;

/// <summary>
/// Temporary debug script to list all objects in the scene
/// </summary>
public class DebugSceneObjects : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("═════════════════════════════════════════════");
        Debug.Log("SCENE OBJECTS DEBUG");
        Debug.Log("═════════════════════════════════════════════");

        // Find Environment
        GameObject env = GameObject.Find("Environment");
        if (env != null)
        {
            Debug.Log("✓ Found 'Environment'");
            ListChildren(env, 0);
        }
        else
        {
            Debug.LogWarning("✗ 'Environment' not found");
        }

        Debug.Log("═════════════════════════════════════════════");
    }

    void ListChildren(GameObject parent, int depth)
    {
        string indent = new string(' ', depth * 2);
        foreach (Transform child in parent.transform)
        {
            Debug.Log(indent + "├─ " + child.name + " (" + child.gameObject.tag + ")");
            if (child.childCount > 0)
            {
                ListChildren(child.gameObject, depth + 1);
            }
        }
    }
}
