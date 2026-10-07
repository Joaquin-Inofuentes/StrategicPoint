using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Tanda #104-#132, P3: cuartel. #104 visibilidad de los enemigos en altura (torres, reflectores) y faro visible / con efectos al romperse.
    public static partial class ChecksBugs065
    {
        static Soldier BuscarPorNombre(string nombre)
        {
            foreach (var a in ActorRegistry.All)
            {
                var s = a as Soldier;
                if (s != null && s.name == nombre && s.gameObject.activeInHierarchy) return s;
            }
            return null;
        }

        // Pixeles visibles del soldado 'e' vistos desde 'desde' a altura de ojos (oclusion real, 1280x720, FOV de la camara de juego).
        static int PixelesDelSoldadoDesde(Soldier e, Vector3 desde, float fov = 60f)
        {
            var smr = e.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null) return -1;
            var rt = new RenderTexture(1280, 720, 24);
            var cam = CamaraTemporal(desde, e.transform.position + Vector3.up * 0.3f, fov, rt, out var go);
            int px = ContarPixelesVisibles(smr, cam);
            cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(go);
            return px;
        }

        // Utilidad de iteracion (eval, en Play): medicion a 39 m (la posicion del reporte #104: (-6,6; 0,8; -227)) y a 60 m del artillero de TV2.
        public static string Medir104(string nombre = "Enemigo_Artillero_TV2")
        {
            var e = BuscarPorNombre(nombre);
            if (e == null) return "no esta " + nombre;
            var sb = new StringBuilder();
            sb.Append($"{nombre} en {e.transform.position.ToString("F1")}: ");
            foreach (var d in new[] { 25f, 39f, 60f })
            {
                var dir = new Vector3(1f, 0f, 0f);   // desde el patio (x > torre), hacia la torre
                var p = new Vector3(e.transform.position.x, 1.7f, e.transform.position.z) + dir * d;
                sb.Append($"d={d:0}m px={PixelesDelSoldadoDesde(e, p)}; ");
            }
            return sb.ToString();
        }

        // Puntos a 'radio' m del soldado (anillo de 15 grados) a altura de ojos y con linea libre hasta su cabeza (sin contar la propia torre).
        static List<Vector3> PuntosConVista(Soldier e, float radio)
        {
            var l = new List<Vector3>();
            var cabeza = e.transform.position + Vector3.up * 0.7f;
            for (int ang = 0; ang < 360; ang += 15)
            {
                var p = new Vector3(e.transform.position.x, 1.7f, e.transform.position.z) + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * radio;
                if (Physics.CheckSphere(p, 0.4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                bool libre = true;
                foreach (var h in Physics.RaycastAll(p, cabeza - p, Vector3.Distance(p, cabeza), ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.GetComponentInParent<TorreDestruible>() != null || h.collider.GetComponentInParent<ReflectorVigia>() != null || h.collider.GetComponentInParent<Soldier>() == e) continue;
                    libre = false; break;
                }
                if (libre) l.Add(p);
            }
            return l;
        }

        public static string Medir104Anillo(string nombre = "Enemigo_Artillero_TV2", float radio = 35f)
        {
            var e = BuscarPorNombre(nombre);
            if (e == null) return "no esta " + nombre;
            var sb = new StringBuilder();
            foreach (var p in PuntosConVista(e, radio)) sb.Append($"({p.x:0},{p.z:0}):{PixelesDelSoldadoDesde(e, p)} ");
            return nombre + " r=" + radio + " -> " + sb;
        }

        // ---------------------------------------------------------------- #104 el artillero de TV2 y los operadores de reflector se VEN
        static IEnumerator Bug104()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(1);
            foreach (var x in Esperar(2f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            // Artillero de TV2. (1) Desde la posicion del reporte (a 39 m, en el patio): ahi un edificio del cuartel tapa buena parte de la
            // torre, asi que solo se informa (antes de la correccion el cuerpo no se veia). (2) Desde puntos a 30 m con linea libre hasta su
            // cabeza (sin contar la torre): el parapeto ya no lo tapa, todos deben dar >= 150 px a 1280x720.
            var art = BuscarPorNombre("Enemigo_Artillero_TV2");
            if (art == null) { Fin("FALLO no esta el artillero de TV2"); yield break; }
            var posReporte = new Vector3(-6.6f, 1.7f, -227f);
            int pxArt = PixelesDelSoldadoDesde(art, posReporte);
            sb.Append($"artillero TV2 desde el punto del reporte ({Vector3.Distance(posReporte, art.transform.position):0} m, con edificios delante): {pxArt} px (informativo); ");
            var puntos = PuntosConVista(art, 30f);
            int minPx = int.MaxValue, maxPx = 0;
            foreach (var p in puntos) { int px = PixelesDelSoldadoDesde(art, p); minPx = Mathf.Min(minPx, px); maxPx = Mathf.Max(maxPx, px); }
            if (puntos.Count == 0 || minPx < 150) ok = false;
            sb.Append($"a 30 m con linea libre ({puntos.Count} puntos): minimo {(puntos.Count == 0 ? 0 : minPx)} px, maximo {maxPx} px (umbral 150){(puntos.Count == 0 || minPx < 150 ? " MAL" : "")}; ");
            if (puntos.Count > 0) RenderTemporal("v3_104_tv2_30m", puntos[0], art.transform.position + Vector3.up * 0.3f, 40f);
            // Luz de silueta de los enemigos en altura.
            var luzSil = art.transform.Find(PuestoElevado.NombreLuzDeSilueta);
            if (luzSil == null) ok = false;
            sb.Append($"luz de silueta={(luzSil != null)}; ");
            // Operadores de reflector (R3 y R4 estan junto a TV2): se miden desde 30 m al frente de cada uno.
            foreach (var nm in new[] { "Enemigo_Operador_R3", "Enemigo_Operador_R4" })
            {
                var op = BuscarPorNombre(nm);
                if (op == null) { sb.Append(nm + " no esta; "); ok = false; continue; }
                var desde = new Vector3(op.transform.position.x, 1.7f, op.transform.position.z + 30f);
                int px = PixelesDelSoldadoDesde(op, desde);
                if (px < 60) ok = false;
                sb.Append($"{nm} a 30 m: {px} px{(px < 60 ? " MAL (se piden >=60)" : "")}; ");
            }
            // Render de validacion desde la posicion del reporte con la camara de juego.
            RenderTemporal("v3_104_tv2_desde_patio", posReporte, art.transform.position + Vector3.up * 0.3f, 40f);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Color medio (canal maximo, 0..1) del centro de la imagen que ve 'cam' (lado 'lado' px).
        static float BrilloDelCentro(Camera cam, RenderTexture rt, int lado = 90)
        {
            cam.targetTexture = rt; cam.Render();
            var tex = Leer(rt);
            float suma = 0f; int n = 0;
            int cx = rt.width / 2, cy = rt.height / 2;
            for (int y = cy - lado / 2; y < cy + lado / 2; y++)
                for (int x = cx - lado / 2; x < cx + lado / 2; x++)
                {
                    var c = tex.GetPixel(x, y);
                    suma += Mathf.Max(c.r, Mathf.Max(c.g, c.b)); n++;
                }
            Object.Destroy(tex);
            return suma / Mathf.Max(1, n);
        }

        // ---------------------------------------------------------------- #104b romper el faro: vidrio, chispas, humo, sonido; la lente rota sigue visible
        static IEnumerator Bug104b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(1);
            foreach (var x in Esperar(2f)) yield return x;
            ReflectorVigia faro = null;
            foreach (var r in ReflectorVigia.Todos) if (r != null && r.name == "Reflector_R3") faro = r;
            if (faro == null || faro.cabeza == null) { Fin("FALLO no esta Reflector_R3"); yield break; }
            var lum = faro.cabeza.GetComponent<Luminaria>();
            var luz = faro.cabeza.GetComponent<Light>();
            var sb = new StringBuilder(); bool ok = true;
            bool visibleAntes = faro.cabeza.GetComponentInChildren<Renderer>() != null;
            // ANTES: render del faro entero y brillo de la lente.
            var rt = new RenderTexture(1280, 720, 24);
            var frente = faro.cabeza.position + faro.cabeza.forward * 9f + Vector3.up * 0.5f;
            var cam = CamaraTemporal(frente, faro.cabeza.position, 25f, rt, out var go);
            float brilloAntes = BrilloDelCentro(cam, rt);
            RenderTemporal("v3_104_faro_antes", frente, faro.cabeza.position, 25f);

            // El poseido se acerca al faro: el sonido 3D de un faro a 150 m no se reproduce (fuera de rango, ahorro de voces).
            var yo = Poseido();
            if (yo != null) { yo.transform.position = new Vector3(faro.cabeza.position.x, yo.transform.position.y, faro.cabeza.position.z - 14f); }
            yield return null;
            int escombros0 = DebrisPool.ActiveCount;
            int sonidos0 = AudioDirector.TotalReproducidos;
            int humo0 = SpriteFx.Lanzados;
            lum.TakeDamage(1, faro.cabeza.position + faro.cabeza.forward * 0.6f);
            yield return null;
            bool luzEnParpadeo = lum.transform.Find("ParpadeoDeFaro") != null;   // en el cuadro siguiente a la rotura (el editor va lento: el parpadeo dura 0,4 s de simulacion)
            int escombros = DebrisPool.ActiveCount - escombros0;
            int sonidos = AudioDirector.TotalReproducidos - sonidos0;
            int humo = SpriteFx.Lanzados - humo0;
            if (escombros < 10) ok = false;
            if (sonidos < 1) ok = false;
            if (humo < 1) ok = false;
            if (!lum.Rota) ok = false;
            sb.Append($"escombros +{escombros} (>=10), sonidos +{sonidos} (>=1), sprites de humo/chispa +{humo} (>=1), Rota={lum.Rota}; ");
            // El parpadeo dura ~0,4 s: en el cuadro siguiente hay luz cosmetica parpadeando, a ~1 s todo apagado.
            foreach (var x in Esperar(1.2f)) yield return x;
            bool luzApagada = (luz == null || !luz.enabled) && lum.transform.Find("ParpadeoDeFaro") == null;
            if (!luzApagada || !luzEnParpadeo) ok = false;
            sb.Append($"parpadeo activo al romperse={luzEnParpadeo}{(luzEnParpadeo ? "" : " MAL")}, todo apagado a 1,2 s={luzApagada}{(luzApagada ? "" : " MAL")}; ");
            // DESPUES: la lente rota se ve (no negro puro).
            float brilloDespues = BrilloDelCentro(cam, rt);
            RenderTemporal("v3_104_faro_roto", frente, faro.cabeza.position, 25f);
            if (brilloDespues <= 0.1f) ok = false;
            sb.Append($"brillo del centro (canal max) antes={brilloAntes:0.00} despues={brilloDespues:0.00} (>0,10){(brilloDespues > 0.1f ? "" : " MAL")}");
            cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(go);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
