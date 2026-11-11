#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
마중불 House 지도 생성기
========================

Assets/Editor/HouseSceneBuilder.cs 의 `const string Map` (48x32 문자 지도)을 만드는 스크립트.
지도를 고칠 때는 빌더의 문자열을 손으로 고치지 말고 이 파일의 좌표를 고친 뒤 다시 생성한다.
(Python 3 표준 라이브러리만 쓴다. 저장소 루트에서 실행한다.)

사용법
------
  python3 tools/mapgen/gen.py            # 지도를 그대로 출력(눈으로 확인용)
  python3 tools/mapgen/gen.py --cs       # 빌더에 붙여 넣을 C# 문자열 줄을 출력
  python3 tools/mapgen/gen.py --check    # 빌더에 들어 있는 지도와 같은지 비교(다르면 차이를 보이고 종료 코드 1)

빌더에 반영하는 순서
--------------------
  1. 이 파일의 put/fill/rect_wall 좌표를 고친다. 좌표는 (x, r) = (열, 행)이고 행 0이 맨 위다.
     (Core.GridMap 은 y가 위로 커지므로 GridMap 좌표로는 y = 31 - r 이다.)
  2. `python3 tools/mapgen/gen.py --cs` 출력 전체(32줄)를 복사해
     HouseSceneBuilder.cs 의 `const string Map =` 다음 줄부터 마지막 `...\n";` 줄까지를 통째로 바꾼다.
  3. 새 문자를 가구로 추가했다면 같은 파일의 `Blocking` 문자열과 `Props` 표에도 넣는다.
  4. `python3 tools/mapgen/gen.py --check` 로 붙여 넣은 결과가 생성기와 같은지 확인하고,
     `bash tools/verify/verify.sh` 로 Core 테스트·컴파일·지도 검사를 돌린다.
  5. Unity Editor에서 메뉴 `Majungbul ▸ Build House Prototype` 을 실행해 Assets/Scenes/House.unity 를
     다시 만든다. 씬의 HouseFlow.mapText 는 빌드할 때 복사되므로, 이 단계를 빼면 게임은 옛 지도로 돈다.

문자 뜻(빌더 주석과 같다)
-------------------------
  # 벽   . 집 바닥   , 마당   D 현관문   E 뒷문   w 창문   S 창고 건물
  P 첫 등장   Q 사흘째 시작   F 아이 자리   G 불 피우는 자리   V 그것이 서는 자리
  Y 내쳐지는 자리   X 대문(출구)   O 송별불   L 등롱   그 밖의 소문자·T·C·A 는 가구(Props 표)
