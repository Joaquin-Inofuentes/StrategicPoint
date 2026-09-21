#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Indexador de Strategic Point para lectura por IA.

Recorre el proyecto entero y escribe en Docs/INDICE_IA/ ocho mapas, en Markdown
(para leer) y un indice.json (para consultar con codigo). La idea es que un
agente pueda contestar "donde vive X", "quien publica el evento Y", "que pasa si
toco Z" sin abrir 221 archivos.

    py -3 Tools/Indice/indexar_ia.py            # escribe Docs/INDICE_IA/
    py -3 Tools/Indice/indexar_ia.py --check    # falla si el indice quedo viejo

Mapas que genera:
    00_MAPA.md          punto de entrada: como esta armado el juego
    01_archivos.md      cada script: subsistema, lineas, que hace (su cabecera)
    02_simbolos.md      clases, metodos publicos, propiedades y campos estaticos
    03_dependencias.md  quien depende de quien, entre subsistemas (grafo mermaid)
    04_eventos.md       el bus: quien publica y quien escucha cada evento
    05_entrada.md       cada tecla y que hace
    06_escenas.md       escenas, prefabs, materiales, recursos
    07_pruebas.md       fases de la suite y que cubre cada una
    08_estaticos.md     estado estatico mutable y si alguien lo restablece
    indice.json         todo lo anterior, en una sola estructura

Solo biblioteca estandar. No toca nada del proyecto: solo lee y escribe en
Docs/INDICE_IA/.
"""

from __future__ import annotations

import hashlib
import json
import re
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]
SALIDA = RAIZ / "Docs" / "INDICE_IA"

# --------------------------------------------------------------------------
# Subsistemas: la carpeta manda, y cada uno lleva una frase de que hace.
# --------------------------------------------------------------------------
SUBSISTEMAS = {
    "Core": "Servicios sin escena: bus de eventos, registros, grilla espacial, navegacion, pools, idioma, dificultad.",
    "Actors": "El soldado: identidad, piezas, motor de movimiento y aspecto.",
    "Ai": "Cerebro de una unidad no poseida y el driver que avanza la simulacion.",
    "Combat": "Vida, dano, armas, proyectiles y catalogo.",
    "Camera": "Rig de camara: hombro, RTS, transiciones, sacudidas, zoom.",
    "Player": "Traduccion de intencion a ordenes: input, posesion, seleccion, ordenes.",
    "Vehicles": "Tanque, asientos, torretas y ametralladoras fijas.",
    "Mision": "La partida: objetivos, oleadas, rehen, helicoptero, cinematica final.",
    "Presentation": "Todo lo que se ve y se oye en el mundo: VFX, audio, marcadores, pools visuales.",
    "UI": "HUD y pantallas: mira, roster, minimapa, radial, menus, ajustes.",
    "Tutorial": "Modulo de ensenanza y su reproductor automatico.",
    "Interaction": "Contrato de lo interactuable.",
    "Demo": "Corredor de demo automatica.",
    "Editor": "Herramientas de Editor: suite headless, constructores de escena, pipelines de arte.",
}

# --------------------------------------------------------------------------
# Expresiones regulares. C# con regex es aproximado a proposito: el indice
# tiene que ser barato de regenerar, no un compilador.
# --------------------------------------------------------------------------
RE_NS = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)
RE_TIPO = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?P<mod>(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|\s)*)"
    r"\b(?P<clase>class|struct|interface|enum)\s+(?P<nombre>\w+)",
    re.M,
)
RE_METODO = re.compile(
    r"^[ \t]*public\s+(?!class\b|struct\b|interface\b|enum\b|const\b)"
    r"(?P<mod>(?:static|virtual|override|async|sealed|partial|readonly|unsafe|new|\s)*)"
    r"(?P<tipo>[\w<>\[\],.?\s]+?)\s+(?P<nombre>\w+)\s*\((?P<args>[^)]*)\)",
    re.M,
)
RE_PROP = re.compile(
    r"^[ \t]*public\s+(?:static\s+)?(?:readonly\s+)?(?P<tipo>[\w<>\[\],.?]+)\s+(?P<nombre>\w+)\s*(?:=>|\{\s*get)",
    re.M,
)
RE_ESTATICO = re.compile(
    r"^[ \t]*(?P<acc>public|internal|private|protected)?\s*static\s+(?!readonly\b)"
    r"(?P<tipo>[\w<>\[\],.?]+)\s+(?P<nombre>\w+)\s*(?:=[^;]*)?;",
    re.M,
)
RE_PUBLISH = re.compile(r"EventBus\.Instance\.Publish\(\s*new\s+(?:[\w]+\.)*(\w+)")
RE_SUBSCRIBE = re.compile(r"EventBus\.Instance\.Subscribe<\s*(\w+)\s*>")
RE_EVENTO_DECL = re.compile(r"(?:readonly\s+)?struct\s+(\w*Event)\b")
RE_USING = re.compile(r"^\s*using\s+(SP\.[\w.]+)\s*;", re.M)
RE_KEY = re.compile(r"\bKey\.(\w+)\b")
RE_BINDING = re.compile(r"public\s+const\s+string\s+(\w+)\s*=\s*\"([^\"]+)\"")
RE_MENUITEM = re.compile(r'\[MenuItem\("([^"]+)"\)\]')
RE_FASE = re.compile(r"\b(?:void|public\s+static\s+void)\s+(RunPhase\w*|Fase\w*)\s*\(")
RE_CHECK = re.compile(r"\bCheck\(\s*\$?\"")
RE_CABECERA = re.compile(r"^\s*//\s?(.*)$")
RE_BUG = re.compile(r"//.*\bBUG\s+(?:REAL|CRITICO)\b", re.I)
RE_TODO = re.compile(r"//.*\b(TODO|FIXME|HACK|OJO)\b")


def subsistema_de(p: Path) -> str:
    partes = p.parts
    for i, x in enumerate(partes):
        # partes[i+1] tiene que ser una CARPETA, no el propio archivo:
        # Assets/TutorialInfo/Scripts/Readme.cs no define un subsistema "Readme.cs".
        if x == "Scripts" and i + 2 < len(partes):
            return partes[i + 1]
    if "Editor" in partes:
        return "Editor"
    return "Otros"


def cabecera(texto: str, max_lineas: int = 6) -> str:
    """Primer bloque de // que sigue a los using: la descripcion del archivo."""
    fuera = []
    visto_codigo = False
    for linea in texto.splitlines():
        s = linea.strip()
        if not s or s.startswith("using ") or s.startswith("namespace") or s == "{":
            if fuera and visto_codigo:
                break
            continue
        m = RE_CABECERA.match(linea)
        if m:
            visto_codigo = True
            t = m.group(1).strip()
            if t and not t.startswith("-"):
                fuera.append(t)
            if len(fuera) >= max_lineas:
                break
        elif fuera:
            break
        else:
            visto_codigo = True
    return " ".join(fuera).strip()


