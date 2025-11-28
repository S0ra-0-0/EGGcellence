using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HierarchyColorCoderInitializer
{
    static HierarchyColorCoderInitializer()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
    }

    private static void OnHierarchyWindowItemGUI(int instanceID, Rect selectionRect)
    {
        if (HierarchyColorCoder.colorData == null || HierarchyColorCoder.colorData.colorEntries.Count == 0)
            return;

        GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (go == null) return;

        string path = HierarchyColorCoder.GetFullPath(go.transform);
        
        foreach (var entry in HierarchyColorCoder.colorData.colorEntries)
        {
            HierarchyColorCoder.CheckAndApplyColor(entry, path, selectionRect, go);
        }
    }
}
