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
        }
    }
}
