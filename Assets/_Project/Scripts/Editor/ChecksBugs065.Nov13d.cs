using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Tanda del 2026-11-13 (2): fotos del HUD del tanque / ruta / plaza, perros de la ciudad y azotea. Sirven para el "antes" y el "despues".
    public static partial class ChecksBugs065
    {
        public static string FotosPrefijo = "despues";      // "antes" | "despues": prefijo de las fotos v13_*
        public static bool PerrosModoAntiguo;               // true = perros sin DondeIr (comportamiento anterior: merodean donde aparecen)
        public static bool HudModoAntiguo;                  // true = texto blanco sobre fondo oscuro (como antes)

        // el Play recarga el dominio y borra los campos estaticos: el modo se lee de un archivo (Temp/modo_fotos.txt con "antes"; sin archivo = "despues")
        static void LeerModoDeFotos()
        {
            string f = System.IO.Path.Combine(Application.dataPath, "..", "Temp", "modo_fotos.txt");
            bool antes = System.IO.File.Exists(f) && System.IO.File.ReadAllText(f).Trim() == "antes";
            FotosPrefijo = antes ? "antes" : "despues"; HudModoAntiguo = antes; PerrosModoAntiguo = antes;
        }

        static IEnumerable FotoConHud(string archivo)
        {
            var cam = Camera.main; if (cam == null) yield break;
            var hud = SP.UI.HudDeTanqueSimple.Instancia; Canvas canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            var modo = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay; var camPrev = canvas != null ? canvas.worldCamera : null; float plano = canvas != null ? canvas.planeDistance : 0f;
            if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f; }
            yield return null;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var previoRt = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = previoRt;
            var previa = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            RenderTexture.active = previa;
            System.IO.File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToJPG(90));
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
            if (canvas != null) { canvas.renderMode = modo; canvas.worldCamera = camPrev; canvas.planeDistance = plano; }
        }

        static bool NombreDeBarricada(string nombre)
        {
            string m = nombre.ToLowerInvariant();
            return m.Contains("barricada") || m.Contains("jersey") || m.Contains("erizo") || m.Contains("valla") || m.Contains("barrera");
        }

        static string UltimasBarricadas = "";
        static int BarricadasCercaDeLaRuta(Transform[] ruta, float radio)
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!NombreDeBarricada(t.name) || t.name.StartsWith("Barricada_BolsasDeArena")) continue;   // las bolsas de arena de los guardias y de la plaza las arma el director en juego (cobertura de combate)
                if (t.parent != null && NombreDeBarricada(t.parent.name)) continue;
                if (t.GetComponentInParent<Vehicle>() != null || t.GetComponentInParent<Soldier>() != null) continue;
                var p = new Vector2(t.position.x, t.position.z); float mejor = 1e9f;
                for (int i = 0; i < ruta.Length - 1; i++)
                {
                    var a = new Vector2(ruta[i].position.x, ruta[i].position.z); var b = new Vector2(ruta[i + 1].position.x, ruta[i + 1].position.z); var ab = b - a;
                    float k = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f; mejor = Mathf.Min(mejor, Vector2.Distance(p, a + ab * k));
                }
                if (mejor <= radio) { n++; UltimasBarricadas += $" [{t.name} en {t.position:0}]"; }
            }
            return n;
        }

        // ---------------------------------------------------------------- 961a fotos: HUD del tanque (texto negro), ruta sin barricadas y plaza con la fuente al centro
        static IEnumerator Bug961a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            LeerModoDeFotos();
            var sb = new StringBuilder(); bool ok = true; string pre = "v13_" + FotosPrefijo + "_";
            foreach (var x in PrepararTanque()) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, embarqueOk && d != null && d.rutaDelTanque != null && d.rutaDelTanque.Length > 4, sb, "el jugador esta en el tanque y la ruta existe");
            if (d == null || d.rutaDelTanque == null || d.rutaDelTanque.Length < 4) { Fin("FALLO " + sb); yield break; }
            // ---- HUD
            var hud = SP.UI.HudDeTanqueSimple.Instancia;
            W8Ok(ref ok, SP.UI.HudDeTanqueSimple.Visible, sb, "la barra de vida del tanque se ve");
            if (hud != null)
            {
                var ft = typeof(SP.UI.HudDeTanqueSimple).GetField("texto", NoPublico); var fv = typeof(SP.UI.HudDeTanqueSimple).GetField("vida", NoPublico);
                var texto = ft != null ? (UnityEngine.UI.Text)ft.GetValue(hud) : null; var vida = fv != null ? (UnityEngine.UI.Image)fv.GetValue(hud) : null;
                if (HudModoAntiguo && texto != null)
                {
                    texto.color = Color.white; texto.fontStyle = FontStyle.Normal;
                    if (vida != null && vida.transform.parent != null) { var bg = vida.transform.parent.GetComponent<UnityEngine.UI.Image>(); if (bg != null) bg.color = new Color(0f, 0f, 0f, 0.6f); }
                }
                yield return null;
                float lum = texto != null ? texto.color.r * 0.3f + texto.color.g * 0.59f + texto.color.b * 0.11f : 1f;
                if (!HudModoAntiguo) W8Ok(ref ok, lum < 0.05f, sb, $"el texto de la barra de vida del tanque es negro ({(texto != null ? texto.color.ToString() : "sin texto")})");
                else sb.Append("[modo antiguo: texto blanco] ");
                foreach (var x in FotoConHud(pre + "hud.jpg")) yield return x;
            }
            // ---- ruta
            var ruta = d.rutaDelTanque; UltimasBarricadas = ""; int cerca = BarricadasCercaDeLaRuta(ruta, 20f); if (cerca > 0) sb.Append(UltimasBarricadas);
            if (HudModoAntiguo) sb.Append($"[barricadas a menos de 20 m de la ruta: {cerca}] ");
            else W8Ok(ref ok, cerca == 0, sb, $"no queda ninguna barricada a menos de 20 m de la ruta ({cerca})");
            foreach (var fr in new[] { 0.10f, 0.30f, 0.50f, 0.75f })
            {
                int k = Mathf.Clamp(Mathf.RoundToInt((ruta.Length - 2) * fr), 1, ruta.Length - 2);
                var w = ruta[k].position; var dir = ruta[k + 1].position - w; dir.y = 0f; dir.Normalize();
                var cam = w - dir * 16f + Vector3.up * 8f; var mira = w + dir * 18f;
                foreach (var x in FotoCamara(pre + $"ruta_{Mathf.RoundToInt(fr * 100)}.jpg", cam, mira, 62f, 1280, 720)) yield return x;
            }
            // ---- plaza
            var c = new Vector3(322f, 0f, 228f);
            foreach (var x in FotoCamara(pre + "plaza_alta.jpg", c + new Vector3(0f, 34f, -30f), c, 58f, 1280, 720)) yield return x;
            foreach (var x in FotoCamara(pre + "plaza_baja.jpg", c + new Vector3(-14f, 5f, -16f), c + Vector3.up * 1.5f, 60f, 1280, 720)) yield return x;
            Transform cuenco = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t.name == "Fuente_Cuenco") cuenco = t;
            if (cuenco != null)
            {
                sb.Append($"[fuente en {cuenco.position:0.0}] ");
                if (!HudModoAntiguo) W8Ok(ref ok, new Vector2(cuenco.position.x - 322f, cuenco.position.z - 228f).magnitude < 1.5f, sb, "la fuente esta en el centro de la plaza");
            }
            else { ok = false; sb.Append("[no se encontro la fuente] "); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 962a perros de la ciudad: todos encuentran el camino y llegan a la plaza
        static IEnumerator Bug962a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            LeerModoDeFotos();
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(3f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = Poseido();
            if (d == null || yo == null) { Fin("FALLO sin director o jugador"); yield break; }
            var inv = Wp7HacerInvulnerable();
            var plantilla = (GameObject)typeof(OperacionDirector).GetField("plantillaDeRefuerzo", NoPublico).GetValue(d);
            var plaza = d.plaza;
            var apar = (List<Vector3>)typeof(OperacionDirector).GetMethod("AparicionesDeLaCiudad", NoPublico).Invoke(d, null);
            if (plantilla == null || plaza == null || apar == null || apar.Count == 0) { Fin("FALLO faltan plantilla/plaza/apariciones"); yield break; }
            var perros = new List<PerroEnemigo>(); var conCamino = new HashSet<PerroEnemigo>(); int sinCaminoCompleto = 0, fueraDeMalla = 0;
            var metaLibre = UnityEngine.AI.NavMesh.SamplePosition(plaza.position + new Vector3(8f, 0f, 0f), out var hm, 6f, UnityEngine.AI.NavMesh.AllAreas) ? hm.position : plaza.position;   // fuera de la taza de la fuente
            foreach (var a in apar)
            {
                var pos = a;
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position; else fueraDeMalla++;
                var camino = new UnityEngine.AI.NavMeshPath();
                bool hay = UnityEngine.AI.NavMesh.CalculatePath(pos, metaLibre, UnityEngine.AI.NavMesh.AllAreas, camino);
                if (!hay || camino.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) sinCaminoCompleto++;
                var go = Object.Instantiate(plantilla, pos, Quaternion.LookRotation(Vector3.ProjectOnPlane(plaza.position - pos, Vector3.up).normalized, Vector3.up), plantilla.transform.parent);
                go.SetActive(true);
                var s = go.GetComponent<Soldier>(); if (s == null) { Object.Destroy(go); continue; }
                var pr = PerroEnemigo.Aplicar(s); if (pr == null) continue;
                ApoyoEnElPiso.Apoyar(s.transform);
                if (!PerrosModoAntiguo) pr.DondeIr = () => plaza.position;
                perros.Add(pr); if (hay && camino.status == UnityEngine.AI.NavMeshPathStatus.PathComplete) conCamino.Add(pr);
            }
            sb.Append($"[{apar.Count} puntos de aparicion; sin camino completo a la plaza {sinCaminoCompleto}; fuera de la malla {fueraDeMalla}] ");
            foreach (var x in Esperar(40f)) yield return x;
            int llegaron = 0, llegaronConCamino = 0, atascos = 0, parciales = 0, sinCamino = 0; float peorDist = 0f; var det = new StringBuilder();
            foreach (var pr in perros)
            {
                if (pr == null) continue;
                float dp = Vector3.Distance(new Vector3(pr.transform.position.x, 0f, pr.transform.position.z), new Vector3(plaza.position.x, 0f, plaza.position.z));
                float dj = Vector3.Distance(pr.transform.position, yo.transform.position);
                bool cerca = dp < 18f || dj < 12f || pr.Mordidas > 0 || pr.Saltos > 0; if (cerca) { llegaron++; if (conCamino.Contains(pr)) llegaronConCamino++; }
                peorDist = Mathf.Max(peorDist, dp);
                if (pr.Camino != null) { atascos += pr.Camino.Atascos; parciales += pr.Camino.Parciales; sinCamino += pr.Camino.SinCamino; }
                if (!cerca) { var cols = Physics.OverlapSphere(pr.transform.position + Vector3.up * 0.5f, 1.0f, ~0, QueryTriggerInteraction.Ignore); var nm = new StringBuilder(); foreach (var cc in cols) if (cc != null && !cc.transform.IsChildOf(pr.transform)) nm.Append(cc.name + ","); det.Append($" [{pr.name} a {dp:0} m de la plaza en {pr.transform.position:0}, estado {pr.Estado}, toca: {nm}]"); }
            }
            sb.Append($"[{llegaron}/{perros.Count} llegaron en 40 s; peor distancia a la plaza {peorDist:0} m; atascos {atascos}, caminos parciales {parciales}, sin camino {sinCamino}]{det} ");
            if (PerrosModoAntiguo) sb.Append("[modo antiguo] ");
            else W8Ok(ref ok, conCamino.Count > 0 && llegaronConCamino == conCamino.Count, sb, $"todos los perros con camino libre llegan a la plaza o al jugador ({llegaronConCamino}/{conCamino.Count}; el resto aparece tras el porton cerrado o un seto)");
            foreach (var h in inv) if (h != null) h.Invulnerable = false;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 963a azotea: el doble de enemigos, repartidos en el tiempo
        static IEnumerator Bug963a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            int porEscalera = 0; foreach (var n in OperacionDirector.SoldadosPorOleada) porEscalera += n;
            int porCuerda = 0; foreach (var n in OperacionDirector.SoldadosPorCuerda) porCuerda += n;
            int total = porEscalera + porCuerda;
            W8Ok(ref ok, total == 29, sb, $"enemigos programados en la azotea: {porEscalera} por escalera + {porCuerda} por cuerda = {total} (la mitad de los 58 que se habian puesto)");
            var tiempos = new List<float>(OperacionDirector.CuandoSuben); tiempos.AddRange(OperacionDirector.CuandoCuelgan); tiempos.Sort();
            float huecoMax = tiempos[0];
            for (int i = 1; i < tiempos.Count; i++) huecoMax = Mathf.Max(huecoMax, tiempos[i] - tiempos[i - 1]);
            huecoMax = Mathf.Max(huecoMax, OperacionDirector.SegundosDeResistenciaEnAzotea - tiempos[tiempos.Count - 1]);
            W8Ok(ref ok, huecoMax <= 14f, sb, $"el hueco mas largo sin llegadas es de {huecoMax:0} s (antes 28 s)");
            var lista = new List<string>(); foreach (var t in tiempos) lista.Add(t.ToString("0"));
            sb.Append("[llegadas a los " + string.Join(", ", lista) + " s] ");
            // en juego: se corre la azotea con el reloj acelerado y se cuentan los que de verdad aparecen
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; if (d == null) { Fin("FALLO sin director " + sb); yield break; }
            var inv = Wp7HacerInvulnerable();
            d.SaltarALaAzotea();
            foreach (var x in Esperar(1f)) yield return x;
            int vistos = 0; var ya = new HashSet<Soldier>(); float t0 = Time.realtimeSinceStartup; var porVentana = new int[10];
            while (d.AzoteaEnCurso && d.SegundosDeAzotea < OperacionDirector.SegundosDeResistenciaEnAzotea - 1f && Time.realtimeSinceStartup - t0 < 90f)
            {
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Team == TeamId.Enemy && s.name.StartsWith("Mansion_Enemigo_") && ya.Add(s)) { vistos++; porVentana[Mathf.Clamp(Mathf.FloorToInt(d.SegundosDeAzotea / 10f), 0, 9)]++; }
                Time.timeScale = 6f;
                yield return null;
            }
            Time.timeScale = 1f;
            sb.Append($"[aparecieron {vistos} enemigos; por ventana de 10 s: {string.Join(", ", System.Array.ConvertAll(porVentana, v => v.ToString()))}] ");
            W8Ok(ref ok, vistos >= 20 && vistos <= 40, sb, $"en juego aparecen {vistos} enemigos en la azotea (programados 29)");
            foreach (var h in inv) if (h != null) h.Invulnerable = false;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}

