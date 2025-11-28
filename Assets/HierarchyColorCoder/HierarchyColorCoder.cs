using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text.RegularExpressions;

[System.Serializable]
public class HierarchyColorData
{
    public List<ColorEntry> colorEntries = new List<ColorEntry>();
    // Global defaults for new entries
    public ColorEntry defaults = new ColorEntry()
    {
        color = new Color(0.2f, 0.6f, 1f, 1f),
        backgroundAlpha = 0.25f,
        borderMode = BorderMode.Left,
        borderColor = Color.white,
        borderThickness = 2f,
        fontStyle = FontStyle.Normal,
        useTextColor = false,
        textColor = Color.white,
        leftOffset = 0,
        rightOffset = 0,
        cornerRadius = 0f,
        badgeEnabled = false,
        badgeColor = Color.white,
        badgeSize = 6f
    };
    public List<Preset> presets = new List<Preset>();
    public List<AutoRule> rules = new List<AutoRule>();
}

[System.Serializable]
public class ColorEntry
{
    public string path;
    public Color color;
    public bool includeChildren;
    public bool isExpanded = true;
    // Visual customization
    public float leftOffset = 0f;      // extra padding on the left side of the row highlight
    public float rightOffset = 0f;     // extra padding on the right side of the row highlight
    public float backgroundAlpha = 0.3f; // 0..1 background opacity
    public FontStyle fontStyle = FontStyle.Normal;
    public bool useFontSize = false;
    public int fontSize = 0; // 0 = default
    public bool useTextColor = false;
    public Color textColor = Color.white;
    public BorderMode borderMode = BorderMode.Left; // default: keep previous behavior
    public Color borderColor = Color.white;
    public float borderThickness = 2f;
    public float cornerRadius = 0f; // rounded corners radius
    // Badges/icons
    public bool badgeEnabled = false;
    public Color badgeColor = Color.white;
    public float badgeSize = 6f;

    // Header/Folder-like appearance
    public bool headerMode = false; // draw as section header (non-conditional)
    public string headerLabel = "";

    // Conditional application
    public bool useConditions = false; // when true, object must match conditions too
    public string tagEquals = ""; // empty = ignore
    public int layerEquals = -1;   // -1 = ignore
    public string componentTypeContains = ""; // e.g. "Rigidbody" or full name
    public string nameRegex = ""; // regex, case-insensitive by default
    [NonSerialized] public List<ColorEntry> children = new List<ColorEntry>();

    // Non-serialized UI state (foldouts)
    [NonSerialized] public bool _uiFoldAppearance = true;
    [NonSerialized] public bool _uiFoldText = false;
    [NonSerialized] public bool _uiFoldBorder = false;
    [NonSerialized] public bool _uiFoldBadge = false;
    [NonSerialized] public bool _uiFoldHeader = false;
    [NonSerialized] public bool _uiFoldConditions = false;
}

[System.Serializable]
public enum BorderMode
{
    None,
    Left,
    Right,
    LeftAndRight,
    Full
}

[System.Serializable]
public class Preset
{
    public string name;
    public ColorEntry style = new ColorEntry();
}

[System.Serializable]
public class AutoRule
{
    public bool enabled = true;
    public string presetName = "";
    public string tagEquals = "";
    public int layerEquals = -1;
    public string namePrefix = "";
    public string nameRegex = "";
    public string componentTypeContains = "";
}

public class HierarchyColorCoder : EditorWindow
{
    private static HierarchyColorCoder window;
    public static HierarchyColorData colorData;
    private Vector2 scrollPosition;
    private static int topTabIndex = 0;
    private static readonly string[] topTabs = new[] { "Mappings", "Presets", "Defaults", "Rules" };
    // Presets tab UI state
    private int selectedPresetIndex = 0;
    private string newPresetName = "New Preset";

    [MenuItem("Tools/Hierarchy Color Coder")]
    public static void ShowWindow()
    {
        window = GetWindow<HierarchyColorCoder>("Hierarchy Color Coder");
        window.minSize = new Vector2(300, 400);
        LoadData();
    }