def limpiar(s: str) -> str:
    return re.sub(r"\s+", " ", (s or "")).strip()


def escanear() -> dict:
    archivos = []
    for p in sorted(RAIZ.joinpath("Assets").rglob("*.cs"), key=lambda q: q.as_posix()):
        if any(x in p.parts for x in ("Library", "Temp", "obj", "Packages")):
            continue
        try:
            texto = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        rel = p.relative_to(RAIZ).as_posix()
        lineas = texto.count("\n") + 1

        ns = RE_NS.search(texto)
        tipos = []
        for m in RE_TIPO.finditer(texto):
            mods = limpiar(m.group("mod"))
            tipos.append(
                {
                    "nombre": m.group("nombre"),
                    "clase": m.group("clase"),
                    "parcial": "partial" in mods,
                    "estatica": "static" in mods,
                    "linea": texto[: m.start()].count("\n") + 1,
                }
            )

        metodos = []
        for m in RE_METODO.finditer(texto):
            tipo = limpiar(m.group("tipo"))
            if tipo in ("class", "struct", "interface", "enum", "event"):
                continue
            metodos.append(
                {
                    "nombre": m.group("nombre"),
                    "devuelve": tipo,
                    "estatico": "static" in limpiar(m.group("mod")),
                    "args": limpiar(m.group("args")),
                    "linea": texto[: m.start()].count("\n") + 1,
                }
            )

        props = [
            {"nombre": m.group("nombre"), "tipo": limpiar(m.group("tipo")),
             "linea": texto[: m.start()].count("\n") + 1}
            for m in RE_PROP.finditer(texto)
        ]

        estaticos = [
            {
                "nombre": m.group("nombre"),
                "tipo": limpiar(m.group("tipo")),
                "acceso": (m.group("acc") or "private"),
                "linea": texto[: m.start()].count("\n") + 1,
            }
            for m in RE_ESTATICO.finditer(texto)
        ]

        archivos.append(
            {
                "ruta": rel,
                "nombre": p.name,
                "subsistema": subsistema_de(p),
                "lineas": lineas,
                "bytes": p.stat().st_size,
                "namespace": ns.group(1) if ns else "",
                "resumen": cabecera(texto),
                "tipos": tipos,
                "metodos_publicos": metodos,
                "propiedades_publicas": props,
                "estaticos_mutables": estaticos,
                "publica_eventos": sorted(set(RE_PUBLISH.findall(texto))),
                "escucha_eventos": sorted(set(RE_SUBSCRIBE.findall(texto))),
                "declara_eventos": sorted(set(RE_EVENTO_DECL.findall(texto))),
                "usa": sorted({u for u in RE_USING.findall(texto)}),
                "teclas": sorted(set(RE_KEY.findall(texto))),
                "menu_items": RE_MENUITEM.findall(texto),
                "checks": len(RE_CHECK.findall(texto)),
                "notas_bug": len(RE_BUG.findall(texto)),
                "notas_todo": len(RE_TODO.findall(texto)),
                "sha1": hashlib.sha1(texto.encode("utf-8", "replace")).hexdigest()[:12],
            }
        )

    assets = {}
    for patron, clave in (
        ("*.unity", "escenas"),
        ("*.prefab", "prefabs"),
        ("*.mat", "materiales"),
        ("*.asset", "assets"),
        ("*.shader", "shaders"),
        ("*.inputactions", "inputactions"),
    ):
        assets[clave] = sorted(
            {
                p.relative_to(RAIZ).as_posix()
                for p in RAIZ.joinpath("Assets").rglob(patron)
                if "Library" not in p.parts and "_Recovery" not in p.parts
            }
        )

    bindings = []
    kb = RAIZ / "Assets/_Project/Scripts/Player/KeyBindings.cs"
    if kb.exists():
        bindings = RE_BINDING.findall(kb.read_text(encoding="utf-8", errors="replace"))

    fases = []
    for p in sorted(RAIZ.joinpath("Assets/_Project/Scripts/Editor").glob("HeadlessTestRunner*.cs"), key=lambda q: q.as_posix()):
        t = p.read_text(encoding="utf-8", errors="replace")
        fases.append(
            {
                "archivo": p.relative_to(RAIZ).as_posix(),
                "lineas": t.count("\n") + 1,
                "fases": sorted(set(RE_FASE.findall(t))),
                "checks": len(RE_CHECK.findall(t)),
            }
        )

    return {
        "generado": datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
        "raiz": RAIZ.name,
        "archivos": archivos,
        "assets": assets,
        "keybindings": bindings,
        "suite": fases,
    }


