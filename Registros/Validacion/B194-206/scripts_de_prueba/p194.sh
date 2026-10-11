source /tmp/e2/lib.sh
D=$1
wf() { U "$(sed "s#__N__#$1#; s/__X__/$2/; s/__Y__/$3/; s/__Z__/$4/; s/__P__/$5/; s/__W__/$6/; s/__D__/${7:-8}/" /tmp/e2/wf.cs)" 80 >/dev/null; }
unity command editor_stop --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 3
unity command editor_play --caller plugin --skill unity-cli >/dev/null 2>&1; sleep 10
U 'return SP.EditorTools.OperacionPrueba.Arrancar(1);' 80 >/dev/null; sleep 5
U 'foreach (var s in SP.Core.ActorRegistry.All) { if (s != null && s.Team == SP.Combat.TeamId.Enemy) { s.gameObject.SetActive(true); break; } } return 1;' 60 >/dev/null; sleep 1.5
UF /tmp/e2/i194d.cs 300 | sed 's/.*"result":"//; s/"}.*//'
wf $D/194_enemigo_vs_aliado 0 500.9 0 0 180 4.5