    private void DrawDefaultsUI()
    {
        if (colorData == null) { EditorGUILayout.HelpBox("No data loaded.", MessageType.Info); return; }
        EditorGUILayout.LabelField("Global Defaults", EditorStyles.boldLabel);
        var d = colorData.defaults;
        EditorGUILayout.BeginVertical("box");
        d.color = EditorGUILayout.ColorField("Color", d.color);
        d.backgroundAlpha = EditorGUILayout.Slider("Bg Alpha", d.backgroundAlpha, 0f, 1f);
        d.fontStyle = (FontStyle)EditorGUILayout.EnumPopup("Font Style", d.fontStyle);
        d.useFontSize = EditorGUILayout.Toggle("Use Font Size", d.useFontSize);
        using (new EditorGUI.DisabledScope(!d.useFontSize))
        {
            d.fontSize = EditorGUILayout.IntField("Font Size", d.fontSize);
        }
        d.useTextColor = EditorGUILayout.Toggle("Use Text Color", d.useTextColor);
        using (new EditorGUI.DisabledScope(!d.useTextColor))
        {
            d.textColor = EditorGUILayout.ColorField("Text Color", d.textColor);
        }
        d.borderMode = (BorderMode)EditorGUILayout.EnumPopup("Border", d.borderMode);
        d.borderColor = EditorGUILayout.ColorField("Border Color", d.borderColor);
        d.borderThickness = EditorGUILayout.Slider("Thickness", d.borderThickness, 0f, 6f);
        d.cornerRadius = EditorGUILayout.Slider("Corner Radius", d.cornerRadius, 0f, 12f);
        d.leftOffset = EditorGUILayout.FloatField("Left Offset", d.leftOffset);
        d.rightOffset = EditorGUILayout.FloatField("Right Offset", d.rightOffset);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Apply Defaults To First Entry"))
        {
            if (colorData.colorEntries.Count > 0) ApplyDefaults(colorData.colorEntries[0]);
        }
    }

