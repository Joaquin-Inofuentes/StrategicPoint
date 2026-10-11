source /tmp/e2/lib.sh; D=$1; N="C:/_Proyectos privados/DV_C6_StrategicPoint/Registros/Validacion/B194-206/$D/numeros.txt"
R() { sed 's/.*"result":"//; s/"}.*//'; }
U 'return SP.EditorTools.OperacionPrueba.Arrancar(4, 1, 4);' 80 >/dev/null; sleep 3
echo "### #202 tanque a 40 m de la emboscada de cohetes del canadon" >> "$N"; UF /tmp/e2/e202.cs 300 | R >> "$N"
for i in 1 2 3 4 5 6; do sleep 2.5; UF /tmp/e2/e202b.cs 200 | R >> "$N"; done; cap $D/202_emboscada_del_canadon
