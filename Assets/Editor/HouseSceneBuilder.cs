using System.Collections.Generic;
using System.IO;
using System.Linq;
using Majungbul.Core;
using Majungbul.House;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 메뉴 Majungbul ▸ Build House Prototype
/// 문자 지도(ADR 0006)로 집·마당·창고 회색 박스를 만들고 참조를 연결해 Assets/Scenes/House.unity로 저장한다.
/// 다시 실행하면 씬을 새로 만든다. 조사 문장은 아래 Props 표에서 고친다(단계: 첫날 / 둘째 날 / 사흘째 / 반전 후).
/// </summary>
public static class HouseSceneBuilder
{
    const string ScenePath = "Assets/Scenes/House.unity";
    const string TemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    const string ArtDir = "Assets/Prototype/Art";
    const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

    // 첫 줄이 맨 위. # 벽 / . 집 바닥 / , 마당 / D 현관문 / E 뒷문 / w 창문 / S 창고 건물
    // 표식: P 첫 등장 Q 사흘째 시작 F 아이 자리 G 불 피우는 자리 V 그것이 서는 자리 Y 내쳐지는 자리 X 대문(출구)
    const string Map =
        "################################################\n" +
        "#,,SSSSSS,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "#,,SSSSSS,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "#,,SSSSSS,,,,,,,,,,,Y,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "#,,,,,t,,,,,,,,,a,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "#,,,,,####w###############E#########w#####,,,,,#\n" +
        "#,,,,,#........#.....#.........#.........#,,,,,#\n" +
        "#,,,,,#.q....j.#...m.#.s....z..#.......d.#,,,,,#\n" +
        "#,,,,,#........#.....#.........#.........#,,,,,#\n" +
        "#,,,,,#........#.....#.........#.........w,,,,,#\n" +
        "#,,,,,#........#.....#.........#.........#,,,,,#\n" +
        "#,,,,,#....b...#.u...#........y#.i...e...#,,,,,#\n" +
        "#,,,,,#........#.....#.........#.........#,,,,,#\n" +
        "#,,,,,####.#######.#######.#########.#####,,,,,#\n" +
        "#,,,,,#.......................p..........#,,,,,#\n" +
        "#,,,,,#.............Q....................#,,,,,#\n" +
        "#,,,,,######.###########.#########.####.##,,,,,#\n" +
        "#,,,,,#..........#...........c.#.....#...#,,,,,#\n" +
        "#,,,,,#..A.n..x..#......v......#.....#...#,,,,,#\n" +
        "#,,,,,#..........#.............#.....#.g.#,,,,,#\n" +
        "#,,,,,w................TCF...........#...w,,,,,#\n" +
        "#,,,,,#..........#..........k..#.....#...#,,,,,#\n" +
        "#,,,,,#.......o..#.r.........f.#..P..#...#,,,,,#\n" +
        "#,,,,,#..........#.............#.h...#...#,,,,,#\n" +
        "#,,,,,##################w#########D#######,,,,,#\n" +
        "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,V,,,,,,,,,,,#\n" +
        "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,GO,L,,,,,,,,,,#\n" +
        "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
        "##################################X#############\n";

    const string Blocking = "TCAnocvkrhgpqjbmuszyideStaOLwxf";

    struct PropDef
    {
        public string Id; public HousePropKind Kind; public HouseNoise Noise; public Color Color;
        public string D1, D2, D3, After;
        public PropDef(string id, HousePropKind kind, HouseNoise noise, Color color, string d1, string d2 = "", string d3 = "", string after = "")
        { Id = id; Kind = kind; Noise = noise; Color = color; D1 = d1; D2 = d2; D3 = d3; After = after; }
    }

    static PropDef P(string id, HousePropKind k, HouseNoise n, Color c, string d1, string d2 = "", string d3 = "", string after = "")
        => new PropDef(id, k, n, c, d1, d2, d3, after);

    const HousePropKind Ex = HousePropKind.Examine, No = HousePropKind.Noise;
    const HouseNoise Nn = HouseNoise.None;

