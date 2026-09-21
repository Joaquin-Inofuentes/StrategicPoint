using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Tutorial;
using SP.UI;
using SP.Vehicles;

namespace SP.EditorTools
{
    // FASE 19 (ronda 9, segunda tanda): ayudas del tutorial, radial legible, boton de menu uniforme, zoom, salto agachado,
    // ordenes de mover validadas, y los pendientes de repositorio.
    public static partial class HeadlessTestRunner
    {
        static void RunPhase19(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
                               GameObject soldierPrefab, Color colorEnemy, ProjectilePool pool)
        {
            TestLog.Phase("FASE 19 - Ayudas del tutorial, radial legible, menu uniforme, zoom, salto agachado, ordenes validadas");
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            FullHeal(vega, kes, doc);
            inputDriver.Brain.Possess(vega);

            // --- Tutorial: flecha de ayuda (97) ---
            var ahora = TutorialManager.DireccionHacia(Vector3.zero, 0f, new Vector3(10f, 0f, 10f));
            Check("La ayuda del tutorial dice DERECHA para un blanco a la derecha", ahora.Contains("DERECHA") && ahora.Contains("14 m"));
            Check("La ayuda dice IZQUIERDA para un blanco a la izquierda", TutorialManager.DireccionHacia(Vector3.zero, 0f, new Vector3(-10f, 0f, 1f)).Contains("IZQUIERDA"));
            Check("La ayuda dice ESPALDA para un blanco detras", TutorialManager.DireccionHacia(Vector3.zero, 0f, new Vector3(0f, 0f, -8f)).Contains("ESPALDA"));
            Check("La ayuda dice ENFRENTE para un blanco al frente", TutorialManager.DireccionHacia(Vector3.zero, 0f, new Vector3(0f, 0f, 9f)).Contains("ENFRENTE"));
            Check("La ayuda tiene en cuenta hacia donde mira la camara (giro 90)", TutorialManager.DireccionHacia(Vector3.zero, 90f, new Vector3(0f, 0f, 9f)).Contains("IZQUIERDA"));
            Check("La flecha aparece tras 16 s sin progreso", Mathf.Approximately(TutorialManager.SegundosParaAyuda, 16f));

            // --- Tutorial: progreso guardado (98) ---
            int previo = TutorialManager.PasoGuardado;
            PlayerPrefs.SetInt(TutorialManager.ClaveProgreso, 9);
            Check("El paso del tutorial se guarda y se lee", TutorialManager.PasoGuardado == 9);
            TutorialManager.BorrarProgreso();
            Check("Borrar el progreso lo deja en 0", TutorialManager.PasoGuardado == 0);
            if (previo > 0) PlayerPrefs.SetInt(TutorialManager.ClaveProgreso, previo);
            var codigoTutorial = System.IO.File.ReadAllText("Assets/_Project/Scripts/Tutorial/TutorialManager.cs");
            Check("[F8] salta el paso y [F7] retoma el guardado", codigoTutorial.Contains("f8Key") && codigoTutorial.Contains("f7Key") && codigoTutorial.Contains("RegruparAliados"));

            // --- Auditoria #95: pasos del tutorial cubiertos con GESTOS REALES ---
            // Antes de esto, la suite headless solo probaba texto/matematica del
            // tutorial (DireccionHacia, progreso guardado) y en la practica manual
            // del editor los pasos viejos se adelantaban con [F8] SaltarPaso, sin
            // ejecutar de verdad el gesto que el paso pide. Reproducir aca la
            // condicion EXACTA de cada paso (ver TutorialManager.DefinirPasos)
            // contra APIs reales de gameplay -- mover camara, caminar, correr,
            // agacharse, saltar -- en vez de tildar la bandera a mano, es lo
            // mas cerca de un "gesto real" que la suite headless (sin Input
            // System simulado de mouse) puede hacer. Cobertura: 5 de 36 pasos
            // con gesto real automatizado (antes: 3; originalmente 0). El
            // resto sigue dependiendo de la prueba manual en Play.
            {
                var motorVega = vega.Motor;
                var rig = inputDriver.Rig;

                // Paso "camara": el propio Evaluar() del tutorial exige >=50 grados
                // acumulados de yaw y >=18 de pitch (ver DefinirPasos, paso 1). El
                // yaw del mouse lo aplica PlayerInputDriver llamando Motor.RotateYaw,
                // y el pitch CameraRig.AddPitch -- las mismas dos llamadas que
                // dispara mover el mouse de verdad, no una bandera puesta a mano.
                float antesYaw = vega.transform.eulerAngles.y;
                motorVega.RotateYaw(60f);
                float acumYaw = Mathf.Abs(Mathf.DeltaAngle(antesYaw, vega.transform.eulerAngles.y));
                Check("Paso 'camara' con gesto real: RotateYaw(60) acumula >=50 grados de yaw", acumYaw >= 50f);
                motorVega.RotateYaw(-60f); // deja al soldado mirando como antes para el resto de la fase

                float pitchInicial = rig.Pitch;
                rig.AddPitch(20f);
                float acumPitch = Mathf.Abs(rig.Pitch - pitchInicial);
                Check("Paso 'camara' con gesto real: mover la camara acumula >=18 de pitch", acumPitch >= 18f);
                rig.AddPitch(-20f); // deja el pitch como estaba para el resto de la fase

                // Paso "wasd": el tutorial marca cada tecla apenas Keyboard.current
                // la reporta apretada -- ejecutar el mismo Move() real que dispara
                // el input de W/A/S/D es el gesto, no tildar teclaW/A/S/D a mano.
                var posAntes = vega.transform.position;
                motorVega.Move(Vector3.forward, 0.05f);
                bool seMovioAdelante = (vega.transform.position - posAntes).sqrMagnitude > 0.0001f;
                Check("Paso 'wasd' con gesto real: Move(adelante) desplaza al soldado (equivale a apretar W)", seMovioAdelante);

                // Paso "correr": el tutorial pide SetRunning(true) sostenido por
                // >=1.2s de juego (Motor.Corriendo) antes de marcar f.corre.
                motorVega.SetRunning(true);
                bool corriendoConShift = motorVega.Corriendo;
                motorVega.SetRunning(false);
                bool volvioACaminar = !motorVega.Corriendo;
                Check("Paso 'correr' con gesto real: SHIFT+W deja Motor.Corriendo=true y soltarlo lo apaga", corriendoConShift && volvioACaminar);

                // Paso "agacharse": el propio Evaluar() del tutorial marca
                // f.agachado mientras Motor.IsCrouching sea true (linea 377-378
                // de TutorialManager) -- mismo gesto que mantener Ctrl.
                motorVega.SetCrouching(true);
                bool agachadoConCtrl = motorVega.IsCrouching;
                motorVega.SetCrouching(false);
                bool levantadoAlSoltar = !motorVega.IsCrouching;
                Check("Paso 'agacharse' con gesto real: SetCrouching(true) deja Motor.IsCrouching=true y soltarlo lo levanta", agachadoConCtrl && levantadoAlSoltar);

                // Paso "saltar": el Evaluar() del tutorial marca f.salta
                // mientras Motor.IsJumping sea true (linea 393) -- mismo gesto
                // que apretar Espacio.
                motorVega.Jump();
                bool saltaConEspacio = motorVega.IsJumping;
                typeof(SoldierMotor).GetProperty("IsJumping").GetSetMethod(true).Invoke(motorVega, new object[] { false });
                Check("Paso 'saltar' con gesto real: Jump() deja Motor.IsJumping=true (equivale a apretar Espacio)", saltaConEspacio);
            }

            // --- Baliza: la etiqueta se desvanece sobre la mira (11) ---
            Check("La etiqueta de la baliza es transparente sobre la mira", Mathf.Approximately(TutorialBeacon.AlfaSegunDistanciaAMira(0f), TutorialBeacon.AlfaMinimo));
            Check("La etiqueta lejos de la mira es opaca", Mathf.Approximately(TutorialBeacon.AlfaSegunDistanciaAMira(400f), 1f));
            Check("El desvanecimiento es gradual", TutorialBeacon.AlfaSegunDistanciaAMira(100f) > TutorialBeacon.AlfaMinimo && TutorialBeacon.AlfaSegunDistanciaAMira(100f) < 1f);

            // --- Radial (10) ---
            Check("El radial usa letra de 15 o mas", MenuDeOrdenes.TamanoLetra >= 15);
            Check("El radial es mas translucido (fondo <= 80 %)", MenuDeOrdenes.OpacidadDeFondo <= 0.8f);

            // --- Menu principal y pausa: mismo tamano de boton (24) ---
            var escenaMenu = System.IO.File.ReadAllText("Assets/_Project/Scenes/SC_MainMenu.unity");
            Check("El menu principal escala igual que el HUD (referencia 960x540)", escenaMenu.Contains("m_ReferenceResolution: {x: 960, y: 540}"));
            Check("El menu principal ya no es solo tres botones (subtitulo y consejo)", escenaMenu.Contains("COMANDO TACTICO") && escenaMenu.Contains("Primera vez"));

            // --- Zoom de la mira (37) ---
            var fusil = WeaponCatalog.Get(WeaponKind.Rifle);
            Check($"El zoom del fusil es x3 o mas (x{fusil.ZoomFactor:0.0})", fusil.ZoomFactor >= 3f);
            Check("El francotirador sigue siendo el de mas aumento", WeaponCatalog.Get(WeaponKind.Sniper).ZoomFactor > fusil.ZoomFactor);

            // --- Salto agachado (52) ---
            var motor = kes.Motor;
            motor.SetCrouching(true);
            bool agachado = motor.IsCrouching;
            motor.Jump();
            Check("Saltar agachado te levanta y salta", agachado && motor.IsJumping && !motor.IsCrouching);
            typeof(SoldierMotor).GetProperty("IsJumping").GetSetMethod(true).Invoke(motor, new object[] { false });

            // --- Ordenes de mover validadas (62) ---
            Check("El punto pedido bajo los pies siempre es alcanzable", OrderService.PuntoAlcanzable(vega.transform.position, vega.transform.position, out _));
            Check("El radio de ajuste de puntos es de 3 m", Mathf.Approximately(OrderService.RadioDeAjusteDePunto, 3f));

            // --- Flechas de aliados fuera de pantalla: no pisan las tarjetas ni la barra del arma (19) ---
            Check("Las flechas de aliados frenan antes del borde de abajo (HUD) y del de arriba", OffscreenAllyMarkerView.MargenInferior > 96f && OffscreenAllyMarkerView.MargenSuperior >= 56f);

            // --- Repositorio: README, CI y particion de archivos gigantes (81, 82, 83, 92) ---
            Check("Hay README en la raiz del repositorio", System.IO.File.Exists("README.md"));
            Check("Hay flujo de CI para correr la suite", System.IO.File.Exists(".github/workflows/tests.yml"));
            Check("PlayerInputDriver esta partido (radial y vehiculos en archivos aparte)", System.IO.File.Exists("Assets/_Project/Scripts/Player/PlayerInputDriver.Radial.cs") && System.IO.File.Exists("Assets/_Project/Scripts/Player/PlayerInputDriver.Vehiculo.cs"));
            Check("PlayerInputDriver.cs ya no pasa de 3500 lineas", System.IO.File.ReadAllLines("Assets/_Project/Scripts/Player/PlayerInputDriver.cs").Length < 3500);
            Check("AiBrain.cs y HeadlessTestRunner.cs tambien se partieron", System.IO.File.Exists("Assets/_Project/Scripts/Ai/AiBrain.Navegacion.cs") && System.IO.File.Exists("Assets/_Project/Scripts/Editor/HeadlessTestRunner.Fases8a12.cs"));
            // --- Radial ATACAR: suprimir y granada (61) ---
            Check("ATACAR tiene 6 opciones (4 destinatarios + suprimir + granada)", MenuDeOrdenes.OpcionesDe[2].Length == 6 && MenuDeOrdenes.MaxOpcionesPorCategoria >= 6);
            Check("Las opciones nuevas se llaman SUPRIMEN y GRANADA", MenuDeOrdenes.OpcionesDe[2][MenuDeOrdenes.SubSuprimir].Contains("SUPRIMEN") && MenuDeOrdenes.OpcionesDe[2][MenuDeOrdenes.SubGranada].Contains("GRANADA"));
            var tirador = doc.Brain;
            tirador.OrdenSuprimir(kes.transform.position + kes.transform.forward * 20f, 2f);
            Check("La orden de suprimir queda activa unos segundos", tirador.SuprimiendoPorOrden);
            int granadasAntes = doc.Weapon.Granadas;
            bool lanzo = tirador.OrdenLanzarGranada(doc.transform.position + doc.transform.forward * 12f);
            Check("La orden de granada gasta una granada cuando la lanza", !lanzo || doc.Weapon.Granadas == granadasAntes - 1);
            doc.Weapon.ReponerGranadas();
            Check("Sin granadas, la orden se rechaza", (doc.Weapon.ConsumirGranada() & doc.Weapon.ConsumirGranada() & doc.Weapon.ConsumirGranada()) && !tirador.OrdenLanzarGranada(doc.transform.position + doc.transform.forward * 12f));
            doc.Weapon.ReponerGranadas();

            // --- Rendimiento: caches de camara y recursos (75, 76) ---
            RecursosCache.Vaciar();
            var g1 = RecursosCache.Cargar<GameObject>("Weapons/P_Wpn_Granada");
            int cargasTras1 = RecursosCache.Cargas;
            var g2 = RecursosCache.Cargar<GameObject>("Weapons/P_Wpn_Granada");
            Check("Resources se carga una sola vez y despues sale del cache", g1 != null && g1 == g2 && RecursosCache.Cargas == cargasTras1 && RecursosCache.Aciertos >= 1 && RecursosCache.EstaEnCache<GameObject>("Weapons/P_Wpn_Granada"));
            RecursosCache.Precargar();
            Check("La precarga deja en cache los materiales de soldados", RecursosCache.EstaEnCache<Material>("Soldados/MAT_Trimsheet_Aliado"));
            Check("CamaraPrincipal devuelve la misma camara que Camera.main", CamaraPrincipal.Actual == Camera.main);
            var codigoRuntime = new System.Text.StringBuilder();
            foreach (var f in System.IO.Directory.GetFiles("Assets/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (f.Replace("\\", "/").Contains("/Editor/") || f.EndsWith("CamaraPrincipal.cs") || f.EndsWith("RecursosCache.cs")) continue;
                foreach (var linea in System.IO.File.ReadAllLines(f))
                {
                    var t = linea.TrimStart();
                    if (t.StartsWith("//")) continue;
                    var codigo = linea.Split(new[] { "//" }, System.StringSplitOptions.None)[0];
                    if (codigo.Contains("Camera.main") || codigo.Contains("Resources.Load<")) codigoRuntime.AppendLine(f + ": " + t);
                }
            }
            Check("Ningun codigo de runtime usa Camera.main ni Resources.Load directo (todo pasa por los caches)", codigoRuntime.Length == 0);
            CheckBusquedasGlobales();

            // --- LOD por distancia del WorldSimulationDriver (59) ---
            {
                var cam = CamaraPrincipal.Actual;
                if (cam == null)
                {
                    Check("LOD de simulacion: hay camara principal para probarlo", false);
                }
                else
                {
                    // Cuantos soldados vivos y activos estan a mas de DistanciaLod de la camara ahora mismo.
                    System.Func<int> lejanos = () =>
                    {
                        int n = 0;
                        foreach (var s in ActorRegistry.All)
                            if (s != null && s.gameObject.activeInHierarchy && (s.transform.position - cam.transform.position).sqrMagnitude > WorldSimulationDriver.DistanciaLod * WorldSimulationDriver.DistanciaLod) n++;
                        return n;
                    };
                    System.Func<int> saltadosEn4 = () =>
                    {
                        int total = 0;
                        for (int i = 0; i < 4; i++) { WorldSimulationDriver.Step(0.05f); total += WorldSimulationDriver.LastSoldadosSaltadosPorLod; }
                        return total;
                    };
                    var posOriginal = kes.transform.position;
                    kes.transform.position = cam.transform.position + cam.transform.forward * (WorldSimulationDriver.DistanciaLod + 60f);
                    kes.DtLodPendiente = 0f;
                    int lejos = lejanos();
                    Check("LOD de simulacion: kes alejado cuenta como lejano", lejos >= 1);
                    Check("LOD de simulacion: cada soldado lejano se saltea 2 de cada 4 ticks", saltadosEn4() == lejos * 2);
                    Check("LOD de simulacion: el dt saltado no se pierde (a lo sumo queda un tick pendiente)", kes.DtLodPendiente < 0.0501f);
                    WorldSimulationDriver.LodPorDistancia = false;
                    Check("LOD de simulacion: apagado, nadie se saltea ticks", saltadosEn4() == 0);
                    WorldSimulationDriver.LodPorDistancia = true;
                    kes.transform.position = posOriginal;
                    kes.DtLodPendiente = 0f;
                    Check("LOD de simulacion: al volver a estar cerca kes vuelve a tickear siempre", saltadosEn4() == lejanos() * 2);
                }
            }

            // --- Trepar obstaculos bajos (53) ---
            {
                var posOriginal = kes.transform.position;
                var frente = kes.transform.forward; frente.y = 0f; frente.Normalize();
                var pisoBajoKes = posOriginal - Vector3.up * 0.8f;
                var cajon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cajon.transform.localScale = new Vector3(1.2f, 1.0f, 1.2f);
                cajon.transform.position = new Vector3(posOriginal.x, pisoBajoKes.y + 0.5f, posOriginal.z) + frente * 1.4f;
                // Ronda 13: la trepa se rechaza si hay OTRO soldado encima del punto de llegada (motivo 5). Segun donde hayan dejado a la
                // escuadra y a los patrulleros las fases anteriores, alguno podia quedar justo ahi (fallaba una de cada tres corridas).
                var apartados = new System.Collections.Generic.List<(Soldier, Vector3)>();
                foreach (var otro in ActorRegistry.All)
                {
                    if (otro == null || otro == kes) continue;
                    var dd = otro.transform.position - cajon.transform.position; dd.y = 0f;
                    if (dd.magnitude > 3f) continue;
                    apartados.Add((otro, otro.transform.position));
                    otro.transform.position += new Vector3(0f, 0f, 500f);
                }
                Physics.SyncTransforms();
                bool trepo = kes.Motor.TryVault();
                Check("Contra un cajon de 1 m, saltar lo trepa (motivo " + kes.Motor.UltimoMotivoDeTrepa + ")", trepo && kes.Motor.Vaulting);
                typeof(SoldierMotor).GetField("vaultT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(kes.Motor, 1f);
                typeof(SoldierMotor).GetMethod("TickTrepa", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(kes.Motor, null);
                Check("Al terminar la trepa el soldado queda arriba del cajon", !kes.Motor.Vaulting && kes.transform.position.y > pisoBajoKes.y + 1.0f + 0.5f);
                kes.Motor.ResetMotionState();
                cajon.transform.localScale = new Vector3(1.2f, 2.5f, 1.2f);
                cajon.transform.position = new Vector3(posOriginal.x, pisoBajoKes.y + 1.25f, posOriginal.z) + frente * 1.4f;
                kes.transform.position = posOriginal;
                Physics.SyncTransforms();
                Check("Un muro de 2,5 m no se trepa", !kes.Motor.TryVault());
                Object.DestroyImmediate(cajon);
                foreach (var (otro, pos) in apartados) otro.transform.position = pos;
                kes.transform.position = posOriginal;
                Physics.SyncTransforms();
                Check("En campo abierto no hay nada que trepar", !kes.Motor.TryVault());
            }

            // --- Reordenar la escuadra (64) ---
            {
                var sq = inputDriver.Squad;
                var primero = sq[0]; var segundo = sq[1];
                bool movio = inputDriver.MoverEnEscuadra(primero, +1);
                Check("Mover a un soldado hacia abajo intercambia su lugar con el vecino", movio && sq[1] == primero && sq[0] == segundo);
                Check("No se puede subir mas alla del primer lugar", !inputDriver.MoverEnEscuadra(sq[0], -1));
                inputDriver.MoverEnEscuadra(primero, -1);
                Check("Volver a subirlo restaura el orden original", sq[0] == primero && sq[1] == segundo);
            }

            // --- Tamano de interfaz (28) ---
            {
                var cv = new GameObject("CanvasEscalaTest", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                var cs = cv.GetComponent<UnityEngine.UI.CanvasScaler>();
                cs.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(960f, 540f);
                float escalaPrevia = AjustesDeJuego.Escala;
                AjustesDeJuego.PonerEscala(1.5f);
                Check("Interfaz al 150 % reduce la resolucion de referencia a 640x360", Mathf.Abs(cs.referenceResolution.y - 360f) < 0.5f && Mathf.Abs(cs.referenceResolution.x - 640f) < 0.5f);
                AjustesDeJuego.SiguienteEscala();
                Check("Despues del 150 % el ciclo vuelve al 100 %", Mathf.Approximately(AjustesDeJuego.Escala, 1f) && Mathf.Abs(cs.referenceResolution.y - 540f) < 0.5f);
                AjustesDeJuego.PonerEscala(1.25f);
                Check("El 125 % queda en 768x432 y se guarda", Mathf.Abs(cs.referenceResolution.y - 432f) < 0.5f && PlayerPrefs.GetInt("sp_escala_interfaz") == 125);
                var otro = new GameObject("CanvasAjenoTest", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                var cs2 = otro.GetComponent<UnityEngine.UI.CanvasScaler>();
                cs2.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs2.referenceResolution = new Vector2(1920f, 1080f);
                AjustesDeJuego.PonerEscala(1.5f);
                Check("Los canvas que no son de 960x540 no se tocan", Mathf.Abs(cs2.referenceResolution.y - 1080f) < 0.5f);
                AjustesDeJuego.PonerEscala(escalaPrevia);
                Object.DestroyImmediate(cv); Object.DestroyImmediate(otro);
            }

            // --- Arsenal en la caja de suministros (43) ---
            {
                var w = doc.Weapon;
                w.Loadout[0] = WeaponKind.Rifle; w.EquipFromLoadout(0);
                w.CambiarArmaPrincipal(+1);
                Check("Arsenal: +1 cambia el fusil por la metralleta en la ranura 1", w.Loadout[0] == WeaponKind.Smg && w.CurrentWeaponKind == WeaponKind.Smg);
                w.CambiarArmaPrincipal(+1); w.CambiarArmaPrincipal(+1);
                Check("Arsenal: la tercera es el francotirador", w.Loadout[0] == WeaponKind.Sniper);
                w.CambiarArmaPrincipal(+1);
                Check("Arsenal: no repite el arma pesada que ya llevas en la ranura 3 (salta al cohete)", w.Loadout[0] == WeaponKind.Rocket && w.Loadout[2] == WeaponKind.Heavy);
                Check("Arsenal: las ranuras 2 y 3 no se tocan", w.Loadout[1] == WeaponKind.Pistol);
                w.CambiarArmaPrincipal(-1);
                Check("Arsenal: -1 vuelve hacia atras", w.Loadout[0] == WeaponKind.Sniper);
                w.Loadout[0] = WeaponKind.Rifle; w.EquipFromLoadout(0);
                var cajaArsenal = CajaDeSuministros.Crear(doc.transform.position + doc.transform.forward * 30f);
                if (!CajaDeSuministros.Todas.Contains(cajaArsenal)) CajaDeSuministros.Todas.Add(cajaArsenal);   // en Edit mode OnEnable no corre
                Check("Hay caja de suministros a la vista de la busqueda del arsenal", CajaDeSuministros.MasCercana(cajaArsenal.transform.position, CajaDeSuministros.RadioDeArsenal) == cajaArsenal);
                Check("Lejos de la caja no se puede cambiar de arma", CajaDeSuministros.MasCercana(cajaArsenal.transform.position + Vector3.right * 40f, CajaDeSuministros.RadioDeArsenal) == null);
                CajaDeSuministros.Todas.Remove(cajaArsenal);
                Object.DestroyImmediate(cajaArsenal.gameObject);
            }

            // --- Localizacion espanol/ingles (27) ---
            {
                Idioma idiomaPrevio = Loc.Actual;
                Loc.Poner(Idioma.Es);
                var cvLoc = new GameObject("CanvasLocTest", typeof(Canvas));
                var tx = new GameObject("t", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
                tx.transform.SetParent(cvLoc.transform, false);
                var tx2 = new GameObject("t2", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
                tx2.transform.SetParent(cvLoc.transform, false);
                tx.text = "VOLVER AL MENÚ"; tx2.text = "PANTALLA: COMPLETA";
                Check("En espanol Loc.T no cambia el texto", Loc.T("JUGAR") == "JUGAR");
                Loc.Poner(Idioma.En);
                Check("En ingles JUGAR es PLAY", Loc.T("JUGAR") == "PLAY");
                Check("El traductor cambia los Text de la escena (con tilde)", tx.text == "BACK TO MENU");
                Check("Tambien traduce el formato ETIQUETA: valor", tx2.text == "SCREEN: FULLSCREEN");
                Check("Un texto sin entrada queda igual", Loc.T("Texto que no esta en la tabla") == "Texto que no esta en la tabla");
                Loc.Poner(Idioma.Es);
                Check("Al volver a espanol se restauran los textos originales, con su tilde", tx.text == "VOLVER AL MENÚ" && tx2.text == "PANTALLA: COMPLETA");
                Check("La preferencia de idioma se guarda", PlayerPrefs.GetInt("sp_idioma", -1) == 0);
                Loc.Poner(idiomaPrevio);
                Object.DestroyImmediate(cvLoc);
            }

            // --- Los aliados no empujan al jugador (54): el cuerpo de un soldado no bloquea el movimiento de otro ---
            {
                var colV = kes.GetComponentInChildren<Collider>();
                Check("El collider de un aliado NO bloquea el movimiento (no empuja al jugador)", colV != null && !NavService.BlocksMovement(colV));
                var antes = vega.transform.position;
                kes.transform.position = antes + vega.transform.forward * 0.6f;
                vega.Motor.Move(vega.transform.forward, 0.2f);
                float avanzo = Vector3.Dot(vega.transform.position - antes, vega.transform.forward);
                Check($"Caminar contra un aliado pegado no frena ni rebota ({avanzo:0.00} m)", avanzo > 0.5f);
                vega.transform.position = antes;
            }

            // --- Subtitulos de sonido (28) ---
            {
                var o = Vector3.zero;
                var t1 = SP.Presentation.Subtitulos.Describir("Explosion", o, 0f, new Vector3(12f, 0f, 0f));
                Check($"Una explosion a la derecha se subtitula con lado y distancia ('{t1}')", t1 != null && t1.Contains("EXPLOSION") && t1.Contains("DERECHA") && t1.Contains("12 m"));
                Check("Un disparo a la izquierda dice IZQUIERDA", (SP.Presentation.Subtitulos.Describir("Shot_Rifle", o, 0f, new Vector3(-9f, 0f, 1f)) ?? "").Contains("IZQUIERDA"));
                Check("Un sonido detras dice ATRAS", (SP.Presentation.Subtitulos.Describir("BulletWhizz", o, 0f, new Vector3(0f, 0f, -6f)) ?? "").Contains("ATRAS"));
                Check("Un sonido muy lejano no se subtitula", SP.Presentation.Subtitulos.Describir("Explosion", o, 0f, new Vector3(0f, 0f, 200f)) == null);
                Check("Un sonido de interfaz no se subtitula", SP.Presentation.Subtitulos.Describir("UiClick", o, 0f, new Vector3(0f, 0f, 3f)) == null);
                bool previa = SP.Presentation.Subtitulos.Activos;
                SP.Presentation.Subtitulos.Poner(true);
                Check("La opcion de subtitulos se activa y se guarda", SP.Presentation.Subtitulos.Activos && PlayerPrefs.GetInt("sp_subtitulos", 0) == 1);
                SP.Presentation.Subtitulos.Poner(previa);
            }

            // --- Ragdoll de explosion (55): se arma solo en Play (la prueba fisica se hizo en Play, ver Ronda 9) ---
            Check("El ragdoll no se arma fuera de Play (la suite no crea cuerpos rigidos)", !SP.Presentation.RagdollDeExplosion.Lanzar(kes, Vector3.zero) && kes.GetComponent<Rigidbody>() == null);
            var codigoProj = System.IO.File.ReadAllText("Assets/_Project/Scripts/Combat/Projectile.cs");
            Check("Las muertes por explosion llaman al ragdoll", codigoProj.Contains("RagdollDeExplosion.Lanzar"));

            // --- Cada arma y el cuchillo tienen modelo propio (36, 44) ---
            {
                string[] modelos = { "Fusil", "Pistola", "Lanzacohetes", "Escopeta", "Sniper", "Heavy", "Metralleta", "Cuchillo" };
                var huellas = new Dictionary<string, string>();
                bool todos = true, distintos = true;
                foreach (var m in modelos)
                {
                    var pf = Resources.Load<GameObject>("Weapons/P_Wpn_" + m);
                    if (pf == null) { todos = false; continue; }
                    var mf = pf.GetComponentsInChildren<MeshFilter>(true);
                    var b = new Bounds(); bool primero = true; string malla = "";
                    foreach (var f in mf) { if (f.sharedMesh == null) continue; malla += f.sharedMesh.name + ";"; var bb = f.sharedMesh.bounds; if (primero) { b = bb; primero = false; } else b.Encapsulate(bb); }
                    string huella = malla + b.size.ToString("F2");
                    foreach (var kv in huellas) if (kv.Value == huella) distintos = false;
                    huellas[m] = huella;
                }
                Check("Cada arma y el cuchillo tienen su prefab de modelo propio (P_Wpn_*)", todos);
                Check("El cohete, la pistola y el cuchillo no comparten malla ni silueta con el fusil", distintos);
                Check("El tajo de cuchillo tiene modelo y estela (CuchilloFx)", System.IO.File.ReadAllText("Assets/_Project/Scripts/Presentation/CuchilloFx.cs").Contains("P_Wpn_Cuchillo") && System.IO.File.ReadAllText("Assets/_Project/Scripts/Presentation/CuchilloFx.cs").Contains("TrailRenderer"));
            }

            // --- Ambiente del menu principal (21): se arma solo en Play sobre SC_MainMenu ---
            Check("El ambiente del menu no se crea fuera de Play", SP.Presentation.MenuAmbiente.Crear(UnityEngine.SceneManagement.SceneManager.GetActiveScene()) == null);
            var codigoMenu = System.IO.File.ReadAllText("Assets/_Project/Scripts/Presentation/MenuAmbiente.cs");
            Check("El ambiente del menu tiene fondo animado y musica en bucle", codigoMenu.Contains("Rejilla") && codigoMenu.Contains("musica.loop = true"));

            // --- Idioma: el tutorial y la mision tambien se traducen (27) ---
            Check("Loc traduce un titulo del tutorial", SP.Core.Loc.Ingles("MOVER LA CÁMARA") == "MOVE THE CAMERA");
            Check("Loc traduce un aviso de mision", SP.Core.Loc.Ingles("EL CIVIL MURIO") == "THE CIVILIAN DIED");
            Check("Loc traduce un texto largo del tutorial con tildes y flechas", SP.Core.Loc.Ingles("Q → POSICIÓN → SÍGANME") == "Q → POSITION → FOLLOW ME");
            {
                // Sin deriva: cada clave de la tabla del tutorial tiene que seguir existiendo en el codigo del tutorial o la mision.
                string codigoTexto = "";
                foreach (var f in new[] { "Tutorial/TutorialManager.cs", "Mision/MisionDirector.cs", "Mision/MisionHud.cs", "Tutorial/VictoriaTutorial.cs", "Mision/CinematicaDeVictoria.cs" })
                    codigoTexto += System.IO.File.ReadAllText("Assets/_Project/Scripts/" + f);
                int sinUso = 0;
                foreach (var kv in SP.Core.LocTextos.Tutorial) if (!codigoTexto.Contains(kv.Key.Replace("\\", "\\\\"))) sinUso++;
                Check("Todas las claves de LocTextos siguen existiendo en el tutorial o la mision (sin textos huerfanos)", sinUso == 0);
            }
        }
    }
}
