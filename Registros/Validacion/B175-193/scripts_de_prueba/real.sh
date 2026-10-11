source /tmp/e/lib.sh
cd "C:/_Proyectos privados/DV_C6_StrategicPoint"
D=$1; MODO=$2   # MODO = nuevo | viejo
mkdir -p Registros/Validacion/B175-193/$D; N=Registros/Validacion/B175-193/$D/numeros_real.txt; : > $N
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 3
unity command editor_play --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 9
U 'var s = UnityEngine.InputSystem.InputSystem.settings; s.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus; return "ok";' 50 >/dev/null
U 'return SP.EditorTools.OperacionPrueba.Arrancar(3);' 80 >/dev/null; sleep 3
# ---- #180: clic derecho en RTS sobre la computadora
UF c1.cs 200 >/dev/null; sleep 1; UF c2.cs 60 >/dev/null; sleep 0.5
cap $D/180_a_rts_antes_del_clic
if [ "$MODO" = nuevo ]; then UF c5.cs 150 >> $N; else UF c5v.cs 150 >> $N; fi
sleep 3; cap $D/180_b_rts_despues_del_clic
echo "### #180 estado del hackeo a los 3 s" >> $N; U 'var d=SP.Operacion.OperacionDirector.Instancia; return "hack operador="+(d.GetType().GetField("computadora", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(d) as SP.Interaction.IOrdenableAAliados).Progreso01;' 200 >> $N
# ---- #184: camionetas del centro al empezar la huida
if [ "$MODO" = viejo ]; then UF c6.cs 150 >> $N; fi
U 'UnityEngine.Time.timeScale = 4f; return "x4";' 40 >/dev/null
for i in $(seq 1 10); do sleep 4; r=$(U 'var d=SP.Operacion.OperacionDirector.Instancia; return d.CamionetasDelCentroVivas;' 60 | grep -o '"result":[0-9]*'); [ "$r" = '"result":4' ] && break; done
U 'UnityEngine.Time.timeScale = 1f; return 1;' 40 >/dev/null; UF camw.cs 60 >/dev/null; sleep 1.5; cap $D/184_a_camionetas_del_centro
U 'var d=SP.Operacion.OperacionDirector.Instancia; d.EntrarFase(SP.Operacion.FaseOperacion.Huir); return 1;' 40 >/dev/null; sleep 2.5; cap $D/184_b_al_empezar_la_huida
echo "### #184 vehiculos al empezar la huida (antes: 4 camionetas del centro)" >> $N
U 'var d=SP.Operacion.OperacionDirector.Instancia; int n=0; string cs=""; foreach(var a in UnityEngine.Object.FindObjectsByType<SP.Operacion.OperacionAuto>(UnityEngine.FindObjectsSortMode.None)) { n++; cs+=a.name+" "; } return d.Fase+" autos="+n+" "+cs;' 300 >> $N
# ---- #191: camioneta de oleada
U 'return SP.EditorTools.OperacionPrueba.Arrancar(5);' 80 >/dev/null; sleep 2
UF l191.cs 200 >/dev/null
U 'UnityEngine.Time.timeScale = 4f; return "x4";' 40 >/dev/null
echo "### #191 camioneta de oleada: distancia al soldado mas cercano cada ~1,5 s reales (x4)" >> $N
for i in $(seq 1 12); do sleep 1.5; UF p191.cs 300 | sed 's/.*"result":"//; s/"}.*//' >> $N; done
U 'UnityEngine.Time.timeScale = 1f; return 1;' 40 >/dev/null
cat $N | cut -c1-300
# ---- #193: enemigos disparando a edificios (fase Extraer del registro: helicoptero y 0 aliados)
U 'return SP.EditorTools.OperacionPrueba.Arrancar(6);' 100 >/dev/null; sleep 2
UF e193a.cs 60 >/dev/null
U 'UnityEngine.Time.timeScale = 4f; return "x4";' 40 >/dev/null
echo "### #193 Extraer: disparos de enemigos vs impactos en paredes (40 s reales a x4)" >> $N
for i in 1 2 3 4; do sleep 10; UF e193b.cs 300 | sed 's/.*"result":"//; s/"}.*//' >> $N; done
cap $D/193_extraer_enemigos
U 'UnityEngine.Time.timeScale = 1f; return 1;' 40 >/dev/null
tail -5 $N | cut -c1-300
