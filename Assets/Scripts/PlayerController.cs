using UnityEngine;
using UnityEngine.InputSystem;

namespace Majungbul
{
    /// <summary>
    /// 좌우 이동, 가장 가까운 활성 대상 하나 선택, E 입력 전달.
    /// 중력 0 + 회전·Y 고정으로 한 층의 보행선을 유지한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] Transform visual;
        [SerializeField] InteractableProp[] props;
        [SerializeField] float moveSpeed = 2.5f;
        [SerializeField] float interactRange = 0.8f;
        [SerializeField] float minX = 0.4f;
        [SerializeField] float maxX = 15.6f;

        Rigidbody2D _rb;
        float _dir;

        public InteractableProp Current { get; private set; }
        public float X => transform.position.x;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow == null || flow.InputLocked)
            {
                _dir = 0f;
                Current = null;
                return;
            }

            float d = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) d -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) d += 1f;
            _dir = d;
            if (d != 0f) SetFacing(d > 0f);

            Current = FindNearest();
            if (Current != null && kb.eKey.wasPressedThisFrame)
                flow.RequestInteract(Current);
        }

        void FixedUpdate()
        {
            _rb.linearVelocity = new Vector2(_dir * moveSpeed, 0f);
            var p = _rb.position;
            if (p.x < minX || p.x > maxX)
            {
                p.x = Mathf.Clamp(p.x, minX, maxX);
                _rb.position = p;
                _rb.linearVelocity = Vector2.zero;
            }
        }

        InteractableProp FindNearest()
        {
            InteractableProp best = null;
            float bestDist = interactRange;
            foreach (var prop in props)
            {
                if (prop == null || !prop.gameObject.activeInHierarchy) continue;
                if (!flow.IsPropAvailable(prop)) continue;
                float dist = Mathf.Abs(prop.X - X);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = prop;
                }
            }
            return best;
        }

        public void SetFacing(bool right)
        {
            if (visual == null) return;
            var s = visual.localScale;
            s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
            visual.localScale = s;
        }

        public void Teleport(float x)
        {
            _dir = 0f;
            var p = _rb != null ? _rb.position : (Vector2)transform.position;
            p.x = x;
            if (_rb != null)
            {
                _rb.position = p;
                _rb.linearVelocity = Vector2.zero;
            }
            transform.position = new Vector3(p.x, transform.position.y, transform.position.z);
        }
    }
}
