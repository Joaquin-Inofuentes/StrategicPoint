$path = 'C:\_Proyectos privados\DV_C6_StrategicPoint\Assets\_Project\Scripts\Player\PlayerInputDriver.cs'
$content = Get-Content $path -Raw
$searchStr1 = 'if ((mouse != null && mouse.leftButton.isPressed) || MandoFps.Disparar)'
$repStr1 = 'bool cHeld = KeyBindings.IsPressed(KeyBindings.VerTactico) || (OrdenesMenu != null && OrdenesMenu.Abierto && OrdenesMenu.Seleccion == MenuDeOrdenes.Cubrirse);
            if (cHeld && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (TryResolverCobertura(result, out var puntoCobertura, out var duenoCobertura))
                {
                    IssueCoverOrderC(puntoCobertura, duenoCobertura);
                }
            }
            else if (!cHeld && ((mouse != null && mouse.leftButton.isPressed) || MandoFps.Disparar))'
$content = $content.Replace($searchStr1, $repStr1)

$searchStr2 = 'public bool IssueCoverOrderT(Vector3 punto, Collider dueno)'
$repStr2 = 'float ultimoCCoberturaTiempo;
        Soldier ultimoAliadoC;
        Vector3 ultimoPuntoC;

        public bool IssueCoverOrderC(Vector3 punto, Collider dueno)
        {
            bool repique = Time.unscaledTime - ultimoCCoberturaTiempo < VentanaDobleT;
            ultimoCCoberturaTiempo = Time.unscaledTime;
            
            Soldier elegido = null;
            if (Selection.Selected.Count > 0)
            {
                float minD = float.MaxValue;
                foreach (var s in Selection.Selected) {
                    if (s == null || s == ultimoAliadoC || s.Health == null || !s.Health.IsAlive) continue;
                    float d = (s.transform.position - punto).sqrMagnitude;
                    if (d < minD) { minD = d; elegido = s; }
                }
                if (elegido == null) {
                   foreach (var s in Selection.Selected) {
                       if (s != null && s.Health != null && s.Health.IsAlive) { elegido = s; break; }
                   }
                }
            }
            else elegido = AliadoLibreMasCercano(punto, repique ? ultimoAliadoC : null);

            if (elegido == null) { RejectOrder("NO HAY ALIADOS LIBRES PARA CUBRIRSE"); return false; }

            if (repique && ultimoAliadoC != null)
            {
                var indices = Coberturas.IndicesCercanos(ultimoPuntoC, 2, 8f);
                if (indices.Count > 1)
                {
                    punto = Coberturas.Puntos[indices[1]];
                    dueno = Coberturas.Duenos[indices[1]];
                }
            }

            bool ok = OrdenesDeEscuadra.CoberturaManual(elegido, punto, dueno);
            if (ok) { ultimoAliadoC = elegido; ultimoPuntoC = punto; }
            return ok;
        }

        public bool IssueCoverOrderT(Vector3 punto, Collider dueno)'
$content = $content.Replace($searchStr2, $repStr2)

Set-Content -Path $path -Value $content
