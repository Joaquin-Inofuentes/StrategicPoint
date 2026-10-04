#!/bin/bash
# Arranca la Operacion Cuartel desde el objetivo X (1..6) y deja el estado marcado (json + captura en Registros/).
#   uso: Tools/operacion_desde_objetivo.sh <objetivo 1-6> [puesto 1-3 (solo objetivo 2)]
#        Tools/operacion_desde_objetivo.sh todos        (recorre los 6 y escribe el informe)
# 1 Infiltrar - 2 Puestos de control - 3 Centro de datos - 4 Huir en el tanque - 5 Resistir en la ciudad - 6 Subir al helicoptero
# El editor queda en Play en ese objetivo. Para volver a un estado marcado: SesionLog.RestaurarBug(n).
U="${UNITY_CLI:-C:/Users/PC_JOACO/AppData/Local/Unity/bin/unity.exe}"
P="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || pwd)"
OBJ="${1:?objetivo 1-6 o 'todos'}"; PUESTO="${2:-1}"
ev() { local f; f="$(mktemp --suffix=.cs)"; printf '%s\n' "$1" > "$f"; "$U" cmd eval_file --project-path "$P" --json --result-only -- --file "$f"; rm -f "$f"; }
ev 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/SC_Operacion.unity"); return "escena ok";'
"$U" cmd editor_play --project-path "$P" --json > /dev/null; sleep 15
if [ "$OBJ" = "todos" ]; then
  ev 'SP.EditorTools.OperacionPrueba.Recorrer(4f); return "recorriendo";'
  sleep 45; ev 'return SP.EditorTools.OperacionPrueba.Informe;'
else
  ev "return SP.EditorTools.OperacionPrueba.Arrancar($OBJ, $PUESTO);"
  sleep 3; ev 'return "bug #" + SP.Core.SesionLog.MarcarBug("arranque CLI objetivo '"$OBJ"'");'
fi
echo "Listo. Estados en Registros/."
