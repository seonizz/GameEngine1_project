using System;
using System.Collections;
using Majungbul.Core;
using UnityEngine;

namespace Majungbul.House
{
    /// <summary>
    /// 아이(산 사람). 망자를 보지 못하고 소리 난 물건만 본다(세계 규칙 1).
    /// 들을 수 있는 거리(벽을 돌아가는 실제 걸음 수) 안에서 소리가 나면, 하던 일을 멈추고 그 물건 옆으로 가서 바라본다.
    /// 새 소리가 나면 이전 목적지는 버린다(먼저 부른 콜백은 호출되지 않는다).
    /// </summary>
    public class HouseChild : HouseNpc
    {
        public enum ChildState { Idle, Going, Looking, Returning, Scripted }

        public ChildState State { get; private set; } = ChildState.Idle;
        public Cell Seat { get; private set; }

        Coroutine _return;

        public void SetSeat(Cell seat) => Seat = seat;

        /// <summary>벽을 돌아가는 걸음 수가 range 이하이면 들린다.</summary>
        public bool CanHear(Cell source, int range)
        {
            if (Map == null) return false;
            return Map.TryNearestNeighbor(source, Cell, out _, out int len) && len <= range;
        }

        /// <summary>target 옆으로 가서 바라본다. 갈 수 없으면 false.</summary>
        public bool GoTo(Cell target, Action onArrive)
        {
            if (Map == null || !Map.TryNearestNeighbor(target, Cell, out var stand, out _)) return false;
            CancelReturn();
            State = ChildState.Going;
            WalkTo(stand, () =>
            {
                LookAtCell(target);
                State = ChildState.Looking;
                onArrive?.Invoke();
            });
            return true;
        }

        /// <summary>delay초 뒤 자리로 돌아가 앉는다(위쪽을 본다).</summary>
        public void ReturnToSeat(float delay, Action onSeated = null)
        {
            CancelReturn();
            _return = StartCoroutine(ReturnRoutine(delay, onSeated));
        }

        /// <summary>연출 중에는 소리에 반응하지 않는다.</summary>
        public void SetScripted(bool on)
        {
            if (on) { CancelReturn(); StopWalk(); State = ChildState.Scripted; }
            else State = ChildState.Idle;
        }

        public void ScriptedWalk(Cell goal, Action onArrive)
        {
            SetScripted(true);
            WalkTo(goal, onArrive);
        }

        void CancelReturn()
        {
            if (_return != null) StopCoroutine(_return);
            _return = null;
        }

        IEnumerator ReturnRoutine(float delay, Action onSeated)
        {
            yield return new WaitForSeconds(delay);
            State = ChildState.Returning;
            bool done = false;
            WalkTo(Seat, () => done = true);
            while (!done) yield return null;
            Face(Dir4.Up);
            State = ChildState.Idle;
            _return = null;
            onSeated?.Invoke();
        }
    }
}
