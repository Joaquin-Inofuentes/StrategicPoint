using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // WP11: integracion. Checks que faltaban: #063 (oleadas de la ciudad escalonadas y dispersas) y #064 (los aliados suben al helicoptero y el
    // conteo de supervivientes se fuerza al de los subidos). Hay que estar en Play sobre SC_Operacion (CorrerTodos/CorrerLista lo hacen solos).
    public static partial class ChecksBugs065
    {
        // Captura desde un punto mirando a otro (sin HUD), para las tomas de validacion hechas por eval.
        public static string CapturaDesde(Vector3 pos, Vector3 mira, string archivo, int w = 1600, int h = 900, float fov = 55f)
            => CapturarDesde(pos, Quaternion.LookRotation(mira - pos, Vector3.up), archivo, w, h, fov);

        // Captura de lo que ve el jugador CON el HUD (los canvases de overlay no salen en un render): copia la camara principal, pasa un momento los
        // canvases raiz de overlay a ScreenSpaceCamera sobre esa copia, renderiza a una textura y los deja como estaban. No toca el foco del editor.
        public static string CapturaConHud(string archivo, int w = 1920, int h = 1080)
        {
            var src = CamaraPrincipal.Actual;
            if (src == null) return "sin camara principal";
            var goCam = new GameObject("CamCapturaHudTmp");
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevActiva = RenderTexture.active;
            var cambiados = new List<(Canvas c, int orden)>();
            try
            {
                var cam = goCam.AddComponent<Camera>();
                cam.CopyFrom(src);
                cam.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                cam.targetTexture = rt; cam.cullingMask = ~0;
                foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (c == null || !c.isRootCanvas || !c.gameObject.activeInHierarchy || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    cambiados.Add((c, c.sortingOrder));
                    c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                return "";
            }
            catch (Exception e) { return "captura fallo: " + e.Message; }
            finally
            {
                foreach (var (c, orden) in cambiados) if (c != null) { c.worldCamera = null; c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = orden; }
                RenderTexture.active = prevActiva;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(goCam);
            }
        }

        // ---------------------------------------------------------------- #063 oleadas escalonadas
        // Flujo viejo de Resistir (ResistirSinMando): el reloj de la fase corre solo y la oleada sale por su segundo. Se la adelanta con el reloj
        // y se mira CUANDO y DONDE aparece cada soldado: tienen que entrar de a uno (1 a 2,5 s entre cada uno) y por bocas de calle distintas.
        static IEnumerator Bug063()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            bool sinMandoAntes = OperacionDirector.ResistirSinMando; OperacionDirector.ResistirSinMando = true;
            try
            {
                OperacionPrueba.Arrancar(5);
                foreach (var x in Esperar(1.5f)) yield return x;
                var d = OperacionDirector.Instancia;
                if (d == null || d.Fase != FaseOperacion.Resistir) { Fin("FALLO no se llego a Resistir (fase=" + (d != null ? d.Fase.ToString() : "sin director") + ")"); yield break; }
                if (d.oleadasDeLaCiudad == null || d.oleadasDeLaCiudad.Length == 0 || d.oleadasDeLaCiudad[0].soldados == null) { Fin("FALLO la ciudad no tiene oleadas de reserva"); yield break; }
                var ola = d.oleadasDeLaCiudad[0];
                var soldados = new List<Soldier>();
                foreach (var s in ola.soldados) if (s != null) soldados.Add(s);
                int n = soldados.Count;
                int inactivosAntes = 0; foreach (var s in soldados) if (!s.gameObject.activeInHierarchy) inactivosAntes++;
                bool antesOk = n >= 4 && inactivosAntes == n;
                if (!antesOk) ok = false;
                sb.Append($"oleada 1: {n} soldados, {inactivosAntes} inactivos esperando{(antesOk ? "" : " (MAL: pide >= 4 y todos inactivos)")}; ");

                int liberadosAntes = d.LiberadosEscalonados;
                float proxima0 = d.ProximaOleadaDeLaCiudadEn;
                // Se adelanta el reloj de la fase hasta el segundo de la oleada (no el trayecto del tanque: el de la ciudad no tiene nada de eso).
                var campo = typeof(OperacionDirector).GetField("<Reloj>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (campo == null) { Fin("FALLO no se encontro el reloj del director"); yield break; }
                campo.SetValue(d, ola.segundo + 0.05f);

                var visto = new bool[n]; var tVisto = new float[n]; var pos = new Vector3[n];
                float t0 = Time.time; int vistos = 0; bool pendientesVistos = false;
                while (vistos < n && Time.time - t0 < 30f)
                {
                    yield return null;
                    if (d.PendientesDeLaCiudad > 0) pendientesVistos = true;
                    for (int i = 0; i < n; i++)
                        if (!visto[i] && soldados[i] != null && soldados[i].gameObject.activeInHierarchy)
                        { visto[i] = true; tVisto[i] = Time.time - t0; pos[i] = soldados[i].transform.position; vistos++; }
                }
                bool todos = vistos == n;
                // Tiempos: ordenados, el primero casi enseguida y de a uno despues.
                var ts = new List<float>(tVisto); ts.Sort();
                float gapMin = float.MaxValue, gapMax = 0f;
                for (int i = 1; i < ts.Count; i++) { float g = ts[i] - ts[i - 1]; gapMin = Mathf.Min(gapMin, g); gapMax = Mathf.Max(gapMax, g); }
                float tramo = ts.Count > 0 ? ts[ts.Count - 1] - ts[0] : 0f;
                bool escalonado = todos && gapMin >= 0.8f && gapMax <= 3.3f && tramo >= 2.5f;
                // Lugares: bocas de calle distintas (no todos amontonados en un punto).
                var centros = new List<Vector3>();
                for (int i = 0; i < n; i++)
                {
                    if (!visto[i]) continue;
                    bool nuevo = true;
                    foreach (var c in centros) if (new Vector2(c.x - pos[i].x, c.z - pos[i].z).magnitude < 6f) { nuevo = false; break; }
                    if (nuevo) centros.Add(pos[i]);
                }
                var plaza = d.plaza != null ? d.plaza.position : Vector3.zero;
                float distMin = float.MaxValue;
                for (int i = 0; i < n; i++) if (visto[i]) distMin = Mathf.Min(distMin, new Vector2(pos[i].x - plaza.x, pos[i].z - plaza.z).magnitude);
                bool dispersos = centros.Count >= Mathf.Min(3, n) && distMin >= 35f;
                bool contador = d.LiberadosEscalonados - liberadosAntes == n && pendientesVistos;
                if (!todos || !escalonado || !dispersos || !contador) ok = false;
                sb.Append($"aparecieron {vistos}/{n} en {tramo:0.0} s (huecos entre uno y otro {gapMin:0.0}-{gapMax:0.0} s){(escalonado ? "" : " (MAL: pide huecos de 0,8 a 3,3 s y >= 2,5 s en total)")}; ");
                sb.Append($"{centros.Count} bocas distintas (>= 6 m entre si), la mas cercana a {distMin:0} m de la plaza{(dispersos ? "" : " (MAL: pide >= 3 bocas y >= 35 m)")}; ");
                sb.Append($"LiberadosEscalonados +{d.LiberadosEscalonados - liberadosAntes}, pendientes vistos={pendientesVistos}{(contador ? "" : " (MAL)")}; ");
                // La cuenta atras del HUD de la proxima oleada.
                float proxima1 = d.ProximaOleadaDeLaCiudadEn;
                bool cuenta = d.oleadasDeLaCiudad.Length < 2 || proxima1 >= 0f;
                if (!cuenta) ok = false;
                sb.Append($"proxima oleada en {proxima1:0} s (antes {proxima0:0}){(cuenta ? "" : " (MAL)")}; ");
            }
            finally { OperacionDirector.ResistirSinMando = sinMandoAntes; ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #064 embarque y conteo de supervivientes
        // Extraer: la escuadra sube al helicoptero (ignora todo lo que estaba haciendo), el conteo de supervivientes de la pantalla de resultado
        // se fuerza al de los subidos y las filas de metricas salen. La cadena completa Resistir -> Extraer -> Victoria la cubre Bug101e.
        static IEnumerator Bug064()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                OperacionPrueba.Arrancar(6);
                foreach (var x in Esperar(1.5f)) yield return x;
                var d = OperacionDirector.Instancia; var outcome = GameOutcomeController.Activo;
                if (d == null || outcome == null || d.Fase != FaseOperacion.Extraer) { Fin("FALLO no se llego a Extraer o falta el GameOutcomeController"); yield break; }
                bool sinForzar = EstadisticasDeMision.SupervivientesForzados < 0;
                // Aliados ocupados en otra cosa: uno persiguiendo y otro con la IA pausada de a ratos; igual tienen que subir.
                d.SubirAlHeli();
                float t0 = Time.realtimeSinceStartup;
                while (!outcome.IsShowing && Time.realtimeSinceStartup - t0 < 45f) yield return null;
                if (!outcome.IsShowing) { Fin("FALLO la victoria no llego en 45 s (fase=" + d.Fase + ")"); yield break; }
                int subidos = OperacionDirector.SubidosAlHeli;
                int forzados = EstadisticasDeMision.SupervivientesForzados;
                bool conteo = sinForzar && forzados == subidos && subidos >= 3;
                if (!conteo) ok = false;
                sb.Append($"antes de subir sin forzar={sinForzar}; subidos={subidos}, SupervivientesForzados={forzados}{(conteo ? "" : " (MAL: pide forzados == subidos >= 3)")}; ");
                var filas = EstadisticasDeMision.Filas(true);
                EstadisticasDeMision.Fila sup = default; bool hay = false;
                foreach (var f in filas) if (f.Etiqueta == "SUPERVIVIENTES") { sup = f; hay = true; }
                bool filaOk = hay && sup.Numero == subidos && filas.Count >= 4;
                if (!filaOk) ok = false;
                sb.Append($"filas de la pantalla de resultado={filas.Count}, SUPERVIVIENTES={(hay ? sup.Numero.ToString() : "falta")}{(filaOk ? "" : " (MAL)")}; ");
                bool sinFundido = !d.FundidoActivo && d.Fase == FaseOperacion.Victoria;
                if (!sinFundido) ok = false;
                sb.Append($"fase={d.Fase}, sin fundido a negro={sinFundido}; ");
            }
            finally { Time.timeScale = 1f; ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
