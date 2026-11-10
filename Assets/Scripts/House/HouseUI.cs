using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Majungbul.House
{
    /// <summary>
    /// 쯔꾸르식 아래쪽 메시지 창, 두 갈래 선택창, 검은 화면 자막, 결말 화면, 목표 한 줄.
    /// Canvas는 실행 시 코드로 만든다. 시간 정지(timeScale)는 쓰지 않고 Busy로 조작을 막는다.
    /// 창을 연 키 입력은 닫기·확정에 재사용하지 않는다(키를 한 번 뗀 뒤부터 받는다).
    /// </summary>
    public class HouseUI : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.92f, 0.90f, 0.86f);
        static readonly Color Dim = new Color(0.92f, 0.90f, 0.86f, 0.45f);
        static readonly Color Select = new Color(0.98f, 0.80f, 0.55f);

        Font _font;
        GameObject _msgBox, _choiceBox, _ending, _photo;
        Image _photoFace, _photoBody;
        Text _promptText, _msgText, _goalText, _captionText, _choiceQuote, _endTitle, _endLine;
        Text[] _choiceItems = new Text[2];
        Image _black;

        bool _armed;
        int _choice;
        Action _onClose;
        Action<int> _onPick;
        int _closedFrame = -1;

        public bool MessageOpen => _msgBox != null && _msgBox.activeSelf;
        public bool ChoiceOpen => _choiceBox != null && _choiceBox.activeSelf;
        /// <summary>창이 열려 있거나 이번 프레임에 닫혔으면 true(닫는 키가 조사로 이어지지 않게).</summary>
        public bool Busy => MessageOpen || ChoiceOpen || Time.frameCount == _closedFrame;

        void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Arial" }, 32);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            _goalText = Label(root, "", 18, TextAnchor.UpperLeft, Dim, new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(24, -60), new Vector2(0, -18));
            Label(root, "방향키 이동   Shift 달리기   E 조사   R 처음부터", 13, TextAnchor.UpperRight, new Color(1, 1, 1, 0.25f),
                new Vector2(0.4f, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(-20, -16));

            // 사진 클로즈업(반전 장면). 회색 박스 단계라 액자·인물을 사각형으로 그린다.
            _photo = Node(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-130, -110), new Vector2(130, 230));
            _photo.AddComponent<Image>().color = new Color(0.12f, 0.09f, 0.07f, 1f);
            var paper = Node(_photo.transform, Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));
            paper.AddComponent<Image>().color = new Color(0.78f, 0.74f, 0.66f, 1f);
            var shoulders = Node(paper.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-70, 0), new Vector2(70, 110));
            _photoBody = shoulders.AddComponent<Image>();
            var face = Node(paper.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-45, 115), new Vector2(45, 215));
            _photoFace = face.AddComponent<Image>();
            _photo.SetActive(false);

            // 앞 칸 안내(E로 할 수 있는 일)
            _promptText = Label(root, "", 18, TextAnchor.LowerCenter, Ink, new Vector2(0.3f, 0), new Vector2(0.7f, 0), new Vector2(0, 30), new Vector2(0, 60));
            _promptText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);

            // 메시지 창
            _msgBox = Panel(root, new Vector2(0.08f, 0), new Vector2(0.92f, 0), new Vector2(0, 24), new Vector2(0, 150));
            _msgText = Label(_msgBox.transform, "", 24, TextAnchor.UpperLeft, Ink, Vector2.zero, Vector2.one, new Vector2(28, 18), new Vector2(-28, -18));
            Label(_msgBox.transform, "▼", 16, TextAnchor.LowerRight, Dim, Vector2.zero, Vector2.one, new Vector2(0, 10), new Vector2(-16, 0));
            _msgBox.SetActive(false);

            // 선택창
            _choiceBox = Panel(root, new Vector2(0.08f, 0), new Vector2(0.92f, 0), new Vector2(0, 24), new Vector2(0, 190));
            _choiceQuote = Label(_choiceBox.transform, "", 24, TextAnchor.UpperLeft, Ink, Vector2.zero, Vector2.one, new Vector2(28, 18), new Vector2(-28, -18));
            for (int i = 0; i < 2; i++)
                _choiceItems[i] = Label(_choiceBox.transform, "", 22, TextAnchor.UpperLeft, Ink,
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 70 - i * 38), new Vector2(-28, 104 - i * 38));
            _choiceBox.SetActive(false);

            // 검은 화면 + 자막
            var blackGo = Node(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _black = blackGo.AddComponent<Image>();
            _black.color = new Color(0, 0, 0, 0);
            _black.raycastTarget = false;
            _captionText = Label(blackGo.transform, "", 34, TextAnchor.MiddleCenter, new Color(Ink.r, Ink.g, Ink.b, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 결말
            _ending = Node(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _ending.AddComponent<Image>().color = Color.black;
            _endTitle = Label(_ending.transform, "", 44, TextAnchor.MiddleCenter, Ink, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 30), new Vector2(0, 100));
            _endLine = Label(_ending.transform, "", 22, TextAnchor.MiddleCenter, Ink, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -40), new Vector2(0, 20));
            Label(_ending.transform, "R  처음부터", 16, TextAnchor.MiddleCenter, Dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40), new Vector2(0, 80));
            _ending.SetActive(false);
        }

        // ───────────────────────── 공개 API ─────────────────────────

        public void SetGoal(string goal) => _goalText.text = goal ?? "";

        public void SetPrompt(string text)
        {
            if (_promptText == null) return;
            _promptText.text = (MessageOpen || ChoiceOpen) ? "" : (text ?? "");
        }

        public void Say(string text, Action onClose = null)
        {
            if (string.IsNullOrEmpty(text)) { onClose?.Invoke(); return; }
            _msgText.text = text;
            _onClose = onClose;
            _armed = false;
            _msgBox.SetActive(true);
        }

        /// <summary>여러 줄을 차례로 보여 준다(E로 넘김).</summary>
        public void SayLines(string[] lines, Action onClose = null) => SayFrom(lines, 0, onClose);

        void SayFrom(string[] lines, int i, Action onClose)
        {
            if (lines == null || i >= lines.Length) { onClose?.Invoke(); return; }
            Say(lines[i], () => SayFrom(lines, i + 1, onClose));
        }

        /// <summary>사진 클로즈업과 문장을 함께 보여 주고, 닫으면 사진도 닫는다.</summary>
        public void ShowPhoto(Color faceColor, Color bodyColor, string text, Action onClose = null)
        {
            _photoFace.color = faceColor;
            _photoBody.color = bodyColor;
            _photo.SetActive(true);
            Say(text, () => { _photo.SetActive(false); onClose?.Invoke(); });
        }

        public void Choose(string quote, string a, string b, Action<int> onPick)
        {
            _choiceQuote.text = quote;
            _choiceItems[0].text = a;
            _choiceItems[1].text = b;
            _choice = 0;                                   // 기본값은 거절 쪽
            _onPick = onPick;
            _armed = false;
            RefreshChoice();
            _choiceBox.SetActive(true);
        }

        public void SetBlack(float alpha)
        {
            var c = _black.color; c.a = alpha; _black.color = c;
        }

        public IEnumerator FadeBlack(float to, float duration)
        {
            float from = _black.color.a;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetBlack(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetBlack(to);
        }

        /// <summary>검게 → 자막 → (가려진 동안 mid) → 밝게. 전체 약 2.2초.</summary>
        public IEnumerator Caption(string text, Action mid)
        {
            _captionText.text = text;
            yield return FadeBlack(1f, 0.5f);
            yield return FadeCaption(1f, 0.3f);
            mid?.Invoke();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return FadeCaption(0f, 0.3f);
            yield return FadeBlack(0f, 0.5f);
        }

        /// <summary>이미 검은 화면에서 시작하는 자막(오프닝용).</summary>
        public IEnumerator CaptionOnBlack(string text, float hold)
        {
            SetBlack(1f);
            _captionText.text = text;
            yield return FadeCaption(1f, 0.4f);
            yield return new WaitForSecondsRealtime(hold);
            yield return FadeCaption(0f, 0.4f);
        }

        public void ShowEnding(string title, string line)
        {
            _msgBox.SetActive(false);
            _choiceBox.SetActive(false);
            SetGoal("");
            _endTitle.text = title;
            _endLine.text = line;
            _ending.SetActive(true);
        }

        // ───────────────────────── 입력 ─────────────────────────

        void Update()
        {
            if (!MessageOpen && !ChoiceOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            bool confirmHeld = kb.eKey.isPressed || kb.spaceKey.isPressed || kb.enterKey.isPressed;
            bool confirm = kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
            if (!_armed) { if (!confirmHeld) _armed = true; return; }

            if (MessageOpen)
            {
                if (!confirm) return;
                _msgBox.SetActive(false);
                _closedFrame = Time.frameCount;
                var cb = _onClose; _onClose = null;
                cb?.Invoke();
                return;
            }

            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { _choice = 0; RefreshChoice(); }
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { _choice = 1; RefreshChoice(); }
            if (kb.escapeKey.wasPressedThisFrame) { _choice = 0; confirm = true; }   // Esc = 거절
            if (!confirm) return;

            _choiceBox.SetActive(false);
            _closedFrame = Time.frameCount;
            var pick = _onPick; _onPick = null;
            pick?.Invoke(_choice);
        }

        void RefreshChoice()
        {
            for (int i = 0; i < 2; i++)
            {
                _choiceItems[i].color = i == _choice ? Select : Dim;
                var s = _choiceItems[i].text.TrimStart('▶', ' ');
                _choiceItems[i].text = (i == _choice ? "▶ " : "   ") + s;
            }
        }

        IEnumerator FadeCaption(float to, float duration)
        {
            float from = _captionText.color.a;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                var c = _captionText.color; c.a = Mathf.Lerp(from, to, t / duration); _captionText.color = c;
                yield return null;
            }
            var e = _captionText.color; e.a = to; _captionText.color = e;
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        static GameObject Node(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = oMin; rt.offsetMax = oMax;
            return go;
        }

        static GameObject Panel(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = Node(parent, aMin, aMax, oMin, oMax);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.03f, 0.03f, 0.05f, 0.88f);
            img.raycastTarget = false;
            go.AddComponent<Outline>().effectColor = new Color(0.85f, 0.82f, 0.75f, 0.6f);
            return go;
        }

        Text Label(Transform parent, string text, int size, TextAnchor align, Color color,
                   Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = Node(parent, aMin, aMax, oMin, oMax);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
