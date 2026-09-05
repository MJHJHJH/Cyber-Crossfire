# -*- coding: utf-8 -*-
import shutil
from pathlib import Path

import openpyxl

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "Datas"
DST = ROOT / "Datas_Gen"

ENUM_ITEMS = [
    ("Rifle", "步枪", 0, "玩家步枪"),
    ("Shotgun", "霰弹枪", 1, "玩家霰弹"),
    ("RPG", "火箭筒", 2, "玩家RPG"),
    ("Energy", "能量枪", 3, "玩家能量步枪"),
    ("Sniper", "狙击枪", 4, "玩家狙击"),
    ("Crossbow", "弩", 5, "玩家弩"),
    ("EnemyCanonA", "敌方加农", 6, "EnemyWeapons/EnemyGun_Canon_A"),
]


def write_enums(path: Path) -> None:
    wb = openpyxl.load_workbook(path)
    ws = wb.active
    for r in range(ws.max_row, 3, -1):
        ws.delete_rows(r)
    first = ENUM_ITEMS[0]
    ws.append(
        [
            None,
            "WeaponId",
            False,
            True,
            None,
            "武器Id(与TbWeapon.id一致)",
            None,
            first[0],
            first[1],
            first[2],
            first[3],
            None,
        ]
    )
    for item in ENUM_ITEMS[1:]:
        ws.append(
            [
                None,
                None,
                None,
                None,
                None,
                None,
                None,
                item[0],
                item[1],
                item[2],
                item[3],
                None,
            ]
        )
    wb.save(path)


def main() -> None:
    if DST.exists():
        shutil.rmtree(DST)
    DST.mkdir()

    for f in SRC.iterdir():
        if not f.is_file():
            continue
        if f.name.startswith("~$"):
            continue
        if f.name in ("#Weapon.xlsx", "#Weapon.filled.xlsx"):
            continue
        shutil.copy2(f, DST / f.name)

    filled = SRC / "#Weapon.filled.xlsx"
    if not filled.exists():
        raise SystemExit("missing #Weapon.filled.xlsx")
    shutil.copy2(filled, DST / "#Weapon.xlsx")

    write_enums(DST / "__enums__.xlsx")
    try:
        write_enums(SRC / "__enums__.xlsx")
        print("enums updated in Datas")
    except Exception as ex:
        print("enums Datas update skipped:", ex)

    print("Datas_Gen ready:", DST)


if __name__ == "__main__":
    main()
