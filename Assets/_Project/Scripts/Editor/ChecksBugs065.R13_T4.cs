using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SP.Operacion;
using SP.Player;
using SP.UI;

namespace SP.EditorTools
{
    // Ronda 13, T4: mira del canon fija por defecto en el asiento de artillero de la Operacion (clic derecho alterna) y anillo de impactos
    // en el HUD del tanque. Ids 974a (mira) y 975a (anillo), sin mansion.
    public static partial class ChecksBugs065
    {
        // Mouse virtual (sin ventana no hay Mouse fisico) para simular clics derechos sobre el driver.
        static Mouse R13MouseVirtual()
        {
            var mouse = Mouse.current;
            if (mouse == null) mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (mouse == null) return null;
            mouse.MakeCurrent();
            return mouse;
        }

        static IEnumerable R13ClicDerecho(Mouse mouse)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right, true));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right, false));
            yield return null; yield return null;
        }

        // ---------------------------------------------------------------- 974a mira inicial fija, FOV correcto a 1 s, clic derecho alterna
        static IEnumerator Bug974a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            foreach (var x in PrepararTanque()) yield return x;
            var drv = PlayerInputDriver.Activo; var d = OperacionDirector.Instancia;
            W8Ok(ref ok, embarqueOk && drv != null && drv.CurrentSeat == SP.Vehicles.VehicleSeatRole.Gunner, sb, $"el jugador queda de artillero (embarqueOk={embarqueOk} asiento={(drv != null ? drv.CurrentSeat.ToString() : "-")} enTanque={(d != null && d.EnTanque)})");
            if (drv == null || !embarqueOk) { Fin("FALLO " + sb); yield break; }
            var rig = drv.Rig; var cam = rig.Cam;
            var mouse = R13MouseVirtual();
            W8Ok(ref ok, mouse != null, sb, "hay mouse (virtual)");

            // sin clic: la mira ya esta puesta y el FOV llego al de zoom (si SetZoomed se reiniciara cada cuadro, el FOV avanzaria ~0,3 %/cuadro)
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.0f) yield return null;
            float ads = rig.AdsBlend, fov = cam.fieldOfView, fz = rig.FovDeZoom;
            W8Ok(ref ok, drv.MiraDelCanonFijada && rig.EstaConZoom && ads > 0.9f, sb, $"sin clic: mira fija={drv.MiraDelCanonFijada} zoom={rig.EstaConZoom} ads={ads:0.00} a 1 s (> 0,9)");
            W8Ok(ref ok, Mathf.Abs(fov - fz) < 0.5f, sb, $"FOV a 1 s = {fov:0.00} vs FOV de zoom {fz:0.00} (< 0,5 grados)");

            // clic derecho: sin zoom; otro clic: zoom
            if (mouse != null)
            {
                foreach (var x in R13ClicDerecho(mouse)) yield return x;
                t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 1.0f) yield return null;
                W8Ok(ref ok, !rig.EstaConZoom && !drv.MiraDelCanonFijada && rig.AdsBlend < 0.1f, sb, $"1.er clic derecho: sin zoom (zoom={rig.EstaConZoom} ads={rig.AdsBlend:0.00})");
                foreach (var x in R13ClicDerecho(mouse)) yield return x;
                t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 1.0f) yield return null;
                W8Ok(ref ok, rig.EstaConZoom && drv.MiraDelCanonFijada && rig.AdsBlend > 0.9f, sb, $"2.o clic derecho: zoom (zoom={rig.EstaConZoom} ads={rig.AdsBlend:0.00})");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 975a anillo de impactos: angulo respecto de la camara, se apaga a 2 s
        static IEnumerator Bug975a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            foreach (var x in PrepararTanque()) yield return x;
            var drv = PlayerInputDriver.Activo; var d = OperacionDirector.Instancia;
            if (drv == null || d == null || d.tanque == null || !embarqueOk) { Fin("FALLO sin tanque/artillero " + sb); yield break; }
            var hud = HudDeTanqueSimple.Instancia;
            for (int i = 0; i < 90 && (hud == null || !HudDeTanqueSimple.Visible); i++) { hud = HudDeTanqueSimple.Instancia; yield return null; }
            W8Ok(ref ok, hud != null && HudDeTanqueSimple.Visible && hud.AnilloActivo, sb, "HUD del tanque visible con el anillo de impactos");
            if (hud == null || !HudDeTanqueSimple.Visible) { Fin("FALLO " + sb); yield break; }
            var cam = drv.Rig.Cam; var tq = d.tanque;
            foreach (var x in Esperar(1.5f)) yield return x;   // que termine el fundido de camara del embarque (1 s) y el cuerpo gire parado
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized, der = Vector3.Cross(Vector3.up, fwd);
            // sobre el HUD: aro de 320 px
            W8Ok(ref ok, Mathf.Abs(hud.RectDelAnillo.sizeDelta.x - HudDeTanqueSimple.DiametroDelAnillo) < 0.1f, sb, $"anillo de {hud.RectDelAnillo.sizeDelta.x:0} px");

            // origen a -90 (izquierda de la camara) y a 180 (atras)
            HudDeTanqueSimple.RegistrarImpactoEn(tq, tq.transform.position - der * 25f);
            HudDeTanqueSimple.RegistrarImpactoEn(tq, tq.transform.position - fwd * 25f);
            bool izq = false, atras = false; var lista = new StringBuilder();
            foreach (var a in hud.AngulosActivos) { lista.Append(a.ToString("0")).Append(' '); if (Mathf.Abs(a - -90f) <= 5f) izq = true; if (Mathf.Abs(Mathf.Abs(a) - 180f) <= 5f) atras = true; }
            W8Ok(ref ok, izq && atras, sb, $"AngulosActivos contiene ~-90 y ~180 ({lista})");
            // la marca gira con la camara: no hay que probarlo, pero se mide la rotacion UI de la primera (-ang)
            float rotZ = hud.RectDelAnillo.GetChild(1).localEulerAngles.z; if (rotZ > 180f) rotZ -= 360f;
            W8Ok(ref ok, hud.CantidadDeMarcas == 2 && Mathf.Abs(Mathf.Abs(rotZ) - 90f) < 6f, sb, $"2 marcas, rotacion UI de la 1.a = {rotZ:0} (esperado +90)");

            // se apagan a los 2,1 s
            float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 2.1f) yield return null;
            W8Ok(ref ok, hud.AngulosActivos.Count == 0 && hud.CantidadDeMarcas == 0, sb, $"a los 2,1 s no quedan marcas ({hud.CantidadDeMarcas})");

            // un impacto REAL (explosion de proyectil a la izquierda de la camara, via Projectile.ExplodeAt -> Vehicle.TakeDamage con origen)
            Vector3 fwd2 = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized, der2 = Vector3.Cross(Vector3.up, fwd2);
            int vidaAntes = tq.Health.Current; bool diosAntes = SP.Core.ModoDios.Activo;
            SP.Combat.Projectile.ExplodeAt(tq.transform.position - der2 * 3f, 6f, 40, -3, null);
            bool izqReal = false; var l2 = new StringBuilder();
            foreach (var a in hud.AngulosActivos) { l2.Append(a.ToString("0")).Append(' '); if (Mathf.Abs(a - -90f) <= 10f) izqReal = true; }
            W8Ok(ref ok, izqReal, sb, $"explosion real a la izquierda: marca ~-90 ({l2}; vida {vidaAntes} -> {tq.Health.Current}, dios={diosAntes})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
