using System.Collections;
using UnityEngine;

namespace Majungbul
{
    /// <summary>
    /// Cup · Photo · WindChime · (가짜의 손).
    /// 직접 상태를 바꾸지 않고 GameFlow에 요청만 보낸다. 승인되면 GameFlow가 Apply* 를 호출한다.
    /// </summary>
    public class InteractableProp : MonoBehaviour
    {
        [SerializeField] PropKind kind;
        [SerializeField] Transform promptAnchor;   // E 안내가 뜰 위치
        [SerializeField] Transform swingPivot;     // 풍경 흔들림 회전축
        [SerializeField] float photoTiltDeg = 18f;

        Vector3 _homePos;
        Quaternion _homeRot;
        bool _homeSaved;

        public PropKind Kind => kind;
        public float X => transform.position.x;
        public Vector3 PromptPosition =>
            promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 1.2f;

        void Awake() => SaveHome();

        void SaveHome()
        {
            if (_homeSaved) return;
            _homeSaved = true;
            _homePos = transform.localPosition;
            _homeRot = transform.localRotation;
        }

        /// <summary>날짜 전환/시작 시 기본 위치·그림으로 되돌린다.</summary>
        public void ResetState()
        {
            SaveHome();
            StopAllCoroutines();
            transform.localPosition = _homePos;
            transform.localRotation = _homeRot;
            if (kind == PropKind.Photo) transform.localRotation = Quaternion.Euler(0, 0, photoTiltDeg);
            if (swingPivot != null) swingPivot.localRotation = Quaternion.identity;
        }

        public void ApplyCupNudge(float dx)
        {
            transform.position += Vector3.right * dx;
        }

        public void ApplyPhotoUpright()
        {
            StopAllCoroutines();
            StartCoroutine(RotateTo(Quaternion.identity, 0.25f));
        }

        public void ApplyChimeSwing()
        {
            if (swingPivot == null) return;
            StopAllCoroutines();
            StartCoroutine(Swing());
        }

        IEnumerator RotateTo(Quaternion target, float dur)
        {
            var from = transform.localRotation;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                transform.localRotation = Quaternion.Slerp(from, target, t / dur);
                yield return null;
            }
            transform.localRotation = target;
        }

        IEnumerator Swing()
        {
            const float dur = 1.6f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float a = 14f * Mathf.Exp(-2.4f * t) * Mathf.Sin(t * 13f);
                swingPivot.localRotation = Quaternion.Euler(0, 0, a);
                yield return null;
            }
            swingPivot.localRotation = Quaternion.identity;
        }
    }
}
