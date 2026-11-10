# Majungbul — 마중불

게임엔진1(김정호 교수님) 개인 과제. Unity 2D 민속 공포 어드벤처 「마중불」. 최종 제출은 16주차(12월 중순): Windows 실행 파일, Unity 프로젝트, 한 판 진행 영상, 상세 GDD, 제작 기록.

이 폴더(`GameEngine1/Majungbul`)가 저장소 루트이자 Unity 프로젝트 루트다. 바깥 `GameEngine1` 폴더의 주차 실습 프로젝트는 이 과제와 무관하며 읽거나 수정하지 않는다.

## 문서
- `docs/gdd-v1.md` — 10/30 제출한 중간 GDD 원문. 수정하지 않는다(변경 이력의 기준점).
- `docs/story.md` — 확정된 서사와 서사-규칙 대응표. 서사는 목표와 규칙 이해에 필요한 범위만 둔다.
- `docs/gdd-v2.md` — 최종 상세 GDD의 원본(작성 예정). 규칙이 바뀌면 이 파일, 관련 ADR, 변경 이력을 함께 갱신한다.
- `docs/course-requirements.md` — 수업 8주차 GDD 가이드의 필수 항목·평가 기준과 현재 충족 상태.
- `docs/improvement-candidates.md` — 고도화 후보와 결정 상태. `docs/roadmap.md` — 수업 주차에 맞춘 일정.
- `docs/adr/` — 결정 기록(변경 이력의 근거). `docs/research/` — 출처가 달린 조사 결과.
- `GLOSSARY.md` — 용어집(그릴링에서 생성). 코드 이름은 여기 용어를 따른다.

## 환경
- Unity 6000.6.0f1, URP 2D Renderer, Input System 1.20, Unity Test Framework 1.8.
- Windows PC. 기준 해상도 1280×720. 탑다운 4방향 격자 이동, 집 한 채·방 여러 개(ADR 0005).
- 네임스페이스 `Majungbul`. 내부 용어 "기다리는 이"의 코드 이름은 `Watcher`.

## 현재 코드 상태
- `Assets/Scripts/`에 v1 GDD 기준 프로토타입이 있다(GameFlow, ActorReaction, InteractableProp, UIController 등). `Assets/Editor/MajungbulSceneBuilder.cs`의 메뉴 `Majungbul ▸ Build Prototype Scene`이 임시 도형 씬을 생성한다.
- v2 규칙(같은 얼굴 두 명, 옛 사진·등롱, 배웅·남겨짐 결말)은 아직 반영되지 않았다.
- 탑다운 프로토타입(ADR 0005): 규칙 `Assets/Scripts/Core/`(GridMap·Dir4·Gaze·HoldTimer), 런타임 `Assets/Scripts/House/`, 메뉴 `Majungbul ▸ Build House Prototype` → `Assets/Scenes/House.unity`. 지도와 조사 문장은 `Assets/Editor/HouseSceneBuilder.cs`에 있다. 측면 프로토타입(Mukaebi)은 참고용으로 남겨 둔다.

## 코드 구조 규칙
- 새로 추가하거나 고치는 게임 규칙(단계 전환, 관찰 구역 판정, 시선 대상, 입력 우선순위, 결말 판정)은 `Assets/Scripts/Core/`의 순수 C#(UnityEngine 미참조 asmdef)로 옮겨 가며 작성한다. GameFlow는 점진적으로 Core에 위임하는 어댑터가 된다. 한 번에 전면 재작성하지 않는다.
- MonoBehaviour는 입력 전달과 표현(스프라이트·소리·UI)만 담당한다.
- 시간 의존 로직은 `Tick(dt, ...)`로 받고, 모달 UI가 열린 동안에는 진행하지 않는다. UI 연출은 `Time.unscaledDeltaTime`을 쓴다.
- 조정값은 Inspector에 노출한다(하드코딩 금지).

## 사람이 하는 작업
- `.unity`, `.prefab`, `.asset`, `.meta`를 텍스트로 직접 편집하지 않는다. 씬 구성은 씬 빌더 Editor 스크립트를 고치거나, 사용자가 Editor에서 한다.
- 새 컴포넌트를 만들면 "Editor에서 할 일"(붙일 오브젝트, 연결할 필드)을 작업 끝에 적는다.
- 외부 에셋은 `docs/research/assets.md`에 제작자·원문 URL·이용 조건을 기록한 뒤에만 커밋한다.

## 테스트
- EditMode 테스트: `Assets/Tests/EditMode/` (Core asmdef 참조, Editor 전용 asmdef).
- 배치 실행(Editor가 이 프로젝트를 열고 있으면 실패하므로 먼저 닫는다. Unity 설치 경로는 Unity Hub에서 확인해 맞출 것):
  ```
  "C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -
  ```
  `-quit`은 붙이지 않는다. Editor를 닫을 수 없으면 사용자에게 Test Runner 결과를 요청한다.
- `docs/gdd-v2.md` 검증 기준 표의 각 행은 테스트 또는 수동 확인 절차에 대응하고, 상태를 "예정 / 확인 완료 / 수정 필요"로 표시한다. 하지 않은 테스트를 완료로 쓰지 않는다.

## 작업 방식
- 이슈 트래커: GitHub Issues (저장소 GameEngine1).
- 티켓 하나 = 커밋 하나. 커밋 전 EditMode 테스트 통과.
- 범위 밖: 마을 탐색, 2층·여러 스테이지, 인벤토리·물건 운반, 상점, 저장, 자유 물리 조작, 회상 장면, 안내서 위조, 멀티플레이. 한 집 안의 여러 방과 기다리는 이 1명의 격자 추적은 허용한다(ADR 0005).
- AI 활용은 제작 기록에 "도구 / 사용 범위 / 직접 수정·검증한 내용"으로 남긴다(수업 요구).
- 답변과 문서는 한국어로 쓴다.