# --------------------------------------------------------------------------
# Escritura de los mapas
# --------------------------------------------------------------------------
def tabla(cabeceras, filas):
    out = ["| " + " | ".join(cabeceras) + " |",
           "|" + "|".join("---" for _ in cabeceras) + "|"]
    out += ["| " + " | ".join(str(c) for c in f) + " |" for f in filas]
    return "\n".join(out)


def escribir(nombre, texto):
    SALIDA.mkdir(parents=True, exist_ok=True)
    (SALIDA / nombre).write_text(texto.rstrip() + "\n", encoding="utf-8")


def mapa_00(d):
    porsub = defaultdict(list)
    for a in d["archivos"]:
        porsub[a["subsistema"]].append(a)
    filas = []
    for s in sorted(porsub, key=lambda x: -len(porsub[x])):
        arr = porsub[s]
        filas.append([
            f"**{s}**",
            len(arr),
            sum(a["lineas"] for a in arr),
            SUBSISTEMAS.get(s, ""),
        ])
    total_l = sum(a["lineas"] for a in d["archivos"])
    grandes = sorted(d["archivos"], key=lambda a: -a["lineas"])[:12]
    return f"""# Mapa del proyecto — indice para IA

> Generado por `Tools/Indice/indexar_ia.py` el {d["generado"]}.
> No editar a mano: se regenera. Si algo esta mal, esta mal el generador.

**{len(d["archivos"])} scripts · {total_l:,} lineas · {len(d["assets"]["escenas"])} escenas · {len(d["assets"]["prefabs"])} prefabs**

## Como leer este indice

| Archivo | Para contestar |
|---|---|
| [`01_archivos.md`](01_archivos.md) | "¿donde vive X?" — cada script con su subsistema y que hace |
| [`02_simbolos.md`](02_simbolos.md) | "¿que puedo llamar?" — clases, metodos publicos y propiedades |
| [`03_dependencias.md`](03_dependencias.md) | "¿si toco esto, que se rompe?" — grafo entre subsistemas |
| [`04_eventos.md`](04_eventos.md) | "¿quien se entera de esto?" — publicadores y oyentes del bus |
| [`05_entrada.md`](05_entrada.md) | "¿que hace esta tecla?" — mapa completo de input |
| [`06_escenas.md`](06_escenas.md) | "¿que hay en el mundo?" — escenas, prefabs, materiales, shaders |
| [`07_pruebas.md`](07_pruebas.md) | "¿esto esta cubierto?" — fases de la suite y sus checks |
| [`08_estaticos.md`](08_estaticos.md) | "¿esto sobrevive a la partida?" — estado estatico mutable |
| [`indice.json`](indice.json) | lo mismo, para consultar con codigo |

## La arquitectura en un parrafo

El juego es una simulacion con **un solo camino**: `SP.Ai.WorldSimulationDriver.Step(dt)`. Ese metodo
—y nada mas— avanza la IA, las armas, la vida, los vehiculos y las torretas. El juego real lo llama
desde `Update()`; la suite headless lo llama a mano con un reloj simulado. Todo lo que quiera correr en
los dos lados tiene que entrar por ahi: un `Update()` propio queda fuera de las pruebas.

Alrededor hay tres capas que no deciden nada:
- **Registros** (`Core`): `ActorRegistry`, `WorldSystemsRegistry`, `SpatialGrid`, `NavService`.
  Contestan "que existe y donde esta" sin barrer la escena.
- **Bus de eventos** (`Core/EventBus`): los emisores no conocen a los oyentes. Ver `04_eventos.md`.
- **Presentacion** (`Presentation`, `UI`): sin logica de juego, solo escucha el bus y dibuja.

La entrada del jugador (`Player/PlayerInputDriver`) **traduce**, no decide: llama a los mismos metodos
que la IA y que las pruebas.

## Subsistemas

{tabla(["Subsistema", "Scripts", "Lineas", "Que hace"], filas)}

## Los 12 archivos mas grandes

{tabla(["Archivo", "Lineas", "Subsistema", "Que hace"],
       [[f"`{a['ruta']}`", a["lineas"], a["subsistema"], (a["resumen"][:90] + "…") if len(a["resumen"]) > 90 else a["resumen"]]
        for a in grandes])}

## Documentos de contexto que NO genera este indice

| Documento | Que tiene |
|---|---|
| `Docs/RONDA_10_BUGS.md` | Los 100 bugs abiertos, con archivo y linea |
| `Docs/RONDA_10_PLAN_CORRECCION.md` | Como se arregla cada uno y como se verifica |
| `Docs/RONDA_10_TUTORIAL_22_PASOS_NUEVOS.md` | Los 22 pasos nuevos del tutorial |
| `Docs/AUDITORIA_100_ITEMS.md` | Estado de los 100 puntos de la auditoria original |
| `Docs/TUTORIAL.md` | Como esta armado el tutorial, paso por paso |
| `Tools/Qa/qa_total.bat` | Corrida de QA: capturas, metricas y logs de todo tipo |
"""


