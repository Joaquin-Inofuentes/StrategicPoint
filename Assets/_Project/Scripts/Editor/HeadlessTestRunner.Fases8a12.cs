using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using SP.Core;
using SP.Combat;
using SP.Actors;
using SP.Ai;
using SP.Player;
using SP.CameraSystem;
using SP.UI;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // HeadlessTestRunner (parte): fases 8 a 12 de la suite.
    public partial class HeadlessTestRunner
    {
        // ---------------------------------------------------------------
        // FASE 8: las cinco tareas grandes del plan -- menu de ordenes con
        // [Q] sostenido, trazado de camino, coberturas, atropellar y mira.
        // ---------------------------------------------------------------
        static void RunPhase8(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
            GameObject soldierPrefab, Color enemyColor, ProjectilePool pool)
        {
            TestLog.Phase("FASE 8 - Menu de ordenes, camino trazado, coberturas, atropello y mira");

            // --- E2: el umbral que separa tocar de mantener ---
            SP.Player.KeyBindings.ForzarInicioDePulsacion(SP.Player.KeyBindings.CiclarPosesion, 0.1f);
            bool aLos100ms = SP.Player.KeyBindings.HayPulsacionRegistrada(
                SP.Player.KeyBindings.CiclarPosesion, PlayerInputDriver.SostenerParaMenu);
            Check("Un toque de 0,1 s NO llega al umbral de mantener (no abre el menu)", !aLos100ms);

            // Ronda 11 (punto 8): el umbral pasa de 0,3 a 0,5 s: 0,4 s sigue siendo un toque (accion por defecto), 0,6 s ya abre el menu.
            SP.Player.KeyBindings.ForzarInicioDePulsacion(SP.Player.KeyBindings.CiclarPosesion, 0.4f);
            bool aLos400ms = SP.Player.KeyBindings.HayPulsacionRegistrada(
                SP.Player.KeyBindings.CiclarPosesion, PlayerInputDriver.SostenerParaMenu);
            Check("Sostener 0,4 s sigue siendo un toque (umbral de mantener = 0,5 s)", !aLos400ms && PlayerInputDriver.SostenerParaMenu == 0.5f);
            SP.Player.KeyBindings.ForzarInicioDePulsacion(SP.Player.KeyBindings.CiclarPosesion, 0.6f);
            bool aLos600ms = SP.Player.KeyBindings.HayPulsacionRegistrada(
                SP.Player.KeyBindings.CiclarPosesion, PlayerInputDriver.SostenerParaMenu);
            Check("Sostener 0,6 s SI llega al umbral de mantener (abre el menu)", aLos600ms);

            var menu = inputDriver.OrdenesMenu;
            Check("El menu de ordenes existe en el canvas y arranca cerrado", menu != null && !menu.Abierto);
            if (menu != null)
            {
                menu.Abrir();
                Check("Abrir() deja el menu visible", menu.Abierto);
                menu.Cerrar();
                Check("Cerrar() lo vuelve a esconder", !menu.Abierto);
            }
            Check($"El menu ofrece las 5 ordenes del plan ({SP.UI.MenuDeOrdenes.Opciones.Length})",
                SP.UI.MenuDeOrdenes.Opciones.Length == SP.UI.MenuDeOrdenes.CantidadDeOpciones);

            // --- E2: los dos gestos de la MISMA tecla, uno y el otro ---
            // Se ejercen por ResolverGestoDeQ, que es adonde llega la
            // lectura del teclado. Que la lectura produzca esos booleanos
            // en el momento correcto lo prueba el umbral de arriba.
            inputDriver.Squad = new List<Soldier> { vega, kes, doc };
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.IsPossessedByPlayer = false;
            }
            inputDriver.Brain.Possess(vega);
            inputDriver.OrdenesMenu.Cerrar();

            // Pedido explicito del usuario: "Q debe ser interactuar" -- se saco el
            // fallback que ciclaba de aliado en el toque corto. Ahora un toque sin
            // nada interactuable a la mira no hace NADA (ni cicla, ni abre el menu).
            var antesDelToque = inputDriver.Brain.Current;
            inputDriver.ResolverGestoDeQ(toque: true, sostenido: false, sigueApretada: false);
            Check($"Toque corto sin interactuable: NO cicla de soldado ({antesDelToque.DisplayName} sigue siendo el poseido) y el menu NO aparece",
                inputDriver.Brain.Current == antesDelToque && !inputDriver.OrdenesMenu.Abierto);

            var antesDeMantener = inputDriver.Brain.Current;
            inputDriver.ResolverGestoDeQ(toque: false, sostenido: true, sigueApretada: true);
            Check("Mantener: aparece el menu y NO cicla de soldado",
                inputDriver.OrdenesMenu.Abierto && inputDriver.Brain.Current == antesDeMantener);

            inputDriver.ResolverGestoDeQ(toque: false, sostenido: false, sigueApretada: false);
            Check("Al soltar despues de mantener, el menu se cierra y tampoco cicla",
                !inputDriver.OrdenesMenu.Abierto && inputDriver.Brain.Current == antesDeMantener);

            // --- E2: las cinco ordenes hacen algo medible ---
            inputDriver.Squad = new List<Soldier> { vega, kes, doc };
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.IsPossessedByPlayer = false;
                s.Brain.CancelOrder();
            }
            inputDriver.Brain.Possess(vega);
            vega.Brain.IsPossessedByPlayer = true;
            OrderService.ManejadoAMano = vega;
            inputDriver.Selection.Clear();

            var destinatarios = inputDriver.DestinatariosDeOrden();
            Check($"Sin seleccion, las ordenes del menu igual le llegan a la escuadra viva menos el poseido ({destinatarios.Count})",
                destinatarios.Count == 2 && !destinatarios.Contains(vega));

            bool linea = inputDriver.EjecutarOrdenDelMenu(1);
            Check("Opcion 1 (formacion en linea) se emite y les deja destino a los dos aliados",
                linea && kes.Brain.CurrentOrderDestination.HasValue && doc.Brain.CurrentOrderDestination.HasValue);
            Vector3 destinoKesLinea = kes.Brain.CurrentOrderDestination ?? Vector3.zero;

            bool cuna = inputDriver.EjecutarOrdenDelMenu(2);
            Check("Opcion 2 (cuña) tambien se emite y cambia el destino respecto de la linea",
                cuna && kes.Brain.CurrentOrderDestination.HasValue
                     && (kes.Brain.CurrentOrderDestination.Value - destinoKesLinea).sqrMagnitude > 0.01f);

            bool seguir = inputDriver.EjecutarOrdenDelMenu(3);
            Check("Opcion 3 (siganme) deja a los dos aliados siguiendo al poseido",
                seguir && kes.Brain.State == AiState.Follow && doc.Brain.State == AiState.Follow);

            bool alto = inputDriver.EjecutarOrdenDelMenu(4);
            Check("Opcion 4 (alto) les cancela la orden a los dos",
                alto && !kes.Brain.CurrentOrderDestination.HasValue
                     && !doc.Brain.CurrentOrderDestination.HasValue
                     && kes.Brain.State != AiState.Follow && doc.Brain.State != AiState.Follow);

            // Opcion 5: el enfermero llega y cura de verdad.
            SP.Player.PedidoDeCuracion.Cancelar();
            vega.Health.TakeDamage(60, -1);
            int vidaAntes = vega.Health.Current;
            bool pedido = inputDriver.EjecutarOrdenDelMenu(5);
            Check($"Opcion 5 (necesito curarme) encuentra a quien mandar ({(SP.Player.PedidoDeCuracion.Enfermero != null ? SP.Player.PedidoDeCuracion.Enfermero.DisplayName : "nadie")})",
                pedido && SP.Player.PedidoDeCuracion.Activo);

            // Se lo pone al lado a mano: lo que se prueba aca es que curar
            // ocurre, no que sepa caminar (eso ya lo cubre la orden de seguir).
            SP.Player.PedidoDeCuracion.Enfermero.transform.position = vega.transform.position + Vector3.right * 1f;
            for (int i = 0; i < 60; i++) SimStep(0.05f);
            Check($"Con el enfermero al lado, la vida del herido sube ({vidaAntes} -> {vega.Health.Current})",
                vega.Health.Current > vidaAntes);

            vega.Health.Initialize(vega.Id, vega.Health.MaxHealth);
            SimStep(0.05f);
            Check("Con el herido ya lleno, el pedido de curacion se cierra solo",
                !SP.Player.PedidoDeCuracion.Activo);

            SP.Player.PedidoDeCuracion.Cancelar();
            OrderService.ManejadoAMano = null;
            vega.Brain.IsPossessedByPlayer = false;

            // --- C4: trazar un recorrido y ejecutarlo ---
            SP.Player.TrazadoDeCamino.Limpiar();
            kes.Brain.CancelOrder();
            // Lejos del origen a proposito: todos los obstaculos y los
            // enemigos de la escena viven dentro de |x|,|z| < 8, y un
            // recorrido que los cruza mide la navegacion (o el combate),
            // no el recorrido. El primer intento trazaba por (6,6), que
            // pasa por adentro de Obstaculo_1.
            kes.transform.position = new Vector3(30f, kes.transform.position.y, 30f);

            var recorrido = new Vector3[]
            {
                new Vector3(36f, 0f, 30f), new Vector3(36f, 0f, 36f),
                new Vector3(30f, 0f, 36f), new Vector3(24f, 0f, 30f),
            };
            int marcados = 0;
            foreach (var punto in recorrido)
                if (SP.Player.TrazadoDeCamino.Marcar(punto)) marcados++;
            Check($"Trazar 4 puntos con [Ctrl] deja 4 puntos en el recorrido ({marcados})",
                marcados == 4 && SP.Player.TrazadoDeCamino.Cantidad == 4);

            Check("Un punto pegado al anterior no suma un tramo de la nada",
                !SP.Player.TrazadoDeCamino.Marcar(recorrido[3] + new Vector3(0.2f, 0f, 0f))
                && SP.Player.TrazadoDeCamino.Cantidad == 4);

            int tramos = SP.Player.TrazadoDeCamino.Ejecutar(new List<Soldier> { kes });
            Check($"[Espacio] emite los 4 tramos y deja el trazado vacio ({tramos})",
                tramos == 4 && SP.Player.TrazadoDeCamino.Cantidad == 0);
            Check($"El soldado queda con 1 tramo en curso y 3 encolados ({kes.Brain.QueuedOrderCount} en cola)",
                kes.Brain.QueuedOrderCount == 3 && kes.Brain.CurrentOrderDestination.HasValue);

            // Que de verdad los recorra, y en orden: se anota a que
            // distancia minima paso de cada punto y en que instante.
            var masCerca = new float[recorrido.Length];
            var cuando = new int[recorrido.Length];
            for (int i = 0; i < recorrido.Length; i++) { masCerca[i] = float.MaxValue; cuando[i] = -1; }
            for (int paso = 0; paso < 1600; paso++)
            {
                SimStep(0.05f);
                for (int i = 0; i < recorrido.Length; i++)
                {
                    var plano = kes.transform.position; plano.y = 0f;
                    float d = Vector3.Distance(plano, recorrido[i]);
                    if (d < masCerca[i]) { masCerca[i] = d; if (d < 2f && cuando[i] < 0) cuando[i] = paso; }
                }
            }
            bool pasoPorTodos = true, enOrden = true;
            for (int i = 0; i < recorrido.Length; i++)
            {
                if (masCerca[i] >= 2f) pasoPorTodos = false;
                if (i > 0 && (cuando[i] < 0 || cuando[i - 1] < 0 || cuando[i] < cuando[i - 1])) enOrden = false;
            }
            Check($"Pasa a menos de 2 m de los 4 puntos (minimos: {masCerca[0]:0.0} / {masCerca[1]:0.0} / {masCerca[2]:0.0} / {masCerca[3]:0.0} m)",
                pasoPorTodos);
            Check($"Y los recorre EN ORDEN (pasos: {cuando[0]} -> {cuando[1]} -> {cuando[2]} -> {cuando[3]})", enOrden);

            SP.Player.TrazadoDeCamino.Limpiar();
            kes.Brain.CancelOrder();

            // --- G3: atropellar con el vehiculo en movimiento ---
            // Lejos de todo y con conductor propio adentro, asi el filtro
            // de bando tiene a quien respetar.
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vehicle.transform.position = new Vector3(50f, vehicle.transform.position.y, 50f);
            vehicle.transform.rotation = Quaternion.identity;   // mira a +Z
            vega.Brain.CancelOrder();
            vehicle.Mount(vega, VehicleSeatRole.Driver);
            var motorAtropello = vehicle.GetComponent<VehicleMotor>();

            // Frenar hasta cero ANTES de poner a nadie delante: el
            // vehiculo llega a esta fase con la velocidad que le dejaron
            // las anteriores (11 m/s), y Drive(0) solo le saca 4 m/s por
            // segundo. El primer intento de este test medio "quieto" a
            // 7,3 m/s y atropello al enemigo antes de empezar.
            for (int i = 0; i < 200 && !motorAtropello.IsStopped; i++) motorAtropello.Brake(0.05f);
            vehicle.transform.position = new Vector3(50f, vehicle.transform.position.y, 50f);
            vehicle.transform.rotation = Quaternion.identity;
            Check($"El vehiculo arranca la prueba frenado ({motorAtropello.CurrentSpeed:0.00} m/s)",
                motorAtropello.IsStopped);

            var victima = SpawnSoldier(soldierPrefab, "Atropellado", TeamId.Enemy, RoleType.Enemy,
                new Vector3(50f, 0f, 54f), enemyColor, pool, 100);
            var aliadoEnMedio = SpawnSoldier(soldierPrefab, "AliadoEnMedio", TeamId.Player, RoleType.Assault,
                new Vector3(50f, 0f, 54f), enemyColor, pool, 100);
            SP.Core.ApoyoEnElPiso.Apoyar(victima.transform);
            SP.Core.ApoyoEnElPiso.Apoyar(aliadoEnMedio.transform);
            victima.Brain.enabled = false;
            aliadoEnMedio.Brain.enabled = false;

            // Quieto: el motor tiene que estar tocando al enemigo y no
            // hacerle nada. Se lo pone pegado al casco a proposito.
            victima.transform.position = vehicle.transform.position + new Vector3(0f, 0f, 1.6f);
            int vidaQuieto = victima.Health.Current;
            for (int i = 0; i < 20; i++) motorAtropello.Drive(0f, 0f, 0.05f);
            Check($"Vehiculo quieto ({motorAtropello.CurrentSpeed:0.00} m/s) pegado al enemigo: 0 de daño ({vidaQuieto} -> {victima.Health.Current})",
                Mathf.Abs(motorAtropello.CurrentSpeed) < SP.Vehicles.Atropello.VelocidadMinima
                && victima.Health.Current == vidaQuieto);

            // Andando: se acelera hasta 8 m/s con el enemigo mas adelante.
            victima.transform.position = new Vector3(50f, victima.transform.position.y, 62f);
            aliadoEnMedio.transform.position = new Vector3(50f, aliadoEnMedio.transform.position.y, 62.5f);
            var rotAntes = aliadoEnMedio.transform.rotation;
            var rotVictimaAntes = victima.transform.rotation;
            float velocidadAlChocar = 0f;
            for (int i = 0; i < 200 && victima.Health.IsAlive; i++)
            {
                motorAtropello.Drive(1f, 0f, 0.05f);
                velocidadAlChocar = motorAtropello.CurrentSpeed;
            }
            Check($"Vehiculo a {velocidadAlChocar:0.0} m/s: el enemigo muere atropellado ({victima.Health.Current} de vida)",
                !victima.Health.IsAlive && velocidadAlChocar >= 8f);
            Check($"Y el cuerpo queda tirado, no de pie (angulo con la vertical: {Quaternion.Angle(rotVictimaAntes, victima.transform.rotation):0} grados)",
                Quaternion.Angle(rotVictimaAntes, victima.transform.rotation) > 45f);
            Check($"El aliado que iba en el camino NO fue atropellado por los suyos ({aliadoEnMedio.Health.Current} de vida)",
                aliadoEnMedio.Health.IsAlive && aliadoEnMedio.Health.Current == aliadoEnMedio.Health.MaxHealth
                && Quaternion.Angle(rotAntes, aliadoEnMedio.transform.rotation) < 1f);

            vehicle.Dismount(vega);
            UnityEngine.Object.DestroyImmediate(victima.gameObject);
            UnityEngine.Object.DestroyImmediate(aliadoEnMedio.gameObject);

            // --- F1: registrar los puntos de cobertura ---
            // 4 Obstaculo_N + el 'vehicle' de la prueba de atropello de mas
            // arriba, que sigue vivo en la escena (nunca se destruye, solo
            // se le hace Dismount) -- desde que NavService.BlocksMovement
            // trata al vehiculo como solido (bug real corregido: "el
            // soldado atraviesa el tanque"), tambien cuenta como obstaculo
            // de cobertura como cualquier otro. Antes daba 4 porque el
            // vehiculo estaba explicitamente excluido de "esto es pared".
            var solidos = SP.Core.Coberturas.Solidos();
            Check($"Los obstaculos solidos de la escena son los 4 Obstaculo_N + el vehiculo, ni el piso ni las armas tiradas ({solidos.Count})",
                solidos.Count == 5);

            // El vehiculo (a diferencia de los 4 Obstaculo_N, cajas simples
            // y aisladas) puede traer MAS de un collider solido propio
            // (chasis + torreta): un candidato de cobertura calculado
            // contra el chasis puede caer igual dentro del radio libre de
            // la torreta, y Candidato() lo descarta -- no es un bug, es el
            // mismo criterio de "no generar cobertura adentro de un
            // vecino" que ya se usa entre dos Obstaculo_N pegados, aplicado
            // ahora tambien a los propios colliders de un mismo cuerpo. Por
            // eso el vehiculo puede aportar MENOS de 4; los 4 Obstaculo_N
            // (cajas simples, sin sorpresas) siguen dando 4 cada uno.
            int coberturas = SP.Core.Coberturas.Registrar();
            Check($"Los 4 Obstaculo_N dan 4 coberturas cada uno, el resto (vehiculo) aporta lo que su geometria permita ({coberturas} para {solidos.Count} solidos)",
                coberturas >= 16 && coberturas <= 4 * solidos.Count);

            bool ningunaAdentro = true;
            float masCercaDeUnMuro = float.MaxValue;
            foreach (var p in SP.Core.Coberturas.Puntos)
            {
                var alrededor = Physics.OverlapSphere(p + Vector3.up * 0.5f, SP.Core.Coberturas.RadioLibre,
                    ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in alrededor)
                    if (SP.Core.NavService.BlocksMovement(c)) ningunaAdentro = false;
                foreach (var c in solidos)
                    masCercaDeUnMuro = Mathf.Min(masCercaDeUnMuro, Vector3.Distance(c.ClosestPoint(p), p));
            }
            Check("Ninguna cobertura cae adentro de un collider", ningunaAdentro);
            Check($"Y todas quedan pegadas a su obstaculo (la mas cercana a {masCercaDeUnMuro:0.00} m de la cara)",
                masCercaDeUnMuro < SP.Core.Coberturas.DistanciaDeLaCara + 0.1f);

            // Ronda 6: las marcas del piso estan OCULTAS por defecto y solo se ven al pedirlas ([C] / radial en CUBRIRSE).
            SP.Core.Coberturas.MostrarMarcas(false);
            Check("Las marcas de cobertura estan ocultas por defecto", GameObject.Find(SP.Core.Coberturas.NombreDelRoot) == null);
            SP.Core.Coberturas.MostrarMarcas(true);
            var marcasEnEscena = GameObject.Find(SP.Core.Coberturas.NombreDelRoot);
            Check($"Las coberturas quedan marcadas en el mapa al pedir verlas ({(marcasEnEscena != null ? marcasEnEscena.transform.childCount : 0)} marcas)",
                marcasEnEscena != null && marcasEnEscena.transform.childCount == coberturas);
            SP.Core.Coberturas.MostrarMarcas(false);

            // --- F2: linea de tiro desde una cobertura ---
            // Obstaculo_1 esta en (6, 3) y mide 2x2. El enemigo se pone
            // justo del otro lado: la cobertura de la cara opuesta al
            // enemigo NO puede tirarle, la del costado SI.
            var bloqueo = solidos[0];
            foreach (var c in solidos) if (c.name == "Obstaculo_1") bloqueo = c;
            var centroBloqueo = bloqueo.bounds.center;
            // Muy resistente a proposito: lo que se mide es DONDE termina
            // parado el soldado, y si el blanco se muere a mitad de camino
            // el combate se corta y la medicion no dice nada.
            var blanco = SpawnSoldier(soldierPrefab, "BlancoDetrasDelMuro", TeamId.Enemy, RoleType.Enemy,
                centroBloqueo + new Vector3(0f, 0f, 4f), enemyColor, pool, 100000);
            SP.Core.ApoyoEnElPiso.Apoyar(blanco.transform);
            blanco.Brain.enabled = false;

            var caraOpuesta = new Vector3(centroBloqueo.x, bloqueo.bounds.min.y, centroBloqueo.z - (bloqueo.bounds.extents.z + 1f));
            var costado = new Vector3(centroBloqueo.x + (bloqueo.bounds.extents.x + 1f), bloqueo.bounds.min.y, centroBloqueo.z);
            bool desdeOpuesta = SP.Core.Coberturas.HayLineaDeTiroDesde(caraOpuesta + Vector3.up, blanco, null);
            bool desdeCostado = SP.Core.Coberturas.HayLineaDeTiroDesde(costado + Vector3.up, blanco, null);
            Check($"Con el obstaculo en medio, la cobertura de la cara OPUESTA no tiene linea de tiro ({desdeOpuesta})",
                !desdeOpuesta);
            Check($"Y la del costado SI la tiene ({desdeCostado})", desdeCostado);

            // --- F3: el soldado va a cubrirse y desde ahi tiene tiro ---
            // Con su propio obstaculo a campo abierto y no con los de la
            // escena: el primer intento medio junto al origen y el soldado
            // se trababa contra las tres armas tiradas en el piso (que
            // bloquean el paso), asi que lo que se media era eso y no la
            // cobertura.
            var obstaculoDePrueba = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstaculoDePrueba.name = "ObstaculoDePrueba";
            obstaculoDePrueba.transform.position = new Vector3(40f, 1f, 40f);
            obstaculoDePrueba.transform.localScale = new Vector3(2f, 2f, 2f);
            UnityEngine.Object.DestroyImmediate(blanco.gameObject);
            blanco = SpawnSoldier(soldierPrefab, "BlancoDetrasDelObstaculo", TeamId.Enemy, RoleType.Enemy,
                new Vector3(40f, 0f, 44f), enemyColor, pool, 100000);
            SP.Core.ApoyoEnElPiso.Apoyar(blanco.transform);
            // enabled = false NO alcanza: SimStep llama Tick() a mano sobre
            // todos los cerebros, sin mirar el enabled del componente. Sin
            // esto el blanco devuelve el fuego y mata al soldado a mitad de
            // la medicion. IsPossessedByPlayer es la unica salida temprana
            // real de Tick.
            blanco.Brain.enabled = false;
            blanco.Brain.IsPossessedByPlayer = true;
            SP.Core.NavService.Invalidate();
            int coberturasConPrueba = SP.Core.Coberturas.Registrar();
            Check($"El obstaculo nuevo suma sus 4 coberturas ({coberturasConPrueba})",
                coberturasConPrueba == coberturas + 4);

            kes.Brain.CancelOrder();
            kes.Brain.IsPossessedByPlayer = false;
            kes.transform.position = new Vector3(40f, kes.transform.position.y, blanco.transform.position.z - 15f);
            SP.Core.ApoyoEnElPiso.Apoyar(kes.transform);
            // La vision de la IA es de 10 m y el escenario del plan pide
            // 15: sin esto el soldado ni se entera de que hay un enemigo y
            // lo que se mediria es el sensado, no la cobertura.
            var campoDeVision = GetRequiredField(typeof(AiBrain), "visionRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float visionOriginal = (float)campoDeVision.GetValue(kes.Brain);
            campoDeVision.SetValue(kes.Brain, 20f);
            // A la misma altura que el soldado: el blanco recien creado
            // quedaba con y=0 y el soldado apoyado en y=0,80, asi que el
            // rayo de la linea de tiro bajaba hasta rozar el piso y daba
            // false por el Ground, no por el obstaculo. En terreno plano
            // los dos estan a la misma altura.
            blanco.transform.position = new Vector3(blanco.transform.position.x,
                kes.transform.position.y, blanco.transform.position.z);
            Physics.SyncTransforms();

            float distInicial = Vector3.Distance(kes.transform.position, blanco.transform.position);
            bool tiroAlEmpezar = kes.Brain.TieneLineaDeTiro(blanco);

            for (int i = 0; i < 400; i++) SimStep(0.05f);

            float aLaCobertura = float.MaxValue;
            Vector3 masCercana = Vector3.zero;
            foreach (var p in SP.Core.Coberturas.Puntos)
            {
                float d = Vector3.Distance(new Vector3(kes.transform.position.x, p.y, kes.transform.position.z), p);
                if (d < aLaCobertura) { aLaCobertura = d; masCercana = p; }
            }
            bool tiroAlFinal = kes.Brain.TieneLineaDeTiro(blanco);
            Check($"Y termina ATACANDO desde ahi, no solo parado ({kes.Brain.State}, vivo={kes.Health.IsAlive})",
                kes.Brain.State == AiState.Attack && kes.Health.IsAlive);
            Check($"Arranca a {distInicial:0.0} m del enemigo y SIN linea de tiro (verificacion de la propia prueba)",
                distInicial > 12f && !tiroAlEmpezar);
            Check($"Termina a menos de 1,5 m de una cobertura ({aLaCobertura:0.00} m de {masCercana}), no donde arranco",
                aLaCobertura < 1.5f);
            Check($"Y desde ahi SI le puede disparar ({tiroAlFinal})", tiroAlFinal);

            campoDeVision.SetValue(kes.Brain, visionOriginal);
            UnityEngine.Object.DestroyImmediate(obstaculoDePrueba);
            UnityEngine.Object.DestroyImmediate(blanco.gameObject);
            kes.Brain.CancelOrder();
            SP.Core.Coberturas.Limpiar();

            // --- H1: la mira que amplia de verdad ---
            // Se llega por el mismo camino que en el juego (el visor del
            // arma), no construyendo la optica a mano: lo que se prueba es
            // el cableado, no la clase suelta.
            inputDriver.Brain.Possess(vega);
            var visorMetodo = GetRequiredMethod(typeof(PlayerInputDriver), "UpdateWeaponViewmodel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            inputDriver.Rig.SetZoomed(true);
            vega.Weapon.EquipWeapon(WeaponKind.Rifle, 20, 0.2f, Color.white);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });

            var mira = inputDriver.Mira;
            Check("El arma tiene optica cuando se apunta", mira != null);

            float fovPrincipal = inputDriver.Rig.Cam.fieldOfView;
            Check($"El FOV de la optica es MENOR que el de la camara principal ({mira.Optica.fieldOfView:0.0} contra {fovPrincipal:0.0} grados)",
                mira.Optica.fieldOfView < fovPrincipal);
            // Y tambien menor que el DESTINO del zoom: la principal tarda
            // varios frames en bajar de 60 a 25, y medido en Play la optica
            // llegaba a quedar en 27 contra 25, o sea mas abierta.
            Check($"Y tambien menor que el FOV al que va el zoom ({mira.Optica.fieldOfView:0.0} contra {inputDriver.Rig.FovObjetivo:0.0} grados)",
                mira.Optica.fieldOfView < inputDriver.Rig.FovObjetivo);
            Check($"La optica renderiza a su propia RenderTexture, ya creada ({SP.Presentation.MiraOptica.LadoDeLaTextura} px)",
                mira.Textura != null && mira.Textura.IsCreated() && mira.Optica.targetTexture == mira.Textura);
            Check($"Y no se filma a si misma: recorta por delante del visor del arma ({mira.Optica.nearClipPlane:0.00} m)",
                mira.Optica.nearClipPlane >= SP.Presentation.MiraOptica.RecorteCercano);

            // Apuntando, el arma y su optica tienen que ENTRAR en el
            // encuadre. Medido antes del arreglo: viewport x = 1,88 con el
            // FOV de zoom, o sea el arma casi al doble del borde derecho.
            // Se fuerza el estado final del centrado (el lerp tarda unos
            // frames y aca no hay frames que pasen).
            var campoApuntado = GetRequiredField(typeof(PlayerInputDriver), "apuntadoVisual",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            campoApuntado.SetValue(inputDriver, 1f);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            float fovAntes = inputDriver.Rig.Cam.fieldOfView;
            inputDriver.Rig.Cam.fieldOfView = inputDriver.Rig.FovDeZoom;
            var enPantalla = inputDriver.Rig.Cam.WorldToViewportPoint(mira.Tubo.transform.position);
            inputDriver.Rig.Cam.fieldOfView = fovAntes;
            Check($"Apuntando, la optica queda DENTRO del encuadre con el FOV de zoom (viewport {enPantalla.x:0.00}, {enPantalla.y:0.00})",
                enPantalla.x > 0f && enPantalla.x < 1f && enPantalla.y > 0f && enPantalla.y < 1f && enPantalla.z > 0f);

            // El zoom ahora es REAL: el aumento de cada arma (x2,2 el fusil, x6 el francotirador...)
            // se traduce en el FOV con la tangente, y la reticula es distinta por arma
            // (UI/MirillaView). El tubo con RenderTexture de MiraOptica ya no se muestra.
            var rigZ = inputDriver.Rig;
            var especFusil = WeaponCatalog.Get(WeaponKind.Rifle);
            rigZ.SetZoomFactor(especFusil.ZoomFactor);
            float aumentoFusil = Mathf.Tan(30f * Mathf.Deg2Rad) / Mathf.Tan(rigZ.FovDeZoom * 0.5f * Mathf.Deg2Rad);
            Check($"Con el fusil el zoom es REAL: x{especFusil.ZoomFactor:0.0} pedido, x{aumentoFusil:0.00} medido en el FOV ({rigZ.FovDeZoom:0.0} grados), reticula {especFusil.Reticle}",
                Mathf.Abs(aumentoFusil - especFusil.ZoomFactor) < 0.05f && especFusil.Reticle == ReticleStyle.Punto);
            var especFran = WeaponCatalog.Get(WeaponKind.Sniper);
            rigZ.SetZoomFactor(especFran.ZoomFactor);
            float aumentoFran = Mathf.Tan(30f * Mathf.Deg2Rad) / Mathf.Tan(rigZ.FovDeZoom * 0.5f * Mathf.Deg2Rad);
            Check($"Y el francotirador aumenta x{aumentoFran:0.0} con reticula {especFran.Reticle} (mas que el fusil)",
                Mathf.Abs(aumentoFran - 6f) < 0.1f && especFran.Reticle == ReticleStyle.Telescopica && especFran.ZoomFactor > especFusil.ZoomFactor);
            rigZ.SetZoomFactor(especFusil.ZoomFactor);

            vega.Weapon.EquipWeapon(WeaponKind.Pistol, 20, 0.2f, Color.white);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            Check("Con la pistola la reticula es una cruz con poco aumento y la optica vieja sigue apagada",
                WeaponCatalog.Get(WeaponKind.Pistol).Reticle == ReticleStyle.Cruz
                && WeaponCatalog.Get(WeaponKind.Pistol).ZoomFactor < especFusil.ZoomFactor && !mira.Optica.enabled);

            vega.Weapon.EquipWeapon(WeaponKind.Heavy, 20, 0.2f, Color.white);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            Check("Y el pesado lleva su propia reticula (anillo) con aumento",
                WeaponCatalog.Get(WeaponKind.Heavy).Reticle == ReticleStyle.Anillo && WeaponCatalog.Get(WeaponKind.Heavy).ZoomFactor > 1f);

            inputDriver.Rig.SetZoomed(false);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            Check("Sin apuntar, la optica se esconde y su camara se apaga (no se paga un render por frame de gusto)",
                !mira.Tubo.activeInHierarchy && !mira.Optica.enabled);

            vega.Weapon.EquipWeapon(WeaponKind.Rifle, 20, 0.2f, Color.white);

            TestLog.Phase("FASE 8 FINALIZADA");
        }

        // ---------------------------------------------------------------
        // FASE 9 · las 23 tareas S/M que quedan del plan (numeradas #1 a
        // #23 en la hoja de ruta). Se completa una a la vez: cada tarea
        // suma sus Check() aca mismo, sin abrir una fase nueva por tarea.
        // ---------------------------------------------------------------
        static void RunPhase9(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
            GameObject soldierPrefab, Color enemyColor, ProjectilePool pool)
        {
            TestLog.Phase("FASE 9 - Tarea #1: obstaculos y enemigos vistos en el minimapa");

            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }

            // --- #1 / D1: obstaculos y enemigos vistos en el minimapa ---
            // El minimapa mostraba a la escuadra y a los vehiculos (puestos
            // a mano en SC_Gameplay) pero MinimapIcon.Spawn nunca se llamaba
            // para un obstaculo: grep confirma que el unico llamador vivia
            // en este mismo archivo, para soldados y vehiculos.
            int obstaculoIconosAntes = 0;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null) obstaculoIconosAntes++;
            Check($"Antes de la tarea, ningun obstaculo tenia icono en el minimapa ({obstaculoIconosAntes})",
                obstaculoIconosAntes == 0);

            var obstaculosEnEscena = UnityEngine.Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include);
            int creados = MinimapIcon.RegistrarObstaculos(MinimapIcon.ObstacleMinimapColor);
            Check($"Se crea un icono de minimapa por cada obstaculo ({creados} para {obstaculosEnEscena.Length} en escena)",
                creados == obstaculosEnEscena.Length && creados == 4);

            bool losCuatroVisibles = true;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null && !ic.IsRendered)
                    losCuatroVisibles = false;
            Check("Y los 4 quedan siempre visibles: son terreno, no dependen de la niebla de guerra", losCuatroVisibles);

            // Idempotente por destruir-y-rearmar: llamarlo de nuevo no deja
            // 8 iconos superpuestos sobre los mismos 4 obstaculos.
            int segundaVez = MinimapIcon.RegistrarObstaculos(MinimapIcon.ObstacleMinimapColor);
            int totalTrasRepetir = 0;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null) totalTrasRepetir++;
            Check($"Registrarlos dos veces no duplica iconos ({totalTrasRepetir} tras llamarlo de nuevo, {segundaVez} creados la segunda vez)",
                segundaVez == 4 && totalTrasRepetir == 4);

            // La niebla de guerra sobre un enemigo (EnableFogOfWar +
            // WorldUiDirector.ApplyFog) ya existia en el codigo pero nunca
            // tuvo un Check(): la tarea pide explicitamente "enemigos que
            // la escuadra tenga a la vista", que es este mecanismo.
            // Rebarrer() de entrada: RebuildFogObservers lee
            // ActorRegistry.All directo, y Soldier.Awake (quien registra)
            // no corre en Edit mode -- sin esto el barrido de niebla ve
            // CERO observadores aunque Vega, Kes y Doc esten vivos y
            // parados ahi (medido: ActorRegistry.All.Count == 0).
            SP.Core.ActorRegistry.Rebarrer();
            // nextEvaluateAt se fuerza a 0 por reflexion para no depender
            // de que pasen 0,25 s reales de Time.time entre las dos
            // mediciones (lejos/cerca) de esta misma llamada a eval.
            var director = UnityEngine.Object.FindAnyObjectByType<WorldUiDirector>();
            var campoProximaEvaluacion = GetRequiredField(typeof(WorldUiDirector), "nextEvaluateAt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var enemigoDeNiebla = SpawnSoldier(soldierPrefab, "EnemigoDeNiebla", TeamId.Enemy, RoleType.Enemy,
                new Vector3(60f, 0.8f, 60f), enemyColor, pool, 100);
            enemigoDeNiebla.Brain.enabled = false;
            enemigoDeNiebla.Brain.IsPossessedByPlayer = true;
            MinimapIcon iconoEnemigoDeNiebla = null;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target == enemigoDeNiebla.transform) iconoEnemigoDeNiebla = ic;

            // OnEnable no corre en Edit mode (mismo motivo por el que
            // WorldSystemsRegistry.EnsurePopulated existe unas lineas mas
            // arriba, para obstaculos y vehiculos): sin esto el icono
            // recien creado nunca se da de alta en la lista estatica que
            // recorre Tick(), y ApplyFog no se le llama nunca aunque
            // IsSpotted ya de por si de true. Se registra solo este icono
            // (no EnsurePopulated entero) para no dejar el flag global
            // "populated" en true y que una segunda corrida en la misma
            // sesion de Editor se quede sin registrar sus propios iconos.
            WorldUiDirector.Register(iconoEnemigoDeNiebla);

            vega.transform.position = enemigoDeNiebla.transform.position + new Vector3(30f, 0f, 0f);
            campoProximaEvaluacion.SetValue(director, 0f);
            director.Tick();
            Check($"Un enemigo a 30 m de la escuadra no se ve en el minimapa (visible={iconoEnemigoDeNiebla.IsRendered})",
                !iconoEnemigoDeNiebla.IsRendered);

            vega.transform.position = enemigoDeNiebla.transform.position + new Vector3(8f, 0f, 0f);
            campoProximaEvaluacion.SetValue(director, 0f);
            director.Tick();
            Check($"Y acercando un aliado a 8 m, aparece (visible={iconoEnemigoDeNiebla.IsRendered})",
                iconoEnemigoDeNiebla.IsRendered);

            UnityEngine.Object.DestroyImmediate(enemigoDeNiebla.gameObject);
            vega.Brain.CancelOrder();

            // --- #2 / D2: [M] agranda y minimiza el minimapa ---
            TestLog.Phase("FASE 9 - Tarea #2: [M] agranda y minimiza el minimapa");
            var borderRect = GameObject.Find("MinimapBorder").GetComponent<RectTransform>();
            minimapFollowRef.AplicarTamanoInicial();
            Vector2 tamanoDePartida = borderRect.sizeDelta;
            Check($"El minimapa arranca en MINI (tamanoMini={minimapFollowRef.tamanoMini}, marco={tamanoDePartida})",
                tamanoDePartida == minimapFollowRef.tamanoMini && !minimapFollowRef.Agrandado);

            bool agrandadoTrasM = minimapFollowRef.AlternarTamano();
            Check($"[M] lo expande a tamanoExpandido ({tamanoDePartida} -> {borderRect.sizeDelta})",
                agrandadoTrasM && borderRect.sizeDelta == minimapFollowRef.tamanoExpandido);

            bool agrandadoTrasSegundoM = minimapFollowRef.AlternarTamano();
            Check($"Y el segundo [M] lo devuelve EXACTO al mini ({borderRect.sizeDelta})",
                !agrandadoTrasSegundoM && borderRect.sizeDelta == tamanoDePartida);

            for (int i = 0; i < 10; i++) minimapFollowRef.AlternarTamano();
            Check($"Ni tras 5 ciclos completos hay deriva ({borderRect.sizeDelta})",
                borderRect.sizeDelta == tamanoDePartida);

            // --- #3 / D3: [L] cicla mini -> medio -> expandido ---
            TestLog.Phase("FASE 9 - Tarea #3: [L] cicla el tamaño del minimapa");
            int indiceDeReferencia = minimapFollowRef.CiclarTamanoFijo();
            Vector2 tamanoDeReferencia = borderRect.sizeDelta;
            minimapFollowRef.CiclarTamanoFijo();
            minimapFollowRef.CiclarTamanoFijo();
            int indiceTrasTres = minimapFollowRef.CiclarTamanoFijo();
            Check($"Tres [L] seguidos vuelven al mismo tamaño (indice {indiceDeReferencia} -> {indiceTrasTres}, {tamanoDeReferencia} -> {borderRect.sizeDelta})",
                indiceTrasTres == indiceDeReferencia && borderRect.sizeDelta == tamanoDeReferencia);

            minimapFollowRef.CiclarTamanoFijo();
            borderRect.sizeDelta = new Vector2(999f, 999f); // valor cualquiera, simulando la escena a medio cargar
            minimapFollowRef.AplicarTamanoGuardado();
            Check($"Y al 'recargar la escena' siempre vuelve a MINI, no a lo ultimo que se eligio ({borderRect.sizeDelta})",
                borderRect.sizeDelta == minimapFollowRef.tamanoMini);

            // --- #4 / B1: un circulo radial reutilizable ---
            TestLog.Phase("FASE 9 - Tarea #4: un circulo radial reutilizable");
            var canvasGO = GameObject.Find("Canvas");
            var circulo = SP.UI.CirculoDeProgreso.Construir(canvasGO.transform, 64f, Color.black, Color.cyan);
            Check("El relleno es Filled/Radial360 y ya tiene sprite (sin eso fillAmount no dibuja nada, bug 30)",
                circulo.Relleno.type == Image.Type.Filled
                && circulo.Relleno.fillMethod == Image.FillMethod.Radial360
                && circulo.Relleno.sprite != null
                && circulo.Fondo.sprite != null);

            float[] valoresDePrueba = { 0f, 0.33f, 0.5f, 1f };
            bool todosExactos = true;
            foreach (var v in valoresDePrueba)
            {
                circulo.SetProgreso(v);
                if (!Mathf.Approximately(circulo.Relleno.fillAmount, v)) todosExactos = false;
            }
            Check($"Los 4 valores de prueba (0 / 0,33 / 0,5 / 1) dan el fillAmount exacto",
                todosExactos);

            UnityEngine.Object.DestroyImmediate(circulo.gameObject);

            // --- #5 / B2: la recarga se ve como circulo sobre la mira ---
            TestLog.Phase("FASE 9 - Tarea #5: la recarga se ve como circulo sobre la mira");
            var campoReloadDuration = GetRequiredField(typeof(WeaponHolder), "reloadDuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float duracionDeRecarga = (float)campoReloadDuration.GetValue(vega.Weapon);

            // Reload() se rechaza en silencio con el cargador lleno (y
            // H1, arriba, lo deja lleno tras EquipWeapon): hay que gastar
            // al menos una bala antes de poder recargar de verdad.
            vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
            bool sePudoRecargar = vega.Weapon.Reload();
            Check("Se pudo iniciar la recarga (habia menos balas que el cargador)", sePudoRecargar);
            aimUiRef.UpdateReloadCircle(vega.Weapon);
            var campoCirculoRecarga = GetRequiredField(typeof(AimUI), "circuloRecarga",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var circuloRecarga = (CirculoDeProgreso)campoCirculoRecarga.GetValue(aimUiRef);
            Check("Al recargar, el circulo de progreso aparece sobre la mira", circuloRecarga.gameObject.activeSelf);

            vega.Weapon.Tick(duracionDeRecarga * 0.5f);
            aimUiRef.UpdateReloadCircle(vega.Weapon);
            Check($"A los {duracionDeRecarga * 0.5f:0.00} s de {duracionDeRecarga:0.00}, el circulo va por la mitad (fillAmount={circuloRecarga.Relleno.fillAmount:0.00})",
                Mathf.Abs(circuloRecarga.Relleno.fillAmount - 0.5f) < 0.05f);

            vega.Weapon.Tick(duracionDeRecarga);
            aimUiRef.UpdateReloadCircle(vega.Weapon);
            Check("Y al terminar la recarga, el circulo se esconde", !circuloRecarga.gameObject.activeSelf);

            // --- #6 / B3: al apuntar a un enemigo, su vida como circulo ---
            TestLog.Phase("FASE 9 - Tarea #6: al apuntar a un enemigo, su vida como circulo");
            var enemigoParaB3 = SpawnSoldier(soldierPrefab, "EnemigoParaB3", TeamId.Enemy, RoleType.Enemy,
                new Vector3(70f, 0.8f, 70f), enemyColor, pool, 180);
            enemigoParaB3.Brain.enabled = false;
            enemigoParaB3.Brain.IsPossessedByPlayer = true;
            enemigoParaB3.Health.TakeDamage(120, -1); // 180 - 120 = 60 de 180 => 0,33

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Enemy, Soldier = enemigoParaB3, Point = enemigoParaB3.transform.position });
            var campoCirculoVida = GetRequiredField(typeof(AimUI), "circuloVidaEnemigo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var circuloVida = (CirculoDeProgreso)campoCirculoVida.GetValue(aimUiRef);
            float fraccionEsperada = (float)enemigoParaB3.Health.Current / enemigoParaB3.Health.MaxHealth;
            Check($"Apuntando a un enemigo con {enemigoParaB3.Health.Current}/{enemigoParaB3.Health.MaxHealth} de vida, el circulo muestra esa fraccion ({circuloVida.Relleno.fillAmount:0.00} vs {fraccionEsperada:0.00})",
                circuloVida.gameObject.activeSelf && Mathf.Abs(circuloVida.Relleno.fillAmount - fraccionEsperada) < 0.01f);

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Ground, Point = enemigoParaB3.transform.position });
            Check("Y apuntando al piso, el circulo de vida se apaga", !circuloVida.gameObject.activeSelf);

            UnityEngine.Object.DestroyImmediate(enemigoParaB3.gameObject);

            // --- #7 / B4: circulo de apuntado en la base del objetivo ---
            TestLog.Phase("FASE 9 - Tarea #7: circulo de apuntado en la base del objetivo");
            var enemigoParaB4 = SpawnSoldier(soldierPrefab, "EnemigoParaB4", TeamId.Enemy, RoleType.Enemy,
                new Vector3(80f, 0.8f, 80f), enemyColor, pool, 100);
            enemigoParaB4.Brain.enabled = false;
            enemigoParaB4.Brain.IsPossessedByPlayer = true;

            var metodoUpdateAimRing = GetRequiredMethod(typeof(PlayerInputDriver), "UpdateAimRing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var campoAimRing = GetRequiredField(typeof(PlayerInputDriver), "aimRing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var metodoLateUpdateAnillo = GetRequiredMethod(typeof(SelectionRingFx), "LateUpdate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var resultadoEnemigoB4 = new AimResult { Type = AimTargetType.Enemy, Soldier = enemigoParaB4, Point = enemigoParaB4.transform.position, HitTransform = enemigoParaB4.transform };
            metodoUpdateAimRing.Invoke(inputDriver, new object[] { resultadoEnemigoB4 });
            var anilloDeApuntado = (SelectionRingFx)campoAimRing.GetValue(inputDriver);
            // LateUpdate no corre solo en Edit mode: se fuerza una vez,
            // igual que con WorldUiDirector.Tick() en la tarea #1.
            metodoLateUpdateAnillo.Invoke(anilloDeApuntado, null);

            Physics.SyncTransforms();
            float baseDelCollider = enemigoParaB4.GetComponentInChildren<Collider>().bounds.min.y;
            Check($"Apuntando a un enemigo, el anillo existe y su Y ({anilloDeApuntado.transform.position.y:0.00}) coincide con la base de su collider ({baseDelCollider:0.00})",
                anilloDeApuntado != null && anilloDeApuntado.gameObject.activeSelf
                && Mathf.Abs(anilloDeApuntado.transform.position.y - baseDelCollider) < 0.05f);

            metodoUpdateAimRing.Invoke(inputDriver, new object[] { new AimResult { Type = AimTargetType.None } });
            Check("Y sin objetivo bajo la mira, el anillo se oculta", !anilloDeApuntado.gameObject.activeSelf);

            UnityEngine.Object.DestroyImmediate(enemigoParaB4.gameObject);

            // --- #8 / B5: cursor rojo si es destruible, y latido por tipo ---
            TestLog.Phase("FASE 9 - Tarea #8: cursor rojo si es destruible, y latido por tipo");
            var campoTint = GetRequiredField(typeof(AimUI), "currentAimTint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var obstaculoConMarker = UnityEngine.Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include)[0];
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Obstacle,
                Point = obstaculoConMarker.transform.position, HitTransform = obstaculoConMarker.transform });
            var tintDestructible = (Color)campoTint.GetValue(aimUiRef);
            Check($"Sobre un obstaculo CON ObstacleMarker (destructible), el tinte es rojo {tintDestructible}",
                tintDestructible.r > 0.7f && tintDestructible.g < 0.3f && tintDestructible.b < 0.3f);

            var muroDePrueba = GameObject.CreatePrimitive(PrimitiveType.Cube);
            muroDePrueba.name = "MuroDePrueba";
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Obstacle,
                Point = muroDePrueba.transform.position, HitTransform = muroDePrueba.transform });
            var tintMuro = (Color)campoTint.GetValue(aimUiRef);
            Check($"Y sobre uno SIN ObstacleMarker (pared fija, no destructible), el tinte NO es rojo {tintMuro}",
                !(tintMuro.r > 0.7f && tintMuro.g < 0.3f && tintMuro.b < 0.3f));
            UnityEngine.Object.DestroyImmediate(muroDePrueba);

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Ally, Soldier = kes });
            float frecuenciaAliado = aimUiRef.CurrentPulseFrequency;
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Enemy, Soldier = kes });
            float frecuenciaEnemigo = aimUiRef.CurrentPulseFrequency;
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle });
            float frecuenciaVehiculo = aimUiRef.CurrentPulseFrequency;
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Obstacle,
                Point = obstaculoConMarker.transform.position, HitTransform = obstaculoConMarker.transform });
            float frecuenciaObstaculo = aimUiRef.CurrentPulseFrequency;

            var frecuenciasDistintas = new HashSet<float> { frecuenciaAliado, frecuenciaEnemigo, frecuenciaVehiculo, frecuenciaObstaculo };
            Check($"Las 4 frecuencias de latido son distintas y positivas ({frecuenciaAliado}, {frecuenciaEnemigo}, {frecuenciaVehiculo}, {frecuenciaObstaculo})",
                frecuenciasDistintas.Count == 4 && frecuenciaAliado > 0f && frecuenciaEnemigo > 0f && frecuenciaVehiculo > 0f && frecuenciaObstaculo > 0f);

            // --- #9-#11 / A1+A2+A3: la camara de muerte espera en vez de
            // cambiar sola. DeathSequence es una corrutina
            // (StartCoroutine): no corre en Edit mode, asi que el timing
            // real -- la espera, [Espacio] adelantando el cambio, los 5 s
            // de A3 -- se midio en Play mode sobre SC_Gameplay (no hay
            // Check() posible aca para eso). Lo que si se puede verificar
            // en Edit mode es la pieza de datos que esa espera usa.
            TestLog.Phase("FASE 9 - Tareas #9-#11: A1+A2+A3, la camara de muerte espera en vez de cambiar sola");
            Check($"La espera antes de pasar a RTS es de {PlayerInputDriver.EsperaMaximaTrasMorir} s (A3)",
                PlayerInputDriver.EsperaMaximaTrasMorir == 5f);

            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }
            var elegidoTrasMorirVega = OrderService.FindNearestFreeAlly(vega.transform.position, TeamId.Player, vega);
            Check($"El aliado que A2 pide con [Espacio] es el vivo mas cercano, nunca el propio muerto ({elegidoTrasMorirVega?.DisplayName})",
                elegidoTrasMorirVega != null && elegidoTrasMorirVega != vega && elegidoTrasMorirVega.Health.IsAlive);

            // --- #12 / A4: mantener [E] 5 s revive a un caido ---
            // TryRevivir toma "sostenido lo suficiente" como parametro (no
            // lee Keyboard.current el mismo) exactamente para esto: se
            // puede simular con ForzarInicioDePulsacion + HayPulsacionRegistrada
            // sin depender de un teclado real, que no existe en Edit mode.
            TestLog.Phase("FASE 9 - Tarea #12: mantener [E] 5 s revive a un caido");
            doc.Health.TakeDamage(999999, -1);
            Check($"Doc esta muerto para la prueba ({doc.Health.Current} de vida)", !doc.Health.IsAlive);

            KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, 3f);
            bool revivioA3s = inputDriver.TryRevivir(doc, KeyBindings.HayPulsacionRegistrada(KeyBindings.Interactuar, PlayerInputDriver.TiempoDeRevivir));
            Check($"A los 3 s de {PlayerInputDriver.TiempoDeRevivir} sigue muerto ({doc.Health.IsAlive})",
                !revivioA3s && !doc.Health.IsAlive);

            // +0.1 en vez de EXACTO: HayPulsacionRegistrada compara con
            // Time.unscaledTime, que tras horas de sesion de Editor pierde
            // precision de punto flotante justo en el limite (>=) y el
            // check salia flaky sin que el mecanismo real tuviera nada
            // roto -- en el juego de verdad nadie suelta la tecla en el
            // milisegundo EXACTO del umbral.
            KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, PlayerInputDriver.TiempoDeRevivir + 0.1f);
            bool revivioA5s = inputDriver.TryRevivir(doc, KeyBindings.HayPulsacionRegistrada(KeyBindings.Interactuar, PlayerInputDriver.TiempoDeRevivir));
            Check($"Y a los {PlayerInputDriver.TiempoDeRevivir} s, Health.IsAlive pasa a true ({doc.Health.Current}/{doc.Health.MaxHealth})",
                revivioA5s && doc.Health.IsAlive && doc.Health.Current == doc.Health.MaxHealth);

            // --- #13 / A5: un aliado libre va a revivirte y frena el timer ---
            TestLog.Phase("FASE 9 - Tarea #13: un aliado libre va a revivirte y frena el timer");
            SP.Player.RescateAutomatico.Cancelar();
            vega.transform.position = new Vector3(95f, 0.8f, 95f);
            kes.transform.position = vega.transform.position + new Vector3(10f, 0f, 0f);
            SP.Core.ApoyoEnElPiso.Apoyar(kes.transform);
            doc.transform.position = new Vector3(-95f, 0.8f, -95f); // lejos, no interfiere

            vega.Health.TakeDamage(999999, -1);
            bool solicitoRescate = SP.Player.RescateAutomatico.Solicitar(vega);
            Check($"Con un aliado a 10 m y ningun enemigo cerca, se pide el rescate ({SP.Player.RescateAutomatico.Rescatista?.DisplayName})",
                solicitoRescate && SP.Player.RescateAutomatico.Activo && SP.Player.RescateAutomatico.Rescatista == kes);

            // Se lo pone al lado a mano: lo que se prueba aca es que
            // revivir ocurre, no que sepa caminar (eso ya lo cubre la
            // orden de seguir -- mismo criterio que el enfermero de
            // PedidoDeCuracion unas fases atras).
            kes.transform.position = vega.transform.position + Vector3.right * 1f;
            for (int i = 0; i < 80; i++) SimStep(0.05f); // 4 s de canal
            Check($"A los 4 s de canal (de {SP.Player.RescateAutomatico.TiempoDeCanal} s) sigue muerto", !vega.Health.IsAlive);

            for (int i = 0; i < 30; i++) SimStep(0.05f); // +1.5 s: pasa los 5 s
            Check($"Y a los ~{SP.Player.RescateAutomatico.TiempoDeCanal} s, Health.IsAlive pasa a true ({vega.Health.Current}/{vega.Health.MaxHealth})",
                vega.Health.IsAlive && vega.Health.Current == vega.Health.MaxHealth);
            Check("El rescate se cierra solo tras revivir", !SP.Player.RescateAutomatico.Activo);

            SP.Player.RescateAutomatico.Cancelar();

            TestLog.Phase("FASE 9 FINALIZADA (13/23)");

            // --- #14 / E1: [F] sobre un enemigo da la orden de atacar ---
            TestLog.Phase("FASE 9 - Tarea #14: [F] sobre un enemigo da la orden de atacar");
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }
            inputDriver.Brain.Possess(vega);
            vega.Brain.IsPossessedByPlayer = true;
            var estadoDeVegaAntes = vega.Brain.State;

            inputDriver.Selection.SelectSingle(kes);
            inputDriver.Selection.AddToSelection(doc);

            var enemigoParaE1 = SpawnSoldier(soldierPrefab, "EnemigoParaE1", TeamId.Enemy, RoleType.Enemy,
                new Vector3(85f, 0.8f, 85f), enemyColor, pool, 150);
            enemigoParaE1.Brain.enabled = false;
            enemigoParaE1.Brain.IsPossessedByPlayer = true;

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Enemy, Soldier = enemigoParaE1,
                Point = enemigoParaE1.transform.position, HitTransform = enemigoParaE1.transform });
            // Pedido explicito: sacar el cartel de "atacar" -- molesta. El
            // tinte rojo de la mira (EnemyTint) sigue avisando que hay un
            // enemigo encima; antes este check pedia lo contrario.
            Check($"Apuntando a un enemigo, no aparece cartel de atacar (\"{aimUiRef.CurrentPrompt}\")",
                string.IsNullOrEmpty(aimUiRef.CurrentPrompt));

            OrderService.IssueAttackOrderForSelection(inputDriver.Selection.Selected, enemigoParaE1);
            SimulateSeconds(1f); // deja que Chase/Attack se resuelva tras la orden

            bool kesEnCombate = kes.Brain.State == AiState.Chase || kes.Brain.State == AiState.MovingToAttackOrder || kes.Brain.State == AiState.Attack;
            bool docEnCombate = doc.Brain.State == AiState.Chase || doc.Brain.State == AiState.MovingToAttackOrder || doc.Brain.State == AiState.Attack;
            Check($"[F] deja a los 2 seleccionados yendo a atacar o atacando ({kes.Brain.State}, {doc.Brain.State})",
                kesEnCombate && docEnCombate);
            Check($"Y al poseido no le llega la orden: su estado no cambio ({estadoDeVegaAntes} -> {vega.Brain.State})",
                vega.Brain.State == estadoDeVegaAntes);

            UnityEngine.Object.DestroyImmediate(enemigoParaE1.gameObject);
            kes.Brain.CancelOrder();
            doc.Brain.CancelOrder();
            inputDriver.Selection.Clear();

            TestLog.Phase("FASE 9 FINALIZADA (14/23)");

            // --- #15 / E3: doble [T] reparte, Shift+[T] distribuye ---
            TestLog.Phase("FASE 9 - Tarea #15: doble [T] reparte, Shift+[T] distribuye");
            var puntoT = new Vector3(90f, 0f, 90f);
            inputDriver.IssueGroundOrderT(puntoT, false);
            Check("El primer [T] manda a alguien",
                kes.Brain.CurrentOrderDestination.HasValue || doc.Brain.CurrentOrderDestination.HasValue);

            inputDriver.IssueGroundOrderT(puntoT, false); // segundo T rapido, mismo punto
            Check($"Y el segundo [T] rapido reparte al OTRO, no repite al mismo (Kes={kes.Brain.CurrentOrderDestination.HasValue}, Doc={doc.Brain.CurrentOrderDestination.HasValue})",
                kes.Brain.CurrentOrderDestination.HasValue && doc.Brain.CurrentOrderDestination.HasValue);

            kes.Brain.CancelOrder();
            doc.Brain.CancelOrder();

            // Shift+[T] con 3 libres: Vega esta poseido, asi que un
            // conductor temporal toma el mando para dejar a Vega, Kes y
            // Doc libres los tres a la vez.
            var conductorTemporalE3 = SpawnSoldier(soldierPrefab, "ConductorTemporalE3", TeamId.Player, RoleType.Assault,
                new Vector3(200f, 0.8f, 200f), new Color(0.25f, 0.55f, 0.98f), pool, 100);
            inputDriver.Brain.Possess(conductorTemporalE3);
            conductorTemporalE3.Brain.IsPossessedByPlayer = true;
            vega.Brain.CancelOrder();

            var puntoShiftT = new Vector3(-90f, 0f, -90f);
            inputDriver.IssueGroundOrderT(puntoShiftT, true);

            Vector3? destinoVega = vega.Brain.CurrentOrderDestination;
            Vector3? destinoKes = kes.Brain.CurrentOrderDestination;
            Vector3? destinoDoc = doc.Brain.CurrentOrderDestination;
            Check($"Shift+[T] con 3 libres les da destino a los 3 (Vega={destinoVega.HasValue}, Kes={destinoKes.HasValue}, Doc={destinoDoc.HasValue})",
                destinoVega.HasValue && destinoKes.HasValue && destinoDoc.HasValue);

            float dVK = Vector3.Distance(destinoVega.Value, destinoKes.Value);
            float dVD = Vector3.Distance(destinoVega.Value, destinoDoc.Value);
            float dKD = Vector3.Distance(destinoKes.Value, destinoDoc.Value);
            Check($"Y los 3 destinos quedan a mas de 1,8 m entre si ({dVK:0.00}, {dVD:0.00}, {dKD:0.00})",
                dVK > 1.8f && dVD > 1.8f && dKD > 1.8f);

            UnityEngine.Object.DestroyImmediate(conductorTemporalE3.gameObject);
            inputDriver.Brain.Possess(vega);
            vega.Brain.IsPossessedByPlayer = true;
            vega.Brain.CancelOrder();
            kes.Brain.CancelOrder();
            doc.Brain.CancelOrder();

            TestLog.Phase("FASE 9 FINALIZADA (15/23)");

            // --- #16 / C1: bajo cada unidad: vida, tipo y ocupantes ---
            TestLog.Phase("FASE 9 - Tarea #16: bajo cada unidad: vida, tipo y ocupantes");
            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);
            vehicle.Mount(kes);
            vehicle.Mount(doc);

            var etiquetaVehiculo = UnitLabelView.Construir(vehicle.transform);
            WorldUiDirector.Register(etiquetaVehiculo);
            var etiquetaVega = UnitLabelView.Construir(vega.transform);
            WorldUiDirector.Register(etiquetaVega);

            var directorC1 = UnityEngine.Object.FindAnyObjectByType<WorldUiDirector>();
            var campoProximaEvaluacionC1 = GetRequiredField(typeof(WorldUiDirector), "nextEvaluateAt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // Antes esto simulaba el cambio de modo pisando
            // Camera.main.orthographic directo (era la marca real que
            // usaba WorldUiDirector). Con la RTS en perspectiva (pedido
            // explicito de que no sea ortogonal) esa marca ya no existe:
            // el modo real pasa por CameraRig.SetMode, y WorldUiDirector
            // consulta el singleton CameraRig.Instance -- pero
            // HeadlessTestRunner corre en Edit Mode, donde Unity no manda
            // OnEnable a un MonoBehaviour comun (medido: ni recreando el
            // ciclo enabled=false/true en el rig real de la escena
            // Instance se llega a fijar fuera de Play). EnsureInstanceForTests
            // es el escape valvula explicito para este harness.
            CameraRig.EnsureInstanceForTests(inputDriver.Rig);
            var rigC1 = CameraRig.Instance;
            if (rigC1 == null) { Check("CameraRig.Instance disponible para el test C1", false); return; }
            var modoOriginalC1 = rigC1.Mode;

            // FPS primero: las etiquetas tienen que quedar apagadas.
            rigC1.SetMode(ControlMode.Fps);
            campoProximaEvaluacionC1.SetValue(directorC1, 0f);
            directorC1.Tick();
            Check($"En FPS, la etiqueta se apaga (visible={etiquetaVehiculo.IsVisible})",
                !etiquetaVehiculo.IsVisible);

            // RTS: aparecen, con la vida/tipo/ocupacion correctos.
            rigC1.SetMode(ControlMode.Rts);
            campoProximaEvaluacionC1.SetValue(directorC1, 0f);
            directorC1.Tick();
            Check($"En RTS, con 2 montados de 4, la etiqueta del vehiculo dice 2/4 (\"{etiquetaVehiculo.CurrentText}\")",
                etiquetaVehiculo.IsVisible && etiquetaVehiculo.CurrentText == $"Vehiculo  2/{vehicle.Capacity}");
            Check($"Y la del aliado muestra su tipo y su vida (\"{etiquetaVega.CurrentText}\")",
                etiquetaVega.IsVisible && etiquetaVega.CurrentText == $"{vega.ClassName}  {vega.Health.Current}/{vega.Health.MaxHealth}");

            vehicle.Dismount(doc);
            campoProximaEvaluacionC1.SetValue(directorC1, 0f);
            directorC1.Tick();
            Check($"Al bajar uno, pasa a 1/4 (\"{etiquetaVehiculo.CurrentText}\")",
                etiquetaVehiculo.CurrentText == $"Vehiculo  1/{vehicle.Capacity}");

            rigC1.SetMode(modoOriginalC1);
            vehicle.Dismount(kes);
            UnityEngine.Object.DestroyImmediate(etiquetaVehiculo.transform.parent.gameObject);
            UnityEngine.Object.DestroyImmediate(etiquetaVega.transform.parent.gameObject);

            TestLog.Phase("FASE 9 FINALIZADA (16/23)");

            // --- #17 / C2: circulos para unidades, cuadrados para interactuables ---
            TestLog.Phase("FASE 9 - Tarea #17: circulos para unidades, cuadrados para interactuables");
            int obstaculosRegistradosC2 = MinimapIcon.RegistrarObstaculos(MinimapIcon.ObstacleMinimapColor);

            var todosLosIconosC2 = UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include);
            int cuadrados = 0, circulosDeObstaculo = 0, circulosDeUnidad = 0;
            foreach (var ic in todosLosIconosC2)
            {
                bool esObstaculo = ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null;
                if (esObstaculo)
                {
                    if (ic.EsCuadrado) cuadrados++; else circulosDeObstaculo++;
                }
                else if (!ic.EsCuadrado) circulosDeUnidad++;
            }
            Check($"Los {obstaculosRegistradosC2} obstaculos (interactuables) tienen icono CUADRADO ({cuadrados} cuadrados, {circulosDeObstaculo} circulos entre ellos)",
                cuadrados == obstaculosRegistradosC2 && circulosDeObstaculo == 0);
            Check($"Y las unidades (soldados, vehiculo) siguen con icono CIRCULAR, la forma distingue la categoria ({circulosDeUnidad} circulos de unidad)",
                circulosDeUnidad == todosLosIconosC2.Length - obstaculosRegistradosC2);

            TestLog.Phase("FASE 9 FINALIZADA (17/23)");

            // --- #18 / C3: marca de montable y orden de ir a montar ---
            TestLog.Phase("FASE 9 - Tarea #18: marca de montable y orden de ir a montar");
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }
            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);

            var rellenoC3 = new List<Soldier>();
            for (int i = 0; i < vehicle.Capacity; i++)
            {
                var s = SpawnSoldier(soldierPrefab, $"RellenoC3_{i}", TeamId.Player, RoleType.Assault,
                    vehicle.transform.position, new Color(0.25f, 0.55f, 0.98f), pool, 100);
                vehicle.Mount(s);
                rellenoC3.Add(s);
            }
            Check($"El vehiculo queda lleno ({vehicle.OccupantCount}/{vehicle.Capacity})", !vehicle.HasAnyRoom);

            inputDriver.Selection.SelectSingle(vega);
            var metodoIndicadorRts = GetRequiredMethod(typeof(PlayerInputDriver), "UpdateVehicleMountIndicatorRts",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var campoMountIndicator = GetRequiredField(typeof(PlayerInputDriver), "mountIndicator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            metodoIndicadorRts.Invoke(inputDriver, new object[] { new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = vehicle.transform.position } });
            var indicadorC3 = (VehicleMountIndicator)campoMountIndicator.GetValue(inputDriver);
            Check($"Con el vehiculo lleno, la marca dice IMPOSIBLE (puede={indicadorC3.UltimoPuedeMontar})",
                indicadorC3.UltimoPuedeMontar == false);

            vehicle.Dismount(rellenoC3[0]);
            metodoIndicadorRts.Invoke(inputDriver, new object[] { new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = vehicle.transform.position } });
            Check($"Con lugar libre, la marca dice que SI se puede (puede={indicadorC3.UltimoPuedeMontar})",
                indicadorC3.UltimoPuedeMontar == true);

            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);
            foreach (var s in rellenoC3) if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);

            vehicle.transform.position = new Vector3(60f, 0.6f, 60f);
            doc.transform.position = vehicle.transform.position + new Vector3(-8f, 0f, 0f);
            SP.Core.ApoyoEnElPiso.Apoyar(doc.transform);
            OrderService.IssueMountOrder(doc, vehicle);
            bool subioC3 = SimulateUntil(() => vehicle.Occupants.Count > 0, 12f);
            Check($"Con lugar, el soldado camina y sube (RoleOf={vehicle.RoleOf(doc)})",
                subioC3 && vehicle.RoleOf(doc) != null);

            vehicle.Dismount(doc);
            inputDriver.Selection.Clear();

            TestLog.Phase("FASE 9 FINALIZADA (18/23)");

            // --- #19 / G1: el barril se incendia con un disparo y despues explota ---
            TestLog.Phase("FASE 9 - Tarea #19: el barril se incendia con un disparo y despues explota");

            var barrilGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrilGo.name = "BarrilDePrueba";
            barrilGo.transform.position = new Vector3(80f, 0.75f, 80f);
            barrilGo.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var barril = barrilGo.AddComponent<ObstacleMarker>();
            var campoEsExplosivo = GetRequiredField(typeof(ObstacleMarker), "esExplosivo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            campoEsExplosivo.SetValue(barril, true);

            var testigoCerca = SpawnSoldier(soldierPrefab, "TestigoCercaDelBarril", TeamId.Player, RoleType.Assault,
                barrilGo.transform.position + new Vector3(3f, 0f, 0f), new Color(0.25f, 0.55f, 0.98f), pool, 100);
            testigoCerca.Brain.CancelOrder();
            testigoCerca.Brain.IsPossessedByPlayer = true; // no se mueve solo durante la medicion
            SP.Core.ApoyoEnElPiso.Apoyar(testigoCerca.transform);

            var testigoLejos = SpawnSoldier(soldierPrefab, "TestigoLejosDelBarril", TeamId.Player, RoleType.Assault,
                barrilGo.transform.position + new Vector3(12f, 0f, 0f), new Color(0.25f, 0.55f, 0.98f), pool, 100);
            testigoLejos.Brain.CancelOrder();
            testigoLejos.Brain.IsPossessedByPlayer = true;
            SP.Core.ApoyoEnElPiso.Apoyar(testigoLejos.transform);

            int vidaCercaAntes = testigoCerca.Health.Current;
            int vidaLejosAntes = testigoLejos.Health.Current;

            barril.TakeDamage(10);
            Check($"Un disparo enciende el barril y sigue en pie (encendido={barril.EstaEncendido}, colapsado={barril.IsCollapsed})",
                barril.EstaEncendido && !barril.IsCollapsed);

            bool exploto = SimulateUntil(() => barril.IsCollapsed, 6f);
            Check($"A los pocos segundos el barril queda destruido ({exploto})", exploto);
            Check($"El testigo a 3 m del barril perdio vida ({vidaCercaAntes} -> {testigoCerca.Health.Current})",
                testigoCerca.Health.Current < vidaCercaAntes);
            Check($"El testigo a 12 m del barril NO perdio vida ({vidaLejosAntes} -> {testigoLejos.Health.Current})",
                testigoLejos.Health.Current == vidaLejosAntes);

            testigoCerca.Brain.IsPossessedByPlayer = false;
            testigoLejos.Brain.IsPossessedByPlayer = false;
            UnityEngine.Object.DestroyImmediate(testigoCerca.gameObject);
            UnityEngine.Object.DestroyImmediate(testigoLejos.gameObject);
            UnityEngine.Object.DestroyImmediate(barrilGo);

            TestLog.Phase("FASE 9 FINALIZADA (19/23)");

            // --- #20 / G2: Ctrl para agacharse ---
            TestLog.Phase("FASE 9 - Tarea #20: Ctrl para agacharse");

            vega.gameObject.SetActive(true);
            vega.Health.Initialize(vega.Id, vega.Health.MaxHealth);
            vega.Brain.CancelOrder();
            vega.Brain.IsPossessedByPlayer = true; // congela la IA durante la medicion
            vega.Motor.SetCrouching(false); // estado limpio, por si algo lo dejo agachado antes

            // Desde que el soldado tiene rig animado, agacharse ya NO
            // encoge el collider (ver el comentario en SoldierMotor.SetCrouching:
            // escalar el transform en vivo reventaria el rig humanoide, el
            // mismo bug que ArtBuilder.NormalizarEscala documenta para el
            // import). Lo que SI cambia, y es lo que se mide aca, es la
            // altura del ancla de camara en primera persona -- la pose
            // visible de agachado la da enteramente el Animator.
            float ojoDePie = vega.EyeAnchor.localPosition.y;

            vega.Motor.SetCrouching(true);
            float ojoAgachado = vega.EyeAnchor.localPosition.y;
            Check($"Agachado, la camara en primera persona baja ({ojoAgachado:0.00} m < {ojoDePie:0.00} m)",
                ojoAgachado < ojoDePie);

            vega.Motor.SetCrouching(false);
            float ojoFinal = vega.EyeAnchor.localPosition.y;
            Check($"Al soltar Ctrl, la camara vuelve EXACTA a la de pie ({ojoFinal:0.0000} == {ojoDePie:0.0000})",
                Mathf.Abs(ojoFinal - ojoDePie) < 0.0001f);

            // Dispersion: misma racha acumulada (spreadDeg), leida DE PIE y
            // AGACHADO -- SpreadDegEfectivo es el numero real que usa el
            // proximo tiro, sin depender del azar de ApplySpread (Random.Range).
            if (vega.Weapon.CurrentAmmo < 3) { vega.Weapon.Reload(); SimulateSeconds(2f); }
            vega.Weapon.Tick(10f); // decae cualquier racha de una fase anterior a 0
            for (int i = 0; i < 3; i++) { vega.Weapon.Tick(1f); vega.Weapon.TryFire(vega.transform.position, vega.transform.forward); }
            float spreadDePie = vega.Weapon.SpreadDegEfectivo;
            vega.Motor.SetCrouching(true);
            float spreadAgachado = vega.Weapon.SpreadDegEfectivo;
            Check($"Con la misma racha, agachado dispersa menos que de pie ({spreadAgachado:0.00} grados < {spreadDePie:0.00} grados)",
                spreadDePie > 0f && spreadAgachado < spreadDePie);

            vega.Motor.SetCrouching(false);
            vega.Brain.IsPossessedByPlayer = false;
            vega.Brain.CancelOrder();

            TestLog.Phase("FASE 9 FINALIZADA (20/23)");

            // --- #21 / G4: musica de lucha y de estrategia, que cambia sola ---
            TestLog.Phase("FASE 9 - Tarea #21: musica de lucha y de estrategia");

            var camTransform = Camera.main.transform;
            var posOriginalCamara = camTransform.position;
            var rotOriginalCamara = camTransform.rotation;
            // Aislada: bien lejos de cualquier soldado que haya quedado de
            // una fase anterior, para que "sin combate cerca" no dependa
            // de que nadie mas haya quedado atacando por ahi.
            camTransform.position = new Vector3(500f, 20f, 500f);
            camTransform.rotation = Quaternion.identity;

            SimulateSeconds(2f);
            Check($"Sin combate cerca, la musica de lucha queda en 0 ({MusicDirector.GananciaLucha:0.00})",
                MusicDirector.GananciaLucha < 0.01f);

            var testigoLucha = SpawnSoldier(soldierPrefab, "TestigoDeLuchaG4", TeamId.Enemy, RoleType.Enemy,
                camTransform.position + camTransform.forward * 10f, enemyColor, pool, 100);
            var metodoSetState = GetRequiredMethod(typeof(AiBrain), "SetState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            metodoSetState.Invoke(testigoLucha.Brain, new object[] { AiState.Attack });
            testigoLucha.Brain.IsPossessedByPlayer = true; // congela el estado forzado: Tick() nunca corre y no lo pisa

            bool subioRapido = SimulateUntil(() => MusicDirector.GananciaLucha > 0.8f, 2f);
            Check($"Con un enemigo atacando a 10 m, la musica de lucha sube por encima de 0,8 en menos de 2 s ({MusicDirector.GananciaLucha:0.00})",
                subioRapido);

            testigoLucha.Health.TakeDamage(9999, -1);
            bool bajoDeNuevo = SimulateUntil(() => MusicDirector.GananciaLucha < 0.05f, 3f);
            Check($"Al morir el enemigo, la musica de lucha vuelve a bajar ({MusicDirector.GananciaLucha:0.00})",
                bajoDeNuevo);

            UnityEngine.Object.DestroyImmediate(testigoLucha.gameObject);
            camTransform.position = posOriginalCamara;
            camTransform.rotation = rotOriginalCamara;

            TestLog.Phase("FASE 9 FINALIZADA (21/23)");

            // --- #23 / H2: la linea roja de ataque no debe verse en FPS ---
            // El usuario reportaba "algo negro que le apunta al enemigo en
            // FPS": era AttackLineManager, una linea fina pensada para
            // verse desde arriba en RTS. De cerca en FPS (alignment View,
            // el default del LineRenderer, encara cada punto DE CARA A LA
            // CAMARA) un extremo casi pegado a la camara proyecta como un
            // triangulo enorme y oscuro. El arreglo: no dibujarla fuera de
            // RTS (CameraRig.Instance.Mode; ya no cam.orthographic, que
            // dejo de distinguir los modos al pasar RTS a perspectiva).
            TestLog.Phase("FASE 9 - Tarea #23 / Bug H2: la linea de ataque no se dibuja en FPS");

            doc.gameObject.SetActive(true);
            doc.Health.Initialize(doc.Id, doc.Health.MaxHealth);
            doc.Brain.CancelOrder();
            doc.Brain.IsPossessedByPlayer = false;

            var testigoH2 = SpawnSoldier(soldierPrefab, "TestigoDeAtaqueH2", TeamId.Enemy, RoleType.Enemy,
                doc.transform.position + new Vector3(4f, 0f, 0f), enemyColor, pool, 100);

            var campoTargetH2 = GetRequiredField(typeof(AiBrain), "target",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var metodoSetStateH2 = GetRequiredMethod(typeof(AiBrain), "SetState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            campoTargetH2.SetValue(doc.Brain, testigoH2);
            metodoSetStateH2.Invoke(doc.Brain, new object[] { AiState.Attack });

            var attackLineManager = UnityEngine.Object.FindAnyObjectByType<AttackLineManager>();
            var metodoUpdateAttackLine = GetRequiredMethod(typeof(AttackLineManager), "Update",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // CameraRig.Instance, no inputDriver.Rig -- mismo motivo que
            // en el bloque C1 de arriba: AttackLineManager consulta el
            // singleton real, y este harness en Edit Mode nunca le manda
            // OnEnable (ver el comentario junto a EnsureInstanceForTests).
            CameraRig.EnsureInstanceForTests(inputDriver.Rig);
            var rigH2 = CameraRig.Instance;
            if (rigH2 == null) { Check("CameraRig.Instance disponible para el test H2", false); return; }
            var modoOriginalH2 = rigH2.Mode;

            rigH2.SetMode(ControlMode.Rts); // control -- la linea tiene que seguir existiendo aca
            metodoUpdateAttackLine.Invoke(attackLineManager, null);
            bool lineaEnRts = GameObject.Find("AttackLine") != null;
            Check($"Control: en RTS la linea de ataque SI se dibuja ({lineaEnRts})", lineaEnRts);

            rigH2.SetMode(ControlMode.Fps); // H2: no tiene que existir
            metodoUpdateAttackLine.Invoke(attackLineManager, null);
            bool lineaEnFps = GameObject.Find("AttackLine") != null;
            Check($"H2: en FPS la linea de ataque NO se dibuja ({lineaEnFps})", !lineaEnFps);

            rigH2.SetMode(modoOriginalH2);
            campoTargetH2.SetValue(doc.Brain, null);
            doc.Brain.CancelOrder();
            UnityEngine.Object.DestroyImmediate(testigoH2.gameObject);

            TestLog.Phase("FASE 9 FINALIZADA (23/23)");

            // --- Bug reportado: mantener click derecho paneaba la camara
            // sola (bastaba mover la mano al terminar de apuntar una orden,
            // sin querer arrastrar nada), y no se confiaba en que el mapa
            // real tuviera un limite. Antes: 3 sitios llamaban a Rig.Pan()
            // en PlayerInputDriver, uno de ellos disparado por
            // mouse.rightButton mas ArrastreDerecho. Ahora: 2 sitios, los
            // dos solo por WASD (Assets/_Project/Scripts/Player/
            // PlayerInputDriver.cs). El limite en si (AcotarAlMapa) no se
            // toco -- ya usaba el area real de NavService -- pero nunca
            // tenia un Check() que lo pusiera a prueba con numeros.
            TestLog.Phase("FASE 10 - Bug reportado: click derecho ya no panea, limites de camara reales");

            bool sinCampoDeArrastre = typeof(PlayerInputDriver).GetField("arrastreDerecho",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null;
            bool sinMultiplicadorDeArrastre = typeof(PlayerInputDriver).GetField("rightDragPanMultiplier",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null;
            Check("PlayerInputDriver ya no tiene el campo arrastreDerecho (paneo por click derecho eliminado)",
                sinCampoDeArrastre);
            Check("PlayerInputDriver ya no tiene rightDragPanMultiplier (no queda perilla de un paneo que no existe)",
                sinMultiplicadorDeArrastre);

            bool hayLimites = SP.Core.NavService.TryArea(out var limitesMapa);
            Check($"NavService calculo un area real para acotar la camara ({limitesMapa.min} .. {limitesMapa.max})",
                hayLimites);

            var rig = inputDriver.Rig;
            if (rig != null && hayLimites)
            {
                var campoPanTarget = GetRequiredField(typeof(CameraRig), "panTarget",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Empuja el paneo muchisimo mas alla del mapa en las 4
                // direcciones: si AcotarAlMapa no clampeara, panTarget
                // terminaria bien afuera del area real medida arriba.
                rig.Pan(Vector3.right * 100000f);
                float esteX = ((Vector3)campoPanTarget.GetValue(rig)).x;
                rig.Pan(Vector3.left * 200000f);
                float oesteX = ((Vector3)campoPanTarget.GetValue(rig)).x;
                rig.Pan(Vector3.right * 100000f + Vector3.forward * 100000f);
                float norteZ = ((Vector3)campoPanTarget.GetValue(rig)).z;
                rig.Pan(Vector3.back * 200000f);
                float surZ = ((Vector3)campoPanTarget.GetValue(rig)).z;

                Check($"Paneo al Este se clampea dentro del mapa real ({esteX:0.0} <= {limitesMapa.max.x:0.0})",
                    esteX <= limitesMapa.max.x + 0.01f);
                Check($"Paneo al Oeste se clampea dentro del mapa real ({oesteX:0.0} >= {limitesMapa.min.x:0.0})",
                    oesteX >= limitesMapa.min.x - 0.01f);
                Check($"Paneo al Norte se clampea dentro del mapa real ({norteZ:0.0} <= {limitesMapa.max.z:0.0})",
                    norteZ <= limitesMapa.max.z + 0.01f);
                Check($"Paneo al Sur se clampea dentro del mapa real ({surZ:0.0} >= {limitesMapa.min.z:0.0})",
                    surZ >= limitesMapa.min.z - 0.01f);
            }

            TestLog.Phase("FASE 10 FINALIZADA");

            // -----------------------------------------------------------
            // FASE 11 - pedido del usuario: balanceo de daño, autocuracion,
            // agache de la IA en combate y escucha de disparos aliados.
            // -----------------------------------------------------------
            TestLog.Phase("FASE 11 - Tarea: balanceo de daño (dps sostenido)");

            // dps SOSTENIDO: cargador entero mas la recarga, no el tiro
            // suelto -- es el numero que de verdad importa en un tiroteo
            // largo, y el que estaba invertido antes del ajuste (pistola
            // por encima de rifle y de pesada).
            float DpsSostenido(WeaponKind k)
            {
                var s = WeaponCatalog.Get(k);
                float cicloTotal = s.MagazineSize * s.Cooldown + s.ReloadDuration;
                return (s.MagazineSize * s.Damage) / cicloTotal;
            }
            float dpsPistola = DpsSostenido(WeaponKind.Pistol);
            float dpsRifle = DpsSostenido(WeaponKind.Rifle);
            float dpsPesada = DpsSostenido(WeaponKind.Heavy);
            Check($"El rifle queda como el de mayor dps sostenido (rifle {dpsRifle:0.0} > pesada {dpsPesada:0.0} > pistola {dpsPistola:0.0})",
                dpsRifle > dpsPesada && dpsPesada > dpsPistola);

            TestLog.Phase("FASE 11 - Tarea: autocuracion 3 s despues del ultimo golpe");

            vega.gameObject.SetActive(true);
            vega.Brain.CancelOrder();
            vega.Brain.IsPossessedByPlayer = true; // congela la IA, no la regeneracion
            vega.Motor.SetCrouching(false);
            vega.Health.Initialize(vega.Id, 100);
            vega.Health.TakeDamage(40, -1);
            Check($"Recien golpeado (100 -> {vega.Health.Current}), todavia no regenera ({vega.Health.IsRegenerating})",
                vega.Health.Current == 60 && !vega.Health.IsRegenerating);

            SimulateSeconds(2f);
            Check($"A los 2 s de gracia AUN no subio ({vega.Health.Current} == 60, regenerando={vega.Health.IsRegenerating})",
                vega.Health.Current == 60 && !vega.Health.IsRegenerating);

            SimulateSeconds(1.5f); // total 3.5 s: ya cruzo el umbral de 3 s
            Check($"Pasados los 3 s, esta regenerando y ya subio de 60 ({vega.Health.Current} > 60, regenerando={vega.Health.IsRegenerating})",
                vega.Health.Current > 60 && vega.Health.IsRegenerating);

            int vidaAlRegolpear = vega.Health.Current;
            vega.Health.TakeDamage(5, -1);
            Check($"Un golpe nuevo apaga la regeneracion al instante ({vega.Health.IsRegenerating})",
                !vega.Health.IsRegenerating);
            SimulateSeconds(1f);
            Check($"Y no vuelve a subir antes de esperar los 3 s de nuevo ({vega.Health.Current} <= {vidaAlRegolpear - 5})",
                vega.Health.Current <= vidaAlRegolpear - 5);

            bool llegoAFull = SimulateUntil(() => vega.Health.Current >= vega.Health.MaxHealth, 20f);
            Check($"Dejandolo en paz, termina curandose del todo ({llegoAFull}, {vega.Health.Current}/{vega.Health.MaxHealth})",
                llegoAFull);
            Check("Y al llegar al maximo, la regeneracion se apaga sola", !vega.Health.IsRegenerating);

            vega.Health.Initialize(vega.Id, vega.Health.MaxHealth);
            vega.Brain.IsPossessedByPlayer = false;

            TestLog.Phase("FASE 11 - Tarea: con un obstaculo de por medio, cero disparos");

            var muroDePruebaF11 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            muroDePruebaF11.name = "MuroDePruebaF11";
            muroDePruebaF11.transform.position = new Vector3(10f, 1f, 60f);
            muroDePruebaF11.transform.localScale = new Vector3(4f, 2f, 1f);
            SP.Core.NavService.Invalidate();

            kes.Brain.CancelOrder();
            kes.Brain.IsPossessedByPlayer = false;
            kes.Health.Initialize(kes.Id, kes.Health.MaxHealth);
            kes.transform.position = new Vector3(10f, kes.transform.position.y, 57f);
            SP.Core.ApoyoEnElPiso.Apoyar(kes.transform);
            if (kes.Weapon.CurrentAmmo < kes.Weapon.MagazineSize) { kes.Weapon.Reload(); SimulateSeconds(3f); }

            var blancoBloqueadoF11 = SpawnSoldier(soldierPrefab, "BlancoBloqueadoF11", TeamId.Enemy, RoleType.Enemy,
                new Vector3(10f, 0f, 63f), enemyColor, pool, 100000);
            blancoBloqueadoF11.Brain.enabled = false;
            blancoBloqueadoF11.Brain.IsPossessedByPlayer = true;
            blancoBloqueadoF11.transform.position = new Vector3(blancoBloqueadoF11.transform.position.x, kes.transform.position.y, blancoBloqueadoF11.transform.position.z);
            Physics.SyncTransforms();

            int municionAntesF11 = kes.Weapon.CurrentAmmo;
            float distanciaBloqueoF11 = Vector3.Distance(kes.transform.position, blancoBloqueadoF11.transform.position);
            bool tiroBloqueadoF11 = kes.Brain.TieneLineaDeTiro(blancoBloqueadoF11);
            Check($"Arranca a {distanciaBloqueoF11:0.0} m (dentro del alcance) pero SIN linea de tiro por el muro ({tiroBloqueadoF11})",
                distanciaBloqueoF11 <= 6f && !tiroBloqueadoF11);

            for (int i = 0; i < 20; i++) SimStep(0.05f); // 1 s: alcanza para varias rafagas si el gate fallara

            Check($"En 1 s nunca entra en Attack estando bloqueado ({kes.Brain.State})", kes.Brain.State != AiState.Attack);
            Check($"Y no disparo ni una bala: el cargador sigue igual ({kes.Weapon.CurrentAmmo} == {municionAntesF11})",
                kes.Weapon.CurrentAmmo == municionAntesF11);

            kes.Brain.CancelOrder();
            UnityEngine.Object.DestroyImmediate(blancoBloqueadoF11.gameObject);
            UnityEngine.Object.DestroyImmediate(muroDePruebaF11);
            SP.Core.NavService.Invalidate();

            TestLog.Phase("FASE 11 - Tarea: la IA se agacha mientras dispara desde Attack");

            kes.Brain.CancelOrder();
            kes.Health.Initialize(kes.Id, kes.Health.MaxHealth);
            kes.Motor.SetCrouching(false);
            kes.transform.position = new Vector3(10f, kes.transform.position.y, 40f);
            SP.Core.ApoyoEnElPiso.Apoyar(kes.transform);
            Check($"De pie antes de trabar combate ({kes.Motor.IsCrouching})", !kes.Motor.IsCrouching);

            var blancoAgacheF11 = SpawnSoldier(soldierPrefab, "BlancoParaAgacheF11", TeamId.Enemy, RoleType.Enemy,
                new Vector3(10f, 0f, 44f), enemyColor, pool, 100000);
            blancoAgacheF11.Brain.enabled = false;
            blancoAgacheF11.Brain.IsPossessedByPlayer = true;
            blancoAgacheF11.transform.position = new Vector3(blancoAgacheF11.transform.position.x, kes.transform.position.y, blancoAgacheF11.transform.position.z);
            Physics.SyncTransforms();

            bool entroEnAttackF11 = SimulateUntil(() => kes.Brain.State == AiState.Attack, 5f);
            Check($"kes entra en Attack contra el blanco cercano ({kes.Brain.State})", entroEnAttackF11);
            // El agache vive DENTRO del case Attack de Tick(): en el mismo
            // tick que Chase pasa a Attack, SetState corta con un break
            // antes de llegar a ese case, asi que hace falta UN tick mas
            // (imperceptible a 60 fps reales) para que se aplique.
            if (entroEnAttackF11) SimStep(0.05f);
            Check($"Y se agacha mientras dispara ({kes.Motor.IsCrouching})", entroEnAttackF11 && kes.Motor.IsCrouching);

            blancoAgacheF11.Health.Initialize(blancoAgacheF11.Id, 1);
            blancoAgacheF11.Health.TakeDamage(1, kes.Id);
            SimStep(0.05f);
            Check($"Al morir el objetivo, se para de nuevo en vez de quedar agachado caminando ({kes.Brain.State}, agachado={kes.Motor.IsCrouching})",
                !kes.Motor.IsCrouching);

            kes.Brain.CancelOrder();
            kes.Motor.SetCrouching(false);
            UnityEngine.Object.DestroyImmediate(blancoAgacheF11.gameObject);

            TestLog.Phase("FASE 11 - Tarea: se escucha un disparo enemigo aunque no le pegue a nadie");

            doc.gameObject.SetActive(true);
            doc.Health.Initialize(doc.Id, doc.Health.MaxHealth);
            doc.Brain.CancelOrder();
            doc.Brain.IsPossessedByPlayer = false;
            doc.transform.position = new Vector3(10f, doc.transform.position.y, 90f);
            SP.Core.ApoyoEnElPiso.Apoyar(doc.transform);
            Check($"doc arranca sin combate ({doc.Brain.State})",
                doc.Brain.State == AiState.Patrol || doc.Brain.State == AiState.Idle);

            var tiradorF11 = SpawnSoldier(soldierPrefab, "TiradorLejanoF11", TeamId.Enemy, RoleType.Enemy,
                new Vector3(10f, 0f, 110f), enemyColor, pool, 100000);
            tiradorF11.Brain.enabled = false;
            tiradorF11.Brain.IsPossessedByPlayer = true;

            float distanciaOidoF11 = Vector3.Distance(doc.transform.position, tiradorF11.transform.position);
            Check($"El tirador esta fuera de la vision normal (10 m) pero dentro del oido ({distanciaOidoF11:0.0} m < 30 m)",
                distanciaOidoF11 > 10f && distanciaOidoF11 < 30f);

            SimStep(0.05f);
            Check($"Sin que nadie haya disparado, doc no reacciona ({doc.Brain.State})", doc.Brain.State != AiState.Chase);

            SP.Core.EventBus.Instance.Publish(new SP.Core.ShotFiredEvent(tiradorF11.Id));
            SimStep(0.05f);
            Check($"Un solo tiro sin pegarle a nadie alcanza para que doc vaya a investigar ({doc.Brain.State}, target={doc.Brain.CurrentTarget?.DisplayName})",
                doc.Brain.State == AiState.Chase && doc.Brain.CurrentTarget == tiradorF11);

            doc.Brain.CancelOrder();
            UnityEngine.Object.DestroyImmediate(tiradorF11.gameObject);

            TestLog.Phase("FASE 11 FINALIZADA");

            // FASE 12 - pedido del usuario: "no quiero que se desplacen [al
            // caminar], solo que hagan la animacion". Reproduce el ciclo de
            // "caminar" a mano (Animator.Update, sin Play mode) y mide la
            // cadera real del rig -- no transform.position, que nunca se
            // entera de un desplazamiento horneado DENTRO de la pose (ver
            // el comentario de ArtSetup.ConfigurarAnimaciones). Si algun
            // dia vuelve a arrastrar, este test lo mide en metros y no deja
            // que se cuele de nuevo sin que la suite lo note.
            TestLog.Phase("FASE 12 - Tarea: caminar no desplaza la geometria (root motion)");

            var caminanteF12 = SpawnSoldier(soldierPrefab, "CaminanteF12", TeamId.Player, RoleType.Assault,
                new Vector3(20f, 0f, 20f), Color.white, pool, 100);
            caminanteF12.Brain.enabled = false;
            caminanteF12.Brain.IsPossessedByPlayer = true;

            var animatorF12 = caminanteF12.GetComponentInChildren<Animator>(true);
            if (animatorF12 == null)
            {
                TestLog.Step("Sin Animator en el prefab (arte todavia no importado): se saltea FASE 12.");
            }
            else
            {
                var caderaF12 = animatorF12.GetBoneTransform(HumanBodyBones.Hips);
                Check("El rig humanoide expone el hueso de cadera para medir deriva", caderaF12 != null);

                if (caderaF12 != null)
                {
                    Vector3 raizAntesF12 = caminanteF12.transform.position;
                    Vector3 caderaInicialF12 = caderaF12.position;

                    animatorF12.applyRootMotion = false;
                    animatorF12.SetFloat(SP.Presentation.SoldierAnimatorDriver.ParamVelocidad, 1f);
                    animatorF12.SetFloat(SP.Presentation.SoldierAnimatorDriver.ParamAdelante, 1f);
                    animatorF12.SetFloat(SP.Presentation.SoldierAnimatorDriver.ParamLateral, 0f);
                    animatorF12.SetBool(SP.Presentation.SoldierAnimatorDriver.ParamAgachado, false);
                    animatorF12.Update(0f); // aplica los parametros antes de arrancar a medir

                    float pasoMaximoF12 = 0f;
                    Vector3 caderaPreviaF12 = caderaF12.position;
                    for (int i = 0; i < 90; i++) // 3 s a 30 pasos/seg: un par de ciclos completos de "walking"
                    {
                        animatorF12.Update(1f / 30f);
                        float pasoF12 = Vector3.Distance(
                            new Vector3(caderaF12.position.x, 0f, caderaF12.position.z),
                            new Vector3(caderaPreviaF12.x, 0f, caderaPreviaF12.z));
                        // Un paso de bamboleo normal a 30 fps no salta varios
                        // centimetros de golpe; un root motion de "caminar"
                        // sin anular (1-2 m/s reales) se nota enseguida
                        // contra este limite.
                        pasoMaximoF12 = Mathf.Max(pasoMaximoF12, pasoF12);
                        caderaPreviaF12 = caderaF12.position;
                    }

                    float derivaTotalF12 = Vector3.Distance(
                        new Vector3(caderaF12.position.x, 0f, caderaF12.position.z),
                        new Vector3(caderaInicialF12.x, 0f, caderaInicialF12.z));

                    Check($"El transform del soldado no se movio solo ({Vector3.Distance(caminanteF12.transform.position, raizAntesF12):0.000} m)",
                        Vector3.Distance(caminanteF12.transform.position, raizAntesF12) < 0.001f);
                    Check($"La cadera no salta de golpe cuadro a cuadro (maximo {pasoMaximoF12:0.000} m/frame < 0.15 m/frame)",
                        pasoMaximoF12 < 0.15f);
                    Check($"Tras 3 s 'caminando', la cadera no derivo del lugar mas de 20 cm ({derivaTotalF12:0.000} m)",
                        derivaTotalF12 < 0.2f);
                }
            }

            UnityEngine.Object.DestroyImmediate(caminanteF12.gameObject);
            TestLog.Phase("FASE 12 FINALIZADA");
        }
    }
}
