// 마중불 지도 검사 (tools/verify/verify.sh 전용)
//
// 지도를 고친 뒤 "게임이 막히지 않는가"를 Unity 없이 확인한다. 지도·소품·조정값은 따로 적어 두지 않고
// 실제 코드에서 읽는다(리플렉션):
//   - 지도 `Map`, 막힘 문자 `Blocking`, 소품 표 `Props`, 창문 이름 `WindowIds`  ← Assets/Editor/HouseSceneBuilder.cs
//   - `hearingRange`, `noticeNeeded` 기본값, `KnockOrder`                       ← Assets/Scripts/House/HouseFlow.cs
// 길찾기와 "들림" 판정은 게임과 같은 Core.GridMap(TryNearestNeighbor)을 쓴다(HouseChild.CanHear·GoTo 와 같은 식).
//
// 검사 항목
//   1. 지도·소품 표 일치: 표에 있는 소품 문자는 지도에 있고, 지도의 소문자·대문자는 소품·표식·구조 문자 중 하나다.
//      지도의 모든 창문(w)에 WindowIds 이름이 있고, KnockOrder 의 대상이 지도에 있다.
//   2. 문 둘 다 열림(반전 이후 상태): 주인공 시작 칸 P에서 모든 소품·창문 옆 칸과 모든 표식 칸에 걸어서 닿는다.
//   3. 문 둘 다 닫힘(첫날·둘째 날·내쳐짐): 모든 소품은 집 안(P)이나 마당(Y) 중 한쪽에서 닿고(갇힌 소품 없음),
//      창문은 안쪽(P)과 바깥쪽(Y) 모두에서 닿는다(안에서 조사, 밖에서 노크).
//   4. 첫날 소리: 문이 닫힌 상태에서 주인공이 닿을 수 있고 아이 자리 F에서 hearingRange 걸음 안에 들리는
//      소리 물건(종류 Noise·Door·Trunk) 수 + 찻잔(닿으면 1) >= noticeNeeded.
//   5. 둘째 날 노크: 문이 닫힌 상태에서 아이가 F에서 KnockOrder 의 각 대상 옆까지 걸어갈 수 있고,
//      그 대상 바로 바깥에 그것(Watcher)이 설 마당 칸이 있다.
//
// 한계: 인물이 서 있는 칸(SetBlocked)은 막지 않는다. 씬 Inspector 에서 hearingRange·noticeNeeded 를 바꿨다면
// 그 값이 아니라 코드 기본값으로 검사한다(씬 빌더는 이 두 값을 덮어쓰지 않는다).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Majungbul.Core;
using Majungbul.House;

