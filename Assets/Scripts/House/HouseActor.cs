using System.Collections;
using Majungbul.Core;
using UnityEngine;

namespace Majungbul.House
{
    /// <summary>
    /// 격자 위의 인물(주인공·자녀·기다리는 이). 한 번에 한 칸씩 움직이고 4방향을 바라본다.
    /// 논리상의 칸(Cell)은 걸음을 시작하는 순간 목적지로 바뀌고, 그 칸을 지도에서 막는다.
    /// </summary>
    public class HouseActor : MonoBehaviour
    {
        [SerializeField] Transform faceMarker;          // 바라보는 방향 표시(점)
        [SerializeField] float faceOffset = 0.28f;
        [SerializeField] float stepsPerSecond = 4f;

        public Cell Cell { get; private set; }
        public Dir4 Facing { get; private set; } = Dir4.Down;
        public bool IsMoving { get; private set; }
        public float StepsPerSecond { get => stepsPerSecond; set => stepsPerSecond = value; }
        protected GridMap Map { get; private set; }

        public static Vector3 CellToWorld(Cell c) => new Vector3(c.X + 0.5f, c.Y + 0.5f, 0f);
        public static Cell WorldToCell(Vector3 p) => new Cell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

        /// <summary>지도에 올리고 start 칸에 세운다. 이전 칸의 막힘은 풀고, 진행 중인 걸음은 멈춘다.</summary>
        public virtual void Bind(GridMap map, Cell start, Dir4 facing)
        {
            StopAllCoroutines();
            IsMoving = false;
            if (Map != null) Map.SetBlocked(Cell, false);
            Map = map;
            Cell = start;
            Map.SetBlocked(Cell, true);
            transform.position = CellToWorld(start);
            SetFacing(facing);
        }

        /// <summary>지도에서 내린다(사라지기 전에 호출해 칸 막힘을 푼다).</summary>
        public void Leave()
        {
            StopAllCoroutines();
            IsMoving = false;
            if (Map != null) Map.SetBlocked(Cell, false);
            Map = null;
        }

        public void SetFacing(Dir4 d)
        {
            Facing = d;
            if (faceMarker == null) return;
            var v = Dir4Util.Delta(d);
            faceMarker.localPosition = new Vector3(v.X * faceOffset, v.Y * faceOffset, 0f);
        }

        /// <summary>d 방향으로 돌아서고, 그 칸이 비어 있으면 한 칸 걷는다. 걸음을 시작했으면 true.</summary>
        public bool TryStep(Dir4 d)
        {
            if (IsMoving || Map == null) return false;
            SetFacing(d);
            var next = Cell + Dir4Util.Delta(d);
            if (!Map.IsWalkable(next)) return false;
            StartCoroutine(StepRoutine(next));
            return true;
        }

        /// <summary>바로 옆 칸으로 걷는다(경로 탐색 결과용).</summary>
        public bool TryStepTo(Cell next)
        {
            int dx = next.X - Cell.X, dy = next.Y - Cell.Y;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) return false;
            Dir4 d = dx > 0 ? Dir4.Right : dx < 0 ? Dir4.Left : dy > 0 ? Dir4.Up : Dir4.Down;
            return TryStep(d);
        }

        IEnumerator StepRoutine(Cell next)
        {
            IsMoving = true;
            Map.SetBlocked(Cell, false);
            Map.SetBlocked(next, true);
            Cell = next;

            var from = transform.position;
            var to = CellToWorld(next);
            float dur = 1f / Mathf.Max(0.1f, stepsPerSecond);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, to, t / dur);
                yield return null;
            }
            transform.position = to;
            IsMoving = false;
            OnStepped();
        }

        protected virtual void OnStepped() { }
    }
}
