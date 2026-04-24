/*
 * AirDefenseSceneBuilder.cs  (Editor only)
 * 메뉴: AirDefense > Build Scene
 * 실행하면 Assets/Scenes/AirDefense.unity 씬을 새로 만들고
 * 필요한 GameObject 를 모두 배치한다.
 */

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class AirDefenseSceneBuilder
{
    [MenuItem("AirDefense/Build Scene")]
    static void BuildScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureTag("Target");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Directional Light ──────────────────────────
        GameObject lightGO = new GameObject("Directional Light");
        Light sun = lightGO.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.color     = new Color(1.0f, 0.95f, 0.85f);
        sun.intensity = 1.4f;
        lightGO.transform.eulerAngles = new Vector3(45f, -60f, 0f);
        sun.shadows       = LightShadows.Soft;
        sun.shadowStrength = 0.7f;

        // ── 2. Main Camera (MissileCamera) ────────────────
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.38f, 0.60f, 0.85f);
        cam.farClipPlane    = 500f;
        cam.depth           = 0;
        camGO.AddComponent<MissileCamera>();
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(-8f, 8f, -12f);
        camGO.transform.eulerAngles = new Vector3(19f, 34f, 0f);

        // ── 3. GameManager (SceneSetup + StartupFade) ─────
        GameObject gmGO = new GameObject("GameManager");
        gmGO.AddComponent<SceneSetup>();
        gmGO.AddComponent<StartupFade>();

        // ── 4. LiDAR System ───────────────────────────────
        GameObject lidarGO = new GameObject("LiDARSystem");
        LiDARTargetManager tm = lidarGO.AddComponent<LiDARTargetManager>();
        LiDARReceiver lr = lidarGO.AddComponent<LiDARReceiver>();
        lr.targetManager = tm;

        // ── 5. THAAD Launcher ─────────────────────────────
        GameObject launcherGO = new GameObject("THAADLauncher");
        THAADLauncher thaad = launcherGO.AddComponent<THAADLauncher>();
        thaad.missilePrefab = EnsureMissileController("Assets/Missile.prefab");

        // ── 6. Radar Display ──────────────────────────────
        GameObject radarGO = new GameObject("RadarDisplay");
        RadarDisplay rd = radarGO.AddComponent<RadarDisplay>();
        // launcher 는 Awake 에서 자동 탐색하므로 비워도 됨

        // ── 7. Overview Camera ────────────────────────────
        GameObject overviewGO = new GameObject("OverviewCamera");
        overviewGO.AddComponent<OverviewCamera>();

        // ── 8. Target Spawner (비활성, LiDAR 모드) ────────
        GameObject spawnerGO = new GameObject("TargetSpawner");
        spawnerGO.AddComponent<TargetSpawner>();

        // ── 씬 저장 ───────────────────────────────────────
        string savePath = "Assets/Scenes/AirDefense.unity";
        EditorSceneManager.SaveScene(scene, savePath);

        Debug.Log($"[AirDefense] 씬 생성 완료: {savePath}");
        EditorUtility.DisplayDialog(
            "씬 생성 완료",
            $"씬이 생성되었습니다!\n경로: {savePath}\n\nPlay 버튼을 눌러 실행하세요.",
            "확인");
    }

    // Missile.prefab에 MissileController가 없으면 추가하고 저장
    static GameObject EnsureMissileController(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[AirDefense] {prefabPath} 를 찾을 수 없습니다.");
            return null;
        }

        if (prefab.GetComponent<MissileController>() == null)
        {
            // 프리팹 편집 모드로 열어서 컴포넌트 추가
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = scope.prefabContentsRoot;
                if (root.GetComponent<MissileController>() == null)
                    root.AddComponent<MissileController>();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Debug.Log("[AirDefense] Missile.prefab 에 MissileController 추가 완료");
        }

        return prefab;
    }

    [MenuItem("AirDefense/Fix Missile Prefab")]
    static void FixMissilePrefab()
    {
        GameObject result = EnsureMissileController("Assets/Missile.prefab");
        if (result != null)
            EditorUtility.DisplayDialog("완료", "Missile.prefab에 MissileController가 추가되었습니다.", "확인");
    }

    static void EnsureTag(string tag)
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");

        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
        Debug.Log($"[AirDefense] 태그 추가: {tag}");
    }
}
