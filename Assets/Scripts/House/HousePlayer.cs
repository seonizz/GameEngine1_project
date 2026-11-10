using Majungbul.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Majungbul.House
{
    /// <summary>
    /// 방향키/WASD로 한 칸씩 이동(누르고 있으면 계속), Shift 달리기, E/Space/Enter로 앞 칸 조사, R 처음부터.
    /// 벽을 향해 누르면 돌아서기만 한다(쯔꾸르식).
    /// </summary>
    public class HousePlayer : HouseActor
    {
        [SerializeField] HouseFlow flow;
        [SerializeField] float runMultiplier = 1.7f;

        float _walkSpeed;

        void Awake() => _walkSpeed = StepsPerSecond;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow == null) return;

            if (kb.rKey.wasPressedThisFrame) { flow.Restart(); return; }
            if (!flow.PlayerCanAct) return;

            if (!IsMoving && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                flow.Interact(Cell + Dir4Util.Delta(Facing));
                return;
            }

            Dir4? want = null;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) want = Dir4.Up;
            else if (kb.downArrowKey.isPressed || kb.sKey.isPressed) want = Dir4.Down;
            else if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) want = Dir4.Left;
            else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) want = Dir4.Right;

            if (want.HasValue && !IsMoving)
            {
                StepsPerSecond = _walkSpeed * (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? runMultiplier : 1f);
                TryStep(want.Value);
            }
        }

        protected override void OnStepped()
        {
            if (flow != null) flow.OnPlayerStepped(Cell);
        }
    }
}
