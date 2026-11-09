#!/usr/bin/env bash
# Lee licencia.env, activa Unity, valida que abre el proyecto y (opcional) corre la suite.
#   ./activar_licencia.sh            activa y valida
#   ./activar_licencia.sh --tests    ademas corre la suite headless completa
set -u
AQUI="$(cd "$(dirname "$0")" && pwd)"
RAIZ="$(cd "$AQUI/../.." && pwd)"
UNITY="${UNITY_BIN:-/root/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
ENV="$AQUI/licencia.env"
LOG="${TMPDIR:-/tmp}/unity_licencia.log"

[ -x "$UNITY" ] || { echo "No encuentro Unity en $UNITY (define UNITY_BIN)"; exit 2; }
[ -f "$ENV" ]   || { echo "Falta $ENV. Copia licencia.env.plantilla a licencia.env y completalo."; exit 2; }
# shellcheck disable=SC1090
. "$ENV"

run() { xvfb-run -a "$UNITY" -batchmode -nographics -quit -logFile "$LOG" "$@"; }

if [ -n "${UNITY_ULF:-}" ]; then
  [ -f "$UNITY_ULF" ] || { echo "No existe UNITY_ULF=$UNITY_ULF"; exit 2; }
  echo "[1/3] Activando con .ulf manual..."
  run -manualLicenseFile "$UNITY_ULF"
elif [ -n "${UNITY_SERIAL:-}" ] && [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ]; then
  echo "[1/3] Activando con serie..."
  run -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD"
else
  echo "licencia.env esta incompleto: completa la opcion A (UNITY_ULF) o la B (serie + email + password)."; exit 2
fi
grep -qi "No valid Unity Editor license" "$LOG" && { echo "Activacion fallida, ver $LOG"; exit 3; }

echo "[2/3] Abriendo el proyecto (primera vez importa assets: puede tardar bastante)..."
run -projectPath "$RAIZ"; RC=$?
echo "      codigo de salida: $RC (0 = el proyecto abrio e importo bien). Log: $LOG"
[ "$RC" -eq 0 ] || exit "$RC"

if [ "${1:-}" = "--tests" ]; then
  echo "[3/3] Corriendo suite headless..."
  xvfb-run -a "$UNITY" -batchmode -nographics -projectPath "$RAIZ" \
    -executeMethod SP.EditorTools.HeadlessTestRunner.RunAll -logFile "${TMPDIR:-/tmp}/unity_suite.log"
  echo "Suite: codigo $? (0 = todo OK). Resultado: $RAIZ/Temp/suite_result.txt"
fi
