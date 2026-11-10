using System;
using System.Collections;
using Majungbul.Core;
using UnityEngine;

namespace Majungbul.House
{
    /// <summary>
    /// 아이·문밖의 그것. 시선 모드: 고정 / 칸을 봄 / 다른 인물을 봄.
    /// 걷는 동안에는 진행 방향을 보고, 멈추면 다시 시선 모드를 따른다.
    /// </summary>
    public class HouseNpc : HouseActor
    {
        enum LookMode { Fixed, AtCell, AtActor }

        [SerializeField] HouseActor player;

        LookMode _mode = LookMode.Fixed;
        Cell _lookCell;
        HouseActor _lookActor;
        Coroutine _walk;

        public bool IsWalking => _walk != null;

        public override void Bind(GridMap map, Cell start, Dir4 facing)
        {
            _walk = null;
            base.Bind(map, start, facing);
            _mode = LookMode.Fixed;
        }

        public void Face(Dir4 d)
        {
            _mode = LookMode.Fixed;
            SetFacing(d);
        }

        public void LookAtCell(Cell c)
        {
            _mode = LookMode.AtCell;
            _lookCell = c;
            SetFacing(Dir4Util.Toward(Cell, c, Facing));
        }

        public void TrackActor(HouseActor a)
        {
            _mode = LookMode.AtActor;
            _lookActor = a;
            if (a != null) SetFacing(Dir4Util.Toward(Cell, a.Cell, Facing));
        }

        public void TrackPlayer() => TrackActor(player);

        void Update()
        {
            if (IsMoving) return;
            if (_mode == LookMode.AtCell) SetFacing(Dir4Util.Toward(Cell, _lookCell, Facing));
            else if (_mode == LookMode.AtActor && _lookActor != null) SetFacing(Dir4Util.Toward(Cell, _lookActor.Cell, Facing));
        }

        /// <summary>goal 칸까지 최단 경로로 걷는다. 길이 막히면 잠시 기다렸다 다시 찾는다.</summary>
        public void WalkTo(Cell goal, Action onArrive)
        {
            StopWalk();
            _walk = StartCoroutine(WalkRoutine(goal, onArrive));
        }

        public void StopWalk()
        {
            if (_walk != null) StopCoroutine(_walk);
            _walk = null;
        }

        IEnumerator WalkRoutine(Cell goal, Action onArrive)
        {
            Cell next;
            while (Cell != goal)
            {
                if (!IsMoving)
                {
                    if (Map.TryNextStep(Cell, goal, out next) && Map.IsWalkable(next)) TryStepTo(next);
                    else { yield return new WaitForSeconds(0.3f); continue; }
                }
                yield return null;
            }
            while (IsMoving) yield return null;
            _walk = null;
            onArrive?.Invoke();
        }
    }
}
