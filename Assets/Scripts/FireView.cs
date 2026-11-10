using UnityEngine;

namespace Majungbul
{
    /// <summary>현관의 불(첫날 마중불 / 사흘째 송별불). 켜짐·꺼짐 표시와 흔들림.</summary>
    public class FireView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer flame;
        [SerializeField] SpriteRenderer innerFlame;
        [SerializeField] SpriteRenderer ember;
        [SerializeField] Color emberLit = new Color(0.55f, 0.32f, 0.2f);
        [SerializeField] Color emberOut = new Color(0.25f, 0.24f, 0.23f);

        Vector3 _flameScale, _innerScale;
        bool _init;

        public bool IsLit { get; private set; }

        void Awake() => Init();

        void Init()
        {
            if (_init) return;
            _init = true;
            if (flame != null) _flameScale = flame.transform.localScale;
            if (innerFlame != null) _innerScale = innerFlame.transform.localScale;
        }

        public void SetLit(bool lit)
        {
            Init();
            IsLit = lit;
            if (flame != null) flame.enabled = lit;
            if (innerFlame != null) innerFlame.enabled = lit;
            if (ember != null) ember.color = lit ? emberLit : emberOut;
        }

        void Update()
        {
            if (!IsLit) return;
            float t = Time.time;
            float f = 1f + 0.07f * Mathf.Sin(t * 11f) + 0.06f * (Mathf.PerlinNoise(t * 3f, 0.3f) - 0.5f);
            if (flame != null)
                flame.transform.localScale = new Vector3(_flameScale.x * (2f - f), _flameScale.y * f, 1f);
            if (innerFlame != null)
                innerFlame.transform.localScale = new Vector3(_innerScale.x, _innerScale.y * (0.9f + 0.1f * f), 1f);
        }
    }
}