"""
import argparse
import os
import re
import sys

W, H = 48, 32


def build():
    g = [[','] * W for _ in range(H)]

    def put(x, r, ch):
        g[r][x] = ch

    def rect_wall(x0, r0, x1, r1):
        for x in range(x0, x1 + 1):
            put(x, r0, '#'); put(x, r1, '#')
        for r in range(r0, r1 + 1):
            put(x0, r, '#'); put(x1, r, '#')

    def fill(x0, r0, x1, r1, ch):
        for r in range(r0, r1 + 1):
            for x in range(x0, x1 + 1):
                put(x, r, ch)

    # 울타리
    rect_wall(0, 0, W - 1, H - 1)
    # 집 외벽과 내부 바닥
    rect_wall(6, 6, 41, 25); fill(7, 7, 40, 24, '.')
    # 북쪽 방 / 복도 / 남쪽 방 구분 벽
    for x in range(7, 41):
        put(x, 14, '#'); put(x, 17, '#')
    for r in range(7, 14):
        put(15, r, '#'); put(21, r, '#'); put(31, r, '#')   # 안방|욕실|부엌|자녀방
    for r in range(18, 25):
        put(17, r, '#'); put(31, r, '#'); put(37, r, '#')   # 불단방|거실|현관|다용도실
    # 방문(벽 틈)
    for x in (10, 18, 26, 36):
        put(x, 14, '.')
    for x in (12, 24, 34, 39):
        put(x, 17, '.')
    put(17, 21, '.'); put(31, 21, '.')                      # 불단방-거실, 거실-현관
    # 문(닫힐 수 있음)과 창문
    put(34, 25, 'D'); put(26, 6, 'E')
    for (x, r) in [(10, 6), (36, 6), (6, 21), (41, 10), (41, 21), (24, 25)]:
        put(x, r, 'w')
    # 창고(뒷마당)
    fill(3, 1, 8, 3, 'S'); put(6, 4, 't')                    # t: 창고 앞 상자
    # 앞마당: 불, 아이가 서는 칸, 등롱, 출구(대문)
    put(34, 28, 'O'); put(33, 28, 'G'); put(36, 28, 'L'); put(34, 31, 'X')
    put(20, 3, 'Y')                                          # Y: 내쳐졌을 때 서는 뒷마당 칸
    put(16, 4, 'a')                                          # a: 뒷마당 빈 양동이(소리)
    put(35, 27, 'V')                                         # V: 문밖 그것의 대기 칸(앞마당)
    # 인물
    put(34, 23, 'P')                                         # P: 주인공 등장(현관 안)
    put(25, 21, 'F')                                         # F: 아이 자리(거실)
    put(20, 16, 'Q')                                         # Q: 사흘째 시작 칸(복도)
    # 가구·소리 나는 물건
    # 거실
    put(23, 21, 'T'); put(24, 21, 'C'); put(24, 19, 'v'); put(29, 18, 'c'); put(19, 23, 'r'); put(28, 22, 'k')
    put(29, 23, 'f')                                         # f: 선풍기
    # 불단방
    put(9, 19, 'A'); put(11, 19, 'n'); put(14, 23, 'o')
    put(14, 19, 'x')                                         # x: 불단 종
    # 현관
    put(33, 24, 'h')
    # 다용도실
    put(39, 20, 'g')
    # 복도
    put(30, 15, 'p')
    # 안방
    put(8, 8, 'q'); put(13, 8, 'j'); put(11, 12, 'b')
    # 욕실
    put(19, 8, 'm'); put(17, 12, 'u')
    # 부엌
    put(23, 8, 's'); put(28, 8, 'z'); put(30, 12, 'y')
    # 자녀방
    put(39, 8, 'd'); put(33, 12, 'i'); put(37, 12, 'e')

    return [''.join(r) for r in g]


def to_cs(rows):
    """빌더의 `const string Map =` 아래에 들어가는 줄들(들여쓰기 8칸, 마지막 줄은 ;로 끝남)."""
    out = []
    for i, row in enumerate(rows):
        tail = ';' if i == len(rows) - 1 else ' +'
        out.append('        "' + row + '\\n"' + tail)
    return out


def extract_from_builder(path):
    """HouseSceneBuilder.cs 에서 `const string Map = "..." + ... ;` 의 행들을 꺼낸다."""
    with open(path, encoding='utf-8') as f:
        src = f.read()
    m = re.search(r'const\s+string\s+Map\s*=(.*?);', src, re.S)
    if not m:
        raise SystemExit('빌더에서 `const string Map` 을 찾지 못했다: ' + path)
    text = ''.join(re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(1)))
    text = text.replace('\\n', '\n')
    return [r for r in text.split('\n') if r]


def main():
    repo = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    default_builder = os.path.join(repo, 'Assets', 'Editor', 'HouseSceneBuilder.cs')

    ap = argparse.ArgumentParser(description='마중불 House 지도 생성기')
    ap.add_argument('--cs', action='store_true', help='빌더에 붙여 넣을 C# 문자열 줄로 출력')
    ap.add_argument('--check', action='store_true', help='빌더의 지도와 비교(다르면 종료 코드 1)')
    ap.add_argument('--builder', default=default_builder, help='비교할 HouseSceneBuilder.cs 경로')
    args = ap.parse_args()

    rows = build()
    assert len(rows) == H and all(len(r) == W for r in rows)

    if args.check:
        embedded = extract_from_builder(args.builder)
        if embedded == rows:
            print('지도 일치: 생성기 출력 = HouseSceneBuilder.Map (%dx%d)' % (W, H))
            return 0
        print('지도 불일치: 생성기 출력과 HouseSceneBuilder.Map 이 다르다.')
        if len(embedded) != len(rows):
            print('  행 수: 생성기 %d, 빌더 %d' % (len(rows), len(embedded)))
        for r in range(max(len(rows), len(embedded))):
            a = rows[r] if r < len(rows) else ''
            b = embedded[r] if r < len(embedded) else ''
            if a != b:
                cols = [x for x in range(max(len(a), len(b)))
                        if (a[x] if x < len(a) else None) != (b[x] if x < len(b) else None)]
                print('  행 %2d 열 %s' % (r, cols))
                print('    생성기: ' + a)
                print('    빌더  : ' + b)
        print('빌더를 손으로 고쳤다면 같은 변경을 tools/mapgen/gen.py 에도 옮겨 두 쪽을 맞춘다.')
        return 1

    print('\n'.join(to_cs(rows) if args.cs else rows))
    return 0


if __name__ == '__main__':
    sys.exit(main())