public static class MapChecks
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    static int _fail;

    static void Check(bool ok, string what, string detail = "")
    {
        if (!ok) _fail++;
        Console.WriteLine("  " + (ok ? "통과" : "실패") + " " + what + (detail.Length > 0 ? " — " + detail : ""));
    }

    struct Prop { public char Ch; public string Id; public HousePropKind Kind; public Cell Cell; }

    public static int Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try { return Run(); }
        catch (Exception e)
        {
            Console.WriteLine("  실패 지도 검사를 끝까지 하지 못했다 — " + e.GetType().Name + ": " + e.Message);
            return 1;
        }
    }

    static int Run()
    {

        // ── 코드에서 값 읽기 ──
        var builder = typeof(HouseSceneBuilder);
        string mapText = (string)builder.GetField("Map", Any).GetRawConstantValue();
        string blocking = (string)builder.GetField("Blocking", Any).GetRawConstantValue();
        var propTable = (IDictionary)builder.GetField("Props", Any).GetValue(null);
        var windowIds = (IDictionary)builder.GetField("WindowIds", Any).GetValue(null);

        var flow = Activator.CreateInstance(typeof(HouseFlow));
        int hearingRange = (int)typeof(HouseFlow).GetField("hearingRange", Any).GetValue(flow);
        int noticeNeeded = (int)typeof(HouseFlow).GetField("noticeNeeded", Any).GetValue(flow);
        var knockOrder = (string[])typeof(HouseFlow).GetField("KnockOrder", Any).GetValue(null);

        var map = new GridMap(mapText, blocking);
        int H = map.Height;
        Console.WriteLine($"  지도 {map.Width}x{H}, hearingRange={hearingRange}, noticeNeeded={noticeNeeded}, KnockOrder={string.Join(",", knockOrder)}");

        // 소품 목록(빌더와 같은 방식: 표의 문자마다 지도 위 모든 칸, 그리고 창문 w)
        var props = new List<Prop>();
        foreach (DictionaryEntry kv in propTable)
        {
            char ch = (char)kv.Key;
            var def = kv.Value; var t = def.GetType();
            foreach (var c in map.Markers(ch))
                props.Add(new Prop { Ch = ch, Id = (string)t.GetField("Id").GetValue(def), Kind = (HousePropKind)t.GetField("Kind").GetValue(def), Cell = c });
        }
        var missingWindowIds = new List<string>();
        foreach (var c in map.Markers('w'))
        {
            string id = null;
            foreach (DictionaryEntry kv in windowIds)
            {
                var k = kv.Key; var kt = k.GetType();
                int x = (int)kt.GetField("x").GetValue(k), row = (int)kt.GetField("y").GetValue(k);
                if (x == c.X && row == H - 1 - c.Y) id = (string)kv.Value;
            }
            if (id == null) missingWindowIds.Add(Show(c, H));
            props.Add(new Prop { Ch = 'w', Id = id ?? "window", Kind = HousePropKind.Window, Cell = c });
        }

        Cell P = Marker(map, 'P'), F = Marker(map, 'F'), Y = Marker(map, 'Y');
        Cell D = Marker(map, 'D'), E = Marker(map, 'E');

        // ── 1. 지도·소품 표 일치 ──
        Console.WriteLine("[1] 지도·소품 표 일치");
        const string structural = "#., wDES";        // 벽·바닥·마당·창·문·창고
        const string markers = "PQFGVYX";
        var tableChars = new HashSet<char>(propTable.Keys.Cast<char>());
        var absent = tableChars.Where(ch => map.Markers(ch).Count == 0).ToList();
        Check(absent.Count == 0, "소품 표의 문자가 모두 지도에 있다", absent.Count == 0 ? "" : "지도에 없음: " + new string(absent.ToArray()));
        var unknown = new SortedSet<char>();
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < H; y++)
            {
                char ch = map.TileAt(new Cell(x, y));
                if (structural.IndexOf(ch) < 0 && markers.IndexOf(ch) < 0 && !tableChars.Contains(ch)) unknown.Add(ch);
            }
        Check(unknown.Count == 0, "지도의 문자가 모두 소품·표식·구조 문자다", unknown.Count == 0 ? "" : "뜻이 없는 문자: " + new string(unknown.ToArray()));
        var dupMarkers = markers.Where(ch => map.Markers(ch).Count != 1).Select(ch => ch + "×" + map.Markers(ch).Count).ToList();
        Check(dupMarkers.Count == 0, "표식 P Q F G V Y X 가 각각 한 칸씩 있다", string.Join(", ", dupMarkers));
        Check(map.Markers('D').Count == 1 && map.Markers('E').Count == 1, "현관문 D·뒷문 E 가 각각 한 칸씩 있다");
        Check(missingWindowIds.Count == 0, "모든 창문(w)에 WindowIds 이름이 있다", missingWindowIds.Count == 0 ? "" : "이름 없는 창문(열,행): " + string.Join(" ", missingWindowIds));
        var knockMissing = knockOrder.Where(id => !props.Any(p => p.Id == id)).ToList();
        Check(knockMissing.Count == 0, "KnockOrder 의 대상이 모두 지도에 있다", string.Join(", ", knockMissing));
        var notBlocking = props.Where(p => p.Ch != 'w' && p.Kind != HousePropKind.Door && blocking.IndexOf(p.Ch) < 0).Select(p => p.Ch).Distinct().ToList();
        Check(notBlocking.Count == 0, "문을 뺀 소품 문자가 모두 Blocking 에 있다(가구 칸을 밟고 지나가지 않음)", notBlocking.Count == 0 ? "" : "Blocking 에 없음: " + new string(notBlocking.ToArray()));

        // ── 2. 문 둘 다 열림 ──
        Console.WriteLine("[2] 문 둘 다 열림: P에서 모든 소품·표식에 닿는가");
        map.SetBlocked(D, false); map.SetBlocked(E, false);
        var unreachOpen = props.Where(p => !Reach(map, p.Cell, P)).Select(p => Name(p, H)).ToList();
        Check(unreachOpen.Count == 0, $"소품·창문 {props.Count}개 모두 옆 칸에 닿는다", string.Join(" ", unreachOpen));
        var unreachMarkers = markers.Where(ch => ch != 'P' && map.Markers(ch).Count > 0 && !map.TryPathLength(P, map.Markers(ch)[0], out _))
                                    .Select(ch => ch + Show(map.Markers(ch)[0], H)).ToList();
        Check(unreachMarkers.Count == 0, "표식 Q F G V Y X 칸에 닿는다", string.Join(" ", unreachMarkers));
        var blockedMarkers = markers.Where(ch => map.Markers(ch).Count > 0 && !map.IsWalkable(map.Markers(ch)[0])).ToList();
        Check(blockedMarkers.Count == 0, "표식 칸이 막힌 칸이 아니다", new string(blockedMarkers.ToArray()));

        // ── 3. 문 둘 다 닫힘 ──
        Console.WriteLine("[3] 문 둘 다 닫힘: 갇힌 소품이 없는가");
        map.SetBlocked(D, true); map.SetBlocked(E, true);
        bool split = !map.TryPathLength(P, Y, out _);
        Check(split, "문을 닫으면 집 안(P)과 마당(Y)이 나뉜다", split ? "" : "닫힌 문 말고도 드나들 틈이 있다");
        var sealedProps = props.Where(p => !Reach(map, p.Cell, P) && !Reach(map, p.Cell, Y)).Select(p => Name(p, H)).ToList();
        Check(sealedProps.Count == 0, "모든 소품이 안이나 밖 한쪽에서 닿는다", string.Join(" ", sealedProps));
        var badWindows = props.Where(p => p.Kind == HousePropKind.Window && !(Reach(map, p.Cell, P) && Reach(map, p.Cell, Y)))
                              .Select(p => Name(p, H) + (Reach(map, p.Cell, P) ? "(밖에서 못 닿음)" : "(안에서 못 닿음)")).ToList();
        Check(badWindows.Count == 0, "창문은 안팎 양쪽에서 닿는다", string.Join(" ", badWindows));

        // ── 4. 첫날 소리 ──
        Console.WriteLine($"[4] 첫날 소리: 아이 자리 F{Show(F, H)}에서 {hearingRange}걸음 안");
        var lines = new List<string>();
        var usable = new List<string>();
        foreach (var p in props.Where(p => p.Kind == HousePropKind.Noise || p.Kind == HousePropKind.Door || p.Kind == HousePropKind.Trunk))
        {
            bool playerCan = Reach(map, p.Cell, P);
            bool heard = map.TryNearestNeighbor(p.Cell, F, out _, out int len) && len <= hearingRange;
            string dist = map.TryNearestNeighbor(p.Cell, F, out _, out int l2) ? l2 + "걸음" : "길 없음";
            lines.Add($"{p.Ch} {p.Id,-12} 아이까지 {dist,-6} {(heard ? "들림" : "안 들림")}{(playerCan ? "" : ", 주인공이 못 닿음")}");
            if (heard && playerCan) usable.Add(p.Id);
        }
        foreach (var l in lines) Console.WriteLine("       " + l);
        var cup = props.FirstOrDefault(p => p.Kind == HousePropKind.Cup);
        bool cupOk = cup.Id != null && Reach(map, cup.Cell, P);
        int available = usable.Count + (cupOk ? 1 : 0);
        Check(available >= noticeNeeded, $"알릴 수 있는 물건 {available}개 >= noticeNeeded {noticeNeeded}",
              "소리 " + usable.Count + "개(" + string.Join(",", usable) + ")" + (cupOk ? " + 찻잔" : ", 찻잔 못 닿음"));

        // ── 5. 둘째 날 노크 ──
        Console.WriteLine("[5] 둘째 날 노크: 아이가 F에서 대상까지 가는가");
        foreach (var id in knockOrder)
        {
            var targets = props.Where(p => p.Id == id).ToList();
            if (targets.Count == 0) continue;           // [1]에서 이미 실패로 셌다
            var t = targets[0];
            bool go = map.TryNearestNeighbor(t.Cell, F, out _, out int len);
            bool outside = Enumerable.Range(0, 4).Select(i => t.Cell + Dir4Util.Delta((Dir4)i)).Any(n => map.TileAt(n) == ',' && map.IsWalkable(n));
            Check(go && outside, $"{id}{Show(t.Cell, H)}", (go ? "아이 " + len + "걸음" : "아이가 못 감") + (outside ? ", 바깥 칸 있음" : ", 그것이 설 바깥 칸 없음"));
        }

        Console.WriteLine(_fail == 0 ? "  지도 검사 모두 통과" : $"  지도 검사 실패 {_fail}건");
        return _fail == 0 ? 0 : 1;
    }

    static Cell Marker(GridMap map, char ch)
    {
        if (!map.TryMarker(ch, out var c)) throw new Exception("지도에 표식 '" + ch + "' 가 없다");
        return c;
    }

    static bool Reach(GridMap map, Cell target, Cell from) => map.TryNearestNeighbor(target, from, out _, out _);

    // 보고는 생성기·빌더와 같은 (열,행) 좌표로 한다. 행 0이 맨 위.
    static string Show(Cell c, int h) => "(" + c.X + "," + (h - 1 - c.Y) + ")";
    static string Name(Prop p, int h) => p.Id + Show(p.Cell, h);
}
