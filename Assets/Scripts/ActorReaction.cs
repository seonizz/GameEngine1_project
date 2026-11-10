using System;
using System.Collections;
using UnityEngine;

namespace Majungbul
{
    /// <summary>
    /// 가족(Family)과 가짜(Watcher)의 시선·이동 담당.
    /// 시선은 visual 자식의 localScale.x 부호(+1 = 오른쪽)로 표현한다.
    /// </summary>
    public class ActorReaction : MonoBehaviour
    {
        enum LookMode { Fixed, AtTarget, AtPlayer }

        [SerializeField] Transform visual;
        [SerializeField] GameObject hand;    // 가짜의 손 내밀기 (없어도 됨)
        [SerializeField] GameObject bubble;  // "……" 말풍선 (없어도 됨)
        [SerializeField] Transform player;
        [SerializeField] float moveSpeed = 1.6f;
        [SerializeField] float trackDeadZone = 0.08f;

        LookMode _mode = LookMode.Fixed;
        Transform _target;
        Coroutine _move;

        public bool FacingRight { get; private set; } = true;
        public bool IsMoving { get; private set; }
        public float X => transform.position.x;

        void Update()
        {
            if (IsMoving) return;
            switch (_mode)
            {
                case LookMode.AtTarget:
                    if (_target != null) FaceToward(_target.position.x);
                    break;
                case LookMode.AtPlayer:
                    if (player != null) FaceToward(player.position.x);
                    break;
            }
        }

        void FaceToward(float x)
        {
            float dx = x - transform.position.x;
            if (Mathf.Abs(dx) < trackDeadZone) return;
            SetFacing(dx > 0f);
        }

        public void Face(bool right)
        {
            _mode = LookMode.Fixed;
            SetFacing(right);
        }

        public void LookAt(Transform target)
        {
            _mode = LookMode.AtTarget;
            _target = target;
            if (target != null) FaceToward(target.position.x);
        }

        public void TrackPlayer()
        {
            _mode = LookMode.AtPlayer;
            if (player != null) FaceToward(player.position.x);
        }

        public void SetFacing(bool right)
        {
            FacingRight = right;
            if (visual == null) return;
            var s = visual.localScale;
            s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
            visual.localScale = s;
        }

        public void ShowHand(bool on) { if (hand != null) hand.SetActive(on); }
        public void ShowBubble(bool on) { if (bubble != null) bubble.SetActive(on); }

        public void Place(float x)
        {
            StopMove();
            var p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
        }

        public void MoveTo(float x, Action onArrive)
        {
            StopMove();
            _move = StartCoroutine(MoveRoutine(x, onArrive));
        }

        public void StopMove()
        {
            if (_move != null) StopCoroutine(_move);
            _move = null;
            IsMoving = false;
        }

        IEnumerator MoveRoutine(float destX, Action onArrive)
        {
            IsMoving = true;
            SetFacing(destX > transform.position.x);
            while (Mathf.Abs(transform.position.x - destX) > 0.001f)
            {
                var p = transform.position;
                p.x = Mathf.MoveTowards(p.x, destX, moveSpeed * Time.deltaTime);
                transform.position = p;
                yield return null;
            }
            IsMoving = false;
            _move = null;
            onArrive?.Invoke();
        }
    }
}
