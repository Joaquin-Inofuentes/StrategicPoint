#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Convierte la carpeta de una corrida de QA (Reportes/QA_*) en un informe HTML
navegable: etapas, metricas, galeria de capturas, recorrido del tutorial y
errores. Lo llama Tools/Qa/qa_total.bat al final; tambien se puede correr solo:

    py -3 Tools/Qa/informe_html.py Reportes/QA_20260920_101500

No depende de nada fuera de la biblioteca estandar.
"""

import csv
import html
import json
import os
import sys
from pathlib import Path

CSS = """
:root{--bg:#0f1115;--panel:#171a21;--line:#2a2f3a;--txt:#e6e8ee;--dim:#98a0b3;
      --ok:#4ade80;--ko:#f87171;--skip:#94a3b8;--acc:#38bdf8}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--txt);
     font:15px/1.55 system-ui,-apple-system,Segoe UI,Roboto,sans-serif}
.wrap{max-width:1180px;margin:0 auto;padding:2rem 1.25rem 4rem}
h1{font-size:1.7rem;margin:0 0 .25rem}
h2{font-size:1.15rem;margin:2.5rem 0 .75rem;padding-bottom:.4rem;border-bottom:1px solid var(--line)}
h3{font-size:.95rem;margin:1.25rem 0 .5rem;color:var(--dim);font-weight:600}
.sub{color:var(--dim);margin:0 0 2rem}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:.75rem;margin:1rem 0}
.card{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:.85rem 1rem}
.card .k{color:var(--dim);font-size:.78rem;text-transform:uppercase;letter-spacing:.04em}
.card .v{font-size:1.5rem;font-weight:600;margin-top:.2rem;font-variant-numeric:tabular-nums}
table{border-collapse:collapse;width:100%;background:var(--panel);
      border:1px solid var(--line);border-radius:10px;overflow:hidden}
