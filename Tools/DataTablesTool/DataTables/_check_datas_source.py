# -*- coding: utf-8 -*-
"""导表前校验：正式源表是 Datas/。

若 Datas_Gen 中 #*.xlsx 比 Datas 更新且内容不同，判定为误改 Datas_Gen，失败并提示。
二进制噪声导致“同内容不同哈希”时，用 openpyxl 读出的数据行做语义比较。
"""
from __future__ import annotations

import sys
from pathlib import Path

import openpyxl

ROOT = Path(__file__).resolve().parent
DATAS = ROOT / "Datas"
DATAS_GEN = ROOT / "Datas_Gen"


def _sheet_rows(path: Path):
    wb = openpyxl.load_workbook(path, data_only=True)
    ws = wb.active
    return [tuple(row) for row in ws.iter_rows(values_only=True)]


def _semantic_equal(a: Path, b: Path) -> bool:
    try:
        return _sheet_rows(a) == _sheet_rows(b)
    except Exception as ex:
        print(f"[datas_check] warn: compare failed {a.name}: {ex}", file=sys.stderr)
        return False


def main() -> int:
    if not DATAS.is_dir():
        print(f"[datas_check] ERROR: missing source dir: {DATAS}", file=sys.stderr)
        return 2

    if not DATAS_GEN.is_dir():
        print("[datas_check] OK (no Datas_Gen)")
        return 0

    stale_noise = []
    real_forks = []

    for gen_file in sorted(DATAS_GEN.glob("#*.xlsx")):
        if gen_file.name.startswith("~$"):
            continue
        src_file = DATAS / gen_file.name
        if not src_file.exists():
            real_forks.append(
                f"  - {gen_file.name}: 仅存在于 Datas_Gen，Datas 中缺失（请拷回 Datas）"
            )
            continue

        if _semantic_equal(src_file, gen_file):
            if gen_file.stat().st_mtime != src_file.stat().st_mtime:
                stale_noise.append(gen_file.name)
            continue

        gen_newer = gen_file.stat().st_mtime > src_file.stat().st_mtime
        if gen_newer:
            real_forks.append(
                f"  - {gen_file.name}: Datas_Gen 比 Datas 新且内容不同（疑似改错目录）"
            )
        else:
            stale_noise.append(gen_file.name)

    if stale_noise:
        print(
            "[datas_check] Datas_Gen 与 Datas 存在无关紧要差异，导表后将用 Datas 覆盖镜像: "
            + ", ".join(stale_noise)
        )

    if real_forks:
        print("[datas_check] FAILED: 检测到 Datas_Gen 含未同步的有效改动", file=sys.stderr)
        print("正式导表源: Tools/DataTablesTool/DataTables/Datas/  (luban.conf)", file=sys.stderr)
        print("Datas_Gen 是临时镜像，编辑它不会写入 tbsound/tbweapon。", file=sys.stderr)
        print("请先把 Datas_Gen 的改动覆盖到 Datas，再导表：", file=sys.stderr)
        for line in real_forks:
            print(line, file=sys.stderr)
        print(
            r'  copy /Y "Tools\DataTablesTool\DataTables\Datas_Gen\#Sound.xlsx" '
            r'"Tools\DataTablesTool\DataTables\Datas\#Sound.xlsx"',
            file=sys.stderr,
        )
        return 1

    print("[datas_check] OK (source = Datas)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