    private void DrawPresetsUI()
    {
        if (colorData == null) { EditorGUILayout.HelpBox("No data loaded.", MessageType.Info); return; }
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

        // Save preset from selection
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Save preset from current selection", EditorStyles.miniBoldLabel);
        newPresetName = EditorGUILayout.TextField("Preset Name", newPresetName);
        using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
        {
            if (GUILayout.Button("Save Preset From Selection"))
            {
                SavePresetFromSelectionNamed(string.IsNullOrWhiteSpace(newPresetName) ? "Preset" : newPresetName.Trim());
            }
        }
        EditorGUILayout.EndVertical();

        // Apply preset to selection
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Apply preset to selection", EditorStyles.miniBoldLabel);
        if (colorData.presets.Count == 0)
        {
            EditorGUILayout.HelpBox("No presets. Create one above.", MessageType.None);
        }
        else
        {
            List<string> names = new List<string>(colorData.presets.Count);
            for (int i = 0; i < colorData.presets.Count; i++)
            {
                names.Add(string.IsNullOrEmpty(colorData.presets[i].name) ? $"Preset {i+1}" : colorData.presets[i].name);
            }
            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, colorData.presets.Count - 1);
            selectedPresetIndex = EditorGUILayout.Popup("Preset", selectedPresetIndex, names.ToArray());
            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Apply To Selection"))
                {
                    ApplyPresetToSelectionIndex(selectedPresetIndex);
                }
            }
        }
        EditorGUILayout.EndVertical();

        // Manage presets
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Manage Presets", EditorStyles.miniBoldLabel);
        for (int i = 0; i < colorData.presets.Count; i++)
        {
            EditorGUILayout.BeginHorizontal("box");
            colorData.presets[i].name = EditorGUILayout.TextField("Name", colorData.presets[i].name);
            if (GUILayout.Button("Delete", GUILayout.Width(80)))
            {
                colorData.presets.RemoveAt(i); i--; continue;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawRulesUI()
    {
        EditorGUILayout.LabelField("Rules (auto-apply presets)", EditorStyles.boldLabel);
        if (colorData.rules == null) colorData.rules = new List<AutoRule>();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Add Rule")) colorData.rules.Add(new AutoRule());
        EditorGUILayout.Space(4);

        for (int i = 0; i < colorData.rules.Count; i++)
        {
            var r = colorData.rules[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            r.enabled = EditorGUILayout.ToggleLeft($"Rule {i+1}", r.enabled, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Up", GUILayout.Width(40)) && i > 0)
            { var tmp = colorData.rules[i-1]; colorData.rules[i-1] = r; colorData.rules[i] = tmp; }
            if (GUILayout.Button("Down", GUILayout.Width(50)) && i < colorData.rules.Count-1)
            { var tmp = colorData.rules[i+1]; colorData.rules[i+1] = r; colorData.rules[i] = tmp; }
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            { colorData.rules.RemoveAt(i); i--; EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); continue; }
            EditorGUILayout.EndHorizontal();

            // Preset picker
            List<string> names = new List<string>();
            foreach (var p in colorData.presets) names.Add(p.name);
            int sel = Mathf.Max(0, names.FindIndex(n => string.Equals(n, r.presetName, StringComparison.OrdinalIgnoreCase)));
            int newSel = EditorGUILayout.Popup("Preset", names.Count==0?0:sel, names.ToArray());
            if (names.Count>0) r.presetName = names[newSel]; else EditorGUILayout.HelpBox("No presets available. Create one in the Presets tab.", MessageType.Info);

            // Conditions
            r.tagEquals = EditorGUILayout.TagField("Tag Equals", string.IsNullOrEmpty(r.tagEquals)? "Untagged" : r.tagEquals);
            // Layer with None button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Layer Equals", GUILayout.Width(100));
            int _uiLayer = r.layerEquals < 0 ? 0 : Mathf.Clamp(r.layerEquals, 0, 31);
            _uiLayer = EditorGUILayout.LayerField(_uiLayer);
            if (GUILayout.Button("None", GUILayout.Width(60))) r.layerEquals = -1; else r.layerEquals = _uiLayer;
            EditorGUILayout.EndHorizontal();
            r.namePrefix = EditorGUILayout.TextField("Name Prefix", r.namePrefix);
            r.nameRegex = EditorGUILayout.TextField("Name Regex", r.nameRegex);
            r.componentTypeContains = EditorGUILayout.TextField("Component Contains", r.componentTypeContains);

            EditorGUILayout.EndVertical();
        }
    }

    private void SavePresetFromSelectionNamed(string presetName)
    {
        if (Selection.activeGameObject == null) return;
        string path = GetGameObjectPath(Selection.activeGameObject.transform);
        var entry = FindEntryByPath(path);
        if (entry == null) return;
        colorData.presets.Add(new Preset
        {
            name = presetName,
            style = CloneEntryStyle(entry)
        });
        SaveData();
    }

    private void ApplyPresetToSelectionIndex(int presetIndex)
    {
        if (Selection.activeGameObject == null) return;
        if (presetIndex < 0 || presetIndex >= colorData.presets.Count) return;
        var preset = colorData.presets[presetIndex];
        string path = GetGameObjectPath(Selection.activeGameObject.transform);
        var entry = FindOrCreateEntry(path);
        CopyStyle(preset.style, entry);
        SaveData();
    }

    private void OnEnable()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
        LoadData();
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        
        // Toolbar actions row
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Selected Object")) AddSelectedObject();
        if (GUILayout.Button("Save")) SaveData();
        if (GUILayout.Button("Export")) ExportData();
        if (GUILayout.Button("Import")) ImportData();
        EditorGUILayout.EndHorizontal();

        // Top tabs
        topTabIndex = GUILayout.Toolbar(topTabIndex, topTabs);
        EditorGUILayout.Space(6);

        switch (topTabIndex)
        {
            case 0: // Mappings
                EditorGUILayout.LabelField("Color Mappings", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                if (colorData != null && colorData.colorEntries.Count > 0)
                {
                    for (int i = 0; i < colorData.colorEntries.Count; i++)
                    {
                        DrawColorEntry(colorData.colorEntries[i], i);
                    }
                }
                EditorGUILayout.EndScrollView();
                break;
            case 1: // Presets
                DrawPresetsUI();
                break;
            case 2: // Defaults
                DrawDefaultsUI();
                break;
            case 3: // Rules
                DrawRulesUI();
                break;
        }
    }

    private void DrawColorEntry(ColorEntry entry, int index, int indentLevel = 0)
    {
        // Boxed container per entry
        EditorGUILayout.BeginVertical("box");
        // Header row
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 12);
        entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, "", true, EditorStyles.foldout);
        entry.color = EditorGUILayout.ColorField(entry.color, GUILayout.Width(70));
        string displayName = entry.path;
        if (!string.IsNullOrEmpty(displayName) && displayName.Length > 40)
            displayName = "..." + displayName.Substring(displayName.Length - 37);
        GUILayout.Label(displayName, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        entry.includeChildren = GUILayout.Toggle(entry.includeChildren, "Children", GUILayout.Width(80));
        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            if (indentLevel == 0)
            {
                colorData.colorEntries.RemoveAt(index);
                SaveData();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (entry.isExpanded)
        {
            // Appearance section
            entry._uiFoldAppearance = EditorGUILayout.Foldout(entry._uiFoldAppearance, "Appearance", true);
            if (entry._uiFoldAppearance)
            {
                EditorGUILayout.BeginVertical("box");
                entry.leftOffset = EditorGUILayout.FloatField("Left Offset", entry.leftOffset);
                entry.rightOffset = EditorGUILayout.FloatField("Right Offset", entry.rightOffset);
                entry.backgroundAlpha = EditorGUILayout.Slider("Background Alpha", entry.backgroundAlpha, 0f, 1f);
                entry.cornerRadius = EditorGUILayout.Slider("Corner Radius", entry.cornerRadius, 0f, 12f);
                EditorGUILayout.EndVertical();
            }

            // Text section
            entry._uiFoldText = EditorGUILayout.Foldout(entry._uiFoldText, "Text", true);
            if (entry._uiFoldText)
            {
                EditorGUILayout.BeginVertical("box");
                entry.fontStyle = (FontStyle)EditorGUILayout.EnumPopup("Font Style", entry.fontStyle);
                entry.useFontSize = EditorGUILayout.Toggle("Use Font Size", entry.useFontSize);
                using (new EditorGUI.DisabledScope(!entry.useFontSize))
                {
                    entry.fontSize = EditorGUILayout.IntField("Font Size", entry.fontSize);
                }
                entry.useTextColor = EditorGUILayout.Toggle("Use Text Color", entry.useTextColor);
                using (new EditorGUI.DisabledScope(!entry.useTextColor))
                {
                    entry.textColor = EditorGUILayout.ColorField("Text Color", entry.textColor);
                }
                EditorGUILayout.EndVertical();
            }

            // Border section
            entry._uiFoldBorder = EditorGUILayout.Foldout(entry._uiFoldBorder, "Border", true);
            if (entry._uiFoldBorder)
            {
                EditorGUILayout.BeginVertical("box");
                entry.borderMode = (BorderMode)EditorGUILayout.EnumPopup("Mode", entry.borderMode);
                entry.borderThickness = EditorGUILayout.Slider("Thickness", entry.borderThickness, 0f, 6f);
                entry.borderColor = EditorGUILayout.ColorField("Color", entry.borderColor);
                EditorGUILayout.EndVertical();
            }

            // Badge section
            entry._uiFoldBadge = EditorGUILayout.Foldout(entry._uiFoldBadge, "Badge", true);
            if (entry._uiFoldBadge)
            {
                EditorGUILayout.BeginVertical("box");
                entry.badgeEnabled = EditorGUILayout.Toggle("Enable", entry.badgeEnabled);
                using (new EditorGUI.DisabledScope(!entry.badgeEnabled))
                {
                    entry.badgeColor = EditorGUILayout.ColorField("Color", entry.badgeColor);
                    entry.badgeSize = EditorGUILayout.Slider("Size", entry.badgeSize, 2f, 16f);
                }
                EditorGUILayout.EndVertical();
            }

            // Header section
            entry._uiFoldHeader = EditorGUILayout.Foldout(entry._uiFoldHeader, "Header / Pseudo-folder", true);
            if (entry._uiFoldHeader)
            {
                EditorGUILayout.BeginVertical("box");
                entry.headerMode = EditorGUILayout.Toggle("Header Mode", entry.headerMode);
                using (new EditorGUI.DisabledScope(!entry.headerMode))
                {
                    entry.headerLabel = EditorGUILayout.TextField("Header Label", entry.headerLabel);
                }
                EditorGUILayout.EndVertical();
            }

            // Conditions section
            entry._uiFoldConditions = EditorGUILayout.Foldout(entry._uiFoldConditions, "Conditions", true);
            if (entry._uiFoldConditions)
            {
                EditorGUILayout.BeginVertical("box");
                entry.useConditions = EditorGUILayout.Toggle("Enable Conditions", entry.useConditions);
                using (new EditorGUI.DisabledScope(!entry.useConditions))
                {
                    entry.tagEquals = EditorGUILayout.TagField("Tag Equals", entry.tagEquals);
                    // Layer
                    int _uiLayer = entry.layerEquals < 0 ? 0 : Mathf.Clamp(entry.layerEquals, 0, 31);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Layer Equals", GUILayout.Width(100));
                    _uiLayer = EditorGUILayout.LayerField(_uiLayer);
                    if (GUILayout.Button("None", GUILayout.Width(60))) entry.layerEquals = -1; else entry.layerEquals = _uiLayer;
                    EditorGUILayout.EndHorizontal();
                    entry.componentTypeContains = EditorGUILayout.TextField("Component Contains", entry.componentTypeContains);
                    entry.nameRegex = EditorGUILayout.TextField("Name Regex", entry.nameRegex);
                }
                EditorGUILayout.EndVertical();
            }
        }

        // Children
        if (entry.isExpanded && entry.children != null && entry.children.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Children", EditorStyles.miniBoldLabel);
            for (int i = 0; i < entry.children.Count; i++)
            {
                DrawColorEntry(entry.children[i], i, indentLevel + 1);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void AddSelectedObject()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("No GameObject selected!");
            return;
        }

        if (colorData == null)
            colorData = new HierarchyColorData();

        string path = GetGameObjectPath(Selection.activeGameObject.transform);
        
        // Check if this path already exists
        if (colorData.colorEntries.Exists(e => e.path == path))
        {
            Debug.LogWarning("This GameObject is already in the list!");
            return;
        }

        var newEntry = new ColorEntry
        {
            path = path,
            color = Color.white,
            includeChildren = false,
            isExpanded = true
        };

        // If this is a child of an existing entry, add it as a child
        bool addedAsChild = false;
        foreach (var entry in colorData.colorEntries)
        {
            if (path.StartsWith(entry.path + "/") && entry.includeChildren)
            {
                // Remove the parent path to get the relative path
                string relativePath = path.Substring(entry.path.Length + 1);
                newEntry.path = relativePath;
                entry.children.Add(newEntry);
                addedAsChild = true;
                break;
            }
        }

        if (!addedAsChild)
        {
            colorData.colorEntries.Add(newEntry);
        }

        SaveData();
    }

    private string GetGameObjectPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private static void OnHierarchyWindowItemGUI(int instanceID, Rect selectionRect)
    {
        if (colorData == null || colorData.colorEntries.Count == 0)
            return;

        GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (go == null) return;

        string path = GetFullPath(go.transform);
        
        // Explicit entries take precedence over rules
        bool hasExplicit = AnyEntryMatches(path, go);
        if (!hasExplicit)
        {
            TryApplyRules(go, selectionRect);
        }

        // Still draw explicit entries (may layer multiple if desired)
        foreach (var entry in colorData.colorEntries)
        {
            CheckAndApplyColor(entry, path, selectionRect, go);
        }
    }

    private static bool AnyEntryMatches(string path, GameObject go)
    {
        foreach (var entry in colorData.colorEntries)
        {
            if (PathMatch(entry, path) && ConditionsMatch(entry, go)) return true;
            foreach (var child in entry.children)
            {
                string fullChildPath = entry.path + "/" + child.path;
                if ((path == fullChildPath || (child.includeChildren && path.StartsWith(fullChildPath + "/"))) && ConditionsMatch(child, go))
                    return true;
            }
        }
        return false;
    }

    private static bool TryApplyRules(GameObject go, Rect selectionRect)
    {
        if (colorData.rules == null || colorData.rules.Count == 0) return false;
        foreach (var rule in colorData.rules)
        {
            if (!rule.enabled) continue;
            if (!RuleMatches(rule, go)) continue;
            var preset = ResolvePreset(rule.presetName);
            if (preset == null) return false;
            var style = CloneEntryStyle(preset.style);
            DrawHierarchyHighlight(selectionRect, style, go);
            return true; // first match wins
        }
        return false;
    }

    private static bool RuleMatches(AutoRule r, GameObject go)
    {
        // Tag
        if (!string.IsNullOrEmpty(r.tagEquals) && go.tag != r.tagEquals) return false;
        // Layer
        if (r.layerEquals >= 0 && go.layer != r.layerEquals) return false;
        // Name prefix
        if (!string.IsNullOrEmpty(r.namePrefix) && !go.name.StartsWith(r.namePrefix, StringComparison.OrdinalIgnoreCase)) return false;
        // Regex
        if (!string.IsNullOrEmpty(r.nameRegex))
        {
            try { if (!new Regex(r.nameRegex, RegexOptions.IgnoreCase).IsMatch(go.name)) return false; }
            catch { return false; }
        }
        // Component contains
        if (!string.IsNullOrEmpty(r.componentTypeContains))
        {
            string needle = r.componentTypeContains.ToLowerInvariant();
            bool found = false;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.Name.ToLowerInvariant().Contains(needle) || t.FullName.ToLowerInvariant().Contains(needle)) { found = true; break; }
            }
            if (!found) return false;
        }
        return true;
    }

    private static Preset ResolvePreset(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var p in colorData.presets)
            if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }

    public static void CheckAndApplyColor(ColorEntry entry, string path, Rect selectionRect, GameObject go)
    {
        if (PathMatch(entry, path) && ConditionsMatch(entry, go))
        {
            DrawHierarchyHighlight(selectionRect, entry, go);
        }

        // Check children entries
        foreach (var child in entry.children)
        {
            string fullChildPath = entry.path + "/" + child.path;
            if (PathMatchChild(entry, child, path) && ConditionsMatch(child, go))
            {
                DrawHierarchyHighlight(selectionRect, child, go);
            }
        }
    }

    private static bool PathMatch(ColorEntry entry, string path)
    {
        return path == entry.path || (entry.includeChildren && path.StartsWith(entry.path + "/"));
    }

    private static bool PathMatchChild(ColorEntry parent, ColorEntry child, string path)
    {
        string fullChildPath = parent.path + "/" + child.path;
        return path == fullChildPath || (child.includeChildren && path.StartsWith(fullChildPath + "/"));
    }

    private static bool ConditionsMatch(ColorEntry entry, GameObject go)
    {
        if (entry.headerMode) return true; // headers always draw for their own object
        if (!entry.useConditions) return true;

        // Tag
        if (!string.IsNullOrEmpty(entry.tagEquals))
        {
            if (go.tag != entry.tagEquals) return false;
        }

        // Layer
        if (entry.layerEquals >= 0)
        {
            if (go.layer != entry.layerEquals) return false;
        }

        // Component type contains
        if (!string.IsNullOrEmpty(entry.componentTypeContains))
        {
            bool found = false;
            var comps = go.GetComponents<Component>();
            string needle = entry.componentTypeContains.ToLowerInvariant();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.Name.ToLowerInvariant().Contains(needle) || t.FullName.ToLowerInvariant().Contains(needle))
                {
                    found = true; break;
                }
            }
            if (!found) return false;
        }

        // Name regex
        if (!string.IsNullOrEmpty(entry.nameRegex))
        {
            try
            {
                var rx = new Regex(entry.nameRegex, RegexOptions.IgnoreCase);
                if (!rx.IsMatch(go.name)) return false;
            }
            catch { /* invalid regex -> treat as no match */ return false; }
        }

        return true;
    }

    private static void DrawHierarchyHighlight(Rect rowRect, ColorEntry entry, GameObject go)
    {
        // Apply offsets
        Rect rect = rowRect;
        rect.x += entry.leftOffset;
        rect.width -= Mathf.Max(0f, entry.leftOffset) + Mathf.Max(0f, entry.rightOffset);

        // Determine selection/hover blending
        Color baseBg = entry.color;
        baseBg.a = Mathf.Clamp01(entry.backgroundAlpha);
        Color blendedBg = baseBg;
        // Selection color from skin settings
        var selCol = GUI.skin.settings.selectionColor;
        bool isSelected = Selection.activeGameObject == go;
        bool isHover = rect.Contains(Event.current.mousePosition);
        if (isSelected)
        {
            blendedBg = Color.Lerp(baseBg, selCol, 0.45f);
        }
        else if (isHover)
        {
            blendedBg = Color.Lerp(baseBg, selCol, 0.2f);
        }

        // Background (rounded optional)
        if (entry.backgroundAlpha > 0f)
        {
            if (entry.cornerRadius > 0.01f)
            {
                DrawRoundedRect(rect, blendedBg, entry.cornerRadius);
            }
            else
            {
                EditorGUI.DrawRect(rect, blendedBg);
            }
        }

        // Borders
        if (entry.borderMode != BorderMode.None && entry.borderThickness > 0f)
        {
            var bCol = entry.borderColor;
            float t = entry.borderThickness;
            switch (entry.borderMode)
            {
                case BorderMode.Left:
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), bCol);
                    break;
                case BorderMode.Right:
                    EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), bCol);
                    break;
                case BorderMode.LeftAndRight:
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), bCol);
                    EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), bCol);
                    break;
                case BorderMode.Full:
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), bCol); // top
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), bCol); // bottom
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), bCol); // left
                    EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), bCol); // right
                    break;
            }
        }

        // Optional styled label overlay (replace look)
        if (Event.current.type == EventType.Repaint)
        {
            var style = new GUIStyle(EditorStyles.label);
            style.fontStyle = entry.fontStyle;
            if (entry.useFontSize && entry.fontSize > 0) style.fontSize = entry.fontSize;
            if (entry.useTextColor) style.normal.textColor = entry.textColor;

            // Draw header differently
            if (entry.headerMode && !string.IsNullOrEmpty(entry.headerLabel))
            {
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleLeft;
                Rect headerRect = rowRect;
                headerRect.x += 4f;
                EditorGUI.LabelField(headerRect, entry.headerLabel, style);
            }
            else
            {
                // re-draw name to enforce styling (over Unity's default)
                Rect labelRect = rowRect;
                labelRect.x += 2f;
                EditorGUI.LabelField(labelRect, go.name, style);
            }

            // Badge/icon
            if (entry.badgeEnabled)
            {
                float s = entry.badgeSize;
                var center = new Vector2(rowRect.x + 12f, rowRect.center.y);
                DrawCircle(center, s * 0.5f, entry.badgeColor);
            }
        }
    }

    public static string GetFullPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private void DrawDefaultsAndPresetsUI()
    {
        if (colorData == null) return;
        EditorGUILayout.LabelField("Global Defaults", EditorStyles.boldLabel);
        var d = colorData.defaults;
        EditorGUILayout.BeginHorizontal();
        d.color = EditorGUILayout.ColorField("Color", d.color);
        d.backgroundAlpha = EditorGUILayout.Slider("Bg Alpha", d.backgroundAlpha, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        d.fontStyle = (FontStyle)EditorGUILayout.EnumPopup("Font Style", d.fontStyle);
        d.useTextColor = EditorGUILayout.Toggle("Use Text Color", d.useTextColor);
        using (new EditorGUI.DisabledScope(!d.useTextColor))
        {
            d.textColor = EditorGUILayout.ColorField("Text Color", d.textColor);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        d.useFontSize = EditorGUILayout.Toggle("Use Font Size", d.useFontSize);
        using (new EditorGUI.DisabledScope(!d.useFontSize))
        {
            d.fontSize = EditorGUILayout.IntField("Font Size", d.fontSize);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        d.borderMode = (BorderMode)EditorGUILayout.EnumPopup("Border", d.borderMode);
        d.borderColor = EditorGUILayout.ColorField("Border Color", d.borderColor);
        d.borderThickness = EditorGUILayout.Slider("Thickness", d.borderThickness, 0f, 6f);
        d.cornerRadius = EditorGUILayout.Slider("Corner Radius", d.cornerRadius, 0f, 12f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        d.leftOffset = EditorGUILayout.FloatField("Left Offset", d.leftOffset);
        d.rightOffset = EditorGUILayout.FloatField("Right Offset", d.rightOffset);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Apply Defaults To Selected Entry"))
        {
            if (colorData.colorEntries.Count > 0)
            {
                // naive apply to first entry for now; recommend selecting via UI
                ApplyDefaults(colorData.colorEntries[0]);
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Preset From Selection"))
        {
            SavePresetFromSelection();
        }
        if (GUILayout.Button("Apply Preset To Selection"))
        {
            ApplyPresetToSelection();
        }
        EditorGUILayout.EndHorizontal();
        if (colorData.presets != null)
        {
            for (int i = 0; i < colorData.presets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                colorData.presets[i].name = EditorGUILayout.TextField(colorData.presets[i].name);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    colorData.presets.RemoveAt(i);
                    i--; continue;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void ApplyDefaults(ColorEntry target)
    {
        var d = colorData.defaults;
        target.color = d.color;
        target.backgroundAlpha = d.backgroundAlpha;
        target.fontStyle = d.fontStyle;
        target.useTextColor = d.useTextColor;
        target.textColor = d.textColor;
        target.useFontSize = d.useFontSize;
        target.fontSize = d.fontSize;
        target.borderMode = d.borderMode;
        target.borderColor = d.borderColor;
        target.borderThickness = d.borderThickness;
        target.cornerRadius = d.cornerRadius;
        target.leftOffset = d.leftOffset;
        target.rightOffset = d.rightOffset;
    }

    private void SavePresetFromSelection()
    {
        if (Selection.activeGameObject == null) return;
        string path = GetGameObjectPath(Selection.activeGameObject.transform);
        var entry = FindEntryByPath(path);
        if (entry == null) return;
        colorData.presets.Add(new Preset
        {
            name = entry.path,
            style = CloneEntryStyle(entry)
        });
        SaveData();
    }

    private void ApplyPresetToSelection()
    {
        if (Selection.activeGameObject == null) return;
        if (colorData.presets.Count == 0) return;
        // apply first preset for simplicity; can add a dropdown later
        var preset = colorData.presets[0];
        string path = GetGameObjectPath(Selection.activeGameObject.transform);
        var entry = FindOrCreateEntry(path);
        CopyStyle(preset.style, entry);
        SaveData();
    }

    private ColorEntry FindEntryByPath(string path)
    {
        foreach (var e in colorData.colorEntries)
        {
            if (e.path == path) return e;
        }
        return null;
    }

    private ColorEntry FindOrCreateEntry(string path)
    {
        var e = FindEntryByPath(path);
        if (e != null) return e;
        e = new ColorEntry { path = path };
        CopyStyle(colorData.defaults, e);
        colorData.colorEntries.Add(e);
        return e;
    }

    private static ColorEntry CloneEntryStyle(ColorEntry src)
    {
        return new ColorEntry
        {
            color = src.color,
            backgroundAlpha = src.backgroundAlpha,
            fontStyle = src.fontStyle,
            useTextColor = src.useTextColor,
            textColor = src.textColor,
            useFontSize = src.useFontSize,
            fontSize = src.fontSize,
            borderMode = src.borderMode,
            borderColor = src.borderColor,
            borderThickness = src.borderThickness,
            cornerRadius = src.cornerRadius,
            leftOffset = src.leftOffset,
            rightOffset = src.rightOffset,
            badgeEnabled = src.badgeEnabled,
            badgeColor = src.badgeColor,
            badgeSize = src.badgeSize
        };
    }

    private static void CopyStyle(ColorEntry src, ColorEntry dst)
    {
        dst.color = src.color;
        dst.backgroundAlpha = src.backgroundAlpha;
        dst.fontStyle = src.fontStyle;
        dst.useTextColor = src.useTextColor;
        dst.textColor = src.textColor;
        dst.useFontSize = src.useFontSize;
        dst.fontSize = src.fontSize;
        dst.borderMode = src.borderMode;
        dst.borderColor = src.borderColor;
        dst.borderThickness = src.borderThickness;
        dst.cornerRadius = src.cornerRadius;
        dst.leftOffset = src.leftOffset;
        dst.rightOffset = src.rightOffset;
        dst.badgeEnabled = src.badgeEnabled;
        dst.badgeColor = src.badgeColor;
        dst.badgeSize = src.badgeSize;
    }

    // Drawing helpers
    private static void DrawRoundedRect(Rect rect, Color color, float radius)
    {
        Handles.BeginGUI();
        var prev = Handles.color;
        Handles.color = color;
        var pts = BuildRoundedRectPoints(rect, radius);
        Handles.DrawAAConvexPolygon(pts);
        Handles.color = prev;
        Handles.EndGUI();
    }

    private static Vector3[] BuildRoundedRectPoints(Rect r, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
        int seg = 6; // segments per corner
        List<Vector3> pts = new List<Vector3>(seg * 4);
        // corners centers
        Vector2 tl = new Vector2(r.xMin + radius, r.yMin + radius);
        Vector2 tr = new Vector2(r.xMax - radius, r.yMin + radius);
        Vector2 br = new Vector2(r.xMax - radius, r.yMax - radius);
        Vector2 bl = new Vector2(r.xMin + radius, r.yMax - radius);
        // angles
        AddArc(pts, tr, radius, -90f, 0f, seg);
        AddArc(pts, br, radius, 0f, 90f, seg);
        AddArc(pts, bl, radius, 90f, 180f, seg);
        AddArc(pts, tl, radius, 180f, 270f, seg);
        return pts.ToArray();
    }

    private static void AddArc(List<Vector3> pts, Vector2 center, float radius, float startDeg, float endDeg, int seg)
    {
        for (int i = 0; i <= seg; i++)
        {
            float t = (float)i / seg;
            float ang = Mathf.Deg2Rad * Mathf.Lerp(startDeg, endDeg, t);
            pts.Add(new Vector3(center.x + Mathf.Cos(ang) * radius, center.y + Mathf.Sin(ang) * radius));
        }
    }

    private static void DrawCircle(Vector2 center, float radius, Color color)
    {
        Handles.BeginGUI();
        var prev = Handles.color;
        Handles.color = color;
        int seg = Mathf.Clamp(Mathf.CeilToInt(radius * 6f), 12, 48);
        List<Vector3> pts = new List<Vector3>(seg);
        for (int i = 0; i < seg; i++)
        {
            float t = (float)i / seg;
            float ang = t * Mathf.PI * 2f;
            pts.Add(new Vector3(center.x + Mathf.Cos(ang) * radius, center.y + Mathf.Sin(ang) * radius));
        }
        Handles.DrawAAConvexPolygon(pts.ToArray());
        Handles.color = prev;
        Handles.EndGUI();
    }

    private static void SaveData()
    {
        string directory = Path.GetDirectoryName(Application.dataPath + "/Editor/");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(colorData, true);
        File.WriteAllText(Application.dataPath + "/Editor/HierarchyColorData.json", json);
        AssetDatabase.Refresh();
    }

    private static void LoadData()
    {
        string filePath = Application.dataPath + "/Editor/HierarchyColorData.json";
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            colorData = JsonUtility.FromJson<HierarchyColorData>(json);
        }
        else
        {
            colorData = new HierarchyColorData();
        }
    }

    private void ExportData()
    {
        string path = EditorUtility.SaveFilePanel("Export Color Data", "", "HierarchyColors", "json");
        if (!string.IsNullOrEmpty(path))
        {
            string json = JsonUtility.ToJson(colorData, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
        }
    }

    private void ImportData()
    {
        string path = EditorUtility.OpenFilePanel("Import Color Data", "", "json");
        if (!string.IsNullOrEmpty(path))
        {
            string json = File.ReadAllText(path);
            colorData = JsonUtility.FromJson<HierarchyColorData>(json);
            SaveData();
        }
    }
}
