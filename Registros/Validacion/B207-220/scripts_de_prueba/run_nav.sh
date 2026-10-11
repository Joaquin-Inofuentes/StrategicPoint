D4=/tmp/e4
source $D4/lib.sh
cd "C:/_Proyectos privados/DV_C6_StrategicPoint"
D=$1; mkdir -p Registros/Validacion/B207-220/$D; N=Registros/Validacion/B207-220/$D/numeros.txt; : > $N
log() { echo "$1" >> $N; }
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 3
unity command clear_console --caller plugin --skill unity-cli >/dev/null 2>&1
unity command editor_play --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 12
U 'return SP.EditorTools.OperacionPrueba.Arrancar(3, 1, 0);' 80 >/dev/null; sleep 4

# ---------- #212 / #214 enemigos contra muros y barricadas (centro de datos)
U 'return "agentClimb del NavMesh=" + UnityEngine.AI.NavMesh.GetSettingsByID(0).agentClimb;' 200 | R > $D4/climb.txt
log "### #212/#214 enemigos de las reservas del centro de datos rumbo al jugador (45 s de juego)"
log "$(cat $D4/climb.txt)"
UF $D4/t212.cs 300 | R >> $N
sleep 4; wf $D/212_214_barricada_a_los_4s -47.0 0.8 -53.0 40 90 22
sleep 8; wf $D/212_214_barricada_a_los_12s -47.0 0.8 -53.0 40 90 22
sleep 10; UF $D4/t212b.cs 700 | R >> $N
U 'foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player) { s.Brain.Pasivo = false; s.Health.Invulnerable = true; } foreach (var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsSortMode.None)) if (s.name.StartsWith("Reserva_CD")) s.gameObject.SetActive(false); return 1;' 60 >/dev/null

unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; echo listo