def mapa_01(d):
    porsub = defaultdict(list)
    for a in d["archivos"]:
        porsub[a["subsistema"]].append(a)
    partes = ["# Mapa de archivos\n",
              f"> {len(d['archivos'])} scripts. Generado el {d['generado']}.\n"]
    for s in sorted(porsub):
        arr = sorted(porsub[s], key=lambda a: -a["lineas"])
        partes.append(f"\n## {s} — {len(arr)} scripts, {sum(a['lineas'] for a in arr):,} lineas\n")
        if SUBSISTEMAS.get(s):
            partes.append(f"\n> {SUBSISTEMAS[s]}\n")
        filas = []
        for a in arr:
            r = a["resumen"]
            filas.append([
                f"`{a['nombre']}`",
                a["lineas"],
                ", ".join(t["nombre"] for t in a["tipos"][:3]) or "—",
                (r[:120] + "…") if len(r) > 120 else (r or "—"),
            ])
        partes.append("\n" + tabla(["Archivo", "Lineas", "Tipos", "Que hace"], filas) + "\n")
    return "".join(partes)


def mapa_02(d):
    partes = ["# Mapa de simbolos\n",
              "\n> Clases, metodos publicos y propiedades publicas de cada archivo.\n",
              "> Los metodos marcados `static` se pueden llamar sin instancia.\n"]
    for a in sorted(d["archivos"], key=lambda x: (x["subsistema"], x["nombre"])):
        if not (a["metodos_publicos"] or a["propiedades_publicas"]):
            continue
        partes.append(f"\n## `{a['ruta']}`\n")
        if a["tipos"]:
            tt = ", ".join(f"`{t['clase']} {t['nombre']}`" for t in a["tipos"])
            partes.append(f"\n**Tipos:** {tt}\n")
        if a["metodos_publicos"]:
            partes.append("\n**Metodos publicos**\n\n")
            filas = [[f"`{m['nombre']}`", "static" if m["estatico"] else "", f"`{m['devuelve']}`",
                      f"`{m['args'][:70]}`" if m["args"] else "—", m["linea"]]
                     for m in a["metodos_publicos"]]
            partes.append(tabla(["Metodo", "", "Devuelve", "Argumentos", "Linea"], filas) + "\n")
        if a["propiedades_publicas"]:
            props = ", ".join(f"`{p['nombre']}`" for p in a["propiedades_publicas"])
            partes.append(f"\n**Propiedades:** {props}\n")
    return "".join(partes)