    static readonly Dictionary<char, PropDef> Props = new Dictionary<char, PropDef>
    {
        // 거실
        { 'T', P("table", Ex, Nn, C(0.36f, 0.26f, 0.16f), "낮은 밥상. 찻잔이 하나 놓여 있다.", "찻잔이 두 개 놓여 있다.", "", "두 번째 찻잔은 손대지 않은 채 식어 있다.") },
        { 'C', P("cup", HousePropKind.Cup, Nn, C(0.36f, 0.26f, 0.16f), "아이의 찻잔.") },
        { 'v', P("tv", No, HouseNoise.Static, C(0.10f, 0.10f, 0.11f), "꺼진 텔레비전. 화면에 아무도 비치지 않는다.", "", "꺼진 텔레비전. 화면에 누군가 비친다.", "화면 속 나에게는 얼굴이 없다.") },
        { 'c', P("clock", No, HouseNoise.Chime, C(0.45f, 0.35f, 0.22f), "멈춘 벽시계. 건드리니 종이 한 번 울린다.") },
        { 'r', P("radio", No, HouseNoise.Static, C(0.38f, 0.33f, 0.28f), "낡은 라디오. 잡음이 인다.", "", "", "잡음 사이로 누군가 이름을 부른다. 내 이름이 아니다.") },
        { 'f', P("fan", No, HouseNoise.Clatter, C(0.50f, 0.55f, 0.58f), "낡은 선풍기. 날개가 덜덜 돈다.") },
        { 'k', P("calendar", Ex, Nn, C(0.80f, 0.78f, 0.70f), "8월 달력. 13일과 16일에 동그라미.", "", "16일. 오늘이다.") },
        // 불단 방
        { 'A', P("altar", HousePropKind.Photo, Nn, C(0.40f, 0.27f, 0.17f), "불단. 사진이 엎어져 있다. 아직 세울 때가 아니다.", "", "불단. 엎어진 사진.") },
        { 'n', P("incense", Ex, Nn, C(0.35f, 0.30f, 0.25f), "향이 하나 꽂혀 있다.", "", "새 향이 꽂혀 있다.", "향은 처음부터 하나였다.") },
        { 'x', P("bell", No, HouseNoise.Chime, C(0.62f, 0.50f, 0.22f), "불단의 놋쇠 종. 맑게 한 번 울린다.") },
        { 'o', P("cushion", Ex, Nn, C(0.45f, 0.25f, 0.22f), "방석이 하나 놓여 있다.", "방석 옆에 하나가 더 놓여 있다.") },
        // 현관·다용도실·복도
        { 'h', P("shoes", Ex, Nn, C(0.33f, 0.25f, 0.18f), "신발이 한 켤레뿐이다.", "", "현관 밖에 맨발 자국이 있다.") },
        { 'g', P("washer", No, HouseNoise.Clatter, C(0.62f, 0.64f, 0.64f), "낡은 세탁기. 흔드니 덜컹거린다.") },
        { 'p', P("phone", No, HouseNoise.Ring, C(0.08f, 0.08f, 0.09f), "검은 전화기. 수화기를 들썩이니 벨이 울린다.") },
        // 안방
        { 'q', P("closet", No, HouseNoise.Clatter, C(0.30f, 0.22f, 0.16f), "내 옷이 걸려 있다.", "", "", "내 옷이라고 생각했던 옷. 치수가 맞지 않는다.") },
        { 'j', P("family_photo", Ex, Nn, C(0.45f, 0.40f, 0.32f), "오래된 가족사진. 빛이 바래 얼굴이 잘 보이지 않는다.", "", "", "사진 속 어른은 나를 쫓던 그 사람이다.") },
        { 'b', P("futon", Ex, Nn, C(0.55f, 0.55f, 0.60f), "이불이 한 채 깔려 있다.", "이불 옆에 한 채가 더 깔려 있다.") },
        // 욕실
        { 'm', P("mirror", Ex, Nn, C(0.50f, 0.58f, 0.62f), "거울에 내가 비치지 않는다.", "", "", "비칠 얼굴이 처음부터 없었다.") },
        { 'u', P("tub", No, HouseNoise.Water, C(0.55f, 0.62f, 0.66f), "물이 반쯤 찬 욕조. 수면이 흔들린다.") },
        // 부엌
        { 's', P("sink", No, HouseNoise.Water, C(0.55f, 0.57f, 0.58f), "그릇이 하나뿐이다.", "그릇이 둘이다.") },
        { 'z', P("kettle", No, HouseNoise.Clatter, C(0.40f, 0.40f, 0.42f), "가스레인지 위 주전자. 뚜껑이 달그락거린다.") },
        { 'y', P("cupboard", No, HouseNoise.Clatter, C(0.42f, 0.32f, 0.22f), "찻잔 두 개가 놓였던 자국. 하나는 오래 쓰지 않았다.") },
        // 자녀 방
        { 'd', P("desk", Ex, Nn, C(0.40f, 0.30f, 0.20f), "펼친 수첩. '8월 13일, 마중불.'", "수첩. '어젯밤, 찻잔이 움직였다.'", "수첩. '오늘 밤 보내 드린다.'") },
        { 'i', P("child_futon", Ex, Nn, C(0.60f, 0.55f, 0.50f), "아이의 이불.") },
        { 'e', P("bookshelf", No, HouseNoise.Clatter, C(0.35f, 0.25f, 0.18f), "책장. 그림책 한 권이 떨어진다.") },
        // 마당·창고
        { 'a', P("bucket", No, HouseNoise.Clatter, C(0.45f, 0.45f, 0.48f), "빈 양동이. 걷어차니 요란하게 구른다.") },
        { 't', P("trunk", HousePropKind.Trunk, HouseNoise.Clatter, C(0.36f, 0.27f, 0.18f), "창고 앞 낡은 상자. 이 집에 원래 있던 물건 같다.") },
        { 'O', P("fire", HousePropKind.Fire, Nn, C(0.30f, 0.27f, 0.25f), "") },
        { 'L', P("lantern", HousePropKind.Lantern, Nn, C(0.35f, 0.30f, 0.22f), "꺼진 낡은 등롱.") },
        // 문
        { 'D', P("door_front", HousePropKind.Door, HouseNoise.Clatter, C(0.20f, 0.16f, 0.13f), "현관문. 망자는 스스로 문을 열 수 없다.") },
        { 'E', P("door_back", HousePropKind.Door, HouseNoise.Clatter, C(0.20f, 0.16f, 0.13f), "뒷문. 망자는 스스로 문을 열 수 없다.") },
    };

