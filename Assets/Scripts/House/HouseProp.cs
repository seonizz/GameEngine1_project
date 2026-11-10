using System.Collections;
using Majungbul.Core;
using UnityEngine;

namespace Majungbul.House
{
    public enum HousePropKind { Examine, Cup, Photo, Fire, Noise, Window, Door, Trunk, Lantern }
    public enum HouseNoise { None, Clatter, Ring, Static, Chime, Water, Knock }

    /// <summary>
    /// 지도 위 가구·물건. 조사하면 그 단계의 한 줄 문장을 보여 준다.
    /// 단계: 1 첫날, 2 둘째 날, 3 사흘째(반전 전), 4 반전 후. 비어 있으면 앞 단계 문장을 쓴다.
    /// 소리 나는 물건(Noise·Door·Trunk)은 아이가 들을 수 있으면 아이를 부른다(HouseFlow).
    /// </summary>
    public class HouseProp : MonoBehaviour
    {
        [SerializeField] string id;
        [SerializeField] HousePropKind kind;
        [SerializeField] HouseNoise noise;
        [SerializeField, TextArea] string day1Text;
        [SerializeField, TextArea] string day2Text;
        [SerializeField, TextArea] string day3Text;
        [SerializeField, TextArea] string afterText;
        [SerializeField] Transform visual;              // 흔들기·밀기·세우기·문 닫힘 그림
        [SerializeField] SpriteRenderer flash;          // 창문 노크 때 깜빡일 그림
        [SerializeField] Behaviour lightSource;         // 불·등롱의 빛(Light2D)

        Vector3 _homePos, _homeScale;
        Quaternion _homeRot;
        Color _flashBase;
        bool _saved;

        public string Id => id;
        public HousePropKind Kind => kind;
        public HouseNoise Noise => noise;
        public Cell Cell => HouseActor.WorldToCell(transform.position);
        public Transform Visual => visual;

        public string TextFor(int stage)
        {
            string[] byStage = { day1Text, day2Text, day3Text, afterText };
            for (int i = Mathf.Clamp(stage, 1, 4) - 1; i >= 0; i--)
                if (!string.IsNullOrEmpty(byStage[i])) return byStage[i];
            return "";
        }

        void Awake() => Save();

        void Save()
        {
            if (_saved) return;
            _saved = true;
            if (visual != null)
            {
                _homePos = visual.localPosition;
                _homeRot = visual.localRotation;
                _homeScale = visual.localScale;
            }
            if (flash != null) _flashBase = flash.color;
        }

        public void ResetVisual()
        {
            Save();
            StopAllCoroutines();
            if (visual == null) return;
            visual.localPosition = _homePos;
            visual.localRotation = _homeRot;
            visual.localScale = _homeScale;
        }

        public void Nudge(Dir4 d, float distance)
        {
            Save();
            if (visual == null) return;
            var v = Dir4Util.Delta(d);
            visual.localPosition = _homePos + new Vector3(v.X, v.Y, 0f) * distance;
        }

        public void Upright()
        {
            if (visual != null) visual.localRotation = Quaternion.identity;
        }

        public void SetVisualActive(bool on)
        {
            if (visual != null) visual.gameObject.SetActive(on);
        }

        public void SetLight(bool on)
        {
            if (lightSource != null) lightSource.enabled = on;
        }

        /// <summary>불 크기(0~1). 송별불이 사그라드는 표현.</summary>
        public void SetVisualScale(float k)
        {
            Save();
            if (visual != null) visual.localScale = _homeScale * k;
        }

        public void Shake()
        {
            if (visual == null || !isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(ShakeRoutine());
        }

        public void Flash(float seconds)
        {
            if (flash == null || !isActiveAndEnabled) return;
            StartCoroutine(FlashRoutine(seconds));
        }

        IEnumerator ShakeRoutine()
        {
            Save();
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float a = Mathf.Sin(t * 70f) * 0.06f * (1f - t / 0.35f);
                visual.localPosition = _homePos + new Vector3(a, 0f, 0f);
                yield return null;
            }
            visual.localPosition = _homePos;
        }

        IEnumerator FlashRoutine(float seconds)
        {
            Save();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Abs(Mathf.Sin(t * 9f));
                flash.color = Color.Lerp(_flashBase, new Color(0.75f, 0.8f, 0.9f, 1f), k * 0.6f);
                yield return null;
            }
            flash.color = _flashBase;
        }
    }
}
