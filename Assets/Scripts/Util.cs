using UnityEngine;

public static class Util
{
    public static void Print(MonoBehaviour component, string msg)
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Debug.Log($"Frame {Time.frameCount}: {component.name} ({component.GetInstanceID()}): {msg}");
    }

    public static void CloneInStart(GameObject prefab)
    {
        // Prevent infinite spawns
        const string suffix = "-ClonedInStart";
        if (prefab.name.Contains(suffix))
        {
            return;
        }

        var prevName = prefab.name;
        prefab.name = $"{prevName}{suffix}";
        Object.Instantiate(prefab);
        prefab.name = prevName;
    }
}