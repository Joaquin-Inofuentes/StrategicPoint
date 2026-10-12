source /tmp/e2/lib.sh
cd "C:/_Proyectos privados/DV_C6_StrategicPoint"
D=$1; mkdir -p Registros/Validacion/B194-206/$D; N=Registros/Validacion/B194-206/$D/numeros.txt; : > $N
log() { echo "$1" >> $N; }
R() { sed 's/.*"result":"//; s/"}.*//' ; }
wf() { U "$(sed "s#__N__#$1#; s/__X__/$2/; s/__Y__/$3/; s/__Z__/$4/; s/__P__/$5/; s/__W__/$6/; s/__D__/${7:-8}/" /tmp/e2/wf.cs)" 80 >/dev/null; }
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 3
unity command editor_play --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 10
# ---------- #194 (Infiltrar: de noche, con enemigos)
U 'return SP.EditorTools.OperacionPrueba.Arrancar(1);' 80 >/dev/null; sleep 4
r=$(UF /tmp/e2/i194.cs 300 | R); log "### #194 enemigo de cerca: $r"
U 'var p=(UnityEngine.Vector3)System.AppDomain.CurrentDomain.GetData("zzE"); var ci=System.Globalization.CultureInfo.InvariantCulture; return p.x.ToString("0.0",ci)+" "+p.y.ToString("0.0",ci)+" "+p.z.ToString("0.0",ci);' 300 | R > /tmp/e2/pe.txt; read PX PY PZ < /tmp/e2/pe.txt
wf $D/194_enemigo_de_cerca_frente $PX 1.0 $PZ 8 0 4.5
wf $D/194_enemigo_de_cerca_costado $PX 1.0 $PZ 8 90 4.5
# ---------- base: centro de datos
U 'return SP.EditorTools.OperacionPrueba.Arrancar(3);' 80 >/dev/null; sleep 4
# #206
log "### #206 seleccion al pasar de RTS a FPS"; UF /tmp/e2/t206.cs 200 | R >> $N; sleep 1.5; cap $D/206_fps_tras_tab_con_seleccion
# #197/#198
U 'var d=SP.Player.PlayerInputDriver.Activo; if(d.Rig.Mode!=SP.CameraSystem.ControlMode.Rts) d.AlternarVista(); return 1;' 40 >/dev/null; sleep 1.5
UF /tmp/e2/h1.cs 300 >/dev/null; sleep 1.5; UF /tmp/e2/h2.cs 80 >/dev/null; sleep 2
log "### #197/#198 clic derecho sobre aliado herido en RTS"; UF /tmp/e2/h3.cs 300 | R >> $N; sleep 0.5; cap $D/197_198_rts_curar_aliado
# #196
log "### #196 destino sobre un solido"; UF /tmp/e2/s196.cs 400 | R >> $N; sleep 3.5; cap $D/196_destino_bloqueado_rts
# #205
log "### #205 gesto [Espacio] sobre aliado"; UF /tmp/e2/s205.cs 300 | R >> $N; sleep 4; UF /tmp/e2/r205.cs 200 | R >> $N; cap $D/205_aliado_llevado_con_espacio
# #203
log "### #203 limite de paneo de camara"; UF /tmp/e2/s203.cs 300 | R >> $N
# #195 / #204 (FPS)
log "### #195 rafaga a la cadera"; UF /tmp/e2/s195.cs 300 | R >> $N
log "### #204 efectos de la boca del arma con zoom"
sleep 1; U 'return "cola="+SP.Presentation.ImpactFx.ActiveCount;' 60 | R >> $N
sleep 1.5; UF <(sed 's/__Z__/false/g' /tmp/e2/s204.cs) 300 | R >> $N
sleep 2; UF <(sed 's/__Z__/true/g' /tmp/e2/s204.cs) 300 | R >> $N
# #201
U 'return SP.EditorTools.OperacionPrueba.Arrancar(4);' 80 >/dev/null; sleep 3
log "### #201 demolicion de un muro (camara lenta x0,3)"; r=$(UF /tmp/e2/s201.cs 400 | R); log "$r"
CX=-2.2; CY=2.8; CZ=-22.0
sleep 1.0; wf $D/201_a_muro_roto_0_3s $CX $CY $CZ 12 90 22; sleep 1.5; wf $D/201_b_muro_roto_1s $CX $CY $CZ 12 90 22
U 'UnityEngine.Time.timeScale = 1f; return 1;' 40 >/dev/null
# #202
log "### #202 helicoptero enemigo en la huida (x4)"; U 'UnityEngine.Time.timeScale = 4f; return 1;' 40 >/dev/null
U "return SP.EditorTools.OperacionPrueba.Arrancar(4, 1, 4);" 80 >/dev/null; U "UnityEngine.Time.timeScale = 4f; return 1;" 40 >/dev/null
for i in $(seq 1 12); do sleep 5; UF /tmp/e2/s202.cs 300 | R >> $N; done
U 'UnityEngine.Time.timeScale = 1f; return 1;' 40 >/dev/null
cat $N | cut -c1-330
