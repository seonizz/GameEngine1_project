using System.Collections;
using System.Collections.Generic;
using Majungbul.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Majungbul.House
{
    public enum HousePhase { Prologue, Day1, Day2, Day3, Outside, After, Cutscene, Ending }

    /// <summary>
    /// 마중불 진행(ADR 0006).
    /// 오프닝 → 첫날 "알리기"(소리로 아이 부르기 3번 → "…오셨어요?") → 둘째 날 "지키기"(노크 3번, 다른 곳 소리로 아이 주의 돌리기)
    /// → 사흘째 "쫓김"(송별불, 들어온 그것에게 쫓김. 붙잡히면 문밖으로 내쳐짐) → 불단 사진을 세우면 반전
    /// → 반전 후 송별불이 사그라드는 동안 행동으로 결말이 갈림(EndingRule).
    /// 규칙 판정은 Core(GridMap·Gaze·EndingRule)에 있고, 이 클래스는 순서와 연출을 맡는다.
    /// </summary>
    public class HouseFlow : MonoBehaviour
    {
        [Header("지도")]
        [SerializeField, TextArea(6, 40)] string mapText;
        [SerializeField] string blockingChars = "";

        [Header("참조")]
        [SerializeField] HousePlayer player;
        [SerializeField] HouseChild child;
        [SerializeField] HouseNpc watcher;
        [SerializeField] HouseUI ui;
        [SerializeField] HouseCamera cam;
        [SerializeField] HouseProp[] props;
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioSource ambience;
        [SerializeField] Color watcherFace = new Color(0.82f, 0.74f, 0.66f);
        [SerializeField] Color watcherAccent = new Color(0.55f, 0.12f, 0.12f);

        [Header("조정값")]
        [SerializeField] int hearingRange = 14;          // 아이가 소리를 듣는 거리(벽을 돌아가는 걸음 수)
        [SerializeField] float childStepsPerSecond = 2f;
        [SerializeField] int noticeNeeded = 3;           // 첫날: 아이가 알아챈 물건 수
        [SerializeField] float knockFirstDelay = 4f;
        [SerializeField] float knockGap = 5f;            // 둘째 날: 노크 사이 간격
        [SerializeField] float chaseBonusPerOpen = 0.25f;  // 둘째 날 아이가 연 횟수마다 사흘째 그것이 빨라짐
        [SerializeField] float chaseStepsPerSecond = 2.4f;
        [SerializeField] float stunAfterReturn = 2.5f;   // 문안으로 돌아온 직후 그것이 멈추는 시간
        [SerializeField] float fireSeconds = 180f;       // 반전 후 송별불이 꺼지기까지
        [SerializeField] float cupNudge = 0.22f;

        static readonly string[] KnockOrder = { "window_child", "window_altar", "door_front" };

        public HousePhase Phase { get; private set; } = HousePhase.Prologue;
        public bool PlayerCanAct => !ui.Busy &&
            (Phase == HousePhase.Day1 || Phase == HousePhase.Day2 || Phase == HousePhase.Day3
             || Phase == HousePhase.Outside || Phase == HousePhase.After);

        GridMap _map;
        readonly Dictionary<Cell, HouseProp> _propAt = new Dictionary<Cell, HouseProp>();
        readonly Dictionary<string, HouseProp> _byId = new Dictionary<string, HouseProp>();
        readonly HashSet<string> _noticed = new HashSet<string>();
        readonly HashSet<string> _told = new HashSet<string>();
        readonly Dictionary<HouseProp, bool> _doorOpen = new Dictionary<HouseProp, bool>();

        HouseProp _cup, _table, _altar, _fire, _lantern, _trunk, _doorFront, _doorBack;
        Cell _start, _day3Start, _seat, _fireSpot, _watcherWait, _exit, _kickout;
        int _stage = 1;
        bool _skip, _twist, _lanternLit, _fireLit, _photoFound, _childCalled;
        float _stun, _fireLeft;
        int _knockIndex, _opened;
        bool _knockActive;

        // ───────────────────────── 시작 ─────────────────────────

        void Start()
        {
            _map = new GridMap(mapText, blockingChars);
            foreach (var p in props)
            {
                if (p == null) continue;
                _propAt[p.Cell] = p;
                if (!string.IsNullOrEmpty(p.Id)) _byId[p.Id] = p;
            }
            _cup = Get("cup"); _table = Get("table"); _altar = Get("altar"); _fire = Get("fire");
            _lantern = Get("lantern"); _trunk = Get("trunk"); _doorFront = Get("door_front"); _doorBack = Get("door_back");

            _map.TryMarker('P', out _start);
            _map.TryMarker('Q', out _day3Start);
            _map.TryMarker('F', out _seat);
            _map.TryMarker('G', out _fireSpot);
            _map.TryMarker('V', out _watcherWait);
            _map.TryMarker('X', out _exit);
            _map.TryMarker('Y', out _kickout);

            child.StepsPerSecond = childStepsPerSecond;
            child.SetSeat(_seat);
            if (cam != null) cam.SetBounds(_map.Width, _map.Height);
            if (ambience != null) { ambience.clip = HouseAudio.Crickets; ambience.loop = true; }
            StartCoroutine(Prologue());
        }

        HouseProp Get(string id) => _byId.TryGetValue(id, out var p) ? p : null;

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        // ───────────────────────── 오프닝 (대사 없음, E로 건너뛰기) ─────────────────────────

        IEnumerator Prologue()
        {
            Phase = HousePhase.Prologue;
            _stage = 1;
            SetFire(false);
            SetLantern(false);
            if (_table != null) _table.SetVisualActive(false);       // 두 번째 찻잔은 아직 없다
            watcher.gameObject.SetActive(false);
            SetDoor(_doorFront, true);
            SetDoor(_doorBack, false);
            player.Bind(_map, _start, Dir4.Up);
            SetAlpha(player, 0f, 0.8f);
            child.Bind(_map, _fireSpot, Dir4.Right);
            child.SetScripted(true);
            if (cam != null) cam.Snap();
            if (ambience != null) ambience.Play();

            yield return ui.CaptionOnBlack("8월 13일 밤", 1.4f);
            yield return ui.FadeBlack(0f, 0.8f);
            yield return Wait(0.6f);
            if (!_skip) Play(Sfx.Ignite);
            SetFire(true);
            yield return Wait(1.0f);

            if (!_skip)
            {
                bool arrived = false;
                child.ScriptedWalk(_seat, () => arrived = true);
                while (!arrived && !_skip) yield return null;
            }
            if (child.Cell != _seat) child.Bind(_map, _seat, Dir4.Up);
            child.Face(Dir4.Up);
            child.SetScripted(false);
            StartCoroutine(CloseDoorWhenFree(_doorFront, 0f));

            for (float t = 0f; t < 1f; t += Time.deltaTime) { SetAlpha(player, t, 0.8f); yield return null; }
            SetAlpha(player, 1f, 0.8f);

            Phase = HousePhase.Day1;
            ui.SetGoal("아이에게 다녀갔음을 알리자");
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !_skip; t += Time.deltaTime) yield return null;
        }

        // ───────────────────────── 첫날: 알리기 ─────────────────────────

        void Notice(string id)
        {
            if (Phase != HousePhase.Day1) return;
            _noticed.Add(id);
        }

        void CheckDay1Done()
        {
            if (Phase == HousePhase.Day1 && _noticed.Count >= noticeNeeded) StartCoroutine(Day1Finale());
        }

        IEnumerator Day1Finale()
        {
            Phase = HousePhase.Cutscene;
            ui.SetGoal("");
            child.SetScripted(true);
            yield return new WaitForSeconds(0.8f);
            child.Face(Dir4.Left);
            if (_table != null) _table.SetVisualActive(true);        // 아이가 찻잔을 하나 더 내놓는다
            Play(Sfx.Cup);
            yield return new WaitForSeconds(0.9f);
            yield return SayAndWait("“…오셨어요?”");
            yield return new WaitForSeconds(0.8f);
            yield return ui.Caption("8월 14일 밤", SetupDay2);
            Phase = HousePhase.Day2;
            _knockIndex = 0;
            StartCoroutine(KnockLater(knockFirstDelay));
        }

        // ───────────────────────── 둘째 날: 지키기 ─────────────────────────

        void SetupDay2()
        {
            _stage = 2;
            _told.Clear();
            if (_cup != null) _cup.ResetVisual();
            player.Bind(_map, _start, Dir4.Up);
            child.Bind(_map, _seat, Dir4.Up);
            child.SetScripted(false);
            SetDoor(_doorFront, false);
            SetDoor(_doorBack, false);
            HideWatcher();
            if (cam != null) cam.Snap();
        }

        IEnumerator KnockLater(float delay)
        {
            yield return new WaitForSeconds(delay);
            while (ui.Busy) yield return null;
            if (Phase == HousePhase.Day2) StartKnock();
        }

        void StartKnock()
        {
            var target = Get(KnockOrder[_knockIndex]);
            if (target == null) return;
            _knockActive = true;

            // 그것이 바깥에 서서 아이를 들여다본다(나를 보는 게 아니다: 반전 복선)
            watcher.gameObject.SetActive(true);
            watcher.Bind(_map, OutsideOf(target.Cell), Dir4.Up);
            watcher.TrackActor(child);
            SetAlpha(watcher, 1f, 1f);

            Play(HouseAudio.Knock);
            target.Flash(1.4f);
            ui.SetGoal($"무언가 두드린다. 다른 곳에서 소리를 내 아이를 붙잡아 두자 ({_knockIndex + 1}/{KnockOrder.Length})");
            child.GoTo(target.Cell, () => StartCoroutine(KnockReached(target)));
        }

        IEnumerator KnockReached(HouseProp target)
        {
            if (!_knockActive || Phase != HousePhase.Day2) yield break;
            _knockActive = false;
            child.SetScripted(true);
            Play(HouseAudio.Wind);
            if (target.Kind == HousePropKind.Door) SetDoor(target, true);
            child.LookAtCell(target.Cell);
            // 아이는 아무것도 보지 못한다. 그것은 바로 앞에서 아이를 들여다보다가 물러난다.
            yield return new WaitForSeconds(1.8f);
            yield return FadeOutActor(watcher, 1.2f);
            if (target.Kind == HousePropKind.Door) StartCoroutine(CloseDoorWhenFree(target, 0.5f));
            _opened++;                                                  // 사흘째 그것이 그만큼 빨라진다
            child.SetScripted(false);
            child.ReturnToSeat(0.5f);
            NextKnock(false);
        }

        void KnockDiverted()
        {
            _knockActive = false;
            Play(HouseAudio.Wind);
            StartCoroutine(FadeOutActor(watcher, 1.5f));                 // 두드림이 멎고 형체가 물러난다
            NextKnock(true);
        }

        /// <summary>막았든 열렸든 다음 노크로 넘어간다. 결과는 목표 줄에 짧게 남긴다.</summary>
        void NextKnock(bool held)
        {
            _knockIndex++;
            string n = $"({_knockIndex}/{KnockOrder.Length})";
            ui.SetGoal(held ? "두드림이 멎었다 " + n : "아이가 열어 버렸다 " + n);
            if (_knockIndex >= KnockOrder.Length) StartCoroutine(Day2End());
            else StartCoroutine(KnockLater(knockGap));
        }

        IEnumerator Day2End()
        {
            Phase = HousePhase.Cutscene;
            ui.SetGoal("");
            yield return new WaitForSeconds(2.5f);
            yield return ui.Caption("8월 16일 저녁", SetupDay3);
            yield return Day3Opening();
        }

        // ───────────────────────── 사흘째: 쫓김 ─────────────────────────

        void SetupDay3()
        {
            _stage = 3;
            _told.Clear();
            if (_cup != null) _cup.ResetVisual();
            player.Bind(_map, _day3Start, Dir4.Down);
            child.Bind(_map, _seat, Dir4.Up);
            child.SetScripted(true);
            SetDoor(_doorFront, false);
            SetDoor(_doorBack, false);
            SetFire(false);
            HideWatcher();
            if (cam != null) cam.Snap();
        }

        IEnumerator Day3Opening()
        {
            Phase = HousePhase.Cutscene;
            // 아이가 송별불을 피우러 나간다
            Cell inside; int len;
            if (_map.TryNearestNeighbor(_doorFront.Cell, child.Cell, out inside, out len))
                yield return WalkChild(inside);
            SetDoor(_doorFront, true);
            yield return WalkChild(_fireSpot);
            child.Face(Dir4.Right);
            yield return new WaitForSeconds(0.6f);
            Play(Sfx.Ignite);
            SetFire(true);
            yield return new WaitForSeconds(0.8f);

            // 문이 열린 사이 그것이 들어온다. 벌레 소리가 끊긴다.
            if (ambience != null) ambience.Stop();
            watcher.gameObject.SetActive(true);
            watcher.Bind(_map, _watcherWait, Dir4.Up);
            for (float t = 0f; t < 1f; t += Time.deltaTime) { SetAlpha(watcher, t, 1f); yield return null; }
            watcher.TrackPlayer();
            watcher.StepsPerSecond = chaseStepsPerSecond + chaseBonusPerOpen * _opened;
            yield return new WaitForSeconds(0.6f);

            child.ScriptedWalk(_seat, () => { child.Face(Dir4.Up); child.SetScripted(false); });
            _stun = 1.5f;
            Phase = HousePhase.Day3;
            ui.SetGoal("불단에 인사를 남기자");
        }

        void UpdateChase()
        {
            if (_stun > 0f) { _stun -= Time.deltaTime; return; }
            if (watcher.IsMoving) return;
            if (Cell.Manhattan(watcher.Cell, player.Cell) == 1) { StartCoroutine(KickOut()); return; }
            if (_map.TryNextStep(watcher.Cell, player.Cell, out var next) && _map.IsWalkable(next))
                watcher.TryStepTo(next);
        }

        IEnumerator KickOut()
        {
            Phase = HousePhase.Cutscene;
            Play(HouseAudio.Wind);
            yield return ui.FadeBlack(1f, 0.4f);
            player.Bind(_map, _kickout, Dir4.Down);
            StartCoroutine(CloseDoorWhenFree(_doorFront, 0f));
            StartCoroutine(CloseDoorWhenFree(_doorBack, 0f));
            if (cam != null) cam.Snap();
            yield return new WaitForSeconds(0.3f);
            yield return ui.FadeBlack(0f, 0.6f);
            yield return SayAndWait("문밖으로 내쳐졌다.");
            Phase = HousePhase.Outside;
            ui.SetGoal("창문을 두드려 아이를 부르자");
        }

        void KnockFromOutside(HouseProp prop)
        {
            Play(HouseAudio.Knock);
            prop.Flash(1.2f);
            if (child.State == HouseChild.ChildState.Scripted) return;

            // 아이는 더 가까운 문으로 가서 연다
            var door = NearerDoor();
            if (door == null) return;
            child.GoTo(door.Cell, () => { SetDoor(door, true); child.ReturnToSeat(3f); });
        }

        HouseProp NearerDoor()
        {
            int a = int.MaxValue, b = int.MaxValue;
            if (_doorFront != null && _map.TryNearestNeighbor(_doorFront.Cell, child.Cell, out _, out int la)) a = la;
            if (_doorBack != null && _map.TryNearestNeighbor(_doorBack.Cell, child.Cell, out _, out int lb)) b = lb;
            if (a == int.MaxValue && b == int.MaxValue) return null;
            return a <= b ? _doorFront : _doorBack;
        }

        // ───────────────────────── 반전 ─────────────────────────

        IEnumerator Twist()
        {
            Phase = HousePhase.Cutscene;
            _twist = true;
            _stage = 4;
            _told.Clear();
            ui.SetGoal("");
            watcher.StopWalk();
            _altar.Upright();
            Play(Sfx.Photo);
            yield return new WaitForSeconds(0.5f);
            bool closed = false;
            ui.ShowPhoto(watcherFace, watcherAccent, "사진 속 얼굴은, 나를 쫓던 그 사람이었다.", () => closed = true);
            while (!closed) yield return null;
            yield return new WaitForSeconds(0.6f);

            // 그것은 더 쫓지 않는다. 송별불 곁으로 가서 기다린다.
            watcher.StepsPerSecond = 2f;
            watcher.WalkTo(_watcherWait, () => watcher.LookAtCell(_fire.Cell));
            if (!IsOpen(_doorFront)) SetDoor(_doorFront, true);
            if (!IsOpen(_doorBack)) SetDoor(_doorBack, true);           // 뒷마당 창고로 가는 길을 열어 둔다
            _fireLeft = fireSeconds;
            Phase = HousePhase.After;
            ui.SetGoal("송별불이 사그라든다. 뒷마당 창고를 살펴보자");
        }

        void UpdateFire()
        {
            if (_lanternLit || _childCalled) return;                    // 아이가 상자로 오기 시작하면 불은 더 줄지 않는다
            _fireLeft -= Time.deltaTime;
            if (_fire != null) _fire.SetVisualScale(Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(_fireLeft / fireSeconds)));
            if (_fireLeft > 0f) return;
            SetFire(false);
            StartCoroutine(PlayEnding(EndingRule.Decide(_twist, _lanternLit, _fireLit, false, false, true)));
        }

        IEnumerator FoundTrunk()
        {
            child.SetScripted(true);
            yield return new WaitForSeconds(0.8f);
            yield return SayAndWait("“…이 집에 살던 분인가.”");
            yield return new WaitForSeconds(0.4f);
            Cell stand; int len;
            if (_map.TryNearestNeighbor(_lantern.Cell, child.Cell, out stand, out len))
                yield return WalkChild(stand);
            child.LookAtCell(_lantern.Cell);
            yield return new WaitForSeconds(0.6f);
            Play(Sfx.Ignite);
            SetLantern(true);                                           // 낯선 망자를 위한 불
            yield return new WaitForSeconds(0.8f);
            yield return FadeOutActor(watcher, 1.6f);                   // 진짜 부모는 송별불을 건넌다
            ui.SetGoal("등롱 불을 따라가자");
        }

        IEnumerator StepAside()
        {
            Phase = HousePhase.Cutscene;
            ui.SetGoal("");
            yield return new WaitForSeconds(0.5f);
            yield return FadeOutActor(watcher, 1.6f);
            yield return new WaitForSeconds(0.8f);
            yield return PlayEnding(EndingRule.Decide(_twist, _lanternLit, _fireLit, false, true, false));
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        void Update()
        {
            var kb = Keyboard.current;
            if (Phase == HousePhase.Prologue && kb != null &&
                (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                _skip = true;

            ui.SetPrompt(PlayerCanAct ? PromptFor(player.Cell + Dir4Util.Delta(player.Facing)) : "");
            if (ui.Busy) return;
            if (Phase == HousePhase.Day3) UpdateChase();
            else if (Phase == HousePhase.After) UpdateFire();
        }

        // ───────────────────────── 조사·소리 ─────────────────────────

        public void Interact(Cell front)
        {
            if (!PlayerCanAct) return;

            if (Phase == HousePhase.After && !_lanternLit && watcher.gameObject.activeSelf && front == watcher.Cell)
            {
                ui.Choose("“그 불은, 내 아이가 켠 거야.”", "비켜선다", "그대로 있는다", pick =>
                {
                    if (pick == 0) StartCoroutine(StepAside());
                });
                return;
            }

            if (!_propAt.TryGetValue(front, out var prop)) return;
            switch (prop.Kind)
            {
                case HousePropKind.Cup:
                    if (Phase == HousePhase.Day1 && !_noticed.Contains("cup") && child.State == HouseChild.ChildState.Idle)
                    {
                        prop.Nudge(player.Facing, cupNudge);
                        Play(Sfx.Cup);
                        child.LookAtCell(prop.Cell);                     // 아이는 컵을 본다. 나를 보지 않는다.
                        Notice("cup");
                        StartCoroutine(FaceUpThenCheck(1.6f));
                        return;
                    }
                    ui.Say(prop.TextFor(_stage));
                    return;

                case HousePropKind.Photo:
                    if (Phase == HousePhase.Day3) { StartCoroutine(Twist()); return; }
                    ui.Say(_twist ? "바로 선 사진. 내 얼굴이 아니다." : prop.TextFor(_stage));
                    return;

                case HousePropKind.Fire:
                    ui.Say(!_fireLit ? "꺼진 불." : _stage >= 3 ? "송별불." : "마중불.");
                    return;

                case HousePropKind.Lantern:
                    ui.Say(_lanternLit ? "등롱에 불이 켜져 있다." : prop.TextFor(_stage));
                    return;

                case HousePropKind.Window:
                    if (Phase == HousePhase.Outside) { KnockFromOutside(prop); return; }
                    ui.Say(prop.TextFor(_stage));
                    return;

                case HousePropKind.Door:
                    if (IsOpen(prop)) { ui.Say("문이 열려 있다."); return; }
                    if (Phase == HousePhase.Outside) { KnockFromOutside(prop); return; }
                    MakeNoise(prop);
                    return;

                case HousePropKind.Trunk:
                    if (Phase == HousePhase.After && !_photoFound)
                    {
                        _photoFound = true;
                        ui.SayLines(new[]
                        {
                            "상자 안에 사진 한 장과 낡은 등롱이 있다.",
                            "어른 하나와 아이 하나. 어른의 얼굴만 지워져 있다.",
                            "……이 집은 원래, 내 집이었다."
                        }, () => CallChildToTrunk(prop));
                        return;
                    }
                    if (Phase == HousePhase.After) { CallChildToTrunk(prop); return; }
                    MakeNoise(prop);
                    return;

                case HousePropKind.Noise:
                    MakeNoise(prop);
                    return;

                default:
                    ui.Say(prop.TextFor(_stage));
                    return;
            }
        }

        /// <summary>
        /// 반전 후 상자를 조사하면 상자가 크게 울리고, 아이는 거리와 상관없이 그 소리를 따라 상자까지 온다(ADR 0006 쉬움 조정).
        /// 아이가 오기 시작하면 송별불은 더 줄지 않는다.
        /// </summary>
        void CallChildToTrunk(HouseProp trunk)
        {
            if (_childCalled) { ui.Say("아이가 이쪽으로 오고 있다."); return; }
            Play(HouseAudio.For(trunk.Noise));
            trunk.Shake();
            child.SetScripted(false);
            child.StepsPerSecond = 3f;
            if (!child.GoTo(trunk.Cell, () => OnChildArrived(trunk))) return;
            _childCalled = true;
            ui.SetGoal("아이가 상자를 찾을 때까지 기다리자");
        }

        string PromptFor(Cell front)
        {
            if (Phase == HousePhase.After && !_lanternLit && watcher.gameObject.activeSelf && front == watcher.Cell) return "E  다가가기";
            if (!_propAt.TryGetValue(front, out var p)) return "";
            switch (p.Kind)
            {
                case HousePropKind.Noise: return "E  소리 내기";
                case HousePropKind.Trunk: return Phase == HousePhase.After ? "E  열어 보기" : "E  소리 내기";
                case HousePropKind.Door: return IsOpen(p) ? "" : Phase == HousePhase.Outside ? "E  두드리기" : "E  문 흔들기";
                case HousePropKind.Window: return Phase == HousePhase.Outside ? "E  두드리기" : "E  살펴보기";
                case HousePropKind.Cup: return Phase == HousePhase.Day1 && !_noticed.Contains("cup") ? "E  찻잔 밀기" : "E  살펴보기";
                case HousePropKind.Photo: return Phase == HousePhase.Day3 ? "E  사진 세우기" : "E  살펴보기";
                default: return "E  살펴보기";
            }
        }

        IEnumerator FaceUpThenCheck(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (child.State == HouseChild.ChildState.Idle) child.Face(Dir4.Up);
            CheckDay1Done();
        }

        /// <summary>소리를 낸다. 처음 한 번은 조사 문장도 보여 준다. 아이가 들을 수 있으면 그 물건 쪽으로 온다.</summary>
        void MakeNoise(HouseProp prop)
        {
            Play(HouseAudio.For(prop.Noise));
            prop.Shake();
            string key = prop.Id + "@" + _stage;
            if (!_told.Contains(key)) { _told.Add(key); ui.Say(prop.TextFor(_stage)); }

            if (child.State == HouseChild.ChildState.Scripted || _childCalled) return;   // 상자로 오는 중에는 다른 소리에 끌리지 않는다
            if (Phase != HousePhase.Day1 && Phase != HousePhase.Day2 && Phase != HousePhase.Day3 && Phase != HousePhase.After) return;
            if (!child.CanHear(prop.Cell, hearingRange)) return;
            child.GoTo(prop.Cell, () => OnChildArrived(prop));
        }

        void OnChildArrived(HouseProp prop)
        {
            switch (Phase)
            {
                case HousePhase.Day1:
                    Notice(prop.Id);
                    if (prop.Kind == HousePropKind.Door) PeekDoor(prop);
                    child.ReturnToSeat(2f, CheckDay1Done);
                    return;

                case HousePhase.Day2:
                    if (prop.Kind == HousePropKind.Door) PeekDoor(prop);
                    if (_knockActive) KnockDiverted();                  // 노크 대신 내 소리 쪽으로 왔다
                    child.ReturnToSeat(2f);
                    return;

                case HousePhase.After:
                    if (prop.Kind == HousePropKind.Door) { SetDoor(prop, true); return; }   // 열어 둔 채 서 있다
                    if (prop.Kind == HousePropKind.Trunk) { StartCoroutine(FoundTrunk()); return; }
                    child.ReturnToSeat(15f);                            // 다음 소리를 낼 시간을 준다
                    return;

                default:
                    if (prop.Kind == HousePropKind.Door) PeekDoor(prop);
                    child.ReturnToSeat(2f);
                    return;
            }
        }

        /// <summary>아이가 문을 열어 밖을 보고, 잠시 뒤 닫는다.</summary>
        void PeekDoor(HouseProp door)
        {
            SetDoor(door, true);
            Play(HouseAudio.Wind);
            StartCoroutine(CloseDoorWhenFree(door, 2f));
        }

        public void OnPlayerStepped(Cell c)
        {
            if (Phase == HousePhase.Outside && IsDoorCell(c))
            {
                Phase = HousePhase.Day3;                                 // 다시 집 안으로
                _stun = stunAfterReturn;
                ui.SetGoal("불단에 인사를 남기자");
                if (IsOpen(_doorBack)) StartCoroutine(CloseDoorWhenFree(_doorBack, 2f));
                if (!IsOpen(_doorFront)) SetDoor(_doorFront, true);      // 송별불로 가는 문은 열려 있어야 한다
                return;
            }
            if (c != _exit) return;
            if (Phase == HousePhase.After)
            {
                var e = EndingRule.Decide(_twist, _lanternLit, _fireLit, true, false, false);
                if (e != Ending.None) StartCoroutine(PlayEnding(e));
                return;
            }
            if (Phase == HousePhase.Day3 || Phase == HousePhase.Outside) ui.Say("인사도 없이 갈 수는 없다.");
        }

        // ───────────────────────── 결말 (한 화면 문구) ─────────────────────────

        IEnumerator PlayEnding(Ending e)
        {
            if (Phase == HousePhase.Ending || e == Ending.None) yield break;
            Phase = HousePhase.Ending;
            child.StopWalk();
            watcher.StopWalk();
            ui.SetGoal("");
            if (e == Ending.Farewell || e == Ending.StolenFire)
                for (float t = 0f; t < 1.2f; t += Time.deltaTime) { SetAlpha(player, 1f - t / 1.2f, 0.8f); yield return null; }
            else yield return new WaitForSeconds(0.6f);
            yield return ui.FadeBlack(1f, 1.2f);
            switch (e)
            {
                case Ending.Farewell: ui.ShowEnding("배웅", "이듬해 오봉, 그 집 현관에는 불이 둘 켜졌다."); break;
                case Ending.StolenFire: ui.ShowEnding("빼앗은 불", "불은 하나였다."); break;
                case Ending.GaveWay: ui.ShowEnding("남은 자리", "내년에도 불은 켜진다. 나를 위한 불은 아니다."); break;
                case Ending.FireOut: ui.ShowEnding("꺼진 불", "불이 꺼진 뒤, 그 집 현관에는 기다리는 이가 둘이 되었다."); break;
            }
        }

        // ───────────────────────── 도우미 ─────────────────────────

        IEnumerator WalkChild(Cell goal)
        {
            bool done = false;
            child.ScriptedWalk(goal, () => done = true);
            while (!done) yield return null;
        }

        IEnumerator SayAndWait(string text)
        {
            bool closed = false;
            ui.Say(text, () => closed = true);
            while (!closed) yield return null;
        }

        void SetDoor(HouseProp door, bool open)
        {
            if (door == null) return;
            _doorOpen[door] = open;
            _map.SetBlocked(door.Cell, !open);
            door.SetVisualActive(!open);
        }

        bool IsOpen(HouseProp door) => door != null && _doorOpen.TryGetValue(door, out var o) && o;

        bool IsDoorCell(Cell c) => (_doorFront != null && c == _doorFront.Cell) || (_doorBack != null && c == _doorBack.Cell);

        /// <summary>문 칸에 아무도 서 있지 않을 때 닫는다(서 있는 칸을 막으면 지도가 어긋난다).</summary>
        IEnumerator CloseDoorWhenFree(HouseProp door, float delay)
        {
            if (door == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            while (Occupied(door.Cell)) yield return null;
            SetDoor(door, false);
        }

        bool Occupied(Cell c) =>
            player.Cell == c || child.Cell == c || (watcher.gameObject.activeSelf && watcher.Cell == c);

        /// <summary>창·문 바로 바깥(마당 쪽) 칸.</summary>
        Cell OutsideOf(Cell c)
        {
            for (int i = 0; i < 4; i++)
            {
                var n = c + Dir4Util.Delta((Dir4)i);
                if (_map.TileAt(n) == ',' && _map.IsWalkable(n)) return n;
            }
            return _watcherWait;
        }

        void HideWatcher()
        {
            if (!watcher.gameObject.activeSelf) return;
            watcher.Leave();
            watcher.gameObject.SetActive(false);
        }

        IEnumerator FadeOutActor(HouseActor a, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime) { SetAlpha(a, 1f - t / duration, 1f); yield return null; }
            a.Leave();
            a.gameObject.SetActive(false);
        }

        void SetFire(bool lit)
        {
            _fireLit = lit;
            if (_fire == null) return;
            _fire.SetVisualActive(lit);
            _fire.SetLight(lit);
            if (lit) _fire.SetVisualScale(1f);
        }

        void SetLantern(bool lit)
        {
            _lanternLit = lit;
            if (_lantern == null) return;
            _lantern.SetVisualActive(lit);
            _lantern.SetLight(lit);
        }

        static void SetAlpha(Component root, float a, float max)
        {
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>())
            {
                var c = r.color; c.a = a * max; r.color = c;
            }
        }

        void Play(AudioClip clip)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip);
        }
    }
}
