source /tmp/e/lib.sh
cd "C:/_Proyectos privados/DV_C6_StrategicPoint"
D=$1; mkdir -p Registros/Validacion/B175-193/$D; N=Registros/Validacion/B175-193/$D/numeros.txt; : > $N
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 3
unity command editor_play --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 9
U 'return SP.EditorTools.OperacionPrueba.Arrancar(3);' 80 >/dev/null; sleep 3
UF s.cs 80 >/dev/null; sleep 1
UF s1.cs 100 >/dev/null; sleep 1; cam 30 692 698 >/dev/null; sleep 2; UF s1b.cs 60 >/dev/null; sleep 1.5; cap $D/178_188_189_rts_circulo_cartel_coberturas
UF muni.cs 60 >/dev/null; wf $D/192_rombo_de_municion 686 0.3 693 50 0 5
UF noche1.cs 60 >/dev/null; sleep 2.5; wf $D/175_enemigo_de_noche 692 1 700 35 90 9
UF noche2.cs 60 >/dev/null; sleep 1; wf $D/176_enemigo_herido 692 1 700 35 90 9
UF noche3.cs 60 >/dev/null; sleep 2.5; wf $D/176_cadaver_y_municion 692 1 700 35 90 9
UF dia.cs 60 >/dev/null
UF mg.cs 60 >/dev/null; sleep 1.3; wf $D/187_a_metralla_enemiga 689 0.5 697 40 90 11; sleep 3.5
UF canon.cs 60 >/dev/null; sleep 0.5; wf $D/187_b_canon_explosion 689 0.5 697 40 90 11
UF esc.cs 60 >/dev/null; sleep 0.4; wf $D/183_a_derrumbe_0_4s 710 1 700 40 0 14; sleep 2.6; wf $D/183_b_derrumbe_3s 710 1 700 40 0 14
echo "### #190 aliados defensivos con cobertura por orden vs 3 enemigos (24 s de juego)" >> $N; UF m190.cs 200 >> $N; sleep 9; UF m190r.cs 300 >> $N
echo "### #186 8 enemigos por un paso de 3 m (30 s de juego)" >> $N; UF m186.cs 200 >> $N; sleep 11; UF mres.cs 900 >> $N
echo "### #177/#182 3 aliados con orden de mover por el mismo paso (30 s de juego)" >> $N; UF m177.cs 200 >> $N; sleep 11; UF mres.cs 900 >> $N
cat $N | cut -c1-400
