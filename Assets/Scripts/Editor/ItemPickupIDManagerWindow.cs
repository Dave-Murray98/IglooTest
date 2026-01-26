using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor utility for managing ItemPickupInteractable IDs
/// - Detects duplicate IDs in scenes
/// - Auto-fixes duplicates
/// - Provides migration tools for old ID formats
/// </summary>
public class ItemPickupIDManagerWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private bool autoFixOnDetection = true;
    private bool showOnlyDuplicates = false;

    private List<ItemPickupInteractable> allPickups = new List<ItemPickupInteractable>();
    private Dictionary<string, List<ItemPickupInteractable>> idGroups = new Dictionary<string, List<ItemPickupInteractable>>();
    private bool hasScanned = false;

    [MenuItem("Tools/Item Pickup ID Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<ItemPickupIDManagerWindow>("Item Pickup ID Manager");
        window.minSize = new Vector2(600, 400);
        window.Show();
    }

    [MenuItem("Tools/Quick Scan Item Pickup IDs")]
    public static void QuickScan()
    {
        var pickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);
        var duplicates = FindDuplicateIDs(pickups);

        if (duplicates.Count == 0)
        {
            Debug.Log($"✅ No duplicate IDs found! Scanned {pickups.Length} item pickups.");
        }
        else
        {
            Debug.LogWarning($"⚠️ Found {duplicates.Count} duplicate ID groups affecting {duplicates.Sum(g => g.Value.Count)} items!");
            foreach (var group in duplicates)
            {
                Debug.LogWarning($"  Duplicate ID '{group.Key}' used by {group.Value.Count} items:");
                foreach (var item in group.Value)
                {
                    Debug.LogWarning($"    - {GetGameObjectPath(item.gameObject)}", item.gameObject);
                }
            }
        }
    }

    private void OnEnable()
    {
        ScanScene();
    }

    // private void OnGUI()
    // {
    //     EditorGUILayout.Space(10);

    //     // Header
    //     EditorGUILayout.LabelField("Item Pickup ID Manager", EditorStyles.boldLabel);
    //     EditorGUILayout.HelpBox(
    //         "This tool helps manage unique IDs for ItemPickupInteractable objects.\n" +
    //         "It detects duplicates and can automatically fix them.",
    //         MessageType.Info
    //     );

    //     EditorGUILayout.Space(10);

    //     // Settings
    //     EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    //     EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
    //     autoFixOnDetection = EditorGUILayout.Toggle("Auto-fix duplicates on scan", autoFixOnDetection);
    //     showOnlyDuplicates = EditorGUILayout.Toggle("Show only duplicates", showOnlyDuplicates);
    //     EditorGUILayout.EndVertical();

    //     EditorGUILayout.Space(10);

    //     // Actions
    //     EditorGUILayout.BeginHorizontal();

    //     if (GUILayout.Button("Scan Scene", GUILayout.Height(30)))
    //     {
    //         ScanScene();
    //     }

    //     GUI.enabled = hasScanned && idGroups.Any(g => g.Value.Count > 1);
    //     if (GUILayout.Button("Fix All Duplicates", GUILayout.Height(30)))
    //     {
    //         FixAllDuplicates();
    //     }
    //     GUI.enabled = true;

    //     if (GUILayout.Button("Migrate Old IDs", GUILayout.Height(30)))
    //     {
    //         MigrateOldIDFormat();
    //     }

    //     EditorGUILayout.EndHorizontal();

    //     EditorGUILayout.Space(10);

    //     // Results
    //     if (!hasScanned)
    //     {
    //         EditorGUILayout.HelpBox("Click 'Scan Scene' to begin.", MessageType.Warning);
    //         return;
    //     }

    //     DrawResults();
    // }

    private void ScanScene()
    {
        allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None).ToList();
        idGroups = GroupByID(allPickups);
        hasScanned = true;

        int duplicateCount = idGroups.Count(g => g.Value.Count > 1);

        if (duplicateCount > 0)
        {
            Debug.LogWarning($"⚠️ Found {duplicateCount} duplicate ID groups!");

            if (autoFixOnDetection)
            {
                FixAllDuplicates();
            }
        }
        else
        {
            Debug.Log($"✅ No duplicates found! Scanned {allPickups.Count} items.");
        }

        Repaint();
    }

    // private void DrawResults()
    // {
    //     var duplicateGroups = idGroups.Where(g => g.Value.Count > 1).ToList();
    //     var uniqueGroups = idGroups.Where(g => g.Value.Count == 1).ToList();

    //     // Summary
    //     EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    //     EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
    //     EditorGUILayout.LabelField($"Total Items: {allPickups.Count}");
    //     EditorGUILayout.LabelField($"Unique IDs: {uniqueGroups.Count}");
    //     EditorGUILayout.LabelField($"Duplicate Groups: {duplicateGroups.Count}",
    //         duplicateGroups.Count > 0 ? new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } } : EditorStyles.label);
    //     EditorGUILayout.EndVertical();

    //     EditorGUILayout.Space(10);

    //     // Item list
    //     scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

    //     // Show duplicates first
    //     if (duplicateGroups.Count > 0)
    //     {
    //         EditorGUILayout.LabelField("⚠️ DUPLICATES", EditorStyles.boldLabel);
    //         foreach (var group in duplicateGroups)
    //         {
    //             DrawIDGroup(group.Key, group.Value, true);
    //         }
    //     }

    //     // Show unique items if not filtering
    //     if (!showOnlyDuplicates)
    //     {
    //         EditorGUILayout.Space(10);
    //         EditorGUILayout.LabelField("✅ Unique Items", EditorStyles.boldLabel);
    //         foreach (var group in uniqueGroups)
    //         {
    //             DrawIDGroup(group.Key, group.Value, false);
    //         }
    //     }

    //     EditorGUILayout.EndScrollView();
    // }

    // private void DrawIDGroup(string id, List<ItemPickupInteractable> items, bool isDuplicate)
    // {
    //     var bgColor = isDuplicate ? new Color(1f, 0.5f, 0.5f, 0.3f) : new Color(0.5f, 1f, 0.5f, 0.2f);

    //     EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    //     GUI.backgroundColor = bgColor;

    //     EditorGUILayout.BeginHorizontal();

    //     // ID label
    //     var style = new GUIStyle(EditorStyles.label);
    //     if (isDuplicate)
    //     {
    //         style.normal.textColor = Color.red;
    //         style.fontStyle = FontStyle.Bold;
    //     }

    //     EditorGUILayout.LabelField($"ID: {id}", style);

    //     if (isDuplicate)
    //     {
    //         EditorGUILayout.LabelField($"({items.Count} duplicates)", GUILayout.Width(100));

    //         if (GUILayout.Button("Fix Group", GUILayout.Width(80)))
    //         {
    //             FixIDGroup(items);
    //         }
    //     }

    //     EditorGUILayout.EndHorizontal();

    //     // Show each item in group
    //     EditorGUI.indentLevel++;
    //     foreach (var item in items)
    //     {
    //         EditorGUILayout.BeginHorizontal();

    //         EditorGUILayout.ObjectField(item, typeof(ItemPickupInteractable), true);
    //         EditorGUILayout.LabelField(GetGameObjectPath(item.gameObject), EditorStyles.miniLabel);

    //         if (items.Count > 1 && GUILayout.Button("Regenerate ID", GUILayout.Width(120)))
    //         {
    //             RegenerateID(item);
    //         }

    //         EditorGUILayout.EndHorizontal();
    //     }
    //     EditorGUI.indentLevel--;

    //     EditorGUILayout.EndVertical();
    //     GUI.backgroundColor = Color.white;

    //     EditorGUILayout.Space(5);
    // }

    private void FixAllDuplicates()
    {
        int fixedCount = 0;

        foreach (var group in idGroups.Where(g => g.Value.Count > 1))
        {
            fixedCount += FixIDGroup(group.Value);
        }

        if (fixedCount > 0)
        {
            Debug.Log($"✅ Fixed {fixedCount} duplicate IDs!");
            ScanScene(); // Re-scan to update display
        }
    }

    private int FixIDGroup(List<ItemPickupInteractable> items)
    {
        int fixedCount = 0;

        // Keep the first item's ID, regenerate the rest
        for (int i = 1; i < items.Count; i++)
        {
            if (RegenerateID(items[i]))
            {
                fixedCount++;
            }
        }

        return fixedCount;
    }

    private bool RegenerateID(ItemPickupInteractable pickup)
    {
        if (pickup == null) return false;

        Undo.RecordObject(pickup, "Regenerate Item Pickup ID");

        // Use reflection to access private fields
        var editorGUIDField = typeof(ItemPickupInteractable).GetField("editorGUID",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var interactableIDField = typeof(ItemPickupInteractable).GetField("interactableID",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (editorGUIDField != null && interactableIDField != null)
        {
            // Generate new GUID
            string newGUID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            editorGUIDField.SetValue(pickup, newGUID);

            // Regenerate full ID
            string sceneName = pickup.gameObject.scene.name;
            var itemData = pickup.GetItemData();
            string itemName = itemData != null ? itemData.itemName : "UnknownItem";
            string newID = $"Item_{sceneName}_{itemName}_{newGUID}";

            interactableIDField.SetValue(pickup, newID);

            EditorUtility.SetDirty(pickup);

            Debug.Log($"✅ Regenerated ID for {GetGameObjectPath(pickup.gameObject)}: {newID}");
            return true;
        }

        Debug.LogError($"Failed to regenerate ID for {pickup.name} - reflection failed");
        return false;
    }

    private void MigrateOldIDFormat()
    {
        int migratedCount = 0;

        foreach (var pickup in allPickups)
        {
            string currentID = pickup.InteractableID;

            // Check if ID uses old format (contains Unity's GetInstanceID)
            if (!string.IsNullOrEmpty(currentID) && !currentID.Contains("dropped_"))
            {
                // Check if it has a GUID already (new format)
                var editorGUIDField = typeof(ItemPickupInteractable).GetField("editorGUID",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (editorGUIDField != null)
                {
                    string existingGUID = editorGUIDField.GetValue(pickup) as string;

                    if (string.IsNullOrEmpty(existingGUID))
                    {
                        // Migrate to new format
                        if (RegenerateID(pickup))
                        {
                            migratedCount++;
                        }
                    }
                }
            }
        }

        if (migratedCount > 0)
        {
            Debug.Log($"✅ Migrated {migratedCount} items to new ID format!");
            ScanScene();
        }
        else
        {
            Debug.Log("No items needed migration.");
        }
    }

    private static Dictionary<string, List<ItemPickupInteractable>> GroupByID(List<ItemPickupInteractable> pickups)
    {
        var groups = new Dictionary<string, List<ItemPickupInteractable>>();

        foreach (var pickup in pickups)
        {
            string id = pickup.InteractableID;

            if (string.IsNullOrEmpty(id))
            {
                id = "<EMPTY ID>";
            }

            if (!groups.ContainsKey(id))
            {
                groups[id] = new List<ItemPickupInteractable>();
            }

            groups[id].Add(pickup);
        }

        return groups;
    }

    private static Dictionary<string, List<ItemPickupInteractable>> FindDuplicateIDs(ItemPickupInteractable[] pickups)
    {
        return GroupByID(pickups.ToList())
            .Where(g => g.Value.Count > 1)
            .ToDictionary(g => g.Key, g => g.Value);
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}

/// <summary>
/// Automatic duplicate detection that runs when scenes are loaded or saved
/// </summary>
[InitializeOnLoad]
public class ItemPickupIDValidator
{
    static ItemPickupIDValidator()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private static void OnHierarchyChanged()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // Delayed validation to avoid performance issues
            EditorApplication.delayCall += ValidateScene;
        }
    }

    private static void ValidateScene()
    {
        var pickups = MonoBehaviour.FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);

        if (pickups.Length == 0)
            return;

        var duplicates = new Dictionary<string, List<ItemPickupInteractable>>();
        var idCounts = new Dictionary<string, int>();

        foreach (var pickup in pickups)
        {
            string id = pickup.InteractableID;

            if (string.IsNullOrEmpty(id))
                continue;

            if (!idCounts.ContainsKey(id))
            {
                idCounts[id] = 0;
                duplicates[id] = new List<ItemPickupInteractable>();
            }

            idCounts[id]++;
            duplicates[id].Add(pickup);
        }

        // Find actual duplicates
        var actualDuplicates = duplicates.Where(kvp => kvp.Value.Count > 1).ToList();

        if (actualDuplicates.Count > 0)
        {
            Debug.LogWarning($"⚠️ Item Pickup ID Validator: Found {actualDuplicates.Count} duplicate ID groups - AUTO-FIXING...");

            // AUTO-FIX: Regenerate IDs for duplicates (keep first, regenerate rest)
            int fixedCount = 0;
            foreach (var group in actualDuplicates)
            {
                // Skip the first item, regenerate IDs for the rest
                for (int i = 1; i < group.Value.Count; i++)
                {
                    var pickup = group.Value[i];
                    if (pickup != null)
                    {
                        ForceRegenerateID(pickup);
                        fixedCount++;
                    }
                }
            }

            if (fixedCount > 0)
            {
                Debug.Log($"✅ Auto-fixed {fixedCount} duplicate IDs!");
            }
        }
    }

    /// <summary>
    /// Force regenerate ID for a pickup item (used by automatic validator)
    /// </summary>
    private static void ForceRegenerateID(ItemPickupInteractable pickup)
    {
        if (pickup == null) return;

        // Use reflection to access private fields
        var editorGUIDField = typeof(ItemPickupInteractable).GetField("editorGUID",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var interactableIDField = typeof(ItemPickupInteractable).GetField("interactableID",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (editorGUIDField != null && interactableIDField != null)
        {
            // Generate new GUID
            string newGUID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            editorGUIDField.SetValue(pickup, newGUID);

            // Regenerate full ID
            string sceneName = pickup.gameObject.scene.name;
            var itemData = pickup.GetItemData();
            string itemName = itemData != null ? itemData.itemName : "UnknownItem";
            string newID = $"Item_{sceneName}_{itemName}_{newGUID}";

            interactableIDField.SetValue(pickup, newID);

            UnityEditor.EditorUtility.SetDirty(pickup);

            Debug.Log($"  ✅ Auto-fixed: {pickup.gameObject.name} → {newID}");
        }
    }
}