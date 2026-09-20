#!/bin/bash
# Corre el reproductor automatico del tutorial en el editor de Unity, lo vigila y deja el registro para analizarlo.
#
#   Tools/Autoplay/autoplay.sh [--desde N] [--max SEG] [--sin-capturas] [--humano] [--sin-compilar]
#
#   --desde N        arranca en el paso N (los anteriores se adelantan con SaltarPaso). Por defecto 0 = corrida completa.
#   --max SEG        tope de toda la corrida (defecto 1500).
#   --sin-capturas   no saca capturas (mas rapido).
#   --humano         NO ignora el teclado/mouse reales (por defecto quedan desactivados mientras corre).
#   --sin-compilar   no fuerza la recompilacion antes de empezar.
#
# Que hace, en orden: 1) comprueba que el editor responda (si no esta abierto lo abre; reintenta con espera creciente),
# 2) recompila y aborta si hay errores, 3) abre SC_Tutorial y entra en Play, 4) lanza el reproductor y guarda su runId,
# 5) vigila Logs/Autoplay/estado_actual.json (lo escribe el juego cada segundo; NO depende del CLI) y solo si el archivo
# se congela consulta al editor por eval, 6) al terminar (o si algo falla o se corta con Ctrl+C) para Play y devuelve el
# teclado y el mouse a la persona, 7) imprime resumen.txt y una estimacion de tiempo con el historial de corridas.
# Registro: Logs/Autoplay/<runId>/{log.jsonl,resumen.json,resumen.txt,estado.json,cap/*.jpg}  y  Logs/Autoplay/historial.jsonl
# Salida: 0 si la corrida quedo COMPLETA sin pasos fallidos; 1 si hubo fallos; 2 si no se pudo correr.

DESDE=0; MAX=1500; CAPTURAS=true; IGNORAR=true; COMPILAR=true
while [ $# -gt 0 ]; do
  case "$1" in
    --desde) DESDE="$2"; shift 2;;
    --max) MAX="$2"; shift 2;;
    --sin-capturas) CAPTURAS=false; shift;;
    --humano) IGNORAR=false; shift;;
    --sin-compilar) COMPILAR=false; shift;;
    *) echo "argumento desconocido: $1"; exit 2;;
  esac
done

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$RAIZ" || exit 2
U="${UNITY_CLI:-/c/Users/PC_JOACO/AppData/Local/Unity/bin/unity.exe}"
EDITOR_EXE="${UNITY_EDITOR_EXE:-C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe}"
DIR_LOGS="Logs/Autoplay"; ESTADO="$DIR_LOGS/estado_actual.json"
T_INICIO=$(date +%s)
RUNID=""

ts(){ echo "$(date +%H:%M:%S) [+$(( $(date +%s) - T_INICIO ))s]"; }
avisar(){ echo "$(ts) $*"; }

# cli: ejecuta el CLI de Unity con reintentos (espera creciente 2,4,8 s). Devuelve el JSON crudo.
cli(){
  local n=0 out
  while [ $n -lt 4 ]; do
    out=$("$U" --no-banner cmd "$@" 2>&1)
    if echo "$out" | python -c "import sys,json;d=json.load(sys.stdin);sys.exit(0 if d.get('success') else 1)" 2>/dev/null; then echo "$out"; return 0; fi
    n=$((n+1)); sleep $((2**n))
  done
  echo "$out"; return 1
}

# ev: evalua C# en el editor y devuelve el resultado como texto ("ERR:..." si falla).
ev(){
  local out
  out=$(cli eval --json -- --code "$1") || { echo "ERR:cli"; return 1; }
  echo "$out" | python -c "
import sys,json
d=json.load(sys.stdin); r=d['data']['result']
print(r.get('result') or ('ERR:'+str(r.get('diagnostics') or r.get('error'))))" 2>/dev/null || echo "ERR:parse"
}

editor_responde(){ [ "$(ev 'return "ok";')" = "ok" ]; }

# Siempre, pase lo que pase: se para Play y el teclado/mouse reales vuelven a la persona.
limpiar(){
  trap - EXIT INT TERM
  avisar "limpieza: devolviendo teclado y mouse, parando Play..."
  "$U" --no-banner cmd eval --json -- --code 'SP.Tutorial.EntradaVirtual.IgnorarHumano = false; SP.Tutorial.EntradaVirtual.RestaurarHumano(); return "ok";' >/dev/null 2>&1
  "$U" --no-banner cmd editor_stop --json >/dev/null 2>&1
}
trap limpiar EXIT INT TERM

# 1) Editor vivo
avisar "comprobando editor..."
if ! editor_responde; then
  avisar "el editor no responde; se intenta abrir Unity ($EDITOR_EXE)"
  powershell.exe -NoProfile -Command "Start-Process '$EDITOR_EXE' -ArgumentList '-projectPath','$(cygpath -w "$RAIZ")'" >/dev/null 2>&1
  for i in $(seq 1 60); do sleep 6; editor_responde && break; done
  editor_responde || { avisar "ERROR: el editor no respondio en 6 min."; exit 2; }
fi

