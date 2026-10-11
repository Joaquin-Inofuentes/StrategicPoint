P="C:/_Proyectos privados/DV_C6_StrategicPoint"
U() { unity command eval --timeout 100 --caller plugin --skill unity-cli "$1" 2>&1 | grep -a "^eval" | head -1 | cut -c1-${2:-300}; }
UF() { U "$(cat $1)" ${2:-300}; }
R() { sed 's/.*"result":"//; s/"}.*//'; }
cap() { U "UnityEngine.ScreenCapture.CaptureScreenshot(\"$P/Registros/Validacion/B207-220/$1.png\"); return 1;" 60 >/dev/null; sleep 1.5; }
wf() { U "$(sed "s#__N__#$1#; s/__X__/$2/; s/__Y__/$3/; s/__Z__/$4/; s/__P__/$5/; s/__W__/$6/; s/__D__/${7:-8}/" $D4/wf.cs)" 80 >/dev/null; }
