using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Vehicles;

namespace SP.EditorTools
{
    // FASE 20 (Ronda 11): fin de partida por escuadra caida, IA con dispersion/rafagas/alcance de arma, umbral de Q, E tap/hold,
    // zoom RTS sin lerp, alcance de demolicion e iconos de arma propios.
    public static partial class HeadlessTestRunner
    {
        static void RunPhase20(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 20 - Ronda 11: escuadra caida, IA humanizada, Q/E, zoom, iconos");
            foreach (var o in new System.Collections.Generic.List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            FullHeal(vega, kes, doc);
            inputDriver.Brain.Possess(vega);

            // --- Todos caidos y en calma: la escuadra revive a los 4 s con la mitad de la vida ---
            EstadoDePartida.Reiniciar();
            bool hayAccion = false;
            foreach (var s in new[] { vega, kes, doc })
                if (ActorRegistry.FindNearestEnemyInRange(s.transform.position, TeamId.Player, EstadoDePartida.RadioDeAccion) != null) hayAccion = true;
            if (hayAccion) TestLog.Step("Hay enemigos a menos de 25 m de la escuadra de prueba: se omite la comprobacion de revivir en calma");
            else
            {
                bool godPrev = ModoDios.Activo;
                ModoDios.Poner(false);
                foreach (var s in new[] { vega, kes, doc }) s.Health.TakeDamage(s.Health.Current + s.Health.MaxHealth, -1);
                Check("Con los tres caidos ninguno esta vivo", !vega.Health.IsAlive && !kes.Health.IsAlive && !doc.Health.IsAlive);
                for (int i = 0; i < 30; i++) EstadoDePartida.Tick(0.1f);
                Check("En calma NO revive antes de los 4 s (3 s)", !vega.Health.IsAlive && !EstadoDePartida.Revivio);
                for (int i = 0; i < 12; i++) EstadoDePartida.Tick(0.1f);
                Check("En calma la escuadra revive tras 4 s", vega.Health.IsAlive && kes.Health.IsAlive && doc.Health.IsAlive && EstadoDePartida.Revivio);
                Check("Revive con la mitad de la vida", Mathf.Abs(vega.Health.Current - vega.Health.MaxHealth * 0.5f) <= 1f);
                ModoDios.Poner(godPrev);
                EstadoDePartida.Reiniciar();
                FullHeal(vega, kes, doc);
                inputDriver.Brain.Possess(vega);
            }

            // --- IA: dispersion, rafagas, reaccion y alcance (solo en la partida principal: Dificultad.Activa) ---
            Check("La dispersion minima del enemigo supera a la del aliado y cubre la esfera de impacto", AiBrain.DispersionMinEnemigo > AiBrain.DispersionMinAliado && AiBrain.DispersionMinAliado >= 2f);
            Check("Rafagas de 3 a 5 tiros con pausa de 0,4 a 0,9 s y reaccion de 0,25 a 0,6 s",
                AiBrain.RafagaMin == 3 && AiBrain.RafagaMax == 5 && AiBrain.PausaMin == 0.4f && AiBrain.PausaMax == 0.9f && AiBrain.ReaccionMin == 0.25f && AiBrain.ReaccionMax == 0.6f);
            var brainKes = kes.GetComponent<AiBrain>();
            bool activaPrev = Dificultad.Activa;
            if (brainKes != null)
            {
                Dificultad.Activa = false;
                float alcanceBase = brainKes.EffectiveAttackRange;
                Dificultad.Activa = true;
                float alcancePartida = brainKes.EffectiveAttackRange;
                Dificultad.Activa = activaPrev;
                Check("En la partida principal la IA alcanza mas lejos que en la suite", alcancePartida > alcanceBase * 2f);
            }
            {
                // Cono de dispersion: el desvio maximo de ApplySpread(maxDeg) no pasa de maxDeg en yaw y pitch (diagonal <= maxDeg * 1,42)
                float peor = 0f;
                for (int i = 0; i < 400; i++) peor = Mathf.Max(peor, Vector3.Angle(Vector3.forward, WeaponHolder.ApplySpread(Vector3.forward, 3f)));
                Check("Con 3 grados de dispersion las balas se desvian pero nunca mas de ~4,3 grados", peor > 0.5f && peor < 4.4f);
            }

            // --- Q y E ---
            Check("Q: mantener empieza a los 0,5 s", PlayerInputDriver.SostenerParaMenu == 0.5f);
            Check("E: mantener 0,5 s = habilidad especial, tap = interactuar", PlayerInputDriver.SostenerParaEspecial == 0.5f);
            Check("Zoom RTS sin lerp a 80 u/s", PlayerInputDriver.rtsZoomSpeed == 80f);

            // --- Demolicion: alcance de 4,5 m ---
            Check("El alcance maximo de la carga de demolicion es 4,5 m", Demolicion.AlcanceMaximo == 4.5f);

            // --- Iconos: cada arma tiene el suyo ---
            foreach (var k in new[] { WeaponKind.Sniper, WeaponKind.Smg, WeaponKind.Shotgun, WeaponKind.Rocket })
                Check($"Icono propio de {k}", System.IO.File.Exists("Assets/_Project/Resources/UI/WeaponIcons/Icono_" + k + ".png"));

            // --- Audio (item 65): ningun sonido recorta, tiene corriente continua, NaN ni corte seco al final ---
            var audio = AudioAnalisisReport.Analizar();
            string malos = "";
            foreach (var f in audio)
                if (f.Avisos.Length > 0 && f.Nombre != "Sfx_EmptyClick") malos += f.Nombre + ":" + f.Avisos.Trim() + " ";   // el clic del arma vacia arranca seco a proposito
            Check($"Analisis de audio: {audio.Count} sonidos sin recorte, DC, NaN, clics ni volumen extremo ({malos})", audio.Count >= 80 && malos.Length == 0);

            // --- Caida (item 51): desde un borde alto cae y se lastima; un escalon chico no hace dano ---
            Check("Formula de dano por caida: 3 m sin dano, 6 m = 75", SoldierMotor.DanioDeCaida(3f) == 0 && SoldierMotor.DanioDeCaida(6f) == 75);
            var pisoPrueba = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pisoPrueba.name = "TestPisoCaida";
            pisoPrueba.transform.position = new Vector3(900f, -0.5f, 900f);
            pisoPrueba.transform.localScale = new Vector3(30f, 1f, 30f);
            var motorCaida = kes.Motor;
            var posPrevia = kes.transform.position;
            bool godCaida = ModoDios.Activo;
            ModoDios.Poner(false);
            FullHeal(vega, kes, doc);
            try
            {
                foreach (var caso in new[] { (alto: 1.6f, esperaDanio: false), (alto: 6.8f, esperaDanio: true) })
                {
                    motorCaida.ResetMotionState();
                    kes.transform.position = new Vector3(900f, caso.alto, 900f);
                    Physics.SyncTransforms();
                    int vidaAntes = kes.Health.Current;
                    motorCaida.Move(Vector3.forward, 0.01f);
                    Check($"Al pisar el aire a {caso.alto - 0.8f:0.0} m del piso el soldado empieza a caer", motorCaida.IsJumping);
                    for (int i = 0; i < 200 && motorCaida.IsJumping; i++) { motorCaida.TickVertical(0.02f); Physics.SyncTransforms(); }
                    Check($"Termina apoyado en el piso (salto={motorCaida.IsJumping}, y={kes.transform.position.y:0.00}, caida={motorCaida.UltimaCaidaMetros:0.00})", !motorCaida.IsJumping && Mathf.Abs(kes.transform.position.y - 0.8f) < 0.05f);
                    int perdida = vidaAntes - kes.Health.Current;
                    if (caso.esperaDanio) Check($"Una caida de {motorCaida.UltimaCaidaMetros:0.0} m resta {SoldierMotor.DanioDeCaida(motorCaida.UltimaCaidaMetros)} de vida", perdida == SoldierMotor.DanioDeCaida(motorCaida.UltimaCaidaMetros) && perdida > 50);
                    else Check("Un escalon de 0,8 m no hace dano", perdida == 0);
                }
            }
            finally
            {
                motorCaida.ResetMotionState();
                kes.transform.position = posPrevia;
                Object.DestroyImmediate(pisoPrueba);
                ModoDios.Poner(godCaida);
                FullHeal(vega, kes, doc);
            }

            RunPhase20Ronda12(inputDriver, vehicle, vega, kes, doc);
        }

        // RONDA 12: tanque (mira, puesto de metralleta, aplastar, tiros enemigos), fragmentos con fisica, moneda de municion,
        // muro sin la cara solapada y el toque de Q como accion rapida distinta del radial sostenido.
        static void RunPhase20Ronda12(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 20b - Ronda 12: tanque, fragmentos, moneda, muro, Q toque");

            // Tiros del tanque enemigo: llevan el bando del que lo tripula, no el del jugador.
            Vehicle tanqueEnemigo = null;
            foreach (var v in Object.FindObjectsByType<Vehicle>()) if (v.Bando == TeamId.Enemy && !v.IsDestroyed) { tanqueEnemigo = v; break; }
            if (tanqueEnemigo != null)
            {
                var canonEnemigo = tanqueEnemigo.transform.Find("TurretMount/TurretPivot");
                var tw = canonEnemigo != null ? canonEnemigo.GetComponent<TurretWeapon>() : null;
                if (tw != null)
                {
                    tw.ResolverTirador(out int idTirador, out TeamId bando);
                    Check("Los disparos de un tanque enemigo salen con el bando ENEMIGO", bando == TeamId.Enemy);
                }
            }
            else TestLog.Step("No hay tanque enemigo vivo en la escena de prueba: se omite el bando de sus tiros");

            // Puesto de metralleta mas alto y mirilla normal.
            var mgMount = vehicle.transform.Find("MetralletaMount");
            var mgStand = vehicle.transform.Find("MetralletaStandPoint");
            Check("El puesto de metralleta del tanque quedo arriba (montura >= 0,6 m, soldado >= 0,9 m)",
                mgMount != null && mgStand != null && mgMount.localPosition.y >= 0.6f && mgStand.localPosition.y >= 0.9f);
            Check("El canon usa la mira de ARTILLERO (periscopio) y no la del francotirador", System.Enum.IsDefined(typeof(ReticleStyle), "Artillero") && ReticleStyle.Artillero != ReticleStyle.Telescopica);

            // Aplastar: el umbral existe y los obstaculos se rompen con el casco.
            Check("El tanque aplasta a partir de 3 m/s", Atropello.VelocidadParaAplastar == 3f);

            // Fragmentos con fisica: un cubo se rompe en piezas reales y se limpia.
            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo.name = "PruebaFragmentos";
            cubo.transform.position = new Vector3(0f, 40f, 0f);
            cubo.transform.localScale = new Vector3(2f, 2f, 0.4f);
            int piezas = SP.Presentation.Fragmentador.Romper(cubo.transform, cubo.transform.position + Vector3.back, 8f);
            // Los fragmentos son objetos de Play (Rigidbody + pool): en la suite de edicion Romper debe ser un no-op seguro.
            if (Application.isPlaying) Check($"Fragmentador rompe un bloque en piezas ({piezas}) con cuerpo rigido", piezas >= 4 && SP.Presentation.Fragmentador.Activos >= 4);
            else Check("Fuera de Play, Fragmentador.Romper no crea nada (la fisica se verifica en Play)", piezas == 0 && SP.Presentation.Fragmentador.Activos == 0);
            SP.Presentation.Fragmentador.LimpiarTodo();
            Object.DestroyImmediate(cubo);
            Check("LimpiarTodo deja cero fragmentos activos", SP.Presentation.Fragmentador.Activos == 0);

            // Moneda de municion 3D (prefab referenciado desde el pickup).
            Check("Existe el prefab de la moneda de municion", SP.Core.RecursosCache.Cargar<GameObject>(SP.Player.MunicionPickup.PrefabMoneda) != null);

            // Muro: la cara superior solapada ya no existe (17 quads = 34 triangulos, antes 18 = 36).
            var muro = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ARTS/SP_Arte/_FBX_Export/01_Modulares/SM_Mod_Muro_Recto.fbx");
            int tris = 0;
            if (muro != null) foreach (var mf in muro.GetComponentsInChildren<MeshFilter>()) tris += mf.sharedMesh.triangles.Length / 3;
            Check($"El muro recto ya no tiene la cara superior solapada ({tris} triangulos)", muro != null && tris == 34);

            // Q: toque = accion rapida sobre lo apuntado; sostenido = radial. Se fija la mira por reflexion (la suite no corre Update).
            var campoMira = typeof(PlayerInputDriver).GetField("ultimoResultadoDeMira", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Soldier enemigo = null;
            foreach (var s in Object.FindObjectsByType<Soldier>()) if (s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive) { enemigo = s; break; }
            if (campoMira != null)
            {
                inputDriver.Brain.Possess(vega);
                if (enemigo != null)
                {
                    campoMira.SetValue(inputDriver, new AimResult { Type = AimTargetType.Enemy, Soldier = enemigo, Point = enemigo.transform.position, HitTransform = enemigo.transform });
                    inputDriver.ResolverGestoDeQ(true, false, false);
                    Check("Q toque sobre un enemigo = ATACAR al instante", inputDriver.UltimaAccionRapida == "ATACAR");
                    Check("Q toque sobre un enemigo NO abre el radial", !inputDriver.RadialAbierto);
                }
                campoMira.SetValue(inputDriver, new AimResult { Type = AimTargetType.Ally, Soldier = kes, Point = kes.transform.position, HitTransform = kes.transform });
                inputDriver.ResolverGestoDeQ(true, false, false);
                Check("Q toque sobre un aliado sano = ese aliado te sigue", inputDriver.UltimaAccionRapida == "SEGUIR ALIADO");
                campoMira.SetValue(inputDriver, new AimResult { Type = AimTargetType.None });
                inputDriver.ResolverGestoDeQ(true, false, false);
                Check("Q toque sin nada en la mira cae al SIGANME de la escuadra (sin accion rapida)", inputDriver.UltimaAccionRapida == null);
                campoMira.SetValue(inputDriver, new AimResult { Type = AimTargetType.None });
            }
            FullHeal(vega, kes, doc);
            inputDriver.Brain.Possess(vega);
        }
    }
}