def mapa_03(d):
    dep = defaultdict(set)
    for a in d["archivos"]:
        origen = a["subsistema"]
        for u in a["usa"]:
            destino = u.split(".")[-1]
            destino = {"CameraSystem": "Camera", "EditorTools": "Editor"}.get(destino, destino)
            if destino != origen and destino in SUBSISTEMAS:
                dep[origen].add(destino)
    lineas = ["flowchart LR"]
    for o in sorted(dep):
        for t in sorted(dep[o]):
            lineas.append(f"    {o} --> {t}")
    grafo = "\n".join(lineas)

    entrantes = defaultdict(int)
    for o in dep:
        for t in dep[o]:
            entrantes[t] += 1
    filas = [[s, len(dep.get(s, ())), entrantes.get(s, 0),
              ", ".join(sorted(dep.get(s, ()))) or "—"]
             for s in sorted(SUBSISTEMAS)]
    return f"""# Mapa de dependencias

> Derivado de los `using SP.*` de cada archivo. Un subsistema con muchos **entrantes** es caro de
> cambiar: tocarlo mueve a todos los que lo usan.

```mermaid
{grafo}
```

## Cuanto pesa cambiar cada subsistema

{tabla(["Subsistema", "Depende de", "Lo usan", "Sus dependencias"], filas)}

## Como leerlo

- **`Core` con muchos entrantes** es lo esperado: es la base sin escena.
- **`Presentation` y `UI` no deberian tener salientes hacia `Player` o `Ai`**: son capas de salida.
  Si aparecen, es acoplamiento al reves y vale revisarlo.
- **`Editor` puede depender de todo**; nada puede depender de `Editor`.
"""


def mapa_04(d):
    pub = defaultdict(list)
    sub = defaultdict(list)
    decl = {}
    for a in d["archivos"]:
        for e in a["publica_eventos"]:
            pub[e].append(a["ruta"])
        for e in a["escucha_eventos"]:
            sub[e].append(a["ruta"])
        for e in a["declara_eventos"]:
            decl[e] = a["ruta"]
    todos = sorted(set(pub) | set(sub) | set(decl))
    filas = []
    for e in todos:
        p, s = pub.get(e, []), sub.get(e, [])
        aviso = ""
        if p and not s:
            aviso = " ⚠ nadie lo escucha"
        if s and not p:
            aviso = " ⚠ nadie lo publica"
        filas.append([
            f"`{e}`",
            len(p),
            len(s),
            ", ".join(f"`{Path(x).name}`" for x in p[:4]) or "—",
            (", ".join(f"`{Path(x).name}`" for x in s[:6]) or "—") + aviso,
        ])
    huerfanos = [e for e in todos if (pub.get(e) and not sub.get(e)) or (sub.get(e) and not pub.get(e))]
    return f"""# Mapa del bus de eventos

> `EventBus.Instance.Publish<T>` / `Subscribe<T>`. Los emisores no conocen a los oyentes: este mapa es
> la unica forma de ver la conexion completa.

**{len(todos)} tipos de evento.**

{tabla(["Evento", "Publica", "Escucha", "Quien lo publica", "Quien lo escucha"], filas)}

## Eventos con un solo lado ({len(huerfanos)})

{chr(10).join(f"- `{e}`" for e in huerfanos) if huerfanos else "Ninguno."}

Un evento que se publica y nadie escucha es trabajo tirado; uno que se escucha y nadie publica es una
feature que no llega a activarse. Los dos casos valen una revision.

## Reglas del bus

1. **Guardar el `IDisposable` en un campo y liberarlo en `OnDestroy`/`OnDisable`.** Un oyente que no se
   da de baja queda como referencia fake-null y `Publish` lo reporta como excepcion capturada.
2. **`Publish` es sincrono.** Lo que haga un oyente pasa dentro del `Publish`: cuidado con el orden.
3. **Una excepcion en un oyente no corta a los demas** (`EventBus.cs:54-67`), pero se loguea con nombre.
"""


