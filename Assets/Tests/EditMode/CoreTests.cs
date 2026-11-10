using Majungbul.Core;
using NUnit.Framework;

namespace Majungbul.Tests
{
    public class Dir4Tests
    {
        static readonly Cell O = new Cell(5, 5);

        [Test]
        public void Toward_고른축은_차이가_큰_쪽이다()
        {
            Assert.AreEqual(Dir4.Left, Dir4Util.Toward(O, new Cell(1, 6), Dir4.Down));
            Assert.AreEqual(Dir4.Up, Dir4Util.Toward(O, new Cell(6, 9), Dir4.Down));
            Assert.AreEqual(Dir4.Right, Dir4Util.Toward(O, new Cell(9, 5), Dir4.Up));
            Assert.AreEqual(Dir4.Down, Dir4Util.Toward(O, new Cell(5, 0), Dir4.Up));
        }

        [Test]
        public void Toward_정확한_대각선이면_현재_방향이_후보일때_유지한다()
        {
            var ne = new Cell(7, 7);
            Assert.AreEqual(Dir4.Up, Dir4Util.Toward(O, ne, Dir4.Up));
            Assert.AreEqual(Dir4.Right, Dir4Util.Toward(O, ne, Dir4.Right));
            Assert.AreEqual(Dir4.Right, Dir4Util.Toward(O, ne, Dir4.Left)); // 후보가 아니면 가로
        }

        [Test]
        public void Toward_같은_칸이면_현재_방향을_유지한다()
        {
            Assert.AreEqual(Dir4.Left, Dir4Util.Toward(O, O, Dir4.Left));
        }
    }

    public class GazeTests
    {
        // 거실 배치: 컵(13,4) 자녀(14,4) 기다리는 이(16,3). y는 위가 +.
        static readonly Cell Cup = new Cell(13, 4);
        static readonly Cell Watcher = new Cell(16, 3);

        [Test]
        public void 컵과_같은_쪽에_서면_구분되지_않는다()
        {
            Assert.IsFalse(Gaze.Distinguishes(Watcher, Cup, new Cell(10, 3), Dir4.Up));
        }

        [Test]
        public void 컵과_다른_쪽에_서면_구분된다()
        {
            Assert.IsTrue(Gaze.Distinguishes(Watcher, Cup, new Cell(20, 3), Dir4.Up));  // 오른쪽
            Assert.IsTrue(Gaze.Distinguishes(Watcher, Cup, new Cell(16, 0), Dir4.Up));  // 아래
        }
    }

    public class HoldTimerTests
    {
        [Test]
        public void 연속으로_유지해야_한번만_완료된다()
        {
            var t = new HoldTimer(0.6f);
            Assert.IsFalse(t.Tick(0.5f, true));
            Assert.IsFalse(t.Tick(0.5f, false));   // 끊기면 다시 센다
            Assert.IsFalse(t.Tick(0.5f, true));
            Assert.IsTrue(t.Tick(0.2f, true));
            Assert.IsFalse(t.Tick(1f, true));      // 이미 완료
            Assert.IsTrue(t.Done);
        }
    }

    public class GridMapTests
    {
        const string Map =
            "#######\n" +
            "#.....#\n" +
            "#.#T#.#\n" +
            "#P...G#\n" +
            "#######\n";

        static GridMap Make() => new GridMap(Map, "T");

        [Test]
        public void 첫_줄이_맨_위이고_표식을_찾는다()
        {
            var m = Make();
            Assert.AreEqual(7, m.Width);
            Assert.AreEqual(5, m.Height);
            Assert.IsTrue(m.TryMarker('P', out var p));
            Assert.AreEqual(new Cell(1, 1), p);
            Assert.IsTrue(m.TryMarker('T', out var t));
            Assert.AreEqual(new Cell(3, 2), t);
        }

        [Test]
        public void 벽_가구_인물은_막히고_표식칸은_걸을_수_있다()
        {
            var m = Make();
            Assert.IsFalse(m.IsWalkable(new Cell(0, 0)));
            Assert.IsFalse(m.IsWalkable(new Cell(3, 2)));       // 가구 T
            Assert.IsTrue(m.IsWalkable(new Cell(1, 1)));        // 표식 P
            m.SetBlocked(new Cell(2, 1), true);
            Assert.IsFalse(m.IsWalkable(new Cell(2, 1)));
            m.SetBlocked(new Cell(2, 1), false);
            Assert.IsTrue(m.IsWalkable(new Cell(2, 1)));
            Assert.IsFalse(m.IsWalkable(new Cell(-1, 2)));      // 지도 밖
        }

