using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Operacion;
using SP.Vehicles;

namespace SP.Tutorial
{
    // Ronda 19 (Docs/PLAN_R19_v2.md, punto 3, tareas A2-A4): tutorial SIMPLE v2. Ocho pasos de UN gesto cada uno:
    //   camara · wasd · disparar · cambiar · rts · entrar_tanque · canon_simple · victoria
    // Reglas: mensaje de cada sub-paso <= 110 caracteres, <= 13 sub-pasos en total, ayuda a los 8 s, ningun paso exige el radial
    // (el radial sigue andando), cada paso tiene marca en el mundo. Solo corre con ModoSimple: el completo (R8) no cambia ni un texto,
    // porque las variantes REEMPLAZAN Subs/AlEntrar/Evaluar/AlSalir de los pasos ya definidos en DefinirPasos y los textos del
    // completo siguen literales en TutorialManager.cs (sin claves huerfanas en LocTextos).
    public partial class TutorialManager
    {
        // A3: radio REAL con el que el radial sube al tanque en el completo (el texto del paso dice "a menos de 7 m"); en el simple el
        // anillo usa driver.RadioDeInteraccion, que es el radio del [E] generico de esta escena.
        public const float RadioDelRadialAlTanque = 7f;
        public const string CartelDelTanqueCompleto = "USA [Q] → TANQUE";

        bool olaCanonLanzada, seleccionSimpleHecha;
        public bool OlaDelCanonLanzada => olaCanonLanzada;
        // Para los checks (Bug994e/f/l): la lista de pasos armada y la baliza del tanque.
        public System.Collections.Generic.IReadOnlyList<Paso> Pasos => pasos;
        public bool AnilloDeLaBalizaDelTanqueVisible => balizaTanque != null && balizaTanque.AnilloVisible;
        public bool HayBalizaDelTanque => balizaTanque != null;

        Paso PasoPorId(string id)
        {
            // (un bucle y no List.Find: el contador de Transform.Find por nombre de la suite no distingue el de las listas)
            foreach (var p0 in pasos) if (p0.Id == id) return p0;
            return null;
        }

