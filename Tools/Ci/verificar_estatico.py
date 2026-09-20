#!/usr/bin/env python3
"""Comprobaciones que NO necesitan Unity ni licencia, para que el CI de GitHub valide algo real en cada push.

Uso: python Tools/Ci/verificar_estatico.py   (sale con 0 si todo esta bien, 1 si algo falla)

Revisa sobre los archivos versionados (git ls-files):
  1. Todo archivo de Assets/ tiene su .meta y todo .meta tiene su archivo (Unity los regenera si faltan y rompe referencias).
  2. Ningun script de runtime usa Camera.main ni Resources.Load<...> directo (la suite de Unity comprueba lo mismo).
  3. Los .cs no tienen marcas de conflicto de merge.
  4. Docs/AUDITORIA_100_ITEMS.md: los contadores del resumen suman 100 y coinciden con las filas de la tabla.
  5. Toda escena listada en ProjectSettings/EditorBuildSettings.asset existe.
"""
import os
import re
import subprocess
import sys

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
os.chdir(RAIZ)
fallos = []


def archivos():
    salida = subprocess.run(["git", "ls-files", "-z"], capture_output=True, check=True).stdout
    return [p for p in salida.decode("utf-8").split("\0") if p]


def leer(ruta):
    with open(ruta, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def fallar(regla, detalle):
    fallos.append("[%s] %s" % (regla, detalle))


todos = archivos()
conjunto = set(todos)

# 1) .meta emparejados (solo dentro de Assets/, y sin contar carpetas: sus .meta si se versionan)
for p in todos:
    if not p.startswith("Assets/") or any(parte.endswith("~") for parte in p.split("/")):
        continue  # las carpetas con ~ final no las importa Unity
    if p.endswith(".meta"):
        base = p[:-5]
        if base not in conjunto and not any(q.startswith(base + "/") for q in conjunto):
            fallar("meta-huerfano", p)
    else:
        nombre = os.path.basename(p)
        if nombre.startswith(".") or nombre.endswith("~"):
            continue
        if p + ".meta" not in conjunto:
            fallar("falta-meta", p)

# 2) y 3) codigo
for p in todos:
    if not (p.startswith("Assets/") and p.endswith(".cs")):
        continue
    codigo = leer(p)
    if re.search(r"^(<<<<<<< |>>>>>>> )", codigo, re.M):
        fallar("conflicto-merge", p)
    en_editor = "/Editor/" in p or "/Tests/" in p
    if p.startswith("Assets/_Project/Scripts/") and not en_editor and not p.endswith(("CamaraPrincipal.cs", "RecursosCache.cs")):
        sin_comentarios = re.sub(r"//.*", "", codigo)
        if "Camera.main" in sin_comentarios or "Resources.Load<" in sin_comentarios:
            fallar("camera-main-o-resources-load", p)

# 4) auditoria
ruta = "Docs/AUDITORIA_100_ITEMS.md"
if os.path.exists(ruta):
    texto = leer(ruta)
    m = re.search(r"HECHO:\s*(\d+),\s*YA ESTABA:\s*(\d+),\s*PARCIAL:\s*(\d+),\s*NO(?:/NO APLICA)?:\s*(\d+)", texto)
    if not m:
        fallar("auditoria", "no se encontro la linea de resumen")
    else:
        hecho, estaba, parcial, no = map(int, m.groups())
        if hecho + estaba + parcial + no != 100:
            fallar("auditoria", "el resumen suma %d y no 100" % (hecho + estaba + parcial + no))
        filas = re.findall(r"^\|\s*(\d+)\s*\|.*?\|\s*([A-ZÁÉÍÓÚ/ ]+?)(?:\s*\([^|]*\))?\s*\|", texto, re.M)
        numeros = sorted(int(n) for n, _ in filas)
        if numeros != list(range(1, 101)):
            fallar("auditoria", "la tabla no tiene exactamente las filas 1..100 (tiene %d)" % len(numeros))
        else:
            cuenta = {"HECHO": 0, "YA ESTABA": 0, "PARCIAL": 0, "NO": 0}
            for _, estado in filas:
                estado = estado.strip()
                clave = "NO" if estado.startswith("NO") else estado
                if clave in cuenta:
                    cuenta[clave] += 1
                else:
                    fallar("auditoria", "estado desconocido '%s'" % estado)
            esperado = {"HECHO": hecho, "YA ESTABA": estaba, "PARCIAL": parcial, "NO": no}
            if cuenta != esperado:
                fallar("auditoria", "el resumen dice %s pero la tabla tiene %s" % (esperado, cuenta))

# 5) escenas del build
ruta = "ProjectSettings/EditorBuildSettings.asset"
if os.path.exists(ruta):
    for path in re.findall(r"path:\s*(Assets/\S+\.unity)", leer(ruta)):
        if path not in conjunto:
            fallar("escena-del-build", path + " no esta versionada")

if fallos:
    print("FALLO: %d problema(s)" % len(fallos))
    for f in fallos[:100]:
        print("  " + f)
    sys.exit(1)
print("OK: comprobaciones estaticas superadas (%d archivos versionados)" % len(todos))