namespace SP.EditorTools
{
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 964a la barra de vida del tanque baja con el dano, y la metralla del helicoptero pega la mitad
        static System.Collections.IEnumerator Bug964a()
        {
            if (!UnityEngine.Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new System.Text.StringBuilder(); bool ok = true;
            foreach (var x in PrepararTanque()) yield return x;
            var d = SP.Operacion.OperacionDirector.Instancia; var hud = SP.UI.HudDeTanqueSimple.Instancia;
            W8Ok(ref ok, embarqueOk && d != null && d.tanque != null && hud != null && SP.UI.HudDeTanqueSimple.Visible, sb, "el jugador esta en el tanque y se ve su barra");
            if (!ok) { Fin("FALLO " + sb); yield break; }
            SP.Core.ModoDios.Poner(false);
            var tq = d.tanque; tq.PisoDeVida01 = 0f;
            yield return null; yield return null;
            float lleno = hud.AnchoRellenoDeVida;
            tq.PonerVida01(0.4f);
            yield return null; yield return null;
            float bajo = hud.AnchoRellenoDeVida;
            W8Ok(ref ok, lleno > 0.98f, sb, $"con el tanque sano el relleno de la barra mide {lleno:0.00} del ancho");
            W8Ok(ref ok, bajo > 0.38f && bajo < 0.42f, sb, $"con el 40 % de vida el relleno mide {bajo:0.00} del ancho (la barra baja)");
            foreach (var x in FotoConHud("v14_hud_tanque_40.jpg")) yield return x;
            tq.PonerVida01(0.1f); yield return null; yield return null;
            W8Ok(ref ok, hud.AnchoRellenoDeVida < 0.12f, sb, $"con el 10 % de vida mide {hud.AnchoRellenoDeVida:0.00}");
            tq.PonerVida01(1f);
            // helicoptero: dano y cadencia de la metralla reducidos
            W8Ok(ref ok, SP.Operacion.HelicopteroEnemigo.DanoDeMetralla == 3 && SP.Operacion.HelicopteroEnemigo.DanoDeCohete == 40, sb, $"el helicoptero pega la mitad: metralla {SP.Operacion.HelicopteroEnemigo.DanoDeMetralla} (antes 6), cohete {SP.Operacion.HelicopteroEnemigo.DanoDeCohete} (antes 80)");
            float dpsAntes = 6f / 0.1f * 1.0f / (1.0f + 1.7f), dpsAhora = SP.Operacion.HelicopteroEnemigo.DanoDeMetralla / SP.Operacion.HelicopteroEnemigo.SegundosEntreBalas * SP.Operacion.HelicopteroEnemigo.RafagaNormal / (SP.Operacion.HelicopteroEnemigo.RafagaNormal + 2.5f);
            W8Ok(ref ok, dpsAhora <= dpsAntes * 0.3f, sb, $"dano por segundo medio de la metralla: {dpsAntes:0.0} -> {dpsAhora:0.0} ({dpsAhora / dpsAntes * 100f:0} % de antes)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
