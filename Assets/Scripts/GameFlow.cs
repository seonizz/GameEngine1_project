using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Majungbul
{
    /// <summary>
    /// 진행 관리자. 현재 단계, 관찰 구역 기록, 인사·재점화 여부를 들고
    /// 전환·방해·결과를 '한 번만' 실행한다. 다른 스크립트는 요청만 보낸다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerController player;
        [SerializeField] ActorReaction family;
        [SerializeField] ActorReaction watcher;
        [SerializeField] InteractableProp cup;
        [SerializeField] InteractableProp photo;
        [SerializeField] InteractableProp windChime;
        [SerializeField] InteractableProp watcherHand;
        [SerializeField] FireView fire;
        [SerializeField] ExitZone exitZone;
        [SerializeField] UIController ui;
        [SerializeField] AudioSource sfx;
        [SerializeField] GameObject debugMarkers;

        [Header("Layout (x, 유닛)")]
        [SerializeField] float playerStartX = 13.2f;
        [SerializeField] float familyDay1X = 5.0f;
        [SerializeField] float familyDay2X = 5.0f;
        [SerializeField] float watcherX = 9.0f;
        [SerializeField] float familyDestX = 13.0f;
        [SerializeField] float leftZoneX = 7.8f;
        [SerializeField] float rightZoneX = 10.2f;
        [SerializeField] float zoneWidth = 0.8f;

        [Header("Tuning")]
        [SerializeField] float cupMoveDistance = 0.18f;
        [SerializeField] float observeHold = 1.0f;
        [SerializeField] float hintDelay = 15f;

        public GamePhase Phase { get; private set; } = GamePhase.Title;

        bool _cutscene;
        bool _resolved;
        bool _cupUsed;
        readonly bool[] _zoneDone = new bool[2];
        readonly float[] _zoneTimer = new float[2];
        bool _greeted, _chimed, _relit, _familyArrived;
        float _idle;
        bool _hintShown;

        public bool InputLocked =>
            _cutscene || _resolved
            || Phase == GamePhase.Title || Phase == GamePhase.Transition
            || Phase == GamePhase.Success || Phase == GamePhase.Failure
            || (ui != null && ui.IsModalOpen);

        public bool DebugVisible => debugMarkers != null && debugMarkers.activeSelf;

        // ───────────────────────── 시작 / 재시작 ─────────────────────────

        void Start()
        {
            Time.timeScale = 1f;
            ui.Init(this, player);
            if (debugMarkers != null) debugMarkers.SetActive(false);
            SetupDay1Stage();
            Phase = GamePhase.Title;
            ui.ShowTitle();
        }

        /// <summary>"처음으로": 일시정지를 풀고 현재 씬을 다시 불러온다.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void StartGame()
        {
            if (Phase != GamePhase.Title) return;
            ui.HideTitle();
            StartCoroutine(StartRoutine());
        }

        IEnumerator StartRoutine()
        {
            Phase = GamePhase.Transition;
            yield return ui.Caption("8월 13일 밤", "", null);
            EnterReturn();
        }

        void SetupDay1Stage()
        {
            player.Teleport(playerStartX);
            player.SetFacing(false);
            cup.ResetState();
            photo.ResetState();
            windChime.ResetState();
            fire.SetLit(true);
            family.Place(familyDay1X);
            family.Face(true);           // 현관(마중불) 쪽을 바라보며 기다린다
            watcher.gameObject.SetActive(false);
            watcher.ShowHand(false);
            watcher.ShowBubble(false);
        }

        // ───────────────────────── 단계 진입 ─────────────────────────

        void EnterReturn()
        {
            Phase = GamePhase.Return;
            ResetObservation();
            ui.SetPhase("8월 13일 밤", "컵을 움직여 다녀갔음을 알리자");
            ResetIdle();
        }

        IEnumerator TransitionToDay2()
        {
            _cutscene = true;
            yield return new WaitForSeconds(0.8f);
            Phase = GamePhase.Transition;
            yield return ui.Caption("8월 16일 저녁", "", SetupDay2Stage);
            _cutscene = false;
            EnterAnomaly();
        }

        void SetupDay2Stage()
        {
            player.Teleport(playerStartX);
            player.SetFacing(false);
            cup.ResetState();
            family.Place(familyDay2X);
            // ADR 0004: 두 사람 모두 현관 쪽을 보고 시작한다. 컵을 움직이는 순간 둘이 함께
            // 컵 쪽으로 돌아서고, 차이는 플레이어가 기다리는 이의 오른쪽에 섰을 때만 드러난다.
            family.Face(true);
            watcher.gameObject.SetActive(true);
            watcher.Place(watcherX);
            watcher.Face(true);
            watcher.ShowHand(false);
            watcher.ShowBubble(false);
            fire.SetLit(true);            // 송별불
        }

        void EnterAnomaly()
        {
            Phase = GamePhase.Anomaly;
            ResetObservation();
            ui.SetPhase("8월 16일 저녁", "오늘도 컵을 움직이자");
            ResetIdle();
        }

        IEnumerator WatcherDousesFire()
        {
            _cutscene = true;
            ui.SetGoal("");
            yield return new WaitForSeconds(0.6f);
            watcher.ShowBubble(false);
            watcher.Face(fire.transform.position.x > watcher.X);
            watcher.ShowHand(true);
            yield return new WaitForSeconds(0.9f);
            Play(Sfx.Douse);
            fire.SetLit(false);           // 설명 문구 없이 소화음과 불 그림 변화로만 전달 (ADR 0004-5)
            yield return new WaitForSeconds(1.2f);
            watcher.TrackPlayer();
            _cutscene = false;
            EnterFarewell();
        }

        void EnterFarewell()
        {
            Phase = GamePhase.Farewell;
            _greeted = _chimed = _relit = _familyArrived = false;
            ui.SetPhase("사흘째 밤 · 송별", "사진으로 인사를 남기고, 가족에게 신호를 보내자");
            ResetIdle();
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame && debugMarkers != null)
                debugMarkers.SetActive(!debugMarkers.activeSelf);

            if (_resolved) return;

            if (!_cutscene && (Phase == GamePhase.Return || Phase == GamePhase.Anomaly) && _cupUsed)
                UpdateObservation();

            if (Phase == GamePhase.Farewell && !_cutscene)
                TrySucceed();

            if (Phase == GamePhase.Return || Phase == GamePhase.Anomaly || Phase == GamePhase.Farewell)
            {
                if (!_cutscene)
                {
                    _idle += Time.deltaTime;
                    if (!_hintShown && _idle >= hintDelay)
                    {
                        _hintShown = true;
                        ui.Toast(CurrentHint(), 5f);
                    }
                }
            }

            if (DebugVisible) ui.SetDebug(DebugString());
        }

        void UpdateObservation()
        {
            int zones = Phase == GamePhase.Return ? 1 : 2;
            for (int i = 0; i < zones; i++)
            {
                if (_zoneDone[i]) continue;
                float cx = i == 0 ? leftZoneX : rightZoneX;
                bool inside = Mathf.Abs(player.X - cx) <= zoneWidth * 0.5f;
                if (!inside) { _zoneTimer[i] = 0f; continue; }   // 완료 전 벗어나면 다시 센다

                _zoneTimer[i] += Time.deltaTime;
                if (_zoneTimer[i] >= observeHold)
                {
                    _zoneDone[i] = true;
                    OnZoneCompleted(i);
                }
            }
        }

        void OnZoneCompleted(int index)
        {
            ResetIdle();
            // ADR 0004-5: 관찰 결과(누가 무엇을 보는지)는 글로 알려 주지 않는다. 화면의 시선으로만 읽힌다.
            if (Phase == GamePhase.Return)
            {
                StartCoroutine(TransitionToDay2());
                return;
            }

            // Anomaly: 두 구역을 모두 채우면 불이 꺼진다. 한 구역만 채웠을 때는 아무것도 알리지 않는다.
            if (_zoneDone[0] && _zoneDone[1])
                StartCoroutine(WatcherDousesFire());
        }

        void ResetObservation()
        {
            _cupUsed = false;
            for (int i = 0; i < 2; i++) { _zoneDone[i] = false; _zoneTimer[i] = 0f; }
        }

        void ResetIdle()
        {
            _idle = 0f;
            _hintShown = false;
        }

        string CurrentHint()
        {
            switch (Phase)
            {
                case GamePhase.Return:
                    return !_cupUsed ? "도움말: 불단 옆 탁자의 컵 가까이에서 E를 눌러 보자."
                                     : "도움말: 가족의 오른쪽, 조금 떨어진 곳에 잠시 서서 반응을 보자.";
                case GamePhase.Anomaly:
                    return !_cupUsed ? "도움말: 오늘도 컵을 움직여 보자."
                                     : "도움말: 두 사람 사이에, 그리고 현관 가까이에 각각 잠시 멈춰 서 보자.";
                case GamePhase.Farewell:
                    if (!_greeted) return "도움말: 불단의 기울어진 사진을 바로 세워 인사를 남기자.";
                    if (!_chimed) return "도움말: 현관의 풍경을 울려 가족을 부르자.";
                    return "도움말: 가족이 불을 밝히면 문턱으로 돌아가자.";
            }
            return "";
        }

        // ───────────────────────── 상호작용 요청 ─────────────────────────

        public bool IsPropAvailable(InteractableProp prop)
        {
            if (prop == null || _resolved || _cutscene) return false;
            switch (prop.Kind)
            {
                case PropKind.Cup:
                    return (Phase == GamePhase.Return || Phase == GamePhase.Anomaly) && !_cupUsed;
                case PropKind.Photo:
                    return Phase == GamePhase.Farewell && !_greeted;
                case PropKind.WindChime:
                    return Phase == GamePhase.Farewell && !_chimed;
                case PropKind.WatcherHand:
                    return Phase == GamePhase.Farewell && watcher.gameObject.activeSelf;
            }
            return false;
        }

        public string VerbFor(InteractableProp prop)
        {
            switch (prop.Kind)
            {
                case PropKind.Cup: return "컵 건드리기";
                case PropKind.Photo: return "사진 세우기";
                case PropKind.WindChime: return "풍경 흔들기";
                case PropKind.WatcherHand: return "손을 내민 사람 살펴보기";
            }
            return "";
        }

        public void RequestInteract(InteractableProp prop)
        {
            if (InputLocked || !IsPropAvailable(prop)) return;

            switch (prop.Kind)
            {
                case PropKind.Cup:
                    _cupUsed = true;                       // 기록 먼저
                    cup.ApplyCupNudge(cupMoveDistance);
                    Play(Sfx.Cup);
                    family.LookAt(cup.transform);
                    if (Phase == GamePhase.Return)
                    {
                        ui.SetGoal("가족의 반대편에서 지켜보자");
                    }
                    else
                    {
                        watcher.TrackPlayer();
                        ui.SetGoal("두 사람을 지켜보자");
                    }
                    ResetIdle();
                    break;

                case PropKind.Photo:
                    _greeted = true;
                    photo.ApplyPhotoUpright();
                    Play(Sfx.Photo);
                    family.LookAt(photo.transform);
                    ui.Toast("사진을 바로 세웠다. 가족이 사진 쪽을 바라본다.");
                    ui.SetGoal("현관의 풍경을 울려 가족에게 신호를 보내자");
                    ResetIdle();
                    break;

                case PropKind.WindChime:
                    if (!_greeted)
                    {
                        ui.Toast("떠나기 전에 사진으로 인사를 남기자.");   // 사용 기회 소모 안 함
                        return;
                    }
                    _chimed = true;
                    windChime.ApplyChimeSwing();
                    Play(Sfx.Chime);
                    ui.SetGoal("가족이 불을 다시 밝힐 때까지 기다리자");
                    StartCoroutine(FamilyRespondsToChime());
                    ResetIdle();
                    break;

                case PropKind.WatcherHand:
                    ui.OpenChoice("“이번에는 가지 마.”", "이 사람의 손을 잡을까요?");
                    break;
            }
        }

        IEnumerator FamilyRespondsToChime()
        {
            yield return new WaitForSeconds(1.0f);   // 종소리를 끝까지 들은 뒤 반응
            family.MoveTo(familyDestX, OnFamilyArrived);
        }

        void OnFamilyArrived()
        {
            if (_resolved) return;
            _familyArrived = true;
            family.Face(fire.transform.position.x > family.X);
            fire.SetLit(true);
            _relit = true;
            Play(Sfx.Ignite);
            ui.Toast("가족이 송별불을 다시 밝혔다.");
            ui.SetGoal("불을 따라 문턱 밖으로 돌아가자");
            ResetIdle();
            TrySucceed();     // 출구에서 먼저 기다린 경우도 여기서 다시 판정
        }

        public void OnExitZoneEntered()
        {
            if (Phase != GamePhase.Farewell || _resolved) return;
            if (!_relit) ui.Toast("돌아갈 불이 꺼져 있다.");
            TrySucceed();
        }

        /// <summary>선택창 결과. takeHand=false 는 '잡지 않는다' 또는 Esc 취소.</summary>
        public void OnChoiceResolved(bool takeHand)
        {
            if (_resolved) return;
            if (takeHand) Resolve(false);                    // 실패 우선
            else ui.Toast("손을 잡지 않았다. 저 사람은 계속 나를 본다.");
        }

        void TrySucceed()
        {
            if (_resolved || Phase != GamePhase.Farewell) return;
            if (ui.IsModalOpen) return;                      // 선택창 열려 있으면 귀환 요청 무시
            if (_greeted && _relit && _familyArrived && exitZone.PlayerInside)
                Resolve(true);
        }

        void Resolve(bool success)
        {
            if (_resolved) return;                           // 결과는 한 번만
            _resolved = true;
            StopAllCoroutines();
            family.StopMove();
            Phase = success ? GamePhase.Success : GamePhase.Failure;
            ui.SetGoal("");
            StartCoroutine(ResultRoutine(success));
        }

        IEnumerator ResultRoutine(bool success)
        {
            if (success)
            {
                // 주인공 소멸과 배웅
                var renderers = player.GetComponentsInChildren<SpriteRenderer>();
                var baseColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
                for (float t = 0; t < 1.5f; t += Time.deltaTime)
                {
                    float a = 1f - t / 1.5f;
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        var c = baseColors[i]; c.a *= a; renderers[i].color = c;
                    }
                    yield return null;
                }
                foreach (var r in renderers) r.enabled = false;
                ui.ShowResult("귀환", "가족이 지키는 불을 따라\n돌아왔습니다.\n\n사진은 바로 서 있고,\n가족은 불 곁에 남아 있습니다.");
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
                ui.ShowResult("남겨짐", "낯익은 손을 잡았습니다.\n\n송별불은 다시 켜지지 않고,\n당신은 이 집에 남겨졌습니다.\n\n(실패 원인: 가짜의 손을 잡았다)");
            }
        }

        void Play(AudioClip clip)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip);
        }

        string DebugString()
        {
            return $"[F1 debug] phase={Phase} cut={_cutscene} cup={_cupUsed}\n" +
                   $"zoneL {(_zoneDone[0] ? "done" : _zoneTimer[0].ToString("0.00"))}  " +
                   $"zoneR {(_zoneDone[1] ? "done" : _zoneTimer[1].ToString("0.00"))}\n" +
                   $"greet={_greeted} chime={_chimed} relit={_relit} arrived={_familyArrived} exit={exitZone.PlayerInside}\n" +
                   $"playerX={player.X:0.00} idle={_idle:0.0}";
        }
    }
}
