using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
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

            // --- Trepar obstaculos bajos (53) ---
            {
                var posOriginal = kes.transform.position;
                var frente = kes.transform.forward; frente.y = 0f; frente.Normalize();
                var pisoBajoKes = posOriginal - Vector3.up * 0.8f;
                var cajon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cajon.transform.localScale = new Vector3(1.2f, 1.0f, 1.2f);
                cajon.transform.position = new Vector3(posOriginal.x, pisoBajoKes.y + 0.5f, posOriginal.z) + frente * 1.4f;
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
        }
    }
}
