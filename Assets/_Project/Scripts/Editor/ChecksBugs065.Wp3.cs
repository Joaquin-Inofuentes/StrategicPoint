using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.EditorTools
{
    // WP3: UI de opciones, controles y minimapa (#098, #099, #100, #065 minimapa). Hay que estar en Play sobre SC_Operacion.
    public static partial class ChecksBugs065
    {
        static PauseController PausaDelJuego() => UnityEngine.Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);

        static Transform HijoDe(Transform raiz, string nombre) => SP.Core.BuscarHijo.Ruta(raiz, nombre);

        static string RutaValidacion(string archivo) => Path.Combine(Application.dataPath, "Validacion", archivo).Replace('\\', '/');

        // Dibuja un canvas overlay a w x h con una camara temporal (canvas -> ScreenSpaceCamera un momento) y lo guarda como PNG.
        // recorte: x,y,ancho,alto en px (origen abajo-izquierda) o null para la imagen entera.
        static string CapturarCanvas(Canvas cv, int w, int h, string archivo, RectInt? recorte = null)
        {
            var modo = cv.renderMode; var camPrev = cv.worldCamera; float plano = cv.planeDistance;
            var goCam = new GameObject("CamCapturaTmp");
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevActiva = RenderTexture.active;
            try
            {
                var cam = goCam.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
                cam.cullingMask = 1 << cv.gameObject.layer; cam.nearClipPlane = 0.01f; cam.farClipPlane = 10f; cam.fieldOfView = 60f;
                cam.targetTexture = rt;
                cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = cam; cv.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var r = recorte ?? new RectInt(0, 0, w, h);
                var tex = new Texture2D(r.width, r.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                return "";
            }
            catch (Exception e) { return "captura fallo: " + e.Message; }
            finally
            {
                cv.renderMode = modo; cv.worldCamera = camPrev; cv.planeDistance = plano;
                RenderTexture.active = prevActiva;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(goCam);
                Canvas.ForceUpdateCanvases();
            }
        }

        static void CerrarPausa(PauseController pc)
        {
            if (pc == null) return;
            pc.OnSettingsBackClicked();
            pc.OnControlsBackClicked();
            pc.OnContinueClicked();
        }

        // ---------------------------------------------------------------- #098 perillas de los sliders
        static IEnumerator Bug098()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var pc = PausaDelJuego();
            if (pc == null) { Fin("FALLO no hay PauseController"); yield break; }
            pc.ShowPause(); pc.OnSettingsClicked();
            foreach (var x in Esperar(0.7f)) yield return x;
            var sp = HijoDe(pc.transform, "SettingsPanel");
            var sb = new StringBuilder(); bool ok = true; int n = 0;
            if (sp == null || !sp.gameObject.activeInHierarchy) { CerrarPausa(pc); Fin("FALLO SettingsPanel no esta activo"); yield break; }
            foreach (var s in sp.GetComponentsInChildren<Slider>(false))
            {
                if (s.transform.parent != sp) continue;   // los del desplegable no cuentan
                var h = s.handleRect; if (h == null) { ok = false; sb.Append(s.name + ": sin handle; "); continue; }
                float w = h.rect.width, al = h.rect.height;
                bool bien = Mathf.Abs(w - al) < 0.5f && al >= 20f && al <= 24f;
                if (!bien) ok = false;
                n++;
                sb.Append($"{s.name}={w:0.#}x{al:0.#}{(bien ? "" : "(MAL)")}; ");
            }
            if (n < 8) { ok = false; sb.Append($"solo {n} sliders (se esperaban 8); "); }

            // Captura con zoom de la columna izquierda (Sonido) a 1920x1080 sobre el canvas propio de la pausa.
            string capErr = "";
            var cv = pc.GetComponentInParent<Canvas>() != null ? pc.GetComponentInParent<Canvas>().rootCanvas : null;
            if (cv != null) capErr = CapturarCanvas(cv, 1920, 1080, "v2_098_sliders.png", new RectInt(250, 540, 700, 400));
            CerrarPausa(pc);
            Fin((ok ? "OK " : "FALLO ") + $"{n} perillas: " + sb + (capErr != "" ? capErr : ""));
        }

        // ---------------------------------------------------------------- #099 el tamano de HUD no mueve el panel
        static IEnumerator Bug099()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var pc = PausaDelJuego();
            if (pc == null || pc.HudScaler == null) { Fin("FALLO no hay PauseController/HudScaler"); yield break; }
            var sb = new StringBuilder(); bool ok = true;

            pc.ShowPause(); pc.OnSettingsClicked();
            foreach (var x in Esperar(0.7f)) yield return x;
            var sp = (RectTransform)HijoDe(pc.transform, "SettingsPanel");
            var hud = HijoDe(sp, "Tamaño de HUD_Slider") != null ? HijoDe(sp, "Tamaño de HUD_Slider").GetComponent<Slider>() : null;
            var etiqueta = HijoDe(sp, "Tamaño de HUD_Value") != null ? HijoDe(sp, "Tamaño de HUD_Value").GetComponent<Text>() : null;
            var layout = sp != null ? sp.GetComponent<SP.UI.LayoutDeAjustes>() : null;
            if (hud == null || layout == null) { CerrarPausa(pc); Fin("FALLO sin slider de HUD o sin LayoutDeAjustes"); yield break; }

            float original = hud.value;
            var hudCanvas = pc.HudScaler.GetComponent<Canvas>();
            bool canvasPropio = sp.GetComponentInParent<Canvas>().rootCanvas.name == PauseController.NombreCanvasPausa;
            if (!canvasPropio) { ok = false; sb.Append("la pausa NO esta en su canvas propio; "); }
            if (sp.GetComponentInParent<Canvas>().rootCanvas == hudCanvas.rootCanvas) { ok = false; sb.Append("comparte canvas raiz con el HUD; "); }

            Vector2 Tam() { var c = new Vector3[4]; sp.GetWorldCorners(c); return new Vector2(c[2].x - c[0].x, c[2].y - c[0].y); }
            Vector2 Org() { var c = new Vector3[4]; sp.GetWorldCorners(c); return new Vector2(c[0].x, c[0].y); }

            hud.value = 1.0f;
            foreach (var x in Esperar(0.4f)) yield return x;
            var tam0 = Tam(); var org0 = Org(); int apl0 = layout.Aplicaciones;
            float factorHud1 = hudCanvas.scaleFactor;
            float factor07 = 0f, factor14 = 0f;
            float maxDifTam = 0f, maxDifOrg = 0f;
            foreach (float v in new[] { 1.0f, 0.75f, 0.7f, 1.2f, 1.4f })
            {
                hud.value = v;
                foreach (var x in Esperar(0.4f)) yield return x;
                maxDifTam = Mathf.Max(maxDifTam, Mathf.Abs(Tam().x - tam0.x), Mathf.Abs(Tam().y - tam0.y));
                maxDifOrg = Mathf.Max(maxDifOrg, Mathf.Abs(Org().x - org0.x), Mathf.Abs(Org().y - org0.y));
                if (Mathf.Approximately(v, 0.7f)) factor07 = hudCanvas.scaleFactor;
                if (Mathf.Approximately(v, 1.4f)) factor14 = hudCanvas.scaleFactor;
            }
            if (maxDifTam > 2f || maxDifOrg > 2f) { ok = false; sb.Append("el panel se movio/cambio de tamano; "); }
            sb.Append($"panel {tam0.x:0}x{tam0.y:0} difTam={maxDifTam:0.##} difOrigen={maxDifOrg:0.##}; ");

            // El HUD de verdad si cambia de escala (0.7 -> 1.4 = el doble).
            float ratio = factor07 > 0f ? factor14 / factor07 : 0f;
            if (ratio < 1.9f || ratio > 2.1f) { ok = false; sb.Append("el HUD no escalo x2; "); }
            sb.Append($"HUD scaleFactor 0.7={factor07:0.###} 1.4={factor14:0.###} (x{ratio:0.00}); ");

            // Barrido fino (tics) sin soltar el puntero: el panel no se reacomoda por cada tic.
            int antes = layout.Aplicaciones;
            for (int i = 0; i <= 12; i++) { hud.value = 0.7f + 0.7f * i / 12f; yield return null; }
            foreach (var x in Esperar(0.3f)) yield return x;
            int subio = layout.Aplicaciones - antes;
            int subioTotal = layout.Aplicaciones - apl0;
            if (subioTotal > 1) { ok = false; sb.Append("LayoutDeAjustes.Aplicaciones sube por tic; "); }
            sb.Append($"Aplicaciones +{subioTotal} (barrido +{subio}); ");

            // Arrastre real: con el puntero apretado solo cambia la etiqueta; al soltar se aplica.
            hud.value = 1.0f;
            foreach (var x in Esperar(0.3f)) yield return x;
            var arrastre = hud.GetComponent<SP.UI.SliderDeAjuste>();
            if (arrastre == null) { ok = false; sb.Append("sin SliderDeAjuste; "); }
            else
            {
                var ped = new PointerEventData(EventSystem.current);
                var refAntes = pc.HudScaler.referenceResolution;
                arrastre.OnPointerDown(ped);
                hud.value = 1.4f; yield return null; hud.value = 0.9f; yield return null; hud.value = 1.3f; yield return null;
                var refDurante = pc.HudScaler.referenceResolution;
                bool sinCambioDurante = (refDurante - refAntes).sqrMagnitude < 0.01f;
                bool etiquetaOk = etiqueta != null && etiqueta.text == 1.3f.ToString("0.00");
                arrastre.OnPointerUp(ped);
                yield return null;
                var refDespues = pc.HudScaler.referenceResolution;
                bool aplicoAlSoltar = Mathf.Abs(refDespues.x - 960f / 1.3f) < 0.5f;
                bool guardado = Mathf.Abs(PlayerPrefs.GetFloat("sp_hud_scale", -1f) - 1.3f) < 0.001f;
                if (!(sinCambioDurante && etiquetaOk && aplicoAlSoltar && guardado)) ok = false;
                sb.Append($"arrastre: sinCambioDurante={sinCambioDurante} etiqueta='{(etiqueta != null ? etiqueta.text : "?")}' aplicoAlSoltar={aplicoAlSoltar} guardado={guardado}; ");
            }

            // Restaurar el valor original (aplica y guarda).
            hud.value = original;
            foreach (var x in Esperar(0.3f)) yield return x;
            bool restaurado = Mathf.Abs(pc.HudScaler.referenceResolution.x - 960f / original) < 0.5f;
            if (!restaurado) { ok = false; sb.Append("no se restauro el HUD; "); }
            CerrarPausa(pc);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #100 tabla de controles
        static IEnumerator Bug100()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var pc = PausaDelJuego();
            if (pc == null) { Fin("FALLO no hay PauseController"); yield break; }
            var sb = new StringBuilder(); bool ok = true;

            pc.ShowPause(); pc.OnControlsClicked();
            foreach (var x in Esperar(0.8f)) yield return x;
            var panel = (RectTransform)HijoDe(pc.transform, "ControlsPanel");
            var rebind = HijoDe(panel, "RebindButton");
            if (rebind == null || rebind.gameObject.activeSelf) { ok = false; sb.Append("RebindButton sigue activo; "); }
            var contenido = SP.UI.TablaDeControles.Contenido(panel);
            if (contenido == null) { CerrarPausa(pc); Fin("FALLO no hay tabla de controles"); yield break; }

            int esperadas = 0;
            foreach (var ctx in SP.UI.ControlsTable.AllContexts) foreach (var _ in SP.UI.ControlsTable.For(ctx)) esperadas++;

            int filas = 0, encabezados = 0, sinTecla = 0, solapes = 0, textosCortados = 0;
            RectTransform previo = null; float maxX = 0f;
            var c4 = new Vector3[4]; var p4 = new Vector3[4];
            for (int i = 0; i < contenido.childCount; i++)
            {
                var hijo = (RectTransform)contenido.GetChild(i);
                if (hijo.name == "Encabezado") encabezados++;
                else if (hijo.name == "Fila")
                {
                    filas++;
                    var teclas = HijoDe(hijo, "Teclas");
                    if (teclas == null || teclas.childCount < 1) sinTecla++;
                }
                else continue;
                if (previo != null)
                {
                    previo.GetWorldCorners(p4); hijo.GetWorldCorners(c4);
                    // En el mundo de la UI la Y baja al avanzar: el techo del siguiente no puede pasar el piso del anterior.
                    if (c4[1].y > p4[0].y + 0.5f) solapes++;
                }
                previo = hijo;
            }
            foreach (var t in contenido.GetComponentsInChildren<Text>(false))
            {
                var r = t.rectTransform.rect;
                if (t.preferredHeight > r.height + 1f) textosCortados++;
                if (t.name == "Descripcion" && r.width < 100f) textosCortados++;
            }
            var vp = (RectTransform)contenido.parent;
            vp.GetWorldCorners(c4); panel.GetWorldCorners(p4);
            bool dentro = c4[0].x >= p4[0].x - 0.5f && c4[2].x <= p4[2].x + 0.5f && c4[0].y >= p4[0].y - 0.5f && c4[2].y <= p4[2].y + 0.5f;
            maxX = contenido.rect.width;
            bool desplaza = contenido.rect.height > vp.rect.height + 1f;

            if (filas != esperadas) { ok = false; sb.Append($"filas {filas} != {esperadas}; "); }
            if (encabezados != SP.UI.ControlsTable.AllContexts.Length) { ok = false; sb.Append($"encabezados {encabezados}; "); }
            if (sinTecla > 0) { ok = false; sb.Append($"{sinTecla} filas sin keycap; "); }
            if (solapes > 0) { ok = false; sb.Append($"{solapes} solapes verticales; "); }
            if (textosCortados > 0) { ok = false; sb.Append($"{textosCortados} textos cortados; "); }
            if (!dentro) { ok = false; sb.Append("la tabla se sale del panel; "); }
            if (!desplaza) { ok = false; sb.Append("el contenido no desborda (no hay scroll?); "); }
            sb.Append($"{filas} filas, {encabezados} encabezados, contenido {maxX:0}x{contenido.rect.height:0} en viewport {vp.rect.width:0}x{vp.rect.height:0}, panel {panel.rect.width:0}x{panel.rect.height:0}; ");

            // Capturas a 1080 y a 720 sobre el canvas propio de la pausa.
            var cv = pc.GetComponentInParent<Canvas>().rootCanvas;
            string e1 = CapturarCanvas(cv, 1920, 1080, "v2_100_controles_1080.png");
            string e2 = CapturarCanvas(cv, 1280, 720, "v2_100_controles_720.png");
            if (e1 != "" || e2 != "") sb.Append(e1 + e2);

            // Se reabre: no reconstruye la tabla (misma instancia).
            var tabla1 = SP.UI.TablaDeControles.Existente(panel);
            pc.OnControlsBackClicked(); pc.OnControlsClicked();
            yield return null;
            if (SP.UI.TablaDeControles.Existente(panel) != tabla1) { ok = false; sb.Append("reconstruyo la tabla al reabrir; "); }
            CerrarPausa(pc);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #065 siluetas de muros en el minimapa
        static string Bug065m()
        {
            if (!Application.isPlaying) return "FALLO el editor no esta en Play";
            int n = SiluetasDeMinimapa.Cantidad;
            var sb = new StringBuilder(); bool ok = true;
            bool Tiene(string frag) { foreach (var s in SiluetasDeMinimapa.Nombres) if (s.Contains(frag)) return true; return false; }
            if (n <= 30) { ok = false; sb.Append($"solo {n} siluetas (se esperaban >30); "); }
            foreach (var f in new[] { "Valle", "Borde", "Cuartel" }) if (!Tiene(f)) { ok = false; sb.Append($"falta silueta '{f}'; "); }
            var raiz = SiluetasDeMinimapa.Raiz;
            if (raiz == null) { ok = false; sb.Append("sin raiz de siluetas; "); }
            else
            {
                int capa = LayerMask.NameToLayer("Minimap"); if (capa < 0) capa = 8;
                var mr = raiz.GetComponent<MeshRenderer>(); var mf = raiz.GetComponent<MeshFilter>();
                if (raiz.layer != capa) { ok = false; sb.Append("capa distinta de Minimap; "); }
                if (mr == null || mr.sharedMaterial == null) { ok = false; sb.Append("sin material; "); }
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount != n * 4) { ok = false; sb.Append("el mesh no tiene 4 vertices por silueta; "); }
                else
                {
                    var b = mf.sharedMesh.bounds;
                    if (b.size.x < 600f || b.size.z < 600f) { ok = false; sb.Append($"bounds chico {b.size.x:0}x{b.size.z:0} (el perimetro mide ~780); "); }
                    if (Mathf.Abs(b.center.y - SiluetasDeMinimapa.Altura) > 0.01f) { ok = false; sb.Append("altura de siluetas incorrecta; "); }
                    sb.Append($"bounds {b.size.x:0}x{b.size.z:0}; ");
                }
            }
            return (ok ? "OK " : "FALLO ") + $"{n} siluetas de muros en el minimapa: " + sb;
        }
    }
}