def mapa_05(d):
    filas = [[f"`{const}`", f"`{clave}`"] for const, clave in d["keybindings"]]
    porarch = []
    for a in sorted(d["archivos"], key=lambda x: x["ruta"]):
        if a["teclas"]:
            porarch.append([f"`{a['nombre']}`", ", ".join(f"`{t}`" for t in a["teclas"][:18])])
    return f"""# Mapa de entrada

## Acciones reasignables (`Player/KeyBindings.cs`)

> Cada constante es una accion con tecla configurable, guardada en `PlayerPrefs`.
> Una accion que NO esta en esta tabla pero si se lee con `Keyboard.current.xKey` **no se puede
> reasignar**: eso es un bug (ver `Docs/RONDA_10_BUGS.md`, bug 76).

{tabla(["Constante", "Clave de PlayerPrefs"], filas) if filas else "_Sin bindings detectados._"}

## Teclas leidas directamente, por archivo

> Si un archivo que no es `KeyBindings.cs` ni `ControlsTable.cs` aparece aca con teclas de juego,
> conviene mirar si deberia pasar por `KeyBindings`.

{tabla(["Archivo", "Teclas que nombra"], porarch)}

## Los tres modos de entrada

| Modo | Quien lo atiende | Que manda |
|---|---|---|
| **FPS a pie** | `PlayerInputDriver.UpdateFps` | WASD, mouse, `[Q]` radial, `[Tab]` cambia de modo |
| **RTS** | `PlayerInputDriver.UpdateRts` | Arrastre de seleccion, clic derecho = orden, rueda = zoom |
| **En vehiculo** | `PlayerInputDriver.UpdateInVehicle` | Asientos `1/2/3`, mira del canon, `[E]` bajar |

Los tres son **ramas mutuamente excluyentes** del mismo `Update()`: una tecla puede significar cosas
distintas en cada uno sin colisionar.
"""


def mapa_06(d):
    a = d["assets"]
    def lista(xs, n=60):
        return "\n".join(f"- `{x}`" for x in xs[:n]) + (f"\n- …y {len(xs) - n} mas" if len(xs) > n else "")
    return f"""# Mapa de escenas y recursos

| Tipo | Cantidad |
|---|---|
| Escenas | {len(a["escenas"])} |
| Prefabs | {len(a["prefabs"])} |
| Materiales | {len(a["materiales"])} |
| Assets (`.asset`) | {len(a["assets"])} |
| Shaders | {len(a["shaders"])} |
| Input Actions | {len(a["inputactions"])} |

## Escenas

{lista(a["escenas"])}

## Shaders

{lista(a["shaders"])}

## Input Actions

{lista(a["inputactions"])}

## Prefabs

{lista(a["prefabs"], 80)}

## Como se construyen las escenas

Las escenas de este proyecto **no se arman a mano**: las genera codigo de Editor, y por eso son
reproducibles. Los constructores estan en `Assets/_Project/Scripts/Editor/`:

| Constructor | Que arma |
|---|---|
| `TutorialSceneBuilder` | `SC_Tutorial` entera (menu `Strategic Point > Tutorial > Construir escena`) |
| `MenuSceneBuilder` | `SC_MainMenu` |
| `MisionBuilder` | La mision principal |
| `LevelBlockoutBuilder` | El blocking de nivel con cubos |
| `WorldArtPipeline` / `BlockoutArtDresser` / `ArtBuilder` | Reemplaza el blocking por arte real |
| `SoldierPrefabPipeline` / `SoldierVariantBuilder` | Prefabs de soldado por clase |
| `WeaponPrefabBuilder` | Prefabs de arma desde los FBX |
| `NavMeshSetupPipeline` / `TerrainSetupHelper` | Navegacion y terreno |

**Consecuencia para una IA que edite el proyecto:** cambiar una escena a mano se pierde en la proxima
corrida del constructor. Lo que hay que editar es el constructor.
"""