        void AplicarVariantesSimples()
        {
            var f = Flags;
            var naranja = new Color(1f, 0.66f, 0.25f);

            // 1 camara: un solo sub-paso (antes 2): mirar a los costados y arriba/abajo.
            var camara = PasoPorId("camara");
            if (camara != null)
                camara.Subs = new[]
                {
                    S("Mira alrededor con el MOUSE", "Mueve el MOUSE: mira a los costados y después arriba y abajo.", "Si la cámara no gira, haz clic dentro de la ventana del juego.",
                      () => f.camaraHorizontal && f.camaraVertical, v => { f.camaraHorizontal = v; f.camaraVertical = v; }),
                };

            // 3 disparar: solo el enemigo quieto (sin pared ni caja).
            var disparar = PasoPorId("disparar");
            if (disparar != null)
            {
                disparar.Teclas = "LMB";
                disparar.Subs = new[]
                {
                    S("Derriba al enemigo quieto (rojo)", "Apunta al ENEMIGO ROJO y dispara con el CLIC IZQUIERDO. A LA CABEZA = 1 TIRO.",
                      "Lleva el punto de la mira sobre el soldado. Si no hay balas, recarga con R.", () => f.disparoEnemigo, v => f.disparoEnemigo = v),
                };
                disparar.AlEntrar = () =>
                {
                    if (objDummy != null) balizaDummy = TutorialBeacon.Crear("ENEMIGO QUIETO", new Color(1f, 0.3f, 0.25f), objDummy.position, objDummy, 1.2f, 10f);
                };
            }

            // 4 cambiar: la tecla directa [C] (CambioDeAliado) o el radial; la condicion es la misma (Brain.Current != soldadoInicial).
            var cambiar = PasoPorId("cambiar");
            if (cambiar != null)
            {
                cambiar.Titulo = "CAMBIAR DE SOLDADO";
                cambiar.Teclas = "C";
                cambiar.Subs = new[]
                {
                    S("Aprieta C: controlas a un aliado", "Toca [C]: pasas al aliado más cercano. (También: mira a un aliado y mantén [Q] → POSEER.)",
                      "Toca C una vez, sin mantenerla. Tus aliados tienen la columna celeste.", () => f.cambioDeSoldado, v => f.cambioDeSoldado = v),
                };
            }

            // 5 rts: TAB, clic derecho en el circulo (los 2 aliados quedan seleccionados solos) y TAB de vuelta (absorbe el paso fps).
            var rts = PasoPorId("rts");
            if (rts != null)
            {
                rts.Teclas = "Tab RMB";
                rts.Subs = new[]
                {
                    S("TAB: pasa a la vista táctica (RTS)", "Presiona [TAB]: la cámara sube y ves el mapa desde arriba.", "TAB está encima de Bloq Mayús.", () => f.rtsActivo, v => f.rtsActivo = v),
                    S("CLIC DERECHO en el círculo verde", "Tus 2 aliados ya están seleccionados: haz CLIC DERECHO en el CÍRCULO VERDE y caminan hasta allí.",
                      "Apunta al círculo verde y clic derecho. Cerca del círculo alcanza.", () => f.ordenDeMoverARts, v => f.ordenDeMoverARts = v,
                      () => $"Clic derecho en el círculo verde ({ordenadosARts.Count}/{aliados.Count})"),
                    S("TAB: vuelves a primera persona", "Presiona [TAB] otra vez: vuelves a mirar por los ojos de tu soldado.", "TAB alterna entre la vista táctica y la primera persona.",
                      () => f.vueltaAFps, v => f.vueltaAFps = v),
                };
                var entrarOriginal = rts.AlEntrar;
                var evaluarOriginal = rts.Evaluar;
                var salirOriginal = rts.AlSalir;
                rts.AlEntrar = () => { seleccionSimpleHecha = false; entrarOriginal?.Invoke(); };
                rts.Evaluar = () =>
                {
                    evaluarOriginal?.Invoke();
                    if (f.rtsActivo && !seleccionSimpleHecha && driver.Selection != null && aliados.Count > 0)
                    {
                        seleccionSimpleHecha = true;
                        driver.Selection.SelectAll(aliados);
                        f.aliadosSeleccionados = true;
                    }
                    if (f.ordenDeMoverARts && driver.Rig.Mode == ControlMode.Fps && !driver.Rig.IsTransitioning) f.vueltaAFps = true;
                };
                // Si el paso se salta con [F8] estando en la vista tactica, no se deja al jugador ahi (el paso fps ya no existe en el simple).
                rts.AlSalir = () =>
                {
                    salirOriginal?.Invoke();
                    if (driver != null && driver.Rig != null && driver.Rig.Mode == ControlMode.Rts) driver.Rig.SetMode(ControlMode.Fps);
                };
            }

            // 6 entrar_tanque: con [E] (SoloRadial = false) y el anillo del piso con el radio verdadero de esta escena.
            var entrar = PasoPorId("entrar_tanque");
            if (entrar != null)
            {
                entrar.Titulo = "SUBIR AL TANQUE";
                entrar.Teclas = "E";
                entrar.Subs = new[]
                {
                    S("Sube al tanque con E", "Camina hasta el CÍRCULO VERDE del tanque y aprieta [E] para subir.", "Párate dentro del círculo del piso, junto al tanque, y aprieta E.",
                      () => f.dentroDelTanque, v => { f.dentroDelTanque = v; if (v) f.cercaDelTanque = true; },
                      () => $"Sube al tanque con E ({DistanciaAlTanque():0} m)"),
                };
                entrar.AlEntrar = () =>
                {
                    driver.SoloRadial = false;
                    if (driver.Vehicle != null)
                        balizaTanque = TutorialBeacon.Crear("TANQUE", naranja, driver.Vehicle.transform.position, driver.Vehicle.transform, 2.2f, 14f);
                    MostrarAnilloDelTanque(driver.RadioDeInteraccion, AnilloDeSubida.CartelDelTanque);
                };
                entrar.AlSalir = () =>
                {
                    driver.SoloRadial = false;
                    OcultarAnilloDelTanque();
                    if (balizaTanque != null) { balizaTanque.Quitar(); balizaTanque = null; }
                };
            }

            // 7 canon_simple (nuevo, reemplaza a mira_tanque): [2] al canon y derribar 3. Sentarse en el canon ES el disparador de la ola.
            int iVictoria = pasos.FindIndex(p0 => p0.Id == "victoria");
            if (PasoPorId("canon_simple") == null)
                pasos.Insert(iVictoria >= 0 ? iVictoria : pasos.Count, CrearPasoCanonSimple(naranja));

            // 8 victoria: mensaje corto (el largo del completo sigue en DefinirPasos).
            var victoriaPaso = PasoPorId("victoria");
            if (victoriaPaso != null)
                victoriaPaso.MensajeVivo = () => "¡Nivel 1 completado! Ya sabes moverte, disparar, dar órdenes y usar el tanque.";

            TutorialLog.Escribir("[INICIO]", $"Modo simple v2: {pasos.Count} pasos (esperados {PasosSimples}), {SubPasosTotales()} sub-pasos, ayuda a los {SegundosParaAyudaSimple:0} s");
        }

