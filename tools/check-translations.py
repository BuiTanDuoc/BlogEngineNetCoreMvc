#!/usr/bin/env python3
"""Kiểm tra bản dịch: mọi chuỗi L["..."], [Display(Name)], ErrorMessage trong code phải có trong
Resources/SharedResource.vi.resx (và ngược lại). Chạy:  python3 tools/check-translations.py
Khoá = chính câu tiếng Anh. Lưu ý: tên resource KHÔNG phân biệt hoa/thường -> không dùng hai khoá chỉ khác hoa/thường."""
import re, glob, sys, xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parent.parent / "src" / "BlogEngine.Web"
keys = {}
for p in list(root.rglob("*.cs")) + list(root.rglob("*.cshtml")):
    if {"bin", "obj"} & set(p.parts): continue
    s = p.read_text(encoding="utf-8")
    for pat in (r'\bL\["((?:[^"\\]|\\.)*)"', r'Display\(Name = "((?:[^"\\]|\\.)*)"\)', r'ErrorMessage = "((?:[^"\\]|\\.)*)"'):
        for m in re.finditer(pat, s): keys.setdefault(m.group(1), p.name)
    for m in re.finditer(r'Plural\([^"]*?"((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"', s):
        keys.setdefault(m.group(1), p.name); keys.setdefault(m.group(2), p.name)

resx = ET.parse(root / "Resources" / "SharedResource.vi.resx").getroot()
have = {d.get("name"): (d.findtext("value") or "") for d in resx.findall("data")}

seen, problems = {}, 0
for k in keys:
    if k.casefold() in seen:
        print(f"CASE-DUPLICATE: {seen[k.casefold()]!r} vs {k!r}"); problems += 1
    seen[k.casefold()] = k
for k, f in sorted(keys.items()):
    if k not in have: print(f"MISSING vi: {k!r}  ({f})"); problems += 1
    else:
        ph = lambda t: sorted(re.findall(r"\{\d+\}", t))
        if ph(k) != ph(have[k]): print(f"PLACEHOLDER MISMATCH: {k!r} -> {have[k]!r}"); problems += 1
for k in have:
    if k not in keys: print(f"UNUSED in code: {k!r}")
print(f"{len(keys)} keys, {len(have)} translations, {problems} problem(s)")
sys.exit(1 if problems else 0)
