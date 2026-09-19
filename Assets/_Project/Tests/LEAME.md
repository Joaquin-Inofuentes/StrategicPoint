# Pruebas

Las pruebas del proyecto **no** usan Unity Test Framework: viven en `Assets/_Project/Scripts/Editor/HeadlessTestRunner*.cs`
y se corren con **Strategic Point > Run All Tests Headless** (o `-executeMethod SP.EditorTools.HeadlessTestRunner.RunAll` en batch).
Esta carpeta queda reservada por si se agregan pruebas de UTF (`*.asmdef` con referencia a los ensamblados del juego).
