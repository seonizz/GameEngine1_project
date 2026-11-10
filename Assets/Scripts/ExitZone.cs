using UnityEngine;

namespace Majungbul
{
    /// <summary>귀환 구역(문턱). 체류 여부만 관리하고 성공 판정은 GameFlow가 한다.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ExitZone : MonoBehaviour
    {
        [SerializeField] GameFlow flow;

        public bool PlayerInside { get; private set; }

        void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            PlayerInside = true;
            if (flow != null) flow.OnExitZoneEntered();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            PlayerInside = false;
        }
    }
}
