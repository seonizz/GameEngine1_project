#!/usr/bin/env bash
# 마중불 검증 스크립트 — Unity 없이 돌리는 확인
# =================================================
#
# 실행(저장소 루트 어디서든):
#   bash tools/verify/verify.sh
#
# 필요한 것: Mono C# 컴파일러와 런타임(mcs, mono). Ubuntu·WSL·Debian 기준 설치:
#   sudo apt-get install -y mono-mcs
# (mono-mcs 가 mono 런타임을 함께 설치한다. macOS 는 `brew install mono`.)
# python3 가 있으면 지도 생성기 비교도 한다(없으면 그 항목만 건너뛴다).
# Windows 에서는 WSL 안에서 실행한다. Git Bash 에는 mono 가 없다.
#
# 하는 일(순서대로, 앞 단계가 실패해도 가능한 단계는 계속한다):
#   1. Core 컴파일      Assets/Scripts/Core/*.cs → Majungbul.Core (UnityEngine 미참조)
#   2. Core 테스트      Assets/Tests/EditMode/CoreTests.cs 를 NUnitShim.cs(최소 NUnit 대체)로 실행
#   3. House 타입 검사  Assets/Scripts/House/*.cs 를 UnityStubs.cs(Unity API 스텁)에 대고 컴파일
#   4. 빌더 타입 검사   Assets/Editor/HouseSceneBuilder.cs 를 같은 스텁에 대고 컴파일
#   5. 지도 검사        MapChecks.cs: 빌더의 지도·소품 표와 HouseFlow 조정값을 읽어 도달 가능성·첫날 소리·노크 확인
#   6. 지도 생성기 비교 tools/mapgen/gen.py --check (빌더의 지도가 생성기 출력과 같은가)
# 마지막에 항목별 통과/실패 요약을 출력하고, 하나라도 실패하면 종료 코드 1로 끝난다.
#
# 이 스크립트가 보장하지 않는 것:
#   - Unity Test Runner 결과를 대신하지 않는다. 테스트는 진짜 NUnit 이 아니라 NUnitShim 으로 돈다.
#   - 3·4번은 "스텁에 대한" 타입 검사다. 스텁에 있는 이름만 확인하므로 Unity에서 컴파일·동작한다는 보장이 아니다.
#   - mcs 는 C# 7.x 까지만 안다. Unity 6 은 C# 9 를 쓰므로, House 코드에 C# 8 이상 문법(??=, switch 식,
#     using 선언 등)이 들어오면 Unity에선 문제없어도 3번이 실패한다. 그때는 실패 메시지를 보고 판단한다.
#
# 빌드 산출물은 tools/verify/bin/ 에 만들어지고 git 에서 무시된다.
# 저장소에 이 파일의 실행 권한이 기록되지 않을 수 있으므로(core.filemode=false) 항상 `bash` 로 실행한다.

set -u
export LANG=C.UTF-8 LC_ALL=C.UTF-8

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
OUT="$HERE/bin"
cd "$ROOT" || exit 2

for tool in mcs mono; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "오류: '$tool' 이 없다. 설치: sudo apt-get install -y mono-mcs" >&2
        exit 2
    fi
done

rm -rf "$OUT"
mkdir -p "$OUT"

declare -a NAMES=() RESULTS=()
FAILED=0

record() {   # record <이름> <통과|실패|건너뜀>
    NAMES+=("$1"); RESULTS+=("$2")
    [ "$2" = "실패" ] && FAILED=1
    return 0
}

# compile <이름> <mcs 인자...>: 성공하면 경고 수만, 실패하면 컴파일러 출력 전체를 보인다.
compile() {
    local name="$1"; shift
    local log="$OUT/$name.log"
    if mcs -nologo "$@" >"$log" 2>&1; then
        local warns
        warns=$(grep -c 'warning CS' "$log" || true)
        echo "  컴파일 성공 (경고 ${warns}개, 전체 출력: tools/verify/bin/$name.log)"
        return 0
    fi
    sed 's/^/    /' "$log"
    echo "  컴파일 실패"
    return 1
}