def mapa_07(d):
    filas = [[f"`{Path(f['archivo']).name}`", f["lineas"], len(f["fases"]), f["checks"],
              ", ".join(f["fases"][:6]) or "—"] for f in d["suite"]]
    total_checks = sum(f["checks"] for f in d["suite"])
    sin = [a["ruta"] for a in d["archivos"]
           if a["subsistema"] in ("Ai", "Combat", "Player", "Vehicles", "Core", "Actors", "Mision")
           and a["checks"] == 0]
    return f"""# Mapa de pruebas

## La suite headless

Corre en **Edit mode** con un reloj simulado (`SimulateSeconds` / `SimulateUntil`), llamando al mismo
`WorldSimulationDriver.Step(dt)` que el juego real. Entrada:
`Strategic Point > Run All Tests Headless` o
`-executeMethod SP.EditorTools.HeadlessTestRunner.RunAll`.

**{total_checks} aserciones (`Check`) en {len(d["suite"])} archivos.**

{tabla(["Archivo", "Lineas", "Fases", "Checks", "Algunas fases"], filas)}

## Otras entradas de prueba

| Menu | Que hace |
|---|---|
| `Run All Tests Headless` | La suite entera. Sale con 0 si todo paso, 1 si algo fallo. |
| `Correr 100 iteraciones (flakiness y fugas de estado)` | Repite la suite 100 veces. Detecta estado estatico que sobrevive entre corridas. |
| `Benchmark de rendimiento` | Mide el costo por bloque de `Step` con carga creciente. |
| `Verificar equivalencia SpatialGrid` | Compara la grilla contra el barrido lineal original. |
| `Estres con carga realista (50+)` | Escenarios con 50 o mas unidades. |
| `Tutorial > Reproducir Tutorial Automatico` | El tutorial entero con gestos reales y capturas. |

## Fuera de la suite

| Herramienta | Que cubre |
|---|---|
| `Tools/Ci/verificar_estatico.py` | `.meta` faltantes, reglas de codigo, conteos de auditoria, escenas del build. Corre en cada push, sin licencia de Unity. |
| `Tools/Qa/qa_total.bat` | Corrida completa: suite + build + metricas + capturas en 7 resoluciones + tutorial automatico + todos los logs. |
| `Tools/Autoplay/autoplay.sh` | Solo el tutorial automatico, desde git-bash. |

## Archivos de logica sin una sola asercion ({len(sin)})

> Un archivo de `Ai`, `Combat`, `Player`, `Vehicles`, `Core`, `Actors` o `Mision` sin ningun `Check`
> puede estar cubierto desde otro archivo — pero vale mirarlo.

{chr(10).join(f"- `{x}`" for x in sin[:40])}{f"{chr(10)}- …y {len(sin) - 40} mas" if len(sin) > 40 else ""}
"""