# 2) Compilacion
if $COMPILAR; then
  rm -f Temp/pipeline_recompile_status.json
  cli recompile --json >/dev/null
  for i in $(seq 1 40); do sleep 4; [ -s Temp/pipeline_recompile_status.json ] && break; done
  ESTADO_COMP=$(cat Temp/pipeline_recompile_status.json 2>/dev/null)
  avisar "compilacion: ${ESTADO_COMP:-sin estado}"
  if echo "$ESTADO_COMP" | grep -q '"failed":true'; then avisar "ERROR: el proyecto no compila."; exit 2; fi
  for i in $(seq 1 30); do editor_responde && break; sleep 4; done
fi

# 3) Play sobre SC_Tutorial
cli editor_stop --json >/dev/null; sleep 4
[ "$(ev 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/SC_Tutorial.unity"); return "abierta";')" = "abierta" ] || { avisar "ERROR: no se pudo abrir SC_Tutorial"; exit 2; }
cli editor_play --json >/dev/null
LISTO=""
for i in $(seq 1 40); do
  LISTO=$(ev 'return SP.Tutorial.TutorialManager.Instance==null?"null":"ok";'); [ "$LISTO" = "ok" ] && break; sleep 3
done
[ "$LISTO" = "ok" ] || { avisar "ERROR: el tutorial no arranco en Play."; exit 2; }

# 4) Lanzar
rm -f "$ESTADO"
RUNID=$(ev "return SP.Tutorial.TutorialAutoPlayer.Lanzar($DESDE, $IGNORAR, $CAPTURAS).RunId;")
case "$RUNID" in ERR:*|"") avisar "ERROR: no se pudo lanzar el reproductor: $RUNID"; exit 2;; esac
avisar "corrida $RUNID lanzada (desde=$DESDE, entrada humana ignorada=$IGNORAR, capturas=$CAPTURAS)"

# 5) Vigilar por archivo; si se congela, por eval
leer(){ python - "$ESTADO" <<'PY'
import sys,json
try:
    d=json.load(open(sys.argv[1]))
    print(d['runId'],'1' if d['corriendo'] else '0',d['indice']+1,d['total'],d['id'] or '-',d['fase'],d['ok'],d['fallidos'],d['errores'],d['capturas'],int(d['tRealS']),int(d['fps']))
except Exception as e:
    print('ERR')
PY
}
PREV=""; ULT_CAMBIO=$(date +%s); LIMITE=$(( $(date +%s) + MAX )); ESTADO_FINAL=""
while [ $(date +%s) -lt $LIMITE ]; do
  L=$(leer)
  if [ "$L" != "ERR" ]; then
    set -- $L
    if [ "$1" = "$RUNID" ]; then
      CLAVE="$3 $5 $2"
      if [ "$CLAVE" != "$PREV" ]; then
        avisar "paso $3/$4 $5 [$6] ok=$7 fallidos=$8 errores=$9 capturas=${10} fps=${12}"
        PREV="$CLAVE"; ULT_CAMBIO=$(date +%s)
      fi
      [ "$2" = "0" ] && { ESTADO_FINAL="fin"; break; }
    fi
  fi
  # Sin cambios en 240 s: fallback, se le pregunta al editor (puede que Play se haya cortado o el juego se colgo).
  if [ $(( $(date +%s) - ULT_CAMBIO )) -gt 240 ]; then
    avisar "aviso: 240 s sin cambios en estado_actual.json; se consulta al editor"
    R=$(ev 'var tm=SP.Tutorial.TutorialManager.Instance; return tm==null?"sin-tutorial":(tm.PasoActual!=null?tm.PasoActual.Id:"fin");')
    avisar "editor dice: $R"
    case "$R" in ERR:*|sin-tutorial) ESTADO_FINAL="sin-respuesta"; break;; esac
    ULT_CAMBIO=$(date +%s)
  fi
  sleep 3
done
[ -z "$ESTADO_FINAL" ] && ESTADO_FINAL="tiempo-agotado"
avisar "vigilancia terminada: $ESTADO_FINAL"

# 6) Resultado y estimacion de tiempos
CARPETA="$DIR_LOGS/$RUNID"
sleep 2
if [ -f "$CARPETA/resumen.txt" ]; then
  echo "=================== resumen.txt ==================="
  cat "$CARPETA/resumen.txt"
  echo "==================================================="
else
  avisar "no hay resumen.txt (la corrida no llego a cerrarse). Revisar $CARPETA/log.jsonl"
fi
if [ -f "$DIR_LOGS/historial.jsonl" ]; then
  python - "$DIR_LOGS/historial.jsonl" <<'PY'
import sys,json
corridas=[json.loads(l) for l in open(sys.argv[1]) if l.strip()]
completas=[c for c in corridas if c.get('resultado','').startswith('COMPLETA')]
print('historial: %d corridas (%d completas)'%(len(corridas),len(completas)))
if completas:
    tot=[c['durRealS'] for c in completas]
    print('duracion real de una corrida completa: media %.0f s, min %.0f s, max %.0f s'%(sum(tot)/len(tot),min(tot),max(tot)))
    ids=[]
    for c in completas:
        for k in c['pasos']:
            if k not in ids: ids.append(k)
    print('media por paso (s): '+', '.join('%s=%.0f'%(k,sum(c['pasos'].get(k,0) for c in completas)/len(completas)) for k in ids))
PY
fi
avisar "registro: $CARPETA"

RES=$(python -c "import json;print(json.load(open('$CARPETA/resumen.json'))['resultado'])" 2>/dev/null)
[ "$RES" = "COMPLETA" ] && exit 0
exit 1
