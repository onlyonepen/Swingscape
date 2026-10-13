using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires a Player instance into a scene the same way TestLevel.unity does: makes sure the
/// Utils (GlobalReference) and GameUI prefabs exist in the scene (adding a GameUIManager to
/// GameUI if it's missing, since that component self-wires its own overlay references), then
/// connects the cross-object references that still have to be dragged in by hand (HUD canvas,
/// prediction reticle, HP + invulnerability overlays, GlobalReference.player).
/// </summary>
public static class PlayerSceneSetup
{
    private const string UtilsPrefabPath = "Assets/Prefab/Utils.prefab";
    private const string GameUIPrefabPath = "Assets/Prefab/GameUI.prefab";

    [MenuItem("Tools/Swingscape/Setup Player Scene", false, 0)]
    [MenuItem("GameObject/Swingscape/Setup Player Scene", false, 0)]
    private static void SetupPlayerScene()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Setup Player Scene",
                "Select the Player object first (the prefab asset in the Project window, or an instance already in the scene).", "OK");
            return;
        }

        PlayerManager playerManager = selected.GetComponentInParent<PlayerManager>();
        if (playerManager == null) playerManager = selected.GetComponentInChildren<PlayerManager>();
        if (playerManager == null)
        {
            EditorUtility.DisplayDialog("Setup Player Scene",
                "The selected object has no PlayerManager component on itself, its parents or its children.", "OK");
            return;
        }

        GameObject player = playerManager.gameObject;

        // Selected the prefab asset from the Project window rather than a scene instance -> drop one in.
        if (EditorUtility.IsPersistent(player))
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(player);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Player");
            player = instance;
            playerManager = player.GetComponent<PlayerManager>();
        }

        GlobalReference globalRef = EnsureGlobalReference();
        GameObject gameUI = EnsureGameUI();

        SetRef(globalRef, "player", playerManager);

        if (gameUI != null)
        {
            if (gameUI.GetComponent<GameUIManager>() == null)
            {
                Undo.AddComponent<GameUIManager>(gameUI);
            }

            Canvas canvas = gameUI.GetComponent<Canvas>();
            Transform autoAim = gameUI.transform.Find("AutoAim");
            Transform hpContainer = gameUI.transform.Find("HPContainer");
            Transform boostOverlay = gameUI.transform.Find("BoostOverlay");

            if (gameUI.transform.Find("OOBRespawnPrompt") == null)
            {
                Debug.LogWarning("Setup Player Scene: GameUI prefab has no 'OOBRespawnPrompt' child yet; GameUIManager.oobRespawnPrompt will stay unassigned.");
            }

            GrappleTargeting targeting = player.GetComponentInChildren<GrappleTargeting>(true);
            PlayerHpManager hpManager = player.GetComponentInChildren<PlayerHpManager>(true);

            SetRef(targeting, "targetCanvas", canvas);
            SetRef(targeting, "predictionPoint", autoAim != null ? autoAim.GetComponent<RectTransform>() : null);
            SetRef(hpManager, "HpContainer", hpContainer != null ? hpContainer.gameObject : null);
            SetRef(hpManager, "InvulnerabilityEfffect", boostOverlay != null ? boostOverlay.gameObject : null);
        }

        EditorSceneManager.MarkSceneDirty(player.scene);
        Selection.activeGameObject = player;
        Debug.Log("Setup Player Scene: wired GlobalReference, HUD canvas and overlay references for '" + player.name + "'.");
    }

    [MenuItem("GameObject/Swingscape/Setup Player Scene", true)]
    private static bool ValidateSetupPlayerScene()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return false;
        return selected.GetComponentInParent<PlayerManager>() != null || selected.GetComponentInChildren<PlayerManager>() != null;
    }

    private static GlobalReference EnsureGlobalReference()
    {
        GlobalReference existing = Object.FindFirstObjectByType<GlobalReference>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject utilsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UtilsPrefabPath);
        if (utilsPrefab == null)
        {
            Debug.LogWarning($"Setup Player Scene: could not find Utils prefab at {UtilsPrefabPath}.");
            return null;
        }

        GameObject utilsInstance = (GameObject)PrefabUtility.InstantiatePrefab(utilsPrefab);
        Undo.RegisterCreatedObjectUndo(utilsInstance, "Instantiate Utils");
        return utilsInstance.GetComponentInChildren<GlobalReference>(true);
    }

    private static GameObject EnsureGameUI()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.gameObject.name == "GameUI") return canvas.gameObject;
        }

        GameObject gameUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameUIPrefabPath);
        if (gameUIPrefab == null)
        {
            Debug.LogWarning($"Setup Player Scene: could not find GameUI prefab at {GameUIPrefabPath}.");
            return null;
        }

        GameObject gameUIInstance = (GameObject)PrefabUtility.InstantiatePrefab(gameUIPrefab);
        Undo.RegisterCreatedObjectUndo(gameUIInstance, "Instantiate GameUI");
        return gameUIInstance;
    }

    private static void SetRef(Object target, string propertyName, Object value)
    {
        if (target == null || value == null) return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"Setup Player Scene: property '{propertyName}' not found on {target.GetType().Name}.");
            return;
        }

        Undo.RecordObject(target, "Setup Player Scene");
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
    }
}
