using UnityEngine;

namespace Majungbul.House
{
    /// <summary>플레이어를 부드럽게 따라가되 지도 밖은 보여 주지 않는다.</summary>
    [RequireComponent(typeof(Camera))]
    public class HouseCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float smooth = 8f;

        Camera _cam;
        Vector2 _min, _max;
        bool _hasBounds;

        void Awake() => _cam = GetComponent<Camera>();

        public void SetBounds(float width, float height)
        {
            _min = Vector2.zero;
            _max = new Vector2(width, height);
            _hasBounds = true;
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = Clamp(target.position);
        }

        void LateUpdate()
        {
            if (target == null) return;
            var goal = Clamp(target.position);
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }

        Vector3 Clamp(Vector3 p)
        {
            var pos = new Vector3(p.x, p.y, -10f);
            if (!_hasBounds || _cam == null) return pos;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            pos.x = (_max.x - _min.x) <= halfW * 2f ? (_min.x + _max.x) / 2f : Mathf.Clamp(pos.x, _min.x + halfW, _max.x - halfW);
            pos.y = (_max.y - _min.y) <= halfH * 2f ? (_min.y + _max.y) / 2f : Mathf.Clamp(pos.y, _min.y + halfH, _max.y - halfH);
            return pos;
        }
    }
}
