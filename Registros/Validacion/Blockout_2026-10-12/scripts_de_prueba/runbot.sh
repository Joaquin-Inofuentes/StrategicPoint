# uso: runbot.sh <n> <objeto> <segundos>
n=$1; obj=$2; secs=$3
unity command eval --caller plugin --skill unity-cli "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"Assets/_Project/Scenes/SC_Blockout_0$n.unity\"); return \"ok\";" >/dev/null 2>&1
unity command clear_console >/dev/null 2>&1
unity command editor_play >/dev/null 2>&1; sleep 8
sed "s/OBJ/$obj/" /tmp/g/bot.cs > /tmp/g/bot_n.cs
end=$((SECONDS+secs))
while [ $SECONDS -lt $end ]; do
  r=$(unity command eval_file --caller plugin --skill unity-cli /tmp/g/bot_n.cs 2>&1 | grep -o '"result":"[^"]*"' | cut -c11-)
  echo "$r"
  echo "$r" | grep -q "ts=0 \|obj=True" && break
  sleep 5
done
