D4=/tmp/e4
source $D4/lib.sh
cd "C:/_Proyectos privados/DV_C6_StrategicPoint"
D=$1; mkdir -p Registros/Validacion/B207-220/$D; N=Registros/Validacion/B207-220/$D/numeros_199_200.txt; : > $N
log() { echo "$1" >> $N; }
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 3
unity command editor_play --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 12
U 'return SP.EditorTools.OperacionPrueba.Arrancar(3, 1, 0);' 80 >/dev/null; sleep 4
log "### #199 aliados siguiendo mientras el jugador camina en circulo (24 s)"
UF $D4/t199a.cs 300 | R >> $N
for i in $(seq 1 16); do UF $D4/t199step.cs 40 >/dev/null; sleep 1.5; if [ $((i % 4)) -eq 0 ]; then UF $D4/t199m.cs 300 | R >> $N; fi; done
cap $D/199_seguimiento_en_movimiento
log "### #200 ordenes a destinos dentro de solidos (que no se pueden alcanzar)"
UF $D4/t200a.cs 400 | R >> $N
for i in 1 2 3; do sleep 4; UF $D4/t200b.cs 300 | R >> $N; done
cap $D/200_ordenes_a_destinos_inalcanzables
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; echo listo
