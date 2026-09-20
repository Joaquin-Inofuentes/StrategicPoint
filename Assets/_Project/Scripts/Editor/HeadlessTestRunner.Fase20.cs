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
        }
    }
}
