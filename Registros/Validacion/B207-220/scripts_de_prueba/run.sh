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

# ---------- #207 cuerpo flotando
log "### #207 enemigo muerto que queda en el aire"
UF $D4/t207a.cs 300 | R >> $N
sleep 4; UF $D4/t207b.cs 300 | R >> $N

# ---------- #209 linea roja de un blanco caido
log "### #209 linea roja hacia un enemigo ya muerto"
UF $D4/t209a.cs 300 | R >> $N; sleep 1.5
UF $D4/t209b.cs 300 | R >> $N
cap $D/209_a_linea_con_el_enemigo_vivo
UF $D4/t209c.cs 300 | R >> $N; sleep 1.5
UF $D4/t209b.cs 300 | R >> $N
cap $D/209_b_linea_tras_morir_y_retirarse


# ---------- #208 / #213 cursor en RTS
log "### #208/#213 cursor al apuntar a interactuables desde RTS"
UF $D4/t208.cs 400 | R >> $N

# ---------- #218 siganme solo a los cercanos
log "### #218 SIGANME con un aliado a 90 m"
UF $D4/t218.cs 400 | R >> $N

# ---------- #215 subir al tanque desde RTS
log "### #215 clic derecho sobre el tanque desde RTS"
UF $D4/t215.cs 400 | R >> $N

# ---------- #219 transparencia (RTS)
log "### #219 pared entre la camara y el seleccionado (RTS)"
UF $D4/t219a.cs 400 | R >> $N; sleep 2.0
UF $D4/t219b.cs 300 | R >> $N
cap $D/219_pared_entre_camara_y_seleccionado
UF $D4/t219c.cs 100 >/dev/null; sleep 1.5

# ---------- #211 mira en FPS + #210 clases
log "### #211 mira de FPS al apuntar a un interactuable"
UF $D4/t211.cs 500 | R >> $N
UF $D4/t211b.cs 100 >/dev/null; sleep 0.3; cap $D/211_mira_fps_sobre_interactuable
log "### #210 clases de la escuadra y medico"
UF $D4/t210.cs 500 | R >> $N

# ---------- fase de huida
U 'return SP.EditorTools.OperacionPrueba.Arrancar(4, 1, 4);' 80 >/dev/null; sleep 5
log "### #216 obstaculos estaticos del camino de la huida"
UF $D4/t216.cs 300 | R >> $N
if [ -s $D4/obs.txt ]; then read OX OY OZ < $D4/obs.txt; wf $D/216_camino_de_la_huida $OX 1.0 $OZ 20 90 30; fi
log "### #217 elevacion del canon del tanque"
UF $D4/t217.cs 400 | R >> $N

# ---------- #220 toma de victoria
log "### #220 toma de camara al ganar"
UF $D4/t220b.cs 200 | R >> $N; cap $D/220_a_camara_antes_de_ganar
UF $D4/t220.cs 300 | R >> $N; sleep 3; cap $D/220_b_toma_de_victoria
UF $D4/t220b.cs 200 | R >> $N
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1
echo listo
