using System.IO;
using System.Linq;
using Majungbul;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메뉴 Majungbul ▸ Build Prototype Scene
/// GDD 그림 2의 배치(전체 폭 16유닛, 한 층 보행선)를 임시 도형으로 만들고
/// 모든 참조를 연결한 Mukaebi 씬을 생성한다. 다시 실행하면 씬을 새로 만든다.
/// </summary>
public static class MajungbulSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Mukaebi.unity";
    const string TemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    const string ArtDir = "Assets/Prototype/Art";
    const string UnlitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const float FloorY = 1f;

    static Sprite _square, _circle;
    static Material _unlit;

    [MenuItem("Majungbul/Build Prototype Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Mukaebi 씬 다시 만들기",
                $"{ScenePath} 가 이미 있습니다. 지우고 새로 만들까요?", "새로 만들기", "취소"))
            return;

        PrepareSprites();
        _unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath);

        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

        Scene scene;
        if (File.Exists(TemplatePath) && AssetDatabase.CopyAsset(TemplatePath, ScenePath))
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        SetupCamera();

        // ── 배경 ──
        var env = new GameObject("Environment").transform;
        Shape("RoomWall", env, _square, new Vector2(5.9f, 5f), new Vector2(11.8f, 8f), C(0.40f, 0.37f, 0.33f), -20);
        Shape("RoomWainscot", env, _square, new Vector2(5.9f, 1.45f), new Vector2(11.8f, 0.9f), C(0.32f, 0.28f, 0.24f), -19);
        Shape("Shoji", env, _square, new Vector2(6.6f, 4.6f), new Vector2(2.4f, 2.2f), C(0.52f, 0.50f, 0.45f), -18);
        Shape("EntranceWall", env, _square, new Vector2(13.9f, 5f), new Vector2(4.2f, 8f), C(0.22f, 0.29f, 0.26f), -20);
        Shape("Pillar", env, _square, new Vector2(11.8f, 5f), new Vector2(0.2f, 8f), C(0.20f, 0.16f, 0.13f), -15);
        Shape("Beam", env, _square, new Vector2(8f, 8.65f), new Vector2(16f, 0.3f), C(0.20f, 0.16f, 0.13f), -15);
        Shape("Floor", env, _square, new Vector2(8f, 0.5f), new Vector2(16f, 1f), C(0.20f, 0.17f, 0.15f), -10);
        Shape("Doma", env, _square, new Vector2(13.9f, 0.5f), new Vector2(4.2f, 1f), C(0.30f, 0.30f, 0.28f), -9);

        // 불단 + 사진
        Shape("Altar", env, _square, new Vector2(1.5f, FloorY + 0.5f), new Vector2(0.95f, 1.0f), C(0.45f, 0.33f, 0.24f), 0);
        var photoGo = Empty("Photo", null, new Vector2(1.5f, FloorY + 1.0f));
        photoGo.transform.localRotation = Quaternion.Euler(0, 0, 18f);
        Shape("Frame", photoGo.transform, _square, new Vector2(0, 0.33f), new Vector2(0.5f, 0.66f), C(0.18f, 0.16f, 0.14f), 5);
        Shape("Paper", photoGo.transform, _square, new Vector2(0, 0.35f), new Vector2(0.38f, 0.5f), C(0.76f, 0.73f, 0.66f), 6);
        Shape("Portrait", photoGo.transform, _circle, new Vector2(0, 0.42f), new Vector2(0.18f, 0.18f), C(0.35f, 0.33f, 0.31f), 7);
        Shape("Shoulders", photoGo.transform, _square, new Vector2(0, 0.24f), new Vector2(0.26f, 0.12f), C(0.35f, 0.33f, 0.31f), 7);
        var photoAnchor = Empty("PromptAnchor", photoGo.transform, new Vector2(0, 1.2f));
        var photo = photoGo.AddComponent<InteractableProp>();
        SetEnum(photo, "kind", (int)PropKind.Photo);
        Set(photo, "promptAnchor", photoAnchor.transform);

        // 탁자 + 컵
        Shape("TableTop", env, _square, new Vector2(3.5f, FloorY + 0.68f), new Vector2(1.1f, 0.08f), C(0.42f, 0.30f, 0.21f), 2);
        Shape("TableLegL", env, _square, new Vector2(3.0f, FloorY + 0.34f), new Vector2(0.07f, 0.68f), C(0.36f, 0.26f, 0.18f), 1);
        Shape("TableLegR", env, _square, new Vector2(4.0f, FloorY + 0.34f), new Vector2(0.07f, 0.68f), C(0.36f, 0.26f, 0.18f), 1);
        var cupGo = Empty("Cup", null, new Vector2(3.5f, FloorY + 0.72f));
        Shape("Body", cupGo.transform, _square, new Vector2(0, 0.11f), new Vector2(0.2f, 0.22f), C(0.92f, 0.90f, 0.84f), 8);
        Shape("Handle", cupGo.transform, _square, new Vector2(0.13f, 0.12f), new Vector2(0.06f, 0.1f), C(0.92f, 0.90f, 0.84f), 8);
        var cupAnchor = Empty("PromptAnchor", cupGo.transform, new Vector2(0, 0.75f));
        var cup = cupGo.AddComponent<InteractableProp>();
        SetEnum(cup, "kind", (int)PropKind.Cup);
        Set(cup, "promptAnchor", cupAnchor.transform);

        // 풍경
        var chimeGo = Empty("WindChime", null, new Vector2(12.6f, FloorY + 3.1f));
        Shape("Hook", chimeGo.transform, _square, new Vector2(0, 0.05f), new Vector2(0.12f, 0.06f), C(0.15f, 0.13f, 0.12f), 6);
        var pivot = Empty("Pivot", chimeGo.transform, Vector2.zero);
        Shape("String", pivot.transform, _square, new Vector2(0, -0.3f), new Vector2(0.02f, 0.6f), C(0.85f, 0.83f, 0.78f), 6);
        Shape("Bell", pivot.transform, _circle, new Vector2(0, -0.68f), new Vector2(0.36f, 0.32f), C(0.55f, 0.76f, 0.80f), 7);
        Shape("Tanzaku", pivot.transform, _square, new Vector2(0, -1.12f), new Vector2(0.12f, 0.5f), C(0.92f, 0.88f, 0.78f), 7);
        var chimeAnchor = Empty("PromptAnchor", chimeGo.transform, new Vector2(0, 0.45f));
        var chime = chimeGo.AddComponent<InteractableProp>();
        SetEnum(chime, "kind", (int)PropKind.WindChime);
        Set(chime, "promptAnchor", chimeAnchor.transform);
        Set(chime, "swingPivot", pivot.transform);

        // 현관 등
        Shape("LampCord", env, _square, new Vector2(13.5f, 8.0f), new Vector2(0.03f, 1.0f), C(0.12f, 0.11f, 0.1f), -8);
        Shape("LampShade", env, _circle, new Vector2(13.5f, 7.45f), new Vector2(0.6f, 0.3f), C(0.80f, 0.72f, 0.55f), -7);

        // 불
        var fireGo = Empty("Fire", null, new Vector2(14.0f, FloorY));
        var ember = Shape("Plate", fireGo.transform, _circle, new Vector2(0, 0.06f), new Vector2(0.8f, 0.16f), C(0.55f, 0.32f, 0.2f), 9);
        var flame = Shape("Flame", fireGo.transform, _circle, new Vector2(0, 0.38f), new Vector2(0.36f, 0.62f), C(0.95f, 0.52f, 0.18f), 10);
        var inner = Shape("InnerFlame", fireGo.transform, _circle, new Vector2(0, 0.3f), new Vector2(0.18f, 0.34f), C(1f, 0.86f, 0.45f), 11);
        var fire = fireGo.AddComponent<FireView>();
        Set(fire, "flame", flame);
        Set(fire, "innerFlame", inner);
        Set(fire, "ember", ember);

        // 문 + 귀환 구역
        Shape("Door", env, _square, new Vector2(14.8f, FloorY + 1.3f), new Vector2(0.7f, 2.6f), C(0.55f, 0.60f, 0.55f), -5);
        Shape("DoorFrame", env, _square, new Vector2(14.8f, FloorY + 1.3f), new Vector2(0.84f, 2.74f), C(0.17f, 0.15f, 0.13f), -6);
        var exitGo = Empty("ExitZone", null, new Vector2(14.8f, FloorY + 0.9f));
        var exitCol = exitGo.AddComponent<BoxCollider2D>();
        exitCol.isTrigger = true;
        exitCol.size = new Vector2(0.8f, 1.8f);
        var exit = exitGo.AddComponent<ExitZone>();

        // ── 인물 ──
        var playerGo = Character("Player", 13.2f, C(0.78f, 0.82f, 0.86f), C(0.93f, 0.94f, 0.95f), 30, out var playerVisual);
        var rb = playerGo.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
        var pcol = playerGo.AddComponent<BoxCollider2D>();
        pcol.size = new Vector2(0.5f, 1.6f);
        pcol.offset = new Vector2(0, 0.8f);
        var player = playerGo.AddComponent<PlayerController>();

        var familyGo = Character("Family", 5.0f, C(0.36f, 0.29f, 0.25f), C(0.86f, 0.80f, 0.72f), 20, out var familyVisual);
        var family = familyGo.AddComponent<ActorReaction>();

        var watcherGo = Character("Watcher", 9.0f, C(0.36f, 0.29f, 0.25f), C(0.86f, 0.80f, 0.72f), 20, out var watcherVisual);
        var hand = Shape("Hand", watcherVisual, _square, new Vector2(0.5f, 0.92f), new Vector2(0.44f, 0.1f), C(0.86f, 0.80f, 0.72f), 25).gameObject;
        hand.SetActive(false);
        var bubble = Empty("Bubble", watcherGo.transform, new Vector2(0, 2.05f));
        for (int i = -1; i <= 1; i++)
            Shape("Dot", bubble.transform, _circle, new Vector2(i * 0.17f, 0), new Vector2(0.09f, 0.09f), C(0.95f, 0.93f, 0.88f), 26);
        bubble.SetActive(false);
        var watcher = watcherGo.AddComponent<ActorReaction>();
        var watcherAnchor = Empty("PromptAnchor", watcherGo.transform, new Vector2(0, 2.4f));
        var watcherHand = watcherGo.AddComponent<InteractableProp>();
        SetEnum(watcherHand, "kind", (int)PropKind.WatcherHand);
        Set(watcherHand, "promptAnchor", watcherAnchor.transform);

        // ── 디버그 표식 (F1) ──
        var debug = new GameObject("DebugMarkers");
        Shape("ZoneL_7.8", debug.transform, _square, new Vector2(7.8f, FloorY + 1.5f), new Vector2(0.8f, 3f), new Color(1f, 0.3f, 0.3f, 0.25f), 50);
        Shape("ZoneR_10.2", debug.transform, _square, new Vector2(10.2f, FloorY + 1.5f), new Vector2(0.8f, 3f), new Color(1f, 0.3f, 0.3f, 0.25f), 50);
        Shape("Exit_14.8", debug.transform, _square, new Vector2(14.8f, FloorY + 0.9f), new Vector2(0.8f, 1.8f), new Color(0.3f, 1f, 0.3f, 0.25f), 50);

        // ── 관리자 ──
        var flowGo = new GameObject("GameFlow");
        var audio = flowGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        var flow = flowGo.AddComponent<GameFlow>();
        var uiGo = new GameObject("UI");
        var ui = uiGo.AddComponent<UIController>();

        // ── 참조 연결 ──
        Set(flow, "player", player);
        Set(flow, "family", family);
        Set(flow, "watcher", watcher);
        Set(flow, "cup", cup);
        Set(flow, "photo", photo);
        Set(flow, "windChime", chime);
        Set(flow, "watcherHand", watcherHand);
        Set(flow, "fire", fire);
        Set(flow, "exitZone", exit);
        Set(flow, "ui", ui);
        Set(flow, "sfx", audio);
        Set(flow, "debugMarkers", debug);

        Set(player, "flow", flow);
        Set(player, "visual", playerVisual);
        SetArray(player, "props", new Object[] { cup, photo, chime, watcherHand });

        Set(family, "visual", familyVisual);
        Set(family, "player", playerGo.transform);
        Set(watcher, "visual", watcherVisual);
        Set(watcher, "hand", hand);
        Set(watcher, "bubble", bubble);
        Set(watcher, "player", playerGo.transform);

        Set(exit, "flow", flow);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        Selection.activeGameObject = flowGo;
        Debug.Log("[Majungbul] Mukaebi 씬 생성 완료 → Play 를 눌러 확인하세요. (F1: 관찰 구역 디버그 표시)");
    }

    // ───────────────────────── 헬퍼 ─────────────────────────

    static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;               // 16 x 9 유닛
        cam.transform.position = new Vector3(8f, 4.5f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
    }

    static void AddToBuildSettings()
    {
        var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
            .Select(s => new EditorBuildSettingsScene(s.path, false));
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
    }

    static void PrepareSprites()
    {
        Directory.CreateDirectory(ArtDir);
        string sq = ArtDir + "/square.png";
        string ci = ArtDir + "/circle.png";
        if (!File.Exists(sq)) WritePng(sq, 32, false);
        if (!File.Exists(ci)) WritePng(ci, 64, true);
        AssetDatabase.Refresh();
        ConfigureSprite(sq, 32);
        ConfigureSprite(ci, 64);
        _square = AssetDatabase.LoadAssetAtPath<Sprite>(sq);
        _circle = AssetDatabase.LoadAssetAtPath<Sprite>(ci);
    }

    static void WritePng(string path, int size, bool circle)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float a = 1f;
            if (circle)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                a = Mathf.Clamp01(r - d);
            }
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void ConfigureSprite(string path, int ppu)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;                 // 텍스처 1장 = 1유닛
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
    }

    static GameObject Character(string name, float x, Color body, Color head, int order, out Transform visual)
    {
        var root = Empty(name, null, new Vector2(x, FloorY));
        visual = Empty("Visual", root.transform, Vector2.zero).transform;   // +x 방향을 바라보도록 그림
        Shape("Body", visual, _square, new Vector2(0, 0.58f), new Vector2(0.62f, 1.12f), body, order);
        Shape("Head", visual, _circle, new Vector2(0, 1.42f), new Vector2(0.56f, 0.56f), head, order + 1);
        Shape("Nose", visual, _square, new Vector2(0.31f, 1.40f), new Vector2(0.13f, 0.07f), new Color(head.r * 0.82f, head.g * 0.82f, head.b * 0.82f, 1f), order + 1);
        Shape("Eye", visual, _circle, new Vector2(0.13f, 1.47f), new Vector2(0.08f, 0.08f), C(0.1f, 0.09f, 0.09f), order + 2);
        Shape("LegL", visual, _square, new Vector2(-0.12f, 0.06f), new Vector2(0.08f, 0.14f), C(0.12f, 0.11f, 0.1f), order - 1);
        Shape("LegR", visual, _square, new Vector2(0.12f, 0.06f), new Vector2(0.08f, 0.14f), C(0.12f, 0.11f, 0.1f), order - 1);
        return root;
    }

    static GameObject Empty(string name, Transform parent, Vector2 localPos)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0);
        return go;
    }

    static SpriteRenderer Shape(string name, Transform parent, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order)
    {
        var go = Empty(name, parent, localPos);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (_unlit != null) sr.sharedMaterial = _unlit;
        return sr;
    }

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Majungbul] {target.GetType().Name}.{field} 필드를 찾지 못함"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetEnum(Object target, string field, int index)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Majungbul] {target.GetType().Name}.{field} 필드를 찾지 못함"); return; }
        p.enumValueIndex = index;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Majungbul] {target.GetType().Name}.{field} 필드를 찾지 못함"); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