        Paso CrearPasoCanonSimple(Color acento)
        {
            var f = Flags;
            return new Paso
            {
                Id = "canon_simple", Titulo = "EL CAÑÓN DEL TANQUE", Teclas = "2 LMB", Acento = acento,
                Subs = new[]
                {
                    S("Presiona 2: pasas al cañón", "Presiona [2]: te sientas en el CAÑÓN del tanque.", "1 conductor · 2 cañón · 3 metralleta · 4 pasajero.",
                      () => f.enLaTorreta, v => f.enLaTorreta = v),
                    S("Derriba 3 enemigos con el cañón", "¡Vienen enemigos! Apunta con el mouse y dispara el cañón con el CLIC IZQUIERDO.",
                      "Mueve el mouse para apuntar el cañón hacia los soldados rojos y haz clic izquierdo.", () => f.enemigosEliminados, v => f.enemigosEliminados = v,
                      () => $"Derriba 3 enemigos con el cañón ({Mathf.Min(bajasTotales, BajasNecesarias)}/{BajasNecesarias})"),
                },
                AlEntrar = () => { olaCanonLanzada = false; },
                Evaluar = () =>
                {
                    if (driver.CurrentSeat == VehicleSeatRole.Gunner) f.enLaTorreta = true;
                    // El disparador: recien al sentarse en el canon aparecen los 4 enemigos (a 36 m, avanzan al tanque). Con [F8] no se lanza.
                    if (f.enLaTorreta && !olaCanonLanzada && !f.enemigosEliminados)
                    {
                        olaCanonLanzada = true;
                        PostureDeAliados(CombatStance.AltoElFuego);   // los aliados no le roban las bajas al jugador
                        LanzarOlaA();
                        AlertQueueSafe("¡ENEMIGOS AL FRENTE!");
                    }
                    if (!olaCanonLanzada) return;
                    if (bajasTotales >= BajasNecesarias) f.enemigosEliminados = true;
                    else RellenarOlaA();
                },
                // Los que quedan de la ola no siguen tirandole al tanque durante el panel final (el tutorial ya no los protege al terminar).
                AlSalir = AquietarOlaRestante,
            };
        }

        void AquietarOlaRestante()
        {
            int n = 0;
            for (int i = olaViva.Count - 1; i >= 0; i--)
            {
                var s = olaViva[i];
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                if (s.Brain != null) { s.Brain.Stance = CombatStance.AltoElFuego; s.Brain.Pasivo = true; s.Brain.CancelOrder(); }
                n++;
            }
            olaViva.Clear();
            if (n > 0) TutorialLog.Escribir("[EVENTO]", $"   {n} enemigo(s) de la ola quedan quietos (fin del paso del canon)");
        }

        int SubPasosTotales()
        {
            int n = 0;
            foreach (var p in pasos) n += p.Subs != null ? p.Subs.Length : 0;
            return n;
        }

        // Para los checks (Bug994e): enemigos vivos de la ola A.
        public int EnemigosVivosDeLaOla()
        {
            int n = 0;
            foreach (var s in olaViva) if (s != null && s.Health != null && s.Health.IsAlive && idsOlaA.Contains(s.Id)) n++;
            return n;
        }

        Transform EnemigoVivoDeLaOla()
        {
            foreach (var s in olaViva) if (s != null && s.Health != null && s.Health.IsAlive) return s.transform;
            return null;
        }

        // A3: anillo de subida en el piso del tanque (singleton de la Operacion; en el tutorial no hay director que lo apague cada cuadro).
        void MostrarAnilloDelTanque(float radioDeUso, string cartel)
        {
            if (driver == null || driver.Vehicle == null) return;
            AnilloDeSubida.Mostrar(driver.Vehicle.transform, radioDeUso, cartel);
            if (balizaTanque != null) balizaTanque.OcultarAnillo();
        }

        void OcultarAnilloDelTanque()
        {
            bool subio = driver != null && driver.Vehicle != null && driver.Vehicle.PlayerAboard;
            AnilloDeSubida.Ocultar(subio);
        }
    }
}
