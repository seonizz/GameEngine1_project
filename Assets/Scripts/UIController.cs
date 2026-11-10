using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Majungbul
{
    /// <summary>
    /// 목표·E 안내·안내서·손잡기 선택창·일시정지·결과·자막.
    /// Canvas는 실행 시 코드로 만든다(프로토타입 단계에서 씬 편집 없이 문구·배치를 바꾸기 위함).
    /// 조작을 막는 창은 한 번에 하나만 연다. 창이 열려 있는 동안 Time.timeScale = 0.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        static readonly Color PanelColor = new Color(0.93f, 0.91f, 0.86f, 0.97f);
        static readonly Color InkColor = new Color(0.16f, 0.14f, 0.13f);
        static readonly Color ButtonColor = new Color(0.84f, 0.81f, 0.75f);
        static readonly Color ButtonSelected = new Color(0.98f, 0.82f, 0.55f);
        static readonly Color HudInk = new Color(0.95f, 0.93f, 0.88f);

        GameFlow _flow;
        PlayerController _player;
        Camera _cam;
        Font _font;

        RectTransform _root;
        Text _phaseText, _goalText, _promptText, _toastText, _debugText;
        GameObject _hud, _titlePanel, _guidePanel, _choicePanel, _pausePanel, _resultPanel;
        Image _captionOverlay;
        Text _captionTitle, _captionBody;
        Text _choiceQuote, _choiceQuestion, _resultTitle, _resultBody;
        Image _choiceNoBg, _choiceYesBg;
        Coroutine _toastRoutine;

        int _choiceIndex;          // 0 = 잡지 않는다, 1 = 손을 잡는다
        bool _choiceArmed;         // 창을 연 E 입력은 확정에 재사용하지 않는다

        public bool ChoiceOpen => _choicePanel != null && _choicePanel.activeSelf;
        public bool IsModalOpen =>
            ChoiceOpen
            || (_guidePanel != null && _guidePanel.activeSelf)
            || (_pausePanel != null && _pausePanel.activeSelf);

        // ───────────────────────── 생성 ─────────────────────────

        public void Init(GameFlow flow, PlayerController player)
        {
            _flow = flow;
            _player = player;
            _cam = Camera.main;
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Arial" }, 32);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureEventSystem();
            BuildCanvas();
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            _root = canvasGo.GetComponent<RectTransform>();

            // HUD
            _hud = Node("HUD", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var topBar = Node("TopBar", _hud.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -78), Vector2.zero);
            AddImage(topBar, new Color(0, 0, 0, 0.45f));
            _phaseText = Label(topBar.transform, "", 24, TextAnchor.UpperLeft, HudInk, FontStyle.Bold,
                new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(24, 0), new Vector2(0, -10));
            _goalText = Label(topBar.transform, "", 18, TextAnchor.LowerLeft, HudInk, FontStyle.Normal,
                new Vector2(0, 0), new Vector2(0.8f, 1), new Vector2(24, 10), new Vector2(0, 0));
            Label(topBar.transform, "Tab 안내    Esc 일시정지", 16, TextAnchor.UpperRight, HudInk, FontStyle.Normal,
                new Vector2(0.6f, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-24, -12));

            Label(_hud.transform, "A / D 또는 ← / → 이동\n상호작용할 수 있는 물건 가까이에서 E", 15, TextAnchor.LowerLeft,
                new Color(0.9f, 0.88f, 0.84f, 0.85f), FontStyle.Normal,
                new Vector2(0, 0), new Vector2(0.6f, 0), new Vector2(24, 14), new Vector2(0, 70));

            var prompt = Node("Prompt", _hud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-170, -18), new Vector2(170, 18));
            AddImage(prompt, new Color(1f, 0.97f, 0.88f, 0.92f)).raycastTarget = false;
            _promptHolder = prompt.GetComponent<RectTransform>();
            _promptText = Label(prompt.transform, "", 18, TextAnchor.MiddleCenter, InkColor, FontStyle.Bold,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _toastText = Label(_hud.transform, "", 20, TextAnchor.MiddleCenter, HudInk, FontStyle.Normal,
                new Vector2(0.15f, 0), new Vector2(0.85f, 0), new Vector2(0, 90), new Vector2(0, 140));
            _toastText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);

            _debugText = Label(_hud.transform, "", 14, TextAnchor.UpperRight, new Color(0.6f, 1f, 0.6f), FontStyle.Normal,
                new Vector2(0.45f, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(-20, -84));

            // 자막 오버레이 (날짜 전환)
            var cap = Node("Caption", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _captionOverlay = AddImage(cap, new Color(0.03f, 0.03f, 0.04f, 0f));
            _captionOverlay.raycastTarget = false;
            _captionTitle = Label(cap.transform, "", 44, TextAnchor.MiddleCenter, HudInk, FontStyle.Bold,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(0, 70));
            _captionBody = Label(cap.transform, "", 20, TextAnchor.MiddleCenter, HudInk, FontStyle.Normal,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -60), new Vector2(0, -5));
            SetCaptionAlpha(0f);

            BuildTitle();
            BuildGuide();
            BuildChoice();
            BuildPause();
            BuildResult();

            _hud.SetActive(false);
            _promptHolder.gameObject.SetActive(false);
        }

        RectTransform _promptHolder;

        void BuildTitle()
        {
            _titlePanel = Node("Title", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(_titlePanel, new Color(0.05f, 0.05f, 0.06f, 0.82f));
            var card = Card(_titlePanel.transform, new Vector2(460, 420));
            Label(card.transform, "마중불", 52, TextAnchor.MiddleCenter, InkColor, FontStyle.Bold,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -110), new Vector2(0, -30));
            Label(card.transform, "물건으로 마지막 인사를 남기고,\n송별불을 따라 돌아오십시오.", 19, TextAnchor.MiddleCenter, InkColor, FontStyle.Normal,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -200), new Vector2(-20, -120));
            Label(card.transform, "A / D 이동    E 상호작용\nTab 안내    Esc 일시정지", 16, TextAnchor.MiddleCenter, new Color(0.35f, 0.32f, 0.3f), FontStyle.Normal,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -270), new Vector2(-20, -205));
            MakeButton(card.transform, "시작하기", new Vector2(0, -150), new Vector2(240, 54), () => _flow.StartGame());
        }

        void BuildGuide()
        {
            _guidePanel = Node("Guide", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(_guidePanel, new Color(0, 0, 0, 0.55f));
            var card = Card(_guidePanel.transform, new Vector2(620, 380));
            Label(card.transform, "귀성 안내서", 30, TextAnchor.MiddleCenter, InkColor, FontStyle.Bold,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(0, -24));
            Label(card.transform,
                "하나. 산 사람은 당신을 볼 수 없습니다.\n\n" +
                "둘. 당신을 바라보는 이가 손을 내밀어도 잡지 마십시오.\n\n" +
                "셋. 인사를 남기고 가족이 지키는 송별불을 따라 돌아오십시오.",
                19, TextAnchor.UpperLeft, InkColor, FontStyle.Normal,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(44, 60), new Vector2(-44, -100));
            Label(card.transform, "Tab 또는 Esc로 닫기", 14, TextAnchor.MiddleCenter, new Color(0.4f, 0.37f, 0.34f), FontStyle.Normal,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 16), new Vector2(0, 46));
            _guidePanel.SetActive(false);
        }

        void BuildChoice()
        {
            _choicePanel = Node("Choice", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(_choicePanel, new Color(0, 0, 0, 0.5f));
            var card = Card(_choicePanel.transform, new Vector2(460, 360));
            _choiceQuote = Label(card.transform, "", 26, TextAnchor.MiddleCenter, InkColor, FontStyle.Bold,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(0, -30));
            _choiceQuestion = Label(card.transform, "", 19, TextAnchor.MiddleCenter, InkColor, FontStyle.Normal,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -100));
            _choiceNoBg = MakeButton(card.transform, "잡지 않는다", new Vector2(0, -10), new Vector2(300, 52), () => ConfirmChoice(0));
            _choiceYesBg = MakeButton(card.transform, "손을 잡는다", new Vector2(0, -76), new Vector2(300, 52), () => ConfirmChoice(1));
            Label(card.transform, "A/D·↑/↓ 선택   E 확정   Esc 취소", 13, TextAnchor.MiddleCenter, new Color(0.4f, 0.37f, 0.34f), FontStyle.Normal,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(0, 36));
            _choicePanel.SetActive(false);
        }

        void BuildPause()
        {
            _pausePanel = Node("Pause", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(_pausePanel, new Color(0, 0, 0, 0.6f));
            var card = Card(_pausePanel.transform, new Vector2(360, 340));
            Label(card.transform, "일시정지", 30, TextAnchor.MiddleCenter, InkColor, FontStyle.Bold,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(0, -24));
            MakeButton(card.transform, "계속하기", new Vector2(0, 30), new Vector2(240, 50), ClosePause);
            MakeButton(card.transform, "처음으로", new Vector2(0, -32), new Vector2(240, 50), () => _flow.Restart());
            MakeButton(card.transform, "종료", new Vector2(0, -94), new Vector2(240, 50), () => _flow.Quit());
            _pausePanel.SetActive(false);
        }

        void BuildResult()
        {
            _resultPanel = Node("Result", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(_resultPanel, new Color(0.03f, 0.03f, 0.04f, 0.75f));
            var card = Card(_resultPanel.transform, new Vector2(460, 420));
            _resultTitle = Label(card.transform, "", 40, TextAnchor.MiddleCenter, InkColor, FontStyle.Bold,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(0, -26));
            _resultBody = Label(card.transform, "", 18, TextAnchor.UpperCenter, InkColor, FontStyle.Normal,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 100), new Vector2(-30, -110));
            MakeButton(card.transform, "처음으로", new Vector2(-80, -165), new Vector2(170, 50), () => _flow.Restart());
            MakeButton(card.transform, "종료", new Vector2(110, -165), new Vector2(110, 50), () => _flow.Quit());
            _resultPanel.SetActive(false);
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        void Update()
        {
            if (_flow == null) return;
            var kb = Keyboard.current;
            if (kb != null) HandleKeys(kb);
            UpdatePrompt();
            if (!_flow.DebugVisible && _debugText.text.Length > 0) _debugText.text = "";
        }

        void HandleKeys(Keyboard kb)
        {
            var phase = _flow.Phase;
            bool inPlay = phase != GamePhase.Title && phase != GamePhase.Success && phase != GamePhase.Failure;

            if (phase == GamePhase.Title && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                _flow.StartGame();
                return;
            }
            if (!inPlay) return;

            if (ChoiceOpen)
            {
                if (kb.escapeKey.wasPressedThisFrame) { ConfirmChoice(0, cancelled: true); return; }
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame ||
                    kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                    SetChoiceIndex(0);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                    kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                    SetChoiceIndex(1);

                if (!_choiceArmed)
                {
                    if (!kb.eKey.isPressed) _choiceArmed = true;   // 키를 뗀 뒤부터 새 입력
                }
                else if (kb.eKey.wasPressedThisFrame)
                {
                    ConfirmChoice(_choiceIndex);
                }
                return;
            }

            if (_pausePanel.activeSelf)
            {
                if (kb.escapeKey.wasPressedThisFrame) ClosePause();
                return;
            }

            if (_guidePanel.activeSelf)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) SetModal(_guidePanel, false);
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame) { SetModal(_pausePanel, true); return; }
            if (kb.tabKey.wasPressedThisFrame) SetModal(_guidePanel, true);
        }

        void UpdatePrompt()
        {
            var current = _player != null ? _player.Current : null;
            bool show = current != null && !_flow.InputLocked;
            if (_promptHolder.gameObject.activeSelf != show) _promptHolder.gameObject.SetActive(show);
            if (!show || _cam == null) return;

            _promptText.text = $"E  {_flow.VerbFor(current)}";
            Vector3 screen = _cam.WorldToScreenPoint(current.PromptPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local))
            {
                float w = Mathf.Max(140f, _promptText.preferredWidth + 28f);
                _promptHolder.anchorMin = _promptHolder.anchorMax = new Vector2(0.5f, 0.5f);
                _promptHolder.sizeDelta = new Vector2(w, 36f);
                _promptHolder.anchoredPosition = local;
            }
        }

        // ───────────────────────── GameFlow가 호출 ─────────────────────────

        public void ShowTitle()
        {
            _titlePanel.SetActive(true);
            _hud.SetActive(false);
            ClearSelection();
        }

        public void HideTitle()
        {
            _titlePanel.SetActive(false);
        }

        public void SetPhase(string title, string goal)
        {
            _hud.SetActive(true);
            _phaseText.text = title;
            SetGoal(goal);
        }

        public void SetGoal(string goal)
        {
            _goalText.text = string.IsNullOrEmpty(goal) ? "" : $"목표  {goal}";
        }

        public void SetDebug(string s) => _debugText.text = s;

        public void Toast(string message, float seconds = 3f)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message, seconds));
        }

        IEnumerator ToastRoutine(string message, float seconds)
        {
            _toastText.text = message;
            _toastText.color = HudInk;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                var c = HudInk; c.a = 1f - t / 0.5f; _toastText.color = c;
                yield return null;
            }
            _toastText.text = "";
        }

        /// <summary>검게 페이드 → 자막 → (화면이 가려진 동안 midAction) → 페이드 아웃. 일시정지 시 함께 멈춘다.</summary>
        public IEnumerator Caption(string title, string body, Action midAction)
        {
            _captionTitle.text = title;
            _captionBody.text = body;
            yield return FadeCaption(0f, 1f, 0.7f);
            midAction?.Invoke();
            yield return new WaitForSeconds(1.8f);
            yield return FadeCaption(1f, 0f, 0.7f);
        }

        IEnumerator FadeCaption(float from, float to, float dur)
        {
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                SetCaptionAlpha(Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            SetCaptionAlpha(to);
        }

        void SetCaptionAlpha(float a)
        {
            var c = _captionOverlay.color; c.a = a * 0.96f; _captionOverlay.color = c;
            var t1 = _captionTitle.color; t1.a = a; _captionTitle.color = t1;
            var t2 = _captionBody.color; t2.a = a; _captionBody.color = t2;
        }

        public void OpenChoice(string quote, string question)
        {
            if (IsModalOpen) return;
            _choiceQuote.text = quote;
            _choiceQuestion.text = question;
            _choiceArmed = false;
            SetChoiceIndex(0);                 // 처음에는 거절 쪽을 선택
            SetModal(_choicePanel, true);
        }

        void SetChoiceIndex(int i)
        {
            _choiceIndex = i;
            _choiceNoBg.color = i == 0 ? ButtonSelected : ButtonColor;
            _choiceYesBg.color = i == 1 ? ButtonSelected : ButtonColor;
        }

        void ConfirmChoice(int index, bool cancelled = false)
        {
            if (!ChoiceOpen) return;
            SetModal(_choicePanel, false);
            _flow.OnChoiceResolved(!cancelled && index == 1);
        }

        void ClosePause() => SetModal(_pausePanel, false);

        public void ShowResult(string title, string body)
        {
            _guidePanel.SetActive(false);
            _choicePanel.SetActive(false);
            _pausePanel.SetActive(false);
            Time.timeScale = 1f;
            _resultTitle.text = title;
            _resultBody.text = body;
            _resultPanel.SetActive(true);
            _promptHolder.gameObject.SetActive(false);
            ClearSelection();
        }

        void SetModal(GameObject panel, bool open)
        {
            panel.SetActive(open);
            Time.timeScale = IsModalOpen ? 0f : 1f;
            ClearSelection();
        }

        static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // ───────────────────────── UI 헬퍼 ─────────────────────────

        static GameObject Node(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = oMin; rt.offsetMax = oMax;
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        GameObject Card(Transform parent, Vector2 size)
        {
            var go = Node("Card", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -size / 2f, size / 2f);
            AddImage(go, PanelColor);
            go.AddComponent<Outline>().effectColor = new Color(0.3f, 0.27f, 0.24f, 0.8f);
            return go;
        }

        Text Label(Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style,
                   Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = Node("Text", parent, aMin, aMax, oMin, oMax);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Image MakeButton(Transform parent, string label, Vector2 center, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = Node("Button_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                center - size / 2f, center + size / 2f);
            var img = AddImage(go, ButtonColor);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.8f, 0.75f, 0.65f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            Label(go.transform, label, 20, TextAnchor.MiddleCenter, InkColor, FontStyle.Normal,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return img;
        }
    }
}