def mapa_08(d):
    reinicio = ""
    rp = RAIZ / "Assets/_Project/Scripts/Core/ReinicioDeEstaticos.cs"
    if rp.exists():
        reinicio = rp.read_text(encoding="utf-8", errors="replace")

    filas = []
    for a in d["archivos"]:
        if a["subsistema"] == "Editor" or not a["estaticos_mutables"]:
            continue
        # ¿El propio archivo tiene un hook de reinicio? Eso cubre sus estaticos
        # sin pasar por ReinicioDeEstaticos, y es igual de valido.
        try:
            fuente = (RAIZ / a["ruta"]).read_text(encoding="utf-8", errors="replace")
        except OSError:
            fuente = ""
        hook_propio = "RuntimeInitializeOnLoadMethod" in fuente
        for e in a["estaticos_mutables"]:
            archivo = a["ruta"].split("/")[-1]
            en_reinicio = e["nombre"] in reinicio or any(t["nombre"] in reinicio for t in a["tipos"])
            if en_reinicio:
                cobertura = "`ReinicioDeEstaticos`"
            elif hook_propio:
                cobertura = "hook propio *(revisar si cubre este campo)*"
            else:
                cobertura = "**nada**"
            filas.append([
                f"`{e['nombre']}`",
                f"`{e['tipo']}`",
                f"`{archivo}:{e['linea']}`",
                cobertura,
            ])
    sin_reset = sum(1 for f in filas if f[3] == "**nada**")
    con_hook = sum(1 for f in filas if f[3].startswith("hook"))
    return f"""# Mapa de estado estatico

> Este proyecto corre con **"Enter Play Mode sin recarga de dominio"**: los `static` sobreviven de una
> partida a la siguiente dentro de la misma sesion de Editor. Cualquier estatico mutable que no se
> restablezca es una fuga de estado entre corridas — y la causa de los falsos fallos mas dificiles de
> encontrar.

**{len(filas)} estaticos mutables fuera de `Editor/`:
{len(filas) - sin_reset - con_hook} cubiertos por `ReinicioDeEstaticos`,
{con_hook} en archivos con hook propio (hay que mirar si el hook cubre ESE campo),
y {sin_reset} sin nada.**

{tabla(["Campo", "Tipo", "Donde", "Quien lo restablece"], filas)}

## Las dos formas validas de restablecer

1. **`ReinicioDeEstaticos.Restablecer()`** (`Core/ReinicioDeEstaticos.cs`), que corre en
   `SubsystemRegistration`, antes de que cargue ninguna escena. Es el sitio para el estado de juego.
2. **Un `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` propio** en
   la clase duena del estado. Es el sitio para el estado de un subsistema que se basta solo.

Un estatico que no esta en ninguna de las dos es deuda. Ver `Docs/RONDA_10_BUGS.md`, bug 1.

## Los dos relojes, y por que importa cual se usa

| Reloj | Avanza en Edit mode | Respeta pausa | Cuando usarlo |
|---|---|---|---|
| `Time.time` | **No** (congelado) | Sí | Casi nunca en logica de juego |
| `Time.unscaledTime` | **No** | No | Interfaz, avisos |
| `Time.realtimeSinceStartup` | Sí (reloj de pared) | No | Perfilado |
| **Acumulador de `dt`** | **Sí** (simulado) | Sí | **Toda la logica de juego** |

La convencion del proyecto, documentada en `Combat/Health.cs`, es el acumulador de `dt`: es el unico
que avanza igual en el juego real y en la suite headless. Un `Time.time` en logica de juego significa
que ese codigo **no se puede probar**.
"""


def main():
    solo_check = "--check" in sys.argv
    d = escanear()

    mapas = {
        "00_MAPA.md": mapa_00(d),
        "01_archivos.md": mapa_01(d),
        "02_simbolos.md": mapa_02(d),
        "03_dependencias.md": mapa_03(d),
        "04_eventos.md": mapa_04(d),
        "05_entrada.md": mapa_05(d),
        "06_escenas.md": mapa_06(d),
        "07_pruebas.md": mapa_07(d),
        "08_estaticos.md": mapa_08(d),
    }

    if solo_check:
        viejos = []
        for nombre, texto in mapas.items():
            p = SALIDA / nombre
            actual = p.read_text(encoding="utf-8") if p.exists() else ""
            # se ignora la linea de fecha, que cambia siempre
            a = re.sub(r"\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC", "", actual)
            b = re.sub(r"\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC", "", texto.rstrip() + "\n")
            if a != b:
                viejos.append(nombre)
        if viejos:
            print("El indice quedo viejo. Regeneralo con: py -3 Tools/Indice/indexar_ia.py")
            for v in viejos:
                print(f"  desactualizado: Docs/INDICE_IA/{v}")
            return 1
        print("Indice al dia.")
        return 0

    for nombre, texto in mapas.items():
        escribir(nombre, texto)
    SALIDA.mkdir(parents=True, exist_ok=True)
    (SALIDA / "indice.json").write_text(
        json.dumps(d, ensure_ascii=False, indent=1), encoding="utf-8"
    )

    print(f"Indice escrito en {SALIDA.relative_to(RAIZ)}:")
    for nombre in list(mapas) + ["indice.json"]:
        p = SALIDA / nombre
        print(f"  {nombre:24s} {p.stat().st_size:>8,} bytes")
    print(f"\n{len(d['archivos'])} scripts · "
          f"{sum(a['lineas'] for a in d['archivos']):,} lineas · "
          f"{len(d['assets']['escenas'])} escenas · {len(d['assets']['prefabs'])} prefabs")
    return 0


if __name__ == "__main__":
    sys.exit(main())