shopt -s nullglob
CORE_SRC=(Assets/Scripts/Core/*.cs)
HOUSE_SRC=(Assets/Scripts/House/*.cs)
shopt -u nullglob

echo "== 1. Core 컴파일 (${#CORE_SRC[@]}개 파일) =="
CORE_OK=0
if [ ${#CORE_SRC[@]} -gt 0 ] && compile core -target:library -out:"$OUT/Majungbul.Core.dll" "${CORE_SRC[@]}"; then
    CORE_OK=1; record "Core 컴파일" "통과"
else
    record "Core 컴파일" "실패"
fi

echo "== 2. Core 테스트 (CoreTests.cs, NUnitShim) =="
if [ $CORE_OK -eq 1 ]; then
    TEST_COUNT=$(grep -c '\[Test\]' Assets/Tests/EditMode/CoreTests.cs || true)
    if compile tests -r:"$OUT/Majungbul.Core.dll" -out:"$OUT/CoreTests.exe" \
            Assets/Tests/EditMode/CoreTests.cs "$HERE/NUnitShim.cs" \
       && (cd "$OUT" && mono CoreTests.exe "$TEST_COUNT"); then
        record "Core 테스트" "통과"
    else
        record "Core 테스트" "실패"
    fi
else
    echo "  Core 컴파일 실패로 건너뜀"; record "Core 테스트" "실패"
fi

echo "== 3. House 타입 검사 (${#HOUSE_SRC[@]}개 파일, Unity 스텁) =="
STUB_OK=0; HOUSE_OK=0
if compile stubs -target:library -out:"$OUT/UnityStubs.dll" "$HERE/UnityStubs.cs"; then STUB_OK=1; fi
if [ $CORE_OK -eq 1 ] && [ $STUB_OK -eq 1 ] && [ ${#HOUSE_SRC[@]} -gt 0 ] \
   && compile house -target:library -r:"$OUT/UnityStubs.dll" -r:"$OUT/Majungbul.Core.dll" \
          -out:"$OUT/Majungbul.House.dll" "${HOUSE_SRC[@]}"; then
    HOUSE_OK=1; record "House 타입 검사" "통과"
else
    record "House 타입 검사" "실패"
fi

echo "== 4. 씬 빌더 타입 검사 (HouseSceneBuilder.cs, Unity 스텁) =="
BUILDER_OK=0
if [ $HOUSE_OK -eq 1 ] \
   && compile builder -target:library -r:"$OUT/UnityStubs.dll" -r:"$OUT/Majungbul.Core.dll" -r:"$OUT/Majungbul.House.dll" \
          -out:"$OUT/HouseSceneBuilder.dll" Assets/Editor/HouseSceneBuilder.cs; then
    BUILDER_OK=1; record "빌더 타입 검사" "통과"
else
    [ $HOUSE_OK -eq 1 ] || echo "  House 타입 검사 실패로 건너뜀"
    record "빌더 타입 검사" "실패"
fi

echo "== 5. 지도 검사 (MapChecks.cs) =="
if [ $BUILDER_OK -eq 1 ] \
   && compile mapchecks -r:"$OUT/UnityStubs.dll" -r:"$OUT/Majungbul.Core.dll" -r:"$OUT/Majungbul.House.dll" \
          -r:"$OUT/HouseSceneBuilder.dll" -out:"$OUT/MapChecks.exe" "$HERE/MapChecks.cs" \
   && (cd "$OUT" && mono MapChecks.exe); then
    record "지도 검사" "통과"
else
    [ $BUILDER_OK -eq 1 ] || echo "  빌더 타입 검사 실패로 건너뜀"
    record "지도 검사" "실패"
fi

echo "== 6. 지도 생성기 비교 (tools/mapgen/gen.py --check) =="
if command -v python3 >/dev/null 2>&1; then
    if python3 "$ROOT/tools/mapgen/gen.py" --check | sed 's/^/  /'; [ "${PIPESTATUS[0]}" -eq 0 ]; then
        record "지도 생성기 비교" "통과"
    else
        record "지도 생성기 비교" "실패"
    fi
else
    echo "  python3 이 없어 건너뜀"; record "지도 생성기 비교" "건너뜀"
fi

echo
echo "== 요약 =="
for i in "${!NAMES[@]}"; do
    printf '  %-6s %s\n' "${RESULTS[$i]}" "${NAMES[$i]}"
done
if [ $FAILED -eq 0 ]; then
    echo "결과: 통과"
    exit 0
fi
echo "결과: 실패 (위 출력에서 '실패' 항목을 확인)"
exit 1
