R() { sed 's/.*"result":"//; s/"}.*//'; }
cd /tmp/e2 && bash "/c/_Proyectos privados/DV_C6_StrategicPoint/Registros/Validacion/B194-206/scripts_de_prueba/run2.sh" revalida_1012 > /tmp/e5/out2.txt 2>&1
cd /tmp/e4 && bash "/c/_Proyectos privados/DV_C6_StrategicPoint/Registros/Validacion/B207-220/scripts_de_prueba/run.sh" revalida_1012 > /tmp/e5/out3.txt 2>&1
source /tmp/e4/lib.sh
N="C:/_Proyectos privados/DV_C6_StrategicPoint/Registros/Validacion/B168-174_revalida_1012.txt"; : > "$N"
unity command editor_play --caller plugin --skill unity-cli >/dev/null 2>&1
until unity command eval --caller plugin --skill unity-cli 'return UnityEngine.Application.isPlaying && SP.Player.PlayerInputDriver.Activo != null;' 2>&1 | grep -q '"result":true'; do sleep 2; done
U 'return SP.EditorTools.OperacionPrueba.Arrancar(1);' 200 | R >> "$N"; sleep 4
UF /tmp/e5/a.cs 600 | R >> "$N"; sleep 4
UF /tmp/e5/b.cs 600 | R >> "$N"
cap revalida_1012/169_tras_derribar_las_2_torretas
U 'return SP.EditorTools.OperacionPrueba.Arrancar(4, 1, 4);' 200 | R >> "$N"; sleep 3
UF /tmp/e5/c.cs 600 | R >> "$N"
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1
echo listo
