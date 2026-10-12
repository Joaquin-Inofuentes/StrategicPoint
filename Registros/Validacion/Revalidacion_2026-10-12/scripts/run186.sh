source /tmp/e4/lib.sh; R() { sed 's/.*"result":"//; s/"}.*//'; }
until unity command eval --caller plugin --skill unity-cli 'return UnityEngine.Application.isPlaying && SP.Player.PlayerInputDriver.Activo != null;' 2>&1 | grep -q '"result":true'; do sleep 2; done
U 'return SP.EditorTools.OperacionPrueba.Arrancar(3);' 200 | R; sleep 3
U "$(cat /tmp/e/s.cs)" 200 | R; sleep 1
U 'SP.Core.GameLog.Clear(); return 1;' 50 >/dev/null
U "$(cat /tmp/e/m186.cs)" 200 | R; sleep 12
U "$(cat /tmp/e/mres.cs)" 900 | R
U "$(cat /tmp/e5/d0.cs)" 3000 | R
U "$(cat /tmp/e/m177.cs)" 200 | R; sleep 12
U "$(cat /tmp/e/mres.cs)" 900 | R
U "$(cat /tmp/e5/d0.cs)" 3000 | R