th,td{padding:.55rem .8rem;text-align:left;border-bottom:1px solid var(--line);font-size:.9rem}
th{color:var(--dim);font-weight:600;text-transform:uppercase;font-size:.75rem;letter-spacing:.04em}
tr:last-child td{border-bottom:0}
td.num{text-align:right;font-variant-numeric:tabular-nums}
.pill{display:inline-block;padding:.12rem .55rem;border-radius:999px;font-size:.78rem;font-weight:600}
.pill.ok{background:rgba(74,222,128,.15);color:var(--ok)}
.pill.ko{background:rgba(248,113,113,.15);color:var(--ko)}
.pill.skip{background:rgba(148,163,184,.15);color:var(--skip)}
pre{background:#0a0c10;border:1px solid var(--line);border-radius:10px;
    padding:1rem;overflow:auto;font-size:.82rem;max-height:32rem;color:#cbd5e1}
.gal{display:grid;grid-template-columns:repeat(auto-fit,minmax(310px,1fr));gap:1rem}
.shot{background:var(--panel);border:1px solid var(--line);border-radius:10px;overflow:hidden}
.shot img{display:block;width:100%;height:auto;background:#000}
.shot .cap{padding:.5rem .7rem;font-size:.8rem;color:var(--dim)}
a{color:var(--acc)}
.foot{margin-top:3rem;padding-top:1rem;border-top:1px solid var(--line);color:var(--dim);font-size:.82rem}
@media (prefers-color-scheme: light){
  :root{--bg:#f7f8fa;--panel:#fff;--line:#e2e5ec;--txt:#1a1d24;--dim:#5b6472}
  pre{background:#f1f3f7;color:#2b313c}
}
"""


def leer(p, limite=None):
    try:
        with open(p, "r", encoding="utf-8", errors="replace") as f:
            return f.read(limite) if limite else f.read()
    except OSError:
        return ""


def etapas(out):
    filas = []
    p = out / "estado.csv"
    if not p.exists():
        return filas
    with open(p, "r", encoding="utf-8", errors="replace") as f:
        for fila in csv.DictReader(f, delimiter=";"):
            filas.append(fila)
    return filas


def metricas(out):
    """spmetrics.txt viene como 'clave=valor clave=valor' por linea."""
    d = {}
    for linea in leer(out / "metricas" / "spmetrics.txt").splitlines():
        for tok in linea.split():
            if "=" in tok:
                k, _, v = tok.partition("=")
                d[k] = v
    for nombre in ("suite.txt", "build.txt", "capturas.txt", "autoplay.txt", "errores.txt"):
        for linea in leer(out / "metricas" / nombre).splitlines():
            if "=" in linea and ";" not in linea:
                k, _, v = linea.partition("=")
                d[k.strip()] = v.strip()
    return d


def resumen_tutorial(out):
    a = out / "autoplay"
    if not a.exists():
        return "", {}
    txt, js = "", {}
    for r in sorted(a.rglob("resumen.txt")):
        txt = leer(r)
        break
    for r in sorted(a.rglob("resumen.json")):
        try:
            js = json.loads(leer(r))
        except (ValueError, OSError):
            js = {}
        break
    return txt, js


def pill(resultado):
    r = (resultado or "").upper()
    clase = "ok" if r == "OK" else ("skip" if r == "OMITIDA" else "ko")
    return f'<span class="pill {clase}">{html.escape(r or "?")}</span>'


def tarjeta(k, v):
    return f'<div class="card"><div class="k">{html.escape(k)}</div><div class="v">{html.escape(str(v))}</div></div>'


def main():
    if len(sys.argv) < 2:
        print("uso: informe_html.py <carpeta de la corrida>", file=sys.stderr)
        return 2
    out = Path(sys.argv[1]).resolve()
    if not out.is_dir():
        print(f"no existe: {out}", file=sys.stderr)
        return 2

    filas = etapas(out)
    met = metricas(out)
    tut_txt, tut_js = resumen_tutorial(out)

    fallidas = sum(1 for f in filas if (f.get("resultado") or "").upper() == "FALLO")
    total_seg = sum(int(f.get("segundos") or 0) for f in filas)

    tarjetas = [
        tarjeta("Etapas fallidas", fallidas),
        tarjeta("Duracion", f"{total_seg // 60} min"),
    ]
    for clave, etiqueta in (
        ("checks_ok", "Checks OK"),
        ("checks_fallidos", "Checks fallidos"),
        ("fps_prom", "FPS promedio"),
        ("p95_ms", "p95 (ms)"),
        ("p99_ms", "p99 (ms)"),
        ("heap_gestionado_MB", "Heap (MB)"),
        ("build_MB", "Build (MB)"),
        ("capturas", "Capturas"),
        ("capturas_tutorial", "Capturas tutorial"),
        ("lineas_con_error", "Lineas con error"),
    ):
        if clave in met:
            tarjetas.append(tarjeta(etiqueta, met[clave]))

    fila_html = "\n".join(
        "<tr><td>{}</td><td>{}</td><td class='num'>{}</td><td>{}</td></tr>".format(
            html.escape(f.get("etapa", "")),
            pill(f.get("resultado")),
            html.escape(f.get("segundos", "")),
            html.escape(f.get("detalle", "")),
        )
        for f in filas
    )

    shots = sorted((out / "capturas").glob("*.png"))
    gal = "\n".join(
        '<figure class="shot"><img loading="lazy" src="capturas/{n}" alt="{a}">'
        '<figcaption class="cap">{a}</figcaption></figure>'.format(
            n=html.escape(p.name), a=html.escape(p.stem)
        )
        for p in shots
    ) or "<p class='sub'>Sin capturas en esta corrida.</p>"

    tut_shots = sorted((out / "autoplay").rglob("*.jpg"))
    gal_tut = "\n".join(
        '<figure class="shot"><img loading="lazy" src="{n}" alt="{a}">'
        '<figcaption class="cap">{a}</figcaption></figure>'.format(
            n=html.escape(str(p.relative_to(out)).replace("\\", "/")), a=html.escape(p.stem)
        )
        for p in tut_shots
    ) or "<p class='sub'>Sin recorrido del tutorial en esta corrida.</p>"

    errores = leer(out / "logs" / "_TODOS_LOS_ERRORES.txt", 120_000) or "Sin errores registrados."
    spm = leer(out / "metricas" / "spmetrics.txt") or "Sin metricas de build."

    resultado_tut = tut_js.get("resultado", "?")
    pasos_tut = len(tut_js.get("pasos", {})) if isinstance(tut_js.get("pasos"), dict) else "?"

    doc = f"""<!doctype html>
<html lang="es"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>QA {html.escape(out.name)} - Strategic Point</title>
<style>{CSS}</style></head><body><div class="wrap">

<h1>Strategic Point &mdash; Informe de QA</h1>
<p class="sub">{html.escape(out.name)}
 &nbsp;&middot;&nbsp; tutorial: <strong>{html.escape(str(resultado_tut))}</strong> ({pasos_tut} pasos)
 &nbsp;&middot;&nbsp; <a href="INFORME.md">version markdown</a></p>

<div class="cards">{''.join(tarjetas)}</div>

<h2>Etapas</h2>
<table><thead><tr><th>Etapa</th><th>Resultado</th><th>Segundos</th><th>Detalle</th></tr></thead>
<tbody>{fila_html}</tbody></table>

<h2>Metricas del build real</h2>
<pre>{html.escape(spm)}</pre>

<h2>Capturas del build ({len(shots)})</h2>
<div class="gal">{gal}</div>

<h2>Recorrido del tutorial ({len(tut_shots)} capturas)</h2>
<pre>{html.escape(tut_txt or 'Sin resumen.')}</pre>
<div class="gal">{gal_tut}</div>

<h2>Errores en todos los logs</h2>
<pre>{html.escape(errores)}</pre>

<h2>Donde esta cada cosa</h2>
<table><tbody>
<tr><td><code>capturas/</code></td><td>Build real en 7 resoluciones, de 4:3 a 4K por supersampling</td></tr>
<tr><td><code>autoplay/</code></td><td>Recorrido completo del tutorial: <code>log.jsonl</code>, resumen y una captura por paso</td></tr>
<tr><td><code>metricas/</code></td><td><code>spmetrics.txt</code>, conteos de la suite, peso del build, inventario de capturas</td></tr>
<tr><td><code>logs/</code></td><td><code>Editor.log</code>, <code>Player.log</code>, log de cada etapa, <code>Logs/</code> del proyecto, volcados de crash</td></tr>
<tr><td><code>logs/_TODOS_LOS_ERRORES.txt</code></td><td>Todas las lineas con error, excepcion o fallo, de todos los logs juntos</td></tr>
<tr><td><code>artefactos/</code></td><td>Documentos que la suite genera o actualiza</td></tr>
<tr><td><code>estado.csv</code></td><td>Una fila por etapa</td></tr>
<tr><td><code>../historico.csv</code></td><td>Una fila por corrida, para comparar rondas entre si</td></tr>
</tbody></table>

<p class="foot">Generado por <code>Tools/Qa/informe_html.py</code>.
Las capturas y los logs son relativos a esta carpeta: se puede comprimir entera y abrir en otra maquina.</p>
</div></body></html>
"""
    (out / "INFORME.html").write_text(doc, encoding="utf-8")
    print(f"INFORME.html escrito en {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
