# -*- coding: utf-8 -*-
"""将 Datas/ 全量镜像到 Datas_Gen/（Datas 为正式源，Datas_Gen 仅作旁路/历史兼容）。"""
from __future__ import annotations

import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
DATAS = ROOT / "Datas"
DATAS_GEN = ROOT / "Datas_Gen"


def main() -> int:
    if not DATAS.is_dir():
        print(f"[sync_datas_gen] ERROR: missing {DATAS}", file=sys.stderr)
        return 2

    DATAS_GEN.mkdir(parents=True, exist_ok=True)
    count = 0
    for src in DATAS.iterdir():
        if not src.is_file() or src.name.startswith("~$"):
            continue
        dst = DATAS_GEN / src.name
        shutil.copy2(src, dst)
        count += 1
        print(f"[sync_datas_gen] {src.name}")

    print(f"[sync_datas_gen] OK mirrored {count} file(s) Datas -> Datas_Gen")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
