using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Tanda de bugs #298-#302 (pedidos de juego del 2026-10-27). Cada check pide un Play sobre SC_Operacion:
    //   ChecksBugs065.CorrerLista("Bug298a,Bug299a,Bug301a,Bug302a", true)
    //   298a las piezas modulares del kit no tienen caras duplicadas que parpadeen   299a sin disco amarillo bajo las cargas
    //   301a bandejas, ductos y paneles pegados al techo de la sala                  302a la camioneta enemiga que nace en juego sale en el minimapa
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 298a z-fighting: mallas limpias y sin caras pisadas dentro de una misma pieza
        static IEnumerator Bug298a()
        {
            var sb = new StringBuilder(); bool ok = true;
            int sinLimpiar = 0, limpias = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.sharedMesh.name;
                if (n.EndsWith("_Limpia")) limpias++;
                else if (n == "SM_Mod_Muro_Recto" || n == "SM_Mod_Muro_Medio" || n == "SM_Mod_Muro_Pilar" || n == "SM_Mod_Muro_Puerta" || n == "SM_Mod_Losa") sinLimpiar++;
            }
            W8Ok(ref ok, limpias > 1000 && sinLimpiar == 0, sb, $"las piezas del kit usan la malla limpia (limpias={limpias}, con la original={sinLimpiar})");

            // Una pieza consigo misma: dos caras iguales en el mismo plano que se pisan. Solo cuentan las del kit; los pisos fusionados se separan aparte.
            var l = DetectorDeSolapes.Escanear(new Bounds(new Vector3(0f, 6f, -60f), new Vector3(60f, 20f, 60f)), 2000, 0.004f, 0.05f);
            int mismos = 0;
            foreach (var s in l) if (s.mismaMalla && s.a.Contains("SM_Mod_")) mismos++;
            W8Ok(ref ok, mismos == 0, sb, $"el kit del centro de datos no tiene caras duplicadas dentro de una misma pieza (encontradas {mismos})");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 299a la placa amarilla (el "circulo") y sus bandas ya no existen
        static IEnumerator Bug299a()
        {
            var sb = new StringBuilder(); bool ok = true; int cargas = 0, restos = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (!t.name.StartsWith("Carga_")) continue;
                cargas++;
                foreach (Transform h in t) if (h.name == "Placa" || h.name.StartsWith("Banda_")) restos++;
            }
            W8Ok(ref ok, cargas >= 2 && restos == 0, sb, $"las {cargas} cargas no tienen placa ni bandas en el piso (restos={restos})");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 301a las "vigas" del techo cuelgan pegadas al techo
        static IEnumerator Bug301a()
        {
            var sb = new StringBuilder(); bool ok = true;
            var techo = GameObject.Find("Techo_CD"); var raiz = GameObject.Find("9_Servidores");
            if (techo == null || raiz == null || raiz.transform.Find("Techo") == null) { Fin("FALLO no estan Techo_CD o 9_Servidores/Techo"); yield break; }
            float techoY = techo.GetComponent<Renderer>().bounds.min.y;
            int sueltas = 0, sobrepasan = 0, total = 0;
            foreach (Transform h in raiz.transform.Find("Techo"))
            {
                var r = h.GetComponent<Renderer>(); if (r == null) continue;
                total++;
                if (r.bounds.max.y > techoY) sobrepasan++;
                bool pegada = h.name == "Bandeja" || h.name == "Ducto" || h.name == "Panel_Luz";
                if (pegada && r.bounds.max.y < techoY - 0.03f) sueltas++;
            }
            int viejas = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if ((t.name == "Viga" || t.name == "VigaT" || t.name == "LuzTecho") && t.parent != null && t.parent.name == "ArteExtra") viejas++;
            W8Ok(ref ok, viejas == 0, sb, $"no quedan las vigas viejas bajas ni sus luces de y=3 (quedan {viejas})");
            W8Ok(ref ok, total > 40, sb, $"hay piezas de techo ({total})");
            W8Ok(ref ok, sueltas == 0, sb, $"bandejas, ductos y paneles tocan el techo (a menos de 3 cm de y={techoY:0.00}); sueltas={sueltas}");
            W8Ok(ref ok, sobrepasan == 0, sb, $"ninguna pieza atraviesa el techo (sobrepasan={sobrepasan})");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 303a (etapa A) aliados azules y enemigos rojo oxido: tono distinto, trazadoras por bando
        static IEnumerator Bug303a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1, 0);
            foreach (var x in Esperar(1f)) yield return x;
            float hA = -1f, hE = -1f;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Role == RoleType.Civilian) continue;
                var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null || smr.sharedMaterial == null) continue;
                var mc = smr.sharedMaterial.color; Color.RGBToHSV(mc, out float ht, out _, out _);
                if (s.Team == TeamId.Player && hA < 0f) hA = ht; else if (s.Team == TeamId.Enemy && hE < 0f) hE = ht;
            }
            float d = Mathf.Abs(hA - hE); d = Mathf.Min(d, 1f - d) * 360f;
            W8Ok(ref ok, hA >= 0f && hE >= 0f && d > 120f, sb, $"el tinte de aliados y enemigos separa mas de 120 grados (aliado={hA * 360f:0}, enemigo={hE * 360f:0}, dif={d:0})");
            var a = Projectile.ColorDeTrazadoraAliada; var e = Projectile.ColorDeTrazadoraEnemiga;
            W8Ok(ref ok, a.b > a.r && e.r > e.b, sb, "trazadora aliada fria y enemiga calida");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 304a (etapa C) destruir el generador apaga los reflectores y abre el porton del cuartel
        static IEnumerator Bug304a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1, 0);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, d != null && d.generador != null && d.Fase == FaseOperacion.Infiltrar, sb, "hay generador y la fase es Infiltrar");
            if (d == null || d.generador == null) { Fin("FALLO " + sb); yield break; }
            var hud = OperacionHud.Instancia;
            W8Ok(ref ok, (hud.TextoTitulo ?? "").Contains("CORTÁ LA ENERGÍA"), sb, $"HUD '{hud.TextoTitulo}'");
            W8Ok(ref ok, d.portonCuartel.activeSelf && d.ReflectoresRotos == 0, sb, "antes: porton cerrado y reflectores encendidos");
            d.generador.marca.Demoler();
            foreach (var x in Esperar(2.5f)) yield return x;
            W8Ok(ref ok, d.generador.EstaCaido && d.ReflectoresRotos == OperacionDirector.TotalDeReflectores, sb, $"generador caido={d.generador.EstaCaido}, reflectores rotos={d.ReflectoresRotos}/4");
            W8Ok(ref ok, d.Fase == FaseOperacion.Puestos, sb, $"la fase paso a {d.Fase}");
            foreach (var x in Esperar(1.5f)) yield return x;
            W8Ok(ref ok, !d.portonCuartel.activeSelf, sb, "el porton del cuartel se abrio");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 305a (etapa D) una pieza unica por sector y menos repeticion de props en el cuartel
        static IEnumerator Bug305a()
        {
            var sb = new StringBuilder(); bool ok = true;
            foreach (var n in new[] { "Generador_Cuartel", "Grua_Deposito", "Mastil_Barracas", "Parabolica_Comando" })
                W8Ok(ref ok, GameObject.Find(n) != null, sb, $"existe la pieza unica {n}");
            var zona = new Bounds(new Vector3(0f, 2f, -285f), new Vector3(150f, 12f, 130f));
            var props = new System.Collections.Generic.List<MeshRenderer>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (r.enabled && mf != null && mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("SM_Env_") && zona.Contains(r.bounds.center)) props.Add(r);
            }
            int peor = 0; string donde = "";
            foreach (var r in props)
            {
                int n = 0; var m = r.GetComponent<MeshFilter>().sharedMesh;
                foreach (var o in props) if (o.GetComponent<MeshFilter>().sharedMesh == m && (o.bounds.center - r.bounds.center).sqrMagnitude < 400f) n++;
                if (n > peor) { peor = n; donde = m.name + " en " + r.bounds.center; }
            }
            // Antes de la etapa D el peor racimo eran 38 cajas iguales en 20 m (entrada del cuartel).
            W8Ok(ref ok, peor <= 26, sb, $"peor racimo de props iguales en 20 m = {peor} ({donde}); tope 26");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 302a una camioneta enemiga que nace en juego tiene icono rojo en el minimapa
        static IEnumerator Bug302a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia;
            var m = typeof(OperacionDirector).GetMethod("InstanciarCamioneta", BindingFlags.NonPublic | BindingFlags.Instance);
            var go = (GameObject)m.Invoke(d, new object[] { d.tanque.transform.position + new Vector3(12f, 0f, 0f), Quaternion.identity });
            foreach (var x in Esperar(0.6f)) yield return x;
            var v = go.GetComponent<Vehicle>();
            var icono = MinimapIcon.BuscarPorTarget(v.transform);
            W8Ok(ref ok, v != null && v.Bando == TeamId.Enemy, sb, "la camioneta de la plantilla es enemiga");
            W8Ok(ref ok, icono != null, sb, "la camioneta que nace en juego tiene icono de minimapa");
            if (icono != null)
            {
                var c = icono.GetComponent<MeshRenderer>().sharedMaterial.color; var e = DiamondGizmo.ColorEnemigo;
                W8Ok(ref ok, Mathf.Abs(c.r - e.r) < 0.1f && Mathf.Abs(c.g - e.g) < 0.1f && Mathf.Abs(c.b - e.b) < 0.1f, sb, $"el icono es del color enemigo aunque la maneje la IA sin ocupantes (color={c})");
                W8Ok(ref ok, icono.IsRendered, sb, "el icono se dibuja");
            }
            W8Ok(ref ok, WorldUiDirector.RadioDeVehiculoPropio >= 60f, sb, $"el vehiculo propio ve a {WorldUiDirector.RadioDeVehiculoPropio:0} m (antes 45)");
            Object.Destroy(go);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
