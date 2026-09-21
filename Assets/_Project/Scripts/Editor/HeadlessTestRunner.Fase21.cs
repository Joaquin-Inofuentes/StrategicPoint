using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Vehicles;

namespace SP.EditorTools
{
    // FASE 21 (Ronda 13): los doce pedidos de la ronda. Cada bloque nombra el punto del pedido que audita.
    //   1 revivido obedece / 2 barra de accion / 3 estado de accion / 4 seguir a la mitad / 5 sin cubo al matar / 6 ajustes
    //   7 agachado / 8 medico reanima en calma / 9 barra de vida del tanque / 10 apuntar preciso / 11 tripulacion enemiga
    //   dispara / 12 tanque enemigo no se aborda.
    // Lo que necesita Play (barras dibujadas, capturas, animator vivo) se verifica en Play con las capturas de Validacion/Ronda13.
    public static partial class HeadlessTestRunner
    {
        static void RunPhase21(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
                               GameObject soldierPrefab, Color colorEnemy, ProjectilePool pool)
        {
            TestLog.Phase("FASE 21 - Ronda 13: revivido, barras de accion, seguir, ajustes, agachado, tanques");
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            Reponer(vega, kes, doc);
            inputDriver.Brain.Possess(vega);
            // Las fases anteriores dejan a la escuadra suprimida: en la suite (Edit mode) Time.time no avanza y "Suprimido" no vence nunca,
            // y un soldado suprimido se agacha y no camina. Se limpia antes de auditar movimiento.
            foreach (var sd in new[] { vega, kes, doc })
            {
                var f = typeof(AiBrain).GetField("suprimidoHasta", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                f.SetValue(sd.Brain, 0f);
                typeof(AiBrain).GetField("suprimirOrdenadoHasta", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(sd.Brain, 0f);
                sd.Motor.SetCrouching(false);
                sd.Brain.CancelOrder();
            }
            bool godPrev = ModoDios.Activo;
            ModoDios.Poner(true);
            bool atencionPrev = PedidoDeCuracion.AtencionAutomatica, calmaPrev = PedidoDeCuracion.ReanimarEnCalma;
            var lidersPrev = AjustesDeEscuadra.Lider;

            try
            {
                Fase21_Revivido(inputDriver, vega, kes, doc);
                Fase21_Acciones(vega, kes, doc);
                Fase21_SeguirAMitad(vega, kes, doc);
                Fase21_SinCubo();
                Fase21_ApuntarPreciso(vega, pool);
                Fase21_Agachado(vega);
                Fase21_MedicoEnCalma(vega, kes, doc, soldierPrefab, colorEnemy, pool);
                Fase21_BarraDeVidaDelTanque(vehicle);
                Fase21_TanqueEnemigo(inputDriver, vehicle, vega, kes, doc, soldierPrefab, colorEnemy, pool);
                Fase21_Ajustes(vega, kes, doc);
            }
            finally
            {
                ModoDios.Poner(godPrev);
                PedidoDeCuracion.AtencionAutomatica = atencionPrev;
                PedidoDeCuracion.ReanimarEnCalma = calmaPrev;
                PedidoDeCuracion.Cancelar();
                RescateAutomatico.Cancelar();
                AccionesEnCurso.Limpiar();
                AjustesDeEscuadra.Lider = lidersPrev;
                Reponer(vega, kes, doc);
                inputDriver.Brain.Possess(vega);
            }
        }

        // El modo dios queda ENCENDIDO durante toda la fase (los patrulleros del nivel de prueba no deben matar a la escuadra
        // mientras se audita otra cosa); solo se baja un instante para poder matar a un aliado a proposito.
        static void Matar(Soldier s)
        {
            ModoDios.Poner(false);
            s.Health.TakeDamage(s.Health.Current + s.Health.MaxHealth, -1);
            ModoDios.Poner(true);
        }

        static void Reponer(params Soldier[] soldados)
        {
            foreach (var s in soldados) if (!s.Health.IsAlive) Reanimacion.Ejecutar(s);
            FullHeal(soldados);
        }

        // ---------------------------------------------------------------- 1
        static void Fase21_Revivido(PlayerInputDriver inputDriver, Soldier vega, Soldier kes, Soldier doc)
        {
            vega.transform.position = new Vector3(-60f, 0.8f, -60f);
            kes.transform.position = vega.transform.position + new Vector3(3f, 0f, 3f);
            doc.transform.position = vega.transform.position + new Vector3(-3f, 0f, 3f);
            AjustesDeEscuadra.AsegurarEnEscena();
            var ajustes = Object.FindFirstObjectByType<AjustesDeEscuadra>(FindObjectsInactive.Include);
            ajustes.distanciaParaSeguir = 12.5f; ajustes.distanciaParaDetenerse = 4f; ajustes.Aplicar();
            AjustesDeEscuadra.Lider = vega;

            // El estado con el que muere el aliado: "todos quietos", poseido por el jugador y con una orden a medias.
            kes.Brain.Quieto = true;
            kes.Brain.IsPossessedByPlayer = true;
            Matar(kes);
            Check("Kes esta caido", !kes.Health.IsAlive);
            int avisos = 0;
            System.Action<Soldier> oyente = s => { if (s == kes) avisos++; };
            Reanimacion.Revivido += oyente;
            bool ok = Reanimacion.Ejecutar(kes);
            Reanimacion.Revivido -= oyente;
            Check("Reanimacion.Ejecutar revive al caido y avisa (evento Revivido)", ok && kes.Health.IsAlive && avisos == 1);
            Check("El revivido ya no esta 'quieto'", !kes.Brain.Quieto);
            Check("El revivido ya no queda poseido por el jugador (esta en RTS o manejando a otro)", !kes.Brain.IsPossessedByPlayer);
            Check("Reanimacion.Ejecutar sobre alguien vivo no hace nada", !Reanimacion.Ejecutar(kes));

            var destino = kes.transform.position + new Vector3(0f, 0f, 10f);
            OrderService.IssueMoveOrder(kes, destino);
            bool llego = SimulateUntil(() => Vector3.Distance(kes.transform.position, destino) < 2.5f, 14f);
            Check($"El revivido obedece una orden de mover ({Vector3.Distance(kes.transform.position, destino):0.0} m del destino)", llego);

            kes.Brain.CancelOrder();
            kes.transform.position = vega.transform.position + new Vector3(0f, 0f, 30f);
            SimulateSeconds(0.3f);
            Check($"El revivido sigue al jugador cuando este se aleja mas de {AjustesDeEscuadra.DistanciaParaSeguir:0.#} m ({kes.Brain.State})",
                kes.Brain.SiguiendoAlJugador && kes.Brain.State == AiState.Follow);
            SimulateUntil(() => !kes.Brain.SiguiendoAlJugador, 14f);
            kes.Brain.CancelOrder();

            // Tambien por la tecla [E] (TryRevivir) y por el rescate automatico: mismo camino.
            kes.Brain.Quieto = true;
            Matar(kes);
            Check("[E] sostenido revive por el mismo camino y suelta el 'quieto'", inputDriver.TryRevivir(kes, true) && kes.Health.IsAlive && !kes.Brain.Quieto);
            kes.Brain.Quieto = true;
            Matar(kes);
            doc.transform.position = kes.transform.position + new Vector3(1f, 0f, 0f);
            RescateAutomatico.Solicitar(kes);
            SimulateUntil(() => kes.Health.IsAlive, 30f);
            Check($"El rescate automatico tambien lo deja obediente (vivo {kes.Health.IsAlive}, quieto {kes.Brain.Quieto}, rescatista {(RescateAutomatico.Rescatista != null ? RescateAutomatico.Rescatista.DisplayName : "ninguno")}, doc a {Vector3.Distance(doc.transform.position, kes.transform.position):0.0} m)", kes.Health.IsAlive && !kes.Brain.Quieto);
            RescateAutomatico.Cancelar();
            var codigo = System.IO.File.ReadAllText("Assets/_Project/Scripts/Player/SelectionController.cs");
            Check("La seleccion de RTS vuelve a incluir al revivido (suscrita a Reanimacion.Revivido)", codigo.Contains("Reanimacion.Revivido +="));
            Reponer(vega, kes, doc);
            kes.Brain.CancelOrder();
        }

        // ---------------------------------------------------------------- 2 y 3
        static void Fase21_Acciones(Soldier vega, Soldier kes, Soldier doc)
        {
            Reponer(vega, kes, doc);
            AccionesEnCurso.Limpiar();
            AccionesEnCurso.Reportar(doc, "REVIVIENDO", kes.transform.position, 0.4f, 2.4f, kes.transform);
            Check("Una accion reportada queda registrada con verbo, progreso y tiempo restante",
                AccionesEnCurso.De(doc, out var a) && a.VerboEs == "REVIVIENDO" && Mathf.Approximately(a.Progreso01, 0.4f) && Mathf.Approximately(a.Restante, 2.4f));
            Check("El punto objetivo sigue al objetivo (barra entre actor e interactuable)", AccionesEnCurso.De(doc, out a) && (a.Punto - kes.transform.position).sqrMagnitude < 0.0001f);
            System.Threading.Thread.Sleep(450);
            Check("Una accion que deja de reportarse caduca sola (0,35 s)", !AccionesEnCurso.De(doc, out _) && AccionesEnCurso.Cantidad == 0);

            // El medico de IA que reanima de verdad reporta la barra mientras trabaja.

            vega.transform.position = new Vector3(-60f, 0.8f, -60f);
            kes.transform.position = vega.transform.position + new Vector3(3f, 0f, 3f);
            doc.transform.position = kes.transform.position + new Vector3(1.5f, 0f, 0f);
            AjustesDeEscuadra.Lider = vega;
            Matar(kes);
            PedidoDeCuracion.Cancelar();
            bool pidio = PedidoDeCuracion.SolicitarReanimar(kes);
            Check("El medico acepta reanimar al caido", pidio && PedidoDeCuracion.Enfermero == doc);
            bool vioBarra = SimulateUntil(() => AccionesEnCurso.De(doc, out _), 8f);
            Check("Mientras reanima, el medico reporta la accion REVIVIENDO", vioBarra);
            if (AccionesEnCurso.De(doc, out var enCurso))
            {
                Check($"El progreso esta entre 0 y 1 ({enCurso.Progreso01:0.00}) y falta a lo sumo {PedidoDeCuracion.SegundosDeReanimar:0} s ({enCurso.Restante:0.0})",
                    enCurso.Progreso01 >= 0f && enCurso.Progreso01 < 1f && enCurso.Restante <= PedidoDeCuracion.SegundosDeReanimar + 0.01f);
                float antes = enCurso.Progreso01;
                SimulateSeconds(1f);
                Check("El progreso avanza con el tiempo", !AccionesEnCurso.De(doc, out var despues) || despues.Progreso01 > antes || kes.Health.IsAlive);
            }
            SimulateUntil(() => kes.Health.IsAlive, 10f);
            Check("La reanimacion termina y el aliado vuelve", kes.Health.IsAlive);

            // Botiquin del medico: cura 60 en 4 s y muestra el tiempo.
            doc.Health.TakeDamage(80, -1);
            if (PedidoDeCuracion.BotiquinListoEn <= 0f && PedidoDeCuracion.Botiquin(doc))
            {
                SimStep(0.1f);
                Check("El botiquin reporta 'USANDO BOTIQUIN' con su tiempo", AccionesEnCurso.De(doc, out var bq) && bq.VerboEs == "USANDO BOTIQUIN" && bq.Restante > 0f && bq.Restante <= PedidoDeCuracion.BotiquinSegundos);
            }
            Reponer(vega, kes, doc);

            // Texto de estado (bottom-left): formato y traduccion.
            var acc = new AccionesEnCurso.Accion { Actor = doc, VerboEs = "DETONANDO", Restante = 2.3f };
            Check("Estado propio: '▶ DETONANDO · 2.3 s'", SP.Presentation.AccionesEnCursoView.Linea(acc, true) == "▶ DETONANDO · 2.3 s");
            Check("Estado de un aliado incluye su nombre", SP.Presentation.AccionesEnCursoView.Linea(acc, false).Contains(doc.DisplayName));
            var idiomaPrevio = Loc.Actual;
            Loc.Poner(Idioma.En);
            string ingles = SP.Presentation.AccionesEnCursoView.Linea(acc, true);
            Loc.Poner(Idioma.Es);
            Check("El estado se traduce ('DETONATING')", ingles.Contains("DETONATING"));
            if (idiomaPrevio == Idioma.En) Loc.Poner(Idioma.En);
            foreach (var verbo in new[] { "REVIVIENDO", "CURANDO", "DETONANDO", "AFINANDO PUNTERIA", "USANDO BOTIQUIN" })
                Check($"'{verbo}' tiene traduccion", Loc.TieneEntrada(verbo));

            string driver = System.IO.File.ReadAllText("Assets/_Project/Scripts/Player/PlayerInputDriver.cs");
            string demol = System.IO.File.ReadAllText("Assets/_Project/Scripts/Player/Demolicion.cs");
            Check("Las cuatro habilidades reportan su accion (revivir [E], medico [Ctrl], enfoque de francotirador/asalto y detonar)",
                driver.Contains("\"REVIVIENDO\"") && driver.Contains("\"CURANDO\"") && driver.Contains("\"AFINANDO PUNTERIA\"") && demol.Contains("\"DETONANDO\""));
            AccionesEnCurso.Limpiar();
        }

        // ---------------------------------------------------------------- 4
        static void Fase21_SeguirAMitad(Soldier vega, Soldier kes, Soldier doc)
        {

            var g = new GameObject("F21_Ajustes");
            var nuevo = g.AddComponent<AjustesDeEscuadra>();
            Check("La distancia para empezar a seguir bajo de 25 a 12,5 m", Mathf.Approximately(nuevo.distanciaParaSeguir, 12.5f));
            Check("La distancia para detenerse bajo de 8 a 4 m", Mathf.Approximately(nuevo.distanciaParaDetenerse, 4f));
            Object.DestroyImmediate(g);
            Check("El umbral de llegada de SIGANME es la mitad", Mathf.Approximately(AiBrain.FactorDeCercaniaAlSeguir, 0.5f));

            vega.transform.position = new Vector3(-60f, 0.8f, -60f);
            doc.transform.position = vega.transform.position + new Vector3(0f, 0f, 20f);
            doc.Brain.CancelOrder();
            OrderService.IssueFollowOrder(doc, vega);
            SimulateSeconds(9f);
            float d = Vector3.Distance(doc.transform.position, vega.transform.position);
            Check($"Con SIGANME el aliado queda a {d:0.0} m del jugador (antes ~2,5-3,5 m; ahora <= 2,4 m) [estado {doc.Brain.State}, vivo {doc.Health.IsAlive}, brain {doc.Brain.enabled}, quieto {doc.Brain.Quieto}]", d <= 2.4f);
            doc.Brain.CancelOrder();
        }

        // ---------------------------------------------------------------- 5
        static void Fase21_SinCubo()
        {
            var t = typeof(SP.Presentation.KillFeedbackDirector);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            Check("Ya no existe la silueta-cubo que aparecia al matar (SilhouetteFlash)", t.GetMethod("SilhouetteFlash", flags) == null);
            string codigoKill = System.IO.File.ReadAllText("Assets/_Project/Scripts/Presentation/KillFeedbackDirector.cs");
            string codigoMun = System.IO.File.ReadAllText("Assets/_Project/Scripts/Player/MunicionPickup.cs");
            Check("Matar no crea ningun primitivo cubo (KillFeedbackDirector)", !codigoKill.Contains("PrimitiveType.Cube"));
            Check("La moneda de municion (respaldo incluido) no es un cubo", !codigoMun.Contains("PrimitiveType.Cube"));
        }

        // ---------------------------------------------------------------- 10
        static void Fase21_ApuntarPreciso(Soldier vega, ProjectilePool pool)
        {
            var w = vega.Weapon;
            w.SetPool(pool);
            w.SetApuntado(0f);
            // Un tiro para que la dispersion crezca por encima de cero.
            w.TryFire(vega.transform.position + Vector3.up * 1.5f, vega.transform.forward);
            float cadera = w.SpreadDegEfectivo;
            w.SetApuntado(1f);
            float apuntando = w.SpreadDegEfectivo;
            w.SetApuntado(0f);
            Check($"Apuntando (click derecho) la dispersion baja a {apuntando:0.00} grados desde {cadera:0.00} (<= 20 %)", cadera > 0.01f && apuntando <= cadera * 0.2f);
            Check("El factor de precision al apuntar es 0,15 y el crecimiento por tiro cae a 0,4", Mathf.Approximately(WeaponHolder.FactorApuntando, 0.15f) && Mathf.Approximately(WeaponHolder.CrecimientoApuntando, 0.4f));
            w.SetApuntado(0.5f);
            float medio = w.SpreadDegEfectivo;
            w.SetApuntado(0f);
            Check("A medio apuntar (entrada del zoom) la precision es intermedia", medio < cadera && medio > apuntando);
        }

        // ---------------------------------------------------------------- 7
        static void Fase21_Agachado(Soldier vega)
        {
            var m = vega.Motor;
            m.SetCrouching(false);
            float pie = m.MoveSpeed;
            m.SetCrouching(true);
            float agachado = m.MoveSpeed;
            float caida = m.EyeHeightDrop;
            m.SetCrouching(false);
            Check($"Agachado se camina a la mitad ({agachado:0.0} contra {pie:0.0} m/s)", Mathf.Approximately(agachado, pie * SoldierMotor.FactorDeVelocidadAgachado) && agachado < pie);
            Check("Agachado no sube la altura del ojo", caida >= 0f);
            Check("De pie, EyeHeightDropSuave vuelve a 0 fuera de Play (sin desfase en la suite)", Mathf.Approximately(m.EyeHeightDropSuave, 0f));
            string anim = System.IO.File.ReadAllText("Assets/_Project/Scripts/Presentation/SoldierAnimatorDriver.cs");
            Check("La capa de disparo (torso de pie) se apaga al agacharse", anim.Contains("mezclaAgachado") && anim.Contains("1f - mezclaAgachado"));
            Check("El animator normaliza la velocidad contra el tope de la postura (agachado)", anim.Contains("topeDePostura"));
        }

        // ---------------------------------------------------------------- 8
        static void Fase21_MedicoEnCalma(Soldier vega, Soldier kes, Soldier doc, GameObject soldierPrefab, Color colorEnemy, ProjectilePool pool)
        {
            PedidoDeCuracion.Cancelar();
            PedidoDeCuracion.AtencionAutomatica = true;
            PedidoDeCuracion.ReanimarEnCalma = true;

            vega.transform.position = new Vector3(-60f, 0.8f, -60f);
            kes.transform.position = vega.transform.position + new Vector3(6f, 0f, 3f);
            doc.transform.position = vega.transform.position + new Vector3(-4f, 0f, 3f);
            AjustesDeEscuadra.Lider = vega;
            doc.Brain.CancelOrder();
            Check("Hay calma en el lugar de la prueba", PedidoDeCuracion.HayCalma(kes.transform.position) && PedidoDeCuracion.HayCalma(doc.transform.position));
            Matar(kes);
            bool revivio = SimulateUntil(() => kes.Health.IsAlive, 30f);
            Check($"En calma el medico reanima solo a un aliado caido (sin que nadie se lo pida) [enfermero {(PedidoDeCuracion.Enfermero != null ? PedidoDeCuracion.Enfermero.DisplayName : "nadie")}, target {(doc.Brain.CurrentTarget != null ? doc.Brain.CurrentTarget.DisplayName : "ninguno")}, dist {Vector3.Distance(doc.transform.position, kes.transform.position):0.0}]", revivio);
            Check("El medico dejo de estar 'pasivo' al terminar", doc.Brain != null && !doc.Brain.Pasivo);
            PedidoDeCuracion.Cancelar();

            // Con un enemigo cerca NO reanima.
            var enemigo = SpawnSoldier(soldierPrefab, "F21_Enemigo", TeamId.Enemy, RoleType.Enemy, kes.transform.position + new Vector3(10f, 0f, 0f), colorEnemy, pool, 500);
            enemigo.Brain.Stance = CombatStance.AltoElFuego;   // quieto y sin disparar: solo presencia
            ActorRegistry.Invalidate();
            SimStep(0.02f);
            Matar(kes);
            Check("Con un enemigo a 10 m ya no hay calma", !PedidoDeCuracion.HayCalma(kes.transform.position));
            SimulateSeconds(4f);
            Check("Con enemigos cerca el medico NO reanima por su cuenta", !kes.Health.IsAlive && !PedidoDeCuracion.ReanimacionEsAutomatica);
            Object.DestroyImmediate(enemigo.gameObject);
            ActorRegistry.Invalidate();
            PedidoDeCuracion.ReanimarEnCalma = false;
            SimulateSeconds(3f);
            Check("Con la reanimacion en calma apagada (tutorial) no reanima aunque haya calma", !kes.Health.IsAlive);
            PedidoDeCuracion.ReanimarEnCalma = true;
            Reanimacion.Ejecutar(kes);
            Reponer(vega, kes, doc);
            PedidoDeCuracion.Cancelar();
        }

        // ---------------------------------------------------------------- 9
        static void Fase21_BarraDeVidaDelTanque(Vehicle vehicle)
        {
            vehicle.Health.Heal(vehicle.Health.MaxHealth);
            var barra = vehicle.GetComponent<SP.Presentation.BarraDeVidaVehiculo>();
            bool era = barra != null;
            if (barra == null) barra = vehicle.gameObject.AddComponent<SP.Presentation.BarraDeVidaVehiculo>();
            barra.Refrescar();
            Check($"El tanque tiene barra de vida y arranca llena ({barra.Relleno01:0.00})", barra.Relleno01 > 0.99f);
            int max = vehicle.Health.MaxHealth;
            ModoDios.Poner(false);
            vehicle.TakeDamage(max / 2, -1);
            ModoDios.Poner(true);
            barra.Refrescar();
            Check($"La barra baja con el dano ({barra.Relleno01:0.00})", barra.Relleno01 > 0.35f && barra.Relleno01 < 0.65f);
            var colorJugador = barra.ColorDelRelleno;
            var bandoPrevio = vehicle.Bando;
            vehicle.AsignarBando(TeamId.Enemy, new Color(0.55f, 0.13f, 0.11f));
            barra.Refrescar();
            var colorEnemigo = barra.ColorDelRelleno;
            Check("En un tanque enemigo la barra es roja", colorEnemigo.r > 0.8f && colorEnemigo.g < 0.4f);
            vehicle.AsignarBando(bandoPrevio, Color.white);
            vehicle.Health.Heal(max);
            barra.Refrescar();
            Check("La barra vuelve a llenarse al curar el tanque", barra.Relleno01 > 0.99f);
            Check("La barra de un tanque propio no es igual de roja que la del enemigo", colorJugador != colorEnemigo || colorJugador.g > 0.4f);
            if (!era) Object.DestroyImmediate(barra);
            string codigo = System.IO.File.ReadAllText("Assets/_Project/Scripts/Vehicles/Vehicle.cs");
            Check("Cada tanque agrega su barra al iniciar (Vehicle.Start)", codigo.Contains("BarraDeVidaVehiculo.Asegurar"));
        }

        // ---------------------------------------------------------------- 11 y 12
        static void Fase21_TanqueEnemigo(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
                                         GameObject soldierPrefab, Color colorEnemy, ProjectilePool pool)
        {
            var bandoPrevio = vehicle.Bando;
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vehicle.transform.position = new Vector3(-60f, 0.4f, -20f);
            vega.transform.position = new Vector3(-60f, 0.8f, -14f);
            kes.transform.position = vega.transform.position + new Vector3(2f, 0f, 0f);
            inputDriver.Brain.Possess(vega);
            ModoDios.Poner(true);

            // Los aliados no tienen que matar a la tripulacion antes de que se la audite: quedan pasivos durante este bloque.
            var pasivosPrevios = new[] { vega.Brain.Pasivo, kes.Brain.Pasivo, doc.Brain.Pasivo };
            vega.Brain.Pasivo = kes.Brain.Pasivo = doc.Brain.Pasivo = true;
            vehicle.AsignarBando(TeamId.Enemy, new Color(0.55f, 0.13f, 0.11f));
            var aliado = vega;
            var enemigoA = SpawnSoldier(soldierPrefab, "F21_TripEnemiga", TeamId.Enemy, RoleType.Enemy, vehicle.transform.position + new Vector3(0f, 0.8f, 6f), colorEnemy, pool, 300);

            // --- 12: el tanque enemigo no se aborda ---
            Check("PuedeAbordar: un jugador NO puede subir a un tanque enemigo", !vehicle.PuedeAbordar(aliado, out string motivo) && motivo == Vehicle.MotivoEnemigo);
            Check("PuedeAbordar: un soldado enemigo SI puede (tripulacion)", vehicle.PuedeAbordar(enemigoA));
            Check("Mount(jugador) sobre un tanque enemigo devuelve false y no ocupa asiento", !vehicle.Mount(vega) && vehicle.OccupantCount == 0);
            OrderService.IssueMountOrder(vega, vehicle);
            OrderService.IssueMountOrderForSelection(new List<Soldier> { vega, kes, doc }, vehicle);
            SimulateSeconds(6f);
            Check("La orden de subir a un tanque enemigo se ignora: nadie se sube (ni la seleccion, ni la escuadra)", vehicle.OccupantCount == 0 && vehicle.Occupants.Count == 0);
            inputDriver.EnterVehicle(vehicle);
            Check($"EnterVehicle (tecla [E] junto al tanque) no mete al jugador en un tanque enemigo (aboard {vehicle.PlayerAboard}, driver {(vehicle.Driver != null ? vehicle.Driver.DisplayName : "nadie")})", vehicle.Driver != vega && vehicle.OccupantCount == 0);

            // Con tripulacion adentro: tampoco se posee ni se aborda.
            Check($"La tripulacion enemiga SI se sube a su tanque (vivo {enemigoA.Health.IsAlive}, ocupantes {vehicle.OccupantCount}, asiento libre {vehicle.FirstFreeSeat()})", vehicle.Mount(enemigoA));
            Check("Con la tripulacion adentro el tanque sigue sin poder abordarse por el jugador", !vehicle.PuedeAbordar(vega) && !vehicle.Mount(kes));
            var cargado = inputDriver.Brain.Current;
            typeof(PlayerInputDriver).GetMethod("EnterVehicleViewFromRts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(inputDriver, new object[] { vehicle });
            Check("Desde RTS no se puede poseer a la tripulacion enemiga del tanque", inputDriver.Brain.Current == cargado);

            // --- 11: la tripulacion enemiga que baja del tanque dispara al jugador ---
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            var armas = enemigoA.Weapon;
            armas.SetPool(null);   // el caso real de los 20 soldados de mision: pool sin cablear
            int disparos = 0;
            var sub = EventBus.Instance.Subscribe<ShotFiredEvent>(e => { if (e.ShooterId == enemigoA.Id) disparos++; });
            enemigoA.Brain.Stance = CombatStance.Libre;
            enemigoA.transform.position = vega.transform.position + new Vector3(0f, 0f, 14f);
            enemigoA.transform.rotation = Quaternion.LookRotation(-Vector3.forward);
            ActorRegistry.Invalidate();
            SimulateSeconds(10f);
            sub.Dispose();
            Check($"Un soldado enemigo sin pool cableado dispara igual (se autocura): {disparos} tiros [estado {enemigoA.Brain.State}, objetivo {(enemigoA.Brain.CurrentTarget != null ? enemigoA.Brain.CurrentTarget.DisplayName : "ninguno")}, vivo {enemigoA.Health.IsAlive}, ocup {vehicle.OccupantCount}]", disparos > 0);
            Check("El tirador enemigo terminó con su pool resuelto", (typeof(WeaponHolder).GetField("pool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(armas) != null));

            vega.Brain.Pasivo = pasivosPrevios[0]; kes.Brain.Pasivo = pasivosPrevios[1]; doc.Brain.Pasivo = pasivosPrevios[2];
            vehicle.AsignarBando(bandoPrevio, Color.white);
            Object.DestroyImmediate(enemigoA.gameObject);
            ActorRegistry.Invalidate();

            vehicle.transform.position = new Vector3(-60f, 0.4f, -20f);
        }

        // ---------------------------------------------------------------- 6
        static void Fase21_Ajustes(Soldier vega, Soldier kes, Soldier doc)
        {
            var pc = Object.FindFirstObjectByType<SP.Presentation.PauseController>(FindObjectsInactive.Include);
            var sp = pc != null ? pc.transform.Find("SettingsPanel") : null;
            if (sp == null) { Check("Existe el panel de Configuraciones en la escena de prueba", false); return; }

            // Estado a restaurar: idioma, tamano de interfaz, accesibilidad, calidad y las claves de PlayerPrefs que tocan los botones.
            var claves = new[] { "sp_pantalla_completa", "sp_resolucion", "sp_calidad", "sp_daltonismo", "sp_hud_minimo", "sp_escala_interfaz", "sp_idioma" };
            var guardadas = new Dictionary<string, int>();
            foreach (var k in claves) if (PlayerPrefs.HasKey(k)) guardadas[k] = PlayerPrefs.GetInt(k);
            var idioma = Loc.Actual; float escala = SP.UI.AjustesDeJuego.Escala; bool dalto = SP.UI.AjustesDeJuego.Daltonismo, hudMin = SP.UI.AjustesDeJuego.HudMinimo;
            bool subt = SP.Presentation.Subtitulos.Activos; int calidad = QualitySettings.GetQualityLevel();
            bool estabaActivo = sp.gameObject.activeSelf;
            var raizPausa = pc.gameObject.activeSelf;
            try
            {
                sp.gameObject.SetActive(true);
                SP.UI.PanelAjustesExtra.Preparar(sp.gameObject);
                var layout = sp.GetComponent<SP.UI.LayoutDeAjustes>();
                Check("El panel de Configuraciones tiene su LayoutDeAjustes", layout != null);
                if (layout == null) return;
                var extra = sp.Find("AjustesExtra");
                Check("Existe el bloque de pantalla y accesibilidad", extra != null);

                // Control negativo: el detector NO es ciego (una etiqueta de 10 de alto con fuente 18 tiene que marcarse).
                var etiqueta = sp.Find("Volumen_Label") as RectTransform;
                var alto0 = etiqueta.sizeDelta;
                etiqueta.sizeDelta = new Vector2(400f, 10f);
                var roto = SP.UI.LayoutDeAjustes.Diagnosticar(sp.gameObject, layout.AreaDelCanvas());
                Check("El detector marca una etiqueta de 10 px de alto (control negativo)", roto.Count > 0);
                etiqueta.sizeDelta = alto0;

                var resoluciones = new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(800, 600), new Vector2(1024, 768), new Vector2(2560, 1080),
                    new Vector2(1366, 768), new Vector2(1280, 800), new Vector2(640, 480), new Vector2(720, 1280), new Vector2(3840, 2160) };
                var botones = new[] { "Pantalla", "Resolucion", "Calidad", "Daltonismo", "HudMinimo", "Escala", "Idioma", "Subtitulos" };
                var rnd = new System.Random(13);
                int total = 0, malos = 0, clics = 0, cambiosDeIdioma = 0;
                var primeros = new List<string>();

                // 1) Todas las resoluciones x los tres tamanos de interfaz x los dos idiomas.
                foreach (var esc in SP.UI.AjustesDeJuego.Escalas)
                    foreach (var lang in new[] { Idioma.Es, Idioma.En })
                    {
                        SP.UI.AjustesDeJuego.PonerEscala(esc);
                        if (Loc.Actual != lang) { Loc.Poner(lang); cambiosDeIdioma++; }
                        foreach (var r in resoluciones)
                        {
                            var area = AreaCanvas(r, esc);
                            layout.Aplicar(area); total++;
                            var p = SP.UI.LayoutDeAjustes.Diagnosticar(sp.gameObject, area);
                            if (p.Count > 0) { malos++; if (primeros.Count < 6) primeros.Add($"esc {esc} {lang} {r}: {p[0]}"); }
                        }
                    }
                Check($"Configuraciones: {total} combinaciones de resolucion x tamano de interfaz x idioma sin defectos ({malos} malas) {string.Join(" | ", primeros)}", malos == 0);

                // 2) Uso real: 200 clics al azar (idioma, escala, pantalla, calidad...) con F12 intercalado y cambios de resolucion.
                malos = 0; primeros.Clear();
                for (int i = 0; i < 200; i++)
                {
                    var b = extra.Find(botones[rnd.Next(botones.Length)]);
                    if (b != null) { b.GetComponent<Button>().onClick.Invoke(); clics++; }
                    if (rnd.Next(4) == 0) { Loc.Alternar(); cambiosDeIdioma++; }
                    var r = resoluciones[rnd.Next(resoluciones.Length)];
                    var area = AreaCanvas(r, SP.UI.AjustesDeJuego.Escala);
                    layout.Aplicar(area); total++;
                    var p = SP.UI.LayoutDeAjustes.Diagnosticar(sp.gameObject, area);
                    if (p.Count > 0) { malos++; if (primeros.Count < 6) primeros.Add($"#{i} {r}: {p[0]}"); }
                }
                Check($"Configuraciones: {clics} clics y {cambiosDeIdioma} cambios de idioma seguidos, cada uno con resolucion distinta, sin defectos ({malos} malos) {string.Join(" | ", primeros)}", malos == 0);

                // 3) El idioma vuelve exacto: ida y vuelta repetidas dejan los textos como al principio.
                if (Loc.Actual != Idioma.Es) Loc.Poner(Idioma.Es);
                var antes = new Dictionary<Text, string>();
                foreach (var t in sp.GetComponentsInChildren<Text>(true)) antes[t] = t.text;
                bool identico = true; string primero = null;
                for (int i = 0; i < 12; i++) { Loc.Poner(Idioma.En); Loc.Poner(Idioma.Es); }
                foreach (var kv in antes)
                {
                    if (kv.Key == null) continue;
                    // Los textos del bloque extra que reflejan el idioma/estado se reescriben con Refrescar: se comparan solo los fijos.
                    if (kv.Key.transform.IsChildOf(extra)) continue;
                    if (kv.Key.text != kv.Value) { identico = false; primero = $"{kv.Key.name}: '{kv.Value}' -> '{kv.Key.text}'"; break; }
                }
                Check($"Tras 12 idas y vueltas de idioma los textos fijos del panel quedan idénticos {primero}", identico);

                // 4) Las etiquetas de los toggles existen, tienen texto y caben (era 'no se ve el texto bajo los checks').
                foreach (var n in new[] { "InvertirEjeY", "EfectosDeCamara" })
                {
                    var et = sp.Find(n + "_Label") as RectTransform;
                    var txt = et != null ? et.GetComponent<Text>() : null;
                    Check($"La etiqueta del toggle '{n}' tiene texto y cabe en su caja", txt != null && txt.text.Length > 0 && txt.preferredHeight <= et.rect.height + 1f && txt.verticalOverflow == VerticalWrapMode.Overflow);
                }
                var area1 = AreaCanvas(new Vector2(1920, 1080), 1f);
                layout.Aplicar(area1);
                var toggleCaja = (RectTransform)sp.Find("InvertirEjeY_Toggle");
                var toggleEt = (RectTransform)sp.Find("InvertirEjeY_Label");
                Check("La etiqueta del toggle queda a la derecha de la casilla, sin pisarla", toggleEt.anchoredPosition.x - toggleEt.sizeDelta.x * 0.5f >= toggleCaja.anchoredPosition.x + toggleCaja.sizeDelta.x * 0.5f - 0.5f);
            }
            finally
            {
                foreach (var k in claves) { if (guardadas.TryGetValue(k, out int v)) PlayerPrefs.SetInt(k, v); else PlayerPrefs.DeleteKey(k); }
                PlayerPrefs.Save();
                SP.UI.AjustesDeJuego.PonerEscala(escala);
                SP.UI.AjustesDeJuego.PonerDaltonismo(dalto); SP.UI.AjustesDeJuego.PonerHudMinimo(hudMin);
                SP.Presentation.Subtitulos.Poner(subt);
                QualitySettings.SetQualityLevel(calidad, true);
                if (Loc.Actual != idioma) Loc.Poner(idioma);
                foreach (var k in claves) { if (guardadas.TryGetValue(k, out int v)) PlayerPrefs.SetInt(k, v); else PlayerPrefs.DeleteKey(k); }
                PlayerPrefs.Save();
                sp.gameObject.SetActive(estabaActivo);
            }
        }

        // Area util del canvas (960x540 de referencia / escala, CanvasScaler ScaleWithScreenSize con match 0,5) para una pantalla dada.
        static Vector2 AreaCanvas(Vector2 pantalla, float escalaInterfaz)
        {
            float refW = 960f / escalaInterfaz, refH = 540f / escalaInterfaz;
            float f = Mathf.Pow(2f, 0.5f * Mathf.Log(pantalla.x / refW, 2f) + 0.5f * Mathf.Log(pantalla.y / refH, 2f));
            return new Vector2(pantalla.x / f, pantalla.y / f);
        }
    }
}
