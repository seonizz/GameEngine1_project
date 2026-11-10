using System;
using System.Collections.Generic;

namespace Majungbul.Core
{
    /// <summary>4방향. 쯔꾸르식 격자 이동과 인물 시선에 쓴다.</summary>
    public enum Dir4 { Down, Left, Right, Up }

    /// <summary>격자 칸 좌표. x는 오른쪽, y는 위쪽이 +.</summary>
    public struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y) { X = x; Y = y; }

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(Cell a, Cell b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Cell a, Cell b) => !(a == b);
        public bool Equals(Cell other) => this == other;
        public override bool Equals(object obj) => obj is Cell c && this == c;
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => "(" + X + "," + Y + ")";

        /// <summary>가로·세로 거리의 합(대각선 이동이 없으므로 실제 걸음 수의 하한).</summary>
        public static int Manhattan(Cell a, Cell b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    public static class Dir4Util
    {
        public static Cell Delta(Dir4 d)
        {
            switch (d)
            {
                case Dir4.Left: return new Cell(-1, 0);
                case Dir4.Right: return new Cell(1, 0);
                case Dir4.Up: return new Cell(0, 1);
                default: return new Cell(0, -1);
            }
        }

        /// <summary>
        /// from에서 to를 바라볼 때의 4방향. 가로·세로 중 차이가 큰 축을 고른다.
        /// 정확히 대각선이면 current가 두 후보 중 하나일 때 그대로 두고(시선이 떨리지 않게), 아니면 가로를 고른다.
        /// 같은 칸이면 current를 유지한다.
        /// </summary>
        public static Dir4 Toward(Cell from, Cell to, Dir4 current)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx == 0 && dy == 0) return current;

            Dir4 h = dx > 0 ? Dir4.Right : Dir4.Left;
            Dir4 v = dy > 0 ? Dir4.Up : Dir4.Down;
            int ax = Math.Abs(dx), ay = Math.Abs(dy);
            if (ax > ay) return h;
            if (ay > ax) return v;
            return current == v ? v : h;
        }
    }

    /// <summary>시선 판별 규칙(ADR 0004·0005).</summary>
    public static class Gaze
    {
        /// <summary>
        /// actor가 target(컵)을 볼 때와 player를 볼 때 방향이 달라지는 위치인가.
        /// 참이면 "물건을 보는가, 나를 보는가"가 화면의 시선 하나로 구분된다.
        /// </summary>
        public static bool Distinguishes(Cell actor, Cell target, Cell player, Dir4 current)
            => Dir4Util.Toward(actor, target, current) != Dir4Util.Toward(actor, player, current);
    }

    /// <summary>조건이 연속으로 Hold초 유지되면 한 번만 참을 돌려준다. 조건이 끊기면 처음부터 센다.</summary>
    public sealed class HoldTimer
    {
        public float Hold;
        public float Elapsed { get; private set; }
        public bool Done { get; private set; }

        public HoldTimer(float hold) { Hold = hold; }

        public bool Tick(float dt, bool condition)
        {
            if (Done) return false;
            if (!condition) { Elapsed = 0f; return false; }
            Elapsed += dt;
            if (Elapsed < Hold) return false;
            Done = true;
            return true;
        }

        public void Reset() { Elapsed = 0f; Done = false; }
    }

    /// <summary>
    /// 문자 지도. 한 줄이 한 행이고 첫 줄이 맨 위다.
    /// '#'과 ' '는 벽, blockingChars에 있는 문자는 막힌 가구, 나머지('.' 포함)는 걸을 수 있다.
    /// 인물이 서 있는 칸은 SetBlocked로 따로 막는다.
    /// </summary>
    public sealed class GridMap
    {
        readonly char[,] _tiles;
        readonly HashSet<char> _blocking;
        readonly HashSet<Cell> _dynamic = new HashSet<Cell>();
        readonly Dictionary<char, List<Cell>> _markers = new Dictionary<char, List<Cell>>();

        public int Width { get; }
        public int Height { get; }

        public GridMap(string text, string blockingChars)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var raw = text.Replace("\r", "").Split('\n');
            var rows = new List<string>();
            foreach (var r in raw) if (r.Length > 0) rows.Add(r);
            if (rows.Count == 0) throw new ArgumentException("빈 지도");

            Height = rows.Count;
            Width = 0;
            foreach (var r in rows) Width = Math.Max(Width, r.Length);

            _tiles = new char[Width, Height];
            _blocking = new HashSet<char>(blockingChars ?? "");
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                {
                    char ch = x < rows[row].Length ? rows[row][x] : ' ';
                    _tiles[x, y] = ch;
                    if (ch != '#' && ch != ' ' && ch != '.')
                    {
                        if (!_markers.TryGetValue(ch, out var list)) _markers[ch] = list = new List<Cell>();
                        list.Add(new Cell(x, y));
                    }
                }
            }
        }

        public bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

        public char TileAt(Cell c) => InBounds(c) ? _tiles[c.X, c.Y] : '#';

        public bool IsWall(Cell c)
        {
            char ch = TileAt(c);
            return ch == '#' || ch == ' ';
        }

        /// <summary>벽·가구·인물이 없어 들어갈 수 있는 칸인가.</summary>
        public bool IsWalkable(Cell c)
        {
            if (!InBounds(c) || IsWall(c)) return false;
            if (_blocking.Contains(TileAt(c))) return false;
            return !_dynamic.Contains(c);
        }

        public void SetBlocked(Cell c, bool blocked)
        {
            if (blocked) _dynamic.Add(c); else _dynamic.Remove(c);
        }

        public IReadOnlyList<Cell> Markers(char ch)
            => _markers.TryGetValue(ch, out var list) ? (IReadOnlyList<Cell>)list : Array.Empty<Cell>();

        public bool TryMarker(char ch, out Cell cell)
        {
            var list = Markers(ch);
            cell = list.Count > 0 ? list[0] : default(Cell);
            return list.Count > 0;
        }

        /// <summary>
        /// from에서 goal까지 걸음 수. goal 칸 자체는 막혀 있어도 도착점으로 인정한다. 길이 없으면 false.
        /// 아이가 소리를 "들을 수 있는가"(벽을 돌아가는 실제 거리)를 판정하는 데 쓴다.
        /// </summary>
        public bool TryPathLength(Cell from, Cell goal, out int length)
        {
            length = 0;
            if (from == goal) return true;
            var dist = new Dictionary<Cell, int> { [from] = 0 };
            var queue = new Queue<Cell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    var n = c + Dir4Util.Delta((Dir4)i);
                    if (dist.ContainsKey(n) || !InBounds(n)) continue;
                    if (n != goal && !IsWalkable(n)) continue;
                    dist[n] = dist[c] + 1;
                    if (n == goal) { length = dist[n]; return true; }
                    queue.Enqueue(n);
                }
            }
            return false;
        }

        /// <summary>
        /// target(가구·문 등) 바로 옆의 걸을 수 있는 칸 중 from에서 가장 가까운 칸.
        /// from이 이미 target 옆이면 from을 돌려준다.
        /// </summary>
        public bool TryNearestNeighbor(Cell target, Cell from, out Cell best, out int length)
        {
            best = from; length = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var n = target + Dir4Util.Delta((Dir4)i);
                if (n == from) { best = from; length = 0; return true; }
                if (!IsWalkable(n)) continue;
                if (TryPathLength(from, n, out int len) && len < length) { best = n; length = len; }
            }
            return length != int.MaxValue;
        }

        /// <summary>
        /// from에서 goal까지 최단 경로의 첫 걸음. goal 칸 자체는 막혀 있어도 도착점으로 인정한다
        /// (사람이 서 있는 칸·가구 칸으로 "다가가기"). 경로가 없거나 이미 도착했으면 false.
        /// </summary>
        public bool TryNextStep(Cell from, Cell goal, out Cell next)
        {
            next = from;
            if (from == goal) return false;

            var prev = new Dictionary<Cell, Cell>();
            var queue = new Queue<Cell>();
            prev[from] = from;
            queue.Enqueue(from);
            bool found = false;

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    var n = c + Dir4Util.Delta((Dir4)i);
                    if (prev.ContainsKey(n)) continue;
                    if (n != goal && !IsWalkable(n)) continue;
                    if (!InBounds(n)) continue;
                    prev[n] = c;
                    if (n == goal) { found = true; break; }
                    queue.Enqueue(n);
                }
                if (found) break;
            }
            if (!found) return false;

            var step = goal;
            while (prev[step] != from) step = prev[step];
            next = step;
            return true;
        }
    }
}

namespace Majungbul.Core
{
    public enum Ending { None, Farewell, StolenFire, GaveWay, FireOut }

    /// <summary>
    /// 반전 이후의 결말 판정(ADR 0006). 선택창 없이 행동으로 갈린다.
    /// 배웅: 등롱이 켜진 뒤 출구 / 빼앗은 불: 등롱 없이 송별불로 출구 / 남은 자리: 불 앞에서 비켜섬 / 꺼진 불: 시간 초과.
    /// </summary>
    public static class EndingRule
    {
        public static Ending Decide(bool twistRevealed, bool lanternLit, bool fireLit,
                                    bool reachedExit, bool steppedAside, bool timeUp)
        {
            if (!twistRevealed) return Ending.None;
            if (steppedAside) return Ending.GaveWay;
            if (reachedExit && lanternLit) return Ending.Farewell;
            if (reachedExit && fireLit) return Ending.StolenFire;
            if (timeUp && !lanternLit) return Ending.FireOut;
            return Ending.None;
        }
    }
}