    // 창문: 칸 위치로 이름을 붙인다(둘째 날 노크 순서에 쓰는 이름 포함).
    static readonly Dictionary<Vector2Int, string> WindowIds = new Dictionary<Vector2Int, string>
    {
        { new Vector2Int(10, 6), "window_master" }, { new Vector2Int(36, 6), "window_child_top" },
        { new Vector2Int(6, 21), "window_altar" }, { new Vector2Int(41, 10), "window_child" },
        { new Vector2Int(41, 21), "window_utility" }, { new Vector2Int(24, 25), "window_living" },
    };

    static Sprite _square, _circle;
    static Material _lit;

    [MenuItem("Majungbul/Build House Prototype")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("House 씬 다시 만들기", $"{ScenePath} 가 이미 있습니다. 지우고 새로 만들까요?", "새로 만들기", "취소"))
            return;

        PrepareSprites();
        _lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);

        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Scene scene;
        if (File.Exists(TemplatePath) && AssetDatabase.CopyAsset(TemplatePath, ScenePath))
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var map = new GridMap(Map, Blocking);

        // ── 바닥·벽·창고 ──
        var tiles = new GameObject("Tiles").transform;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new Cell(x, y);
            int row = map.Height - 1 - y;
            char ch = map.TileAt(c);
            var center = new Vector2(x + 0.5f, y + 0.5f);
            if (ch == '#') Shape("Wall", tiles, _square, center, Vector2.one, (x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1) ? C(0.06f, 0.07f, 0.06f) : C(0.10f, 0.09f, 0.08f), 1);
            else if (ch == 'S') Shape("Shed", tiles, _square, center, Vector2.one, C(0.17f, 0.13f, 0.10f), 1);
            else Shape("Floor", tiles, _square, center, Vector2.one * 0.98f, FloorColor(x, row, ch == ',' || IsOutsideMarker(map, c)), 0);
        }

        // ── 가구·물건 ──
        var propsRoot = new GameObject("Props").transform;
        var props = new List<HouseProp>();
        foreach (var kv in Props)
            foreach (var cell in map.Markers(kv.Key))
                props.Add(MakeProp(kv.Value, kv.Value.Id, cell, propsRoot));
        foreach (var cell in map.Markers('w'))
        {
            WindowIds.TryGetValue(new Vector2Int(cell.X, map.Height - 1 - cell.Y), out var wid);
            var def = P(wid ?? "window", HousePropKind.Window, HouseNoise.Knock, C(0.22f, 0.27f, 0.36f),
                "창밖은 어둡다.", "창밖에 누군가 서 있다. 나를 보는 게 아니다.", "창밖으로 송별불이 보인다.");
            props.Add(MakeProp(def, def.Id, cell, propsRoot));
        }

        // ── 인물 ──
        var playerGo = Character("Player", map, 'P', C(0.78f, 0.86f, 1f), C(0.07f, 0.06f, 0.06f), out var playerFace);
        var player = playerGo.AddComponent<HousePlayer>();
        AddPointLight(playerGo, C(0.75f, 0.85f, 1f), 3.6f, 0.9f);                 // 망자의 희미한 빛
        var childGo = Character("Child", map, 'F', C(0.62f, 0.45f, 0.35f), C(0.07f, 0.06f, 0.06f), out var childFace);
        var child = childGo.AddComponent<HouseChild>();
        AddPointLight(childGo, C(1f, 0.8f, 0.55f), 2.4f, 0.55f);                  // 산 사람의 온기
        var watcherGo = Character("Watcher", map, 'V', C(0.07f, 0.06f, 0.07f), C(0.85f, 0.82f, 0.78f), out var watcherFace);
        Shape("Mark", watcherGo.transform, _square, new Vector2(0, -0.08f), new Vector2(0.56f, 0.12f), C(0.55f, 0.12f, 0.12f), 22);   // 사진과 같은 붉은 띠
        var watcher = watcherGo.AddComponent<HouseNpc>();

        // ── 카메라·조명 ──
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            cam = camGo.GetComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var houseCam = cam.GetComponent<HouseCamera>();
        if (houseCam == null) houseCam = cam.gameObject.AddComponent<HouseCamera>();   // Unity 객체는 ?? 대신 == null로 검사
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (l.lightType != Light2D.LightType.Global) continue;
            l.intensity = 0.18f;                                                     // 그림자복도식 어둠
            l.color = C(0.55f, 0.62f, 0.85f);
        }

        // ── 관리자 ──
        var flowGo = new GameObject("HouseFlow");
        var sfx = flowGo.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var amb = flowGo.AddComponent<AudioSource>();
        amb.playOnAwake = false;
        amb.volume = 0.6f;
        var flow = flowGo.AddComponent<HouseFlow>();
        var ui = new GameObject("HouseUI").AddComponent<HouseUI>();

        // ── 참조 연결 ──
        SetString(flow, "mapText", Map);
        SetString(flow, "blockingChars", Blocking);
        Set(flow, "player", player);
        Set(flow, "child", child);
        Set(flow, "watcher", watcher);
        Set(flow, "ui", ui);
        Set(flow, "cam", houseCam);
        SetArray(flow, "props", props.Cast<Object>().ToArray());
        Set(flow, "sfx", sfx);
        Set(flow, "ambience", amb);

        Set(player, "flow", flow);
        Set(player, "faceMarker", playerFace);
        Set(child, "faceMarker", childFace);
        Set(child, "player", player);
        Set(watcher, "faceMarker", watcherFace);
        Set(watcher, "player", player);
        Set(houseCam, "target", playerGo.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        Selection.activeGameObject = flowGo;
        Debug.Log("[Majungbul] House 씬 생성 완료 → Play. 방향키 이동, Shift 달리기, E 조사, R 처음부터. 오프닝은 E로 건너뛸 수 있습니다.");
    }

    // ───────────────────────── 소품 만들기 ─────────────────────────

    static HouseProp MakeProp(PropDef def, string id, Cell cell, Transform root)
    {
        var go = Empty(id, root, new Vector2(cell.X + 0.5f, cell.Y + 0.5f));
        var baseSr = Shape("Base", go.transform, _square, Vector2.zero, Vector2.one * (def.Kind == HousePropKind.Window ? 1f : 0.88f), def.Color, 5);
        Transform visual = null;
        Light2D light = null;

        switch (def.Kind)
        {
            case HousePropKind.Cup:
                visual = Empty("CupVisual", go.transform, Vector2.zero).transform;
                Shape("Cup", visual, _circle, Vector2.zero, Vector2.one * 0.3f, C(0.92f, 0.90f, 0.84f), 7);
                break;
            case HousePropKind.Photo:
                visual = Empty("PhotoVisual", go.transform, new Vector2(0, 0.05f)).transform;
                visual.localRotation = Quaternion.Euler(0, 0, 90f);                  // 엎어진 사진
                Shape("Frame", visual, _square, Vector2.zero, new Vector2(0.36f, 0.46f), C(0.15f, 0.13f, 0.12f), 7);
                Shape("Paper", visual, _square, Vector2.zero, new Vector2(0.26f, 0.34f), C(0.78f, 0.75f, 0.68f), 8);
                break;
            case HousePropKind.Fire:
                visual = Empty("Flame", go.transform, Vector2.zero).transform;
                Shape("Outer", visual, _circle, Vector2.zero, Vector2.one * 0.6f, C(0.95f, 0.52f, 0.18f), 8);
                Shape("Inner", visual, _circle, Vector2.zero, Vector2.one * 0.3f, C(1f, 0.86f, 0.45f), 9);
                light = AddPointLight(go, C(1f, 0.55f, 0.25f), 6f, 1.5f);
                break;
            case HousePropKind.Lantern:
                visual = Empty("LanternFlame", go.transform, Vector2.zero).transform;
                Shape("Glow", visual, _circle, Vector2.zero, Vector2.one * 0.35f, C(1f, 0.85f, 0.5f), 8);
                light = AddPointLight(go, C(1f, 0.82f, 0.5f), 4.5f, 1.2f);
                break;
            case HousePropKind.Door:
                visual = Shape("Slab", go.transform, _square, Vector2.zero, new Vector2(1f, 0.5f), C(0.38f, 0.27f, 0.18f), 6).transform;
                break;
        }
        if (def.Id == "table")                                                       // 아이가 내놓는 두 번째 찻잔
        {
            visual = Empty("Cup2", go.transform, Vector2.zero).transform;
            Shape("Cup", visual, _circle, new Vector2(0.15f, 0.1f), Vector2.one * 0.28f, C(0.92f, 0.90f, 0.84f), 7);
        }

        var prop = go.AddComponent<HouseProp>();
        SetString(prop, "id", id);
        SetEnum(prop, "kind", (int)def.Kind);
        SetEnum(prop, "noise", (int)def.Noise);
        SetString(prop, "day1Text", def.D1);
        SetString(prop, "day2Text", def.D2);
        SetString(prop, "day3Text", def.D3);
        SetString(prop, "afterText", def.After);
        if (visual != null) Set(prop, "visual", visual);
        if (def.Kind == HousePropKind.Window) Set(prop, "flash", baseSr);
        if (light != null) Set(prop, "lightSource", light);
        return prop;
    }

    // ───────────────────────── 헬퍼 ─────────────────────────

    static bool IsOutsideMarker(GridMap map, Cell c)
    {
        // 표식 칸(G, V, X 등)은 주변이 마당이면 마당 색으로 칠한다
        for (int i = 0; i < 4; i++) if (map.TileAt(c + Dir4Util.Delta((Dir4)i)) == ',') return "GVXYLOta".IndexOf(map.TileAt(c)) >= 0;
        return false;
    }

    static Color FloorColor(int x, int row, bool outside)
    {
        if (outside) return C(0.11f, 0.14f, 0.10f);                                  // 마당 풀밭
        if (row == 15 || row == 16) return C(0.27f, 0.20f, 0.14f);                    // 복도: 마루
        if (row >= 18 && x >= 32 && x <= 36) return C(0.22f, 0.22f, 0.21f);          // 현관: 흙바닥
        if (row <= 13 && x >= 16 && x <= 20) return C(0.30f, 0.33f, 0.34f);          // 욕실: 타일
        if (row >= 18 && x >= 38) return C(0.30f, 0.33f, 0.34f);                     // 다용도실: 타일
        if (row >= 18 && x >= 18 && x <= 30) return C(0.30f, 0.23f, 0.16f);          // 거실: 마루
        if (row <= 13 && x >= 22 && x <= 30) return C(0.30f, 0.23f, 0.16f);          // 부엌: 마루
        return C(0.26f, 0.27f, 0.18f);                                              // 다다미
    }

    static GameObject Character(string name, GridMap map, char marker, Color body, Color faceColor, out Transform face)
    {
        map.TryMarker(marker, out var cell);
        var root = Empty(name, null, new Vector2(cell.X + 0.5f, cell.Y + 0.5f));
        Shape("Body", root.transform, _circle, Vector2.zero, Vector2.one * 0.78f, body, 20);
        face = Shape("Face", root.transform, _circle, new Vector2(0, -0.28f), Vector2.one * 0.2f, faceColor, 23).transform;
        return root;
    }

    static Light2D AddPointLight(GameObject go, Color color, float radius, float intensity)
    {
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = radius * 0.15f;
        return l;
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
            if (circle) a = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)));
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
        imp.spritePixelsPerUnit = ppu;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
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
        if (_lit != null) sr.sharedMaterial = _lit;                                   // 조명 영향을 받는 재질
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

    static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Majungbul] {target.GetType().Name}.{field} 필드를 찾지 못함"); return; }
        p.stringValue = value;
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