        [Test]
        public void 최단_경로의_첫_걸음을_돌려준다()
        {
            var m = Make();
            Assert.IsTrue(m.TryNextStep(new Cell(1, 1), new Cell(5, 1), out var next));
            Assert.AreEqual(new Cell(2, 1), next);
        }

        [Test]
        public void 막힌_칸이면_돌아서_간다()
        {
            var m = Make();
            m.SetBlocked(new Cell(3, 1), true);                 // 아랫길을 막으면 윗길로
            Assert.IsTrue(m.TryNextStep(new Cell(1, 1), new Cell(5, 1), out var next));
            Assert.AreEqual(new Cell(1, 2), next);
        }

        [Test]
        public void 목표칸이_막혀_있어도_다가갈_수_있고_길이_없으면_false()
        {
            var m = Make();
            m.SetBlocked(new Cell(5, 1), true);                 // 목표에 누가 서 있음
            Assert.IsTrue(m.TryNextStep(new Cell(4, 1), new Cell(5, 1), out var next));
            Assert.AreEqual(new Cell(5, 1), next);

            m.SetBlocked(new Cell(2, 1), true);
            m.SetBlocked(new Cell(1, 2), true);
            Assert.IsFalse(m.TryNextStep(new Cell(1, 1), new Cell(5, 1), out _));
            Assert.IsFalse(m.TryNextStep(new Cell(1, 1), new Cell(1, 1), out _));
        }
    }

    public class PathTests
    {
        const string Map =
            "#######\n" +
            "#.....#\n" +
            "#.#T#.#\n" +
            "#P...G#\n" +
            "#######\n";

        [Test]
        public void 경로_길이는_벽을_돌아간_걸음_수다()
        {
            var m = new GridMap(Map, "T");
            Assert.IsTrue(m.TryPathLength(new Cell(1, 1), new Cell(5, 1), out int len));
            Assert.AreEqual(4, len);
            m.SetBlocked(new Cell(3, 1), true);
            Assert.IsTrue(m.TryPathLength(new Cell(1, 1), new Cell(5, 1), out len));
            Assert.AreEqual(8, len);                            // 위로 돌아감
        }

        [Test]
        public void 가구_옆의_가장_가까운_칸을_고른다()
        {
            var m = new GridMap(Map, "T");                      // T는 (3,2)
            Assert.IsTrue(m.TryNearestNeighbor(new Cell(3, 2), new Cell(1, 1), out var best, out int len));
            Assert.AreEqual(new Cell(3, 1), best);
            Assert.AreEqual(2, len);
            Assert.IsTrue(m.TryNearestNeighbor(new Cell(3, 2), new Cell(3, 1), out best, out len));
            Assert.AreEqual(new Cell(3, 1), best);              // 이미 옆이면 그 자리
            Assert.AreEqual(0, len);
        }
    }

    public class EndingRuleTests
    {
        [Test]
        public void 반전_전에는_결말이_없다()
        {
            Assert.AreEqual(Ending.None, EndingRule.Decide(false, false, true, true, false, false));
        }

        [Test]
        public void 행동에_따라_네_결말로_갈린다()
        {
            Assert.AreEqual(Ending.Farewell, EndingRule.Decide(true, true, true, true, false, false));
            Assert.AreEqual(Ending.StolenFire, EndingRule.Decide(true, false, true, true, false, false));
            Assert.AreEqual(Ending.GaveWay, EndingRule.Decide(true, false, true, false, true, false));
            Assert.AreEqual(Ending.FireOut, EndingRule.Decide(true, false, false, false, false, true));
            Assert.AreEqual(Ending.None, EndingRule.Decide(true, false, true, false, false, false));
        }

        [Test]
        public void 비켜섬이_출구보다_먼저다()
        {
            Assert.AreEqual(Ending.GaveWay, EndingRule.Decide(true, false, true, true, true, false));
        }
    }
}
