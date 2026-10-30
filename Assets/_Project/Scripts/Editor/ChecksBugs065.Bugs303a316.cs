using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.EditorTools
{
    // Tanda de bugs #303-#316 (reportes con la tecla U del 2026-10-30). La "u" del nombre es de la tecla, para no pisar Bug303a/304a/305a
    // (etapas del rediseño del cuartel). Cada check pide un Play sobre SC_Operacion:
    //   ChecksBugs065.CorrerLista("Bug303u,Bug304u,Bug305u,Bug306u,Bug307u,Bug308u,Bug309u,Bug310u,Bug311u,Bug312u,Bug314u,Bug315u,Bug316u", true)
    public static partial class ChecksBugs065
    {
        static object CampoPrivado(object o, string nombre)
        {
            var f = o.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null ? f.GetValue(o) : null;
        }

        static Bounds BoundsDe(GameObject g)
        {
            var b = new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) b.Encapsulate(c.bounds);
            return b;
        }

        static GameObject Buscar(string nombre)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == nombre && t.gameObject.scene.IsValid()) return t.gameObject;
            return null;
        }

        // ---------------------------------------------------------------- 303u "seguime": los aliados llegan a su ranura y se quedan (la separacion no los saca)
        static IEnumerator Bug303u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            Time.timeScale = 1f;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var kes = Escuadrista(RoleType.Flanker); var doc = Escuadrista(RoleType.Medic);
            var drv = PlayerInputDriver.Activo;
            if (vega == null || kes == null || doc == null || drv == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            try
            {
                // Sin combate: este check mide solo la formacion de "seguime" (con enemigos cerca los aliados correrian a pelear).
                foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy) s.gameObject.SetActive(false);
                Poseer(vega); yield return null;
                vega.transform.position = new Vector3(-20.4f, vega.transform.position.y, -279.6f);
                ApoyoEnElPiso.Apoyar(vega.transform);
                // La escuadra arranca lejos (el Play empieza en la entrada): se la trae a 6 m del lider, como en el reporte.
                Junto(kes, vega.transform.position, 6f, vega.transform.position + new Vector3(4f, 0f, -4f));
                Junto(doc, vega.transform.position, 6f, vega.transform.position + new Vector3(-4f, 0f, -4f));
                yield return null;
                // ANTES (comportamiento viejo, separacion de 3,2 m contra el lider): se mide 8 s despues de la orden, informativo.
                SP.Ai.AiBrain.SeparacionAlSeguirActiva = false;
                int n = drv.SeguirTodosSiOSi();
                foreach (var x in Esperar(8f)) yield return x;
                {
                    int e0 = kes.Brain.EmpujesDeSeparacion + doc.Brain.EmpujesDeSeparacion; var qk = kes.transform.position; var qd = doc.transform.position; float rec = 0f;
                    float ta = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - ta < 3f) { yield return null; rec += (kes.transform.position - qk).magnitude + (doc.transform.position - qd).magnitude; qk = kes.transform.position; qd = doc.transform.position; }
                    sb.Append($"ANTES (sin el arreglo): recorrido {rec:0.0} m en 3 s con el lider quieto, empujes={kes.Brain.EmpujesDeSeparacion + doc.Brain.EmpujesDeSeparacion - e0}, a {Vector3.Distance(kes.transform.position, vega.transform.position):0.0} y {Vector3.Distance(doc.transform.position, vega.transform.position):0.0} m del lider; ");
                }
                SP.Ai.AiBrain.SeparacionAlSeguirActiva = true;
                float tLlegada = Time.realtimeSinceStartup; float tiempoLlegada = -1f;
                while (Time.realtimeSinceStartup - tLlegada < 25f && tiempoLlegada < 0f)
                {
                    yield return null;
                    if (Vector3.Distance(kes.transform.position, vega.transform.position) < 3f && Vector3.Distance(doc.transform.position, vega.transform.position) < 3f) tiempoLlegada = Time.realtimeSinceStartup - tLlegada;
                }
                foreach (var x in Esperar(1.5f)) yield return x;
                int empujes0 = kes.Brain.EmpujesDeSeparacion + doc.Brain.EmpujesDeSeparacion;
                var pk = kes.transform.position; var pd = doc.transform.position;
                float recorrido = 0f;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3f)
                {
                    yield return null;
                    recorrido += (kes.transform.position - pk).magnitude + (doc.transform.position - pd).magnitude;
                    pk = kes.transform.position; pd = doc.transform.position;
                }
                int empujes = kes.Brain.EmpujesDeSeparacion + doc.Brain.EmpujesDeSeparacion - empujes0;
                float dk = Vector3.Distance(kes.transform.position, vega.transform.position), dd = Vector3.Distance(doc.transform.position, vega.transform.position);
                W8Ok(ref ok, n >= 2, sb, $"SeguirTodos ordeno a {n} aliados (pide >= 2)");
                W8Ok(ref ok, recorrido < 0.6f, sb, $"con el lider quieto los aliados no se mueven (recorrido {recorrido:0.00} m en 3 s, pide < 0,6)");
                W8Ok(ref ok, empujes == 0, sb, $"sin empujes de separacion contra el lider (empujes={empujes})");
                W8Ok(ref ok, dk < 3f && dd < 3f, sb, $"llegaron a su ranura (Kes {dk:0.0} m, Doc {dd:0.0} m del lider, pide < 3)");
                foreach (var x in CapturarPantalla("v2_303_despues.png")) yield return x;
            }
            finally { SP.Ai.AiBrain.SeparacionAlSeguirActiva = true; Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 304u iconos del radial por tipo de soldado, sin fuga de sprites
        static IEnumerator Bug304u()
        {
            var sb = new StringBuilder(); bool ok = true;
            for (int sub = 1; sub <= 3; sub++)
                W8Ok(ref ok, MenuDeOrdenes.SoldadoDeOpcion(MenuDeOrdenes.IrAlli, sub) == sub - 1, sb, $"IR ALLI opcion {sub} nombra al soldado {sub}");
            W8Ok(ref ok, MenuDeOrdenes.SoldadoDeOpcion(MenuDeOrdenes.IrAlli, 0) == -1, sb, "IR ALLI 'TODOS' no nombra a nadie");
            var a = RadialIconFactory.ForOpcion(MenuDeOrdenes.Atacar, MenuDeOrdenes.SubSuprimir);
            var b = RadialIconFactory.ForOpcion(MenuDeOrdenes.Atacar, MenuDeOrdenes.SubSuprimir);
            W8Ok(ref ok, a != null && ReferenceEquals(a, b), sb, "ForOpcion(Atacar, Suprimir) devuelve el mismo sprite (sin fuga por cuadro)");
            var c = RadialIconFactory.ForOpcion(MenuDeOrdenes.Tanque, 1); var d = RadialIconFactory.ForOpcion(MenuDeOrdenes.Tanque, 1);
            W8Ok(ref ok, c != null && ReferenceEquals(c, d), sb, "ForOpcion(Tanque, Bajar) devuelve el mismo sprite");
            var yoDemuelo = RadialIconFactory.ForOpcion(MenuDeOrdenes.Demoler, 1);
            W8Ok(ref ok, yoDemuelo != null && !ReferenceEquals(yoDemuelo, RadialIconFactory.ForCategoria(MenuDeOrdenes.Poseer)), sb, "DEMOLER 'yo demuelo' no usa la persona generica");
            var montar = RadialIconFactory.ForOpcion(MenuDeOrdenes.Torreta, 2);
            W8Ok(ref ok, montar != null && !ReferenceEquals(montar, RadialIconFactory.ForCategoria(MenuDeOrdenes.Poseer)), sb, "TORRETA 'que lo monten' no usa la persona generica");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 305u sin el bloque de sacos del callejon
        static IEnumerator Bug305u()
        {
            var sb = new StringBuilder(); bool ok = true; int restos = 0;
            var caja = new Bounds(new Vector3(-30f, 0.8f, -258.5f), new Vector3(6f, 3f, 5f));
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.gameObject.scene.IsValid() || !t.name.Contains("Saco")) continue;
                var r = t.GetComponent<Renderer>(); var c = t.GetComponent<Collider>();
                if ((r != null && caja.Intersects(r.bounds)) || (c != null && caja.Intersects(c.bounds))) restos++;
            }
            W8Ok(ref ok, restos == 0, sb, $"no quedan sacos de arena en el callejon (restos={restos})");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 306u el generador se demuele rapido y de una vez
        static IEnumerator Bug306u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            Time.timeScale = 1f;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault);
            if (d == null || d.generador == null || d.generador.marca == null || vega == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            var g = d.generador;
            try
            {
                W8Ok(ref ok, Mathf.Approximately(Demolicion.SegundosPara(g.marca), Demolicion.SegundosDelGenerador), sb, $"el generador tarda {Demolicion.SegundosPara(g.marca):0.0} s (pide {Demolicion.SegundosDelGenerador:0.0})");
                Poseer(vega); yield return null;
                Junto(vega, g.marca.transform.position, 3f, vega.transform.position);
                var dem = vega.GetComponent<DemoledorAsalto>() ?? vega.gameObject.AddComponent<DemoledorAsalto>();
                bool inicio = dem.IniciarAutomatico(g.marca);
                float t0 = Time.realtimeSinceStartup;
                while (!g.EstaCaido && Time.realtimeSinceStartup - t0 < 4f) yield return null;
                float tardo = Time.realtimeSinceStartup - t0;
                W8Ok(ref ok, inicio && g.EstaCaido && tardo < 3f, sb, $"Q arranca la carga sola y el generador cae en {tardo:0.0} s (inicio={inicio}, pide < 3 s)");
                int partes = 0, caidas = 0;
                if (g.partes != null) foreach (var p in g.partes) if (p != null) { partes++; if (p.IsCollapsed) caidas++; }
                W8Ok(ref ok, caidas == partes, sb, $"todas las partes del generador caen juntas ({caidas}/{partes})");
                if (g.derribaTorresAlCaer) W8Ok(ref ok, d.TorresCaidas(TorreDestruible.Tipo.Torreta) + d.TorresCaidas(TorreDestruible.Tipo.Francotirador) == OperacionDirector.TorresDeTorreta + OperacionDirector.TorresDeFrancotirador, sb, "cayeron torres y torretas");
            }
            finally { Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 307u el rombo salta a la carga del porton y el generador deja restos
        static IEnumerator Bug307u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            if (d == null || d.generador == null || d.cargaPorton == null) { Fin("FALLO faltan piezas"); yield break; }
            d.generador.marca.Demoler();
            foreach (var x in Esperar(1.2f)) yield return x;
            var baliza = CampoPrivado(d, "baliza") as Component;
            float dist = baliza != null ? Vector2.Distance(new Vector2(baliza.transform.position.x, baliza.transform.position.z), new Vector2(d.cargaPorton.transform.position.x, d.cargaPorton.transform.position.z)) : 999f;
            W8Ok(ref ok, baliza != null && dist < 1.5f, sb, $"el rombo apunta a la carga del porton (distancia {dist:0.0} m, pide < 1,5)");
            var restos = Buscar("Generador_Restos");
            W8Ok(ref ok, restos != null && restos.activeInHierarchy && Vector3.Distance(restos.transform.position, d.generador.transform.position) < 2f, sb, "el generador deja restos en su lugar");
            foreach (var x in CapturarPantalla("v2_307_despues.png")) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 308u Q sobre un interactuable que solo vos podes operar: cartel vibrante y no cae en SEGUIR
        static IEnumerator Bug308u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            Time.timeScale = 1f;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var drv = PlayerInputDriver.Activo;
            if (d == null || d.generador == null || d.cargaPorton == null || vega == null || drv == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            try
            {
                d.generador.Caer();
                Poseer(vega); yield return null;
                Junto(vega, d.cargaPorton.transform.position, 9f, vega.transform.position);
                // Los aliados NO pueden operar la carga (solo el Asalto, y el Asalto sos vos): se los saca de la escena de la prueba.
                var kes = Escuadrista(RoleType.Flanker); var doc = Escuadrista(RoleType.Medic);
                var estK = kes != null ? kes.Brain.State : default; var estD = doc != null ? doc.Brain.State : default;
                var aim = new AimResult { Type = AimTargetType.Interactuar, HitTransform = d.cargaPorton.transform, Point = d.cargaPorton.transform.position };
                string accion = drv.AccionContextualDeQ(aim);
                yield return null;
                W8Ok(ref ok, accion == "ACERCATE", sb, $"la accion es 'ACERCATE' (devolvio '{accion}')");
                string cartel = AvisoUnico.TextoActual;
                W8Ok(ref ok, cartel.Contains("ACERCATE") && cartel.Contains("["), sb, $"cartel vibrante con la tecla: '{cartel}'");
                W8Ok(ref ok, (kes == null || kes.Brain.State == estK) && (doc == null || doc.Brain.State == estD), sb, "ningun aliado cambio de estado (no cayo en SEGUIR)");
                foreach (var x in CapturarPantalla("v2_308_despues.png")) yield return x;
            }
            finally { Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 309u el porton blindado vuela con una cinematica de 4 s
        static IEnumerator Bug309u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            Time.timeScale = 1f;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault);
            var arte = Object.FindFirstObjectByType<PortonBlindadoAnimado>();
            if (d == null || vega == null || d.puestos == null || d.puestos.Length < 1) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            try
            {
                W8Ok(ref ok, arte != null && arte.hojaIzq != null && arte.hojaDer != null, sb, "existe el arte del porton con sus dos hojas");
                Poseer(vega); yield return null;
                var carga = d.puestos[0].carga;
                Junto(vega, carga.transform.position, 2f);
                carga.PlantarPorPrueba(vega);
                carga.AcortarMecha(2f);
                float t0 = Time.realtimeSinceStartup;
                while (!carga.EstaVolada && Time.realtimeSinceStartup - t0 < 8f) yield return null;
                float tVolo = Time.realtimeSinceStartup;
                foreach (var x in Esperar(0.5f)) yield return x;
                W8Ok(ref ok, CineDeHuida.Activa, sb, "la cinematica esta activa a los 0,5 s");
                W8Ok(ref ok, d.Fase == FaseOperacion.CentroDeDatos, sb, $"fase {d.Fase} (pide CentroDeDatos)");
                foreach (var x in CapturarPantalla("v2_309_cine.png")) yield return x;
                foreach (var x in Esperar(1.0f)) yield return x;
                W8Ok(ref ok, d.portonBlindado != null && !d.portonBlindado.activeSelf, sb, "el bloqueo invisible se libero a los ~1,5 s");
                while (CineDeHuida.Activa && Time.realtimeSinceStartup - tVolo < 8f) yield return null;
                float dur = Time.realtimeSinceStartup - tVolo;
                W8Ok(ref ok, dur >= 3.7f && dur <= 4.4f, sb, $"la cinematica dura {dur:0.0} s (pide 4,0 +-0,3)");
                if (arte != null)
                {
                    float yi = Mathf.Abs(Mathf.DeltaAngle(0f, arte.hojaIzq.localEulerAngles.y)), yd = Mathf.Abs(Mathf.DeltaAngle(0f, arte.hojaDer.localEulerAngles.y));
                    W8Ok(ref ok, arte.Abierto && yi > 80f && yd > 80f, sb, $"las hojas abiertas (izq {yi:0} deg, der {yd:0} deg, pide > 80)");
                }
                foreach (var x in CapturarPantalla("v2_309_abierto.png")) yield return x;
            }
            finally { Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 310u las computadoras y los gabinetes no se atraviesan
        static IEnumerator Bug310u()
        {
            var sb = new StringBuilder(); bool ok = true; int muebles = 0, sinCollider = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.gameObject.scene.IsValid() || t.GetComponent<MeshRenderer>() == null) continue;
                if (t.name != "Escritorio" && t.name != "Consola" && t.name != "UPS" && t.name != "Gabinete") continue;
                var padre = t.parent != null ? t.parent.name : "";
                if (padre != "PuestosPC" && padre != "PuestosNorte" && padre != "ParedesDeVideo" && padre != "Equipamiento" && padre != "Gabinetes") continue;
                muebles++;
                var c = t.GetComponent<Collider>();
                if (c == null || !c.enabled || c.isTrigger) sinCollider++;
            }
            W8Ok(ref ok, muebles > 0 && sinCollider == 0, sb, $"los {muebles} escritorios, consolas, UPS y gabinetes tienen collider (sin collider={sinCollider})");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 311u el tanque no dispara hasta que salis del Centro de Datos
        static IEnumerator Bug311u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(4, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            Time.timeScale = 1f;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault);
            if (d == null || d.Jefe == null || vega == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas (jefe)"); yield break; }
            try
            {
                Poseer(vega); yield return null;
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Team == TeamId.Player && s.gameObject.activeInHierarchy) { s.transform.position = new Vector3(-9.5f, s.transform.position.y, -54f); ApoyoEnElPiso.Apoyar(s.transform); }
                float t0 = Time.realtimeSinceStartup;
                while ((d.TomaDelJefeActiva || !d.TomaDelJefeHecha) && Time.realtimeSinceStartup - t0 < 12f) yield return null;
                foreach (var x in Esperar(10f)) yield return x;
                W8Ok(ref ok, d.Jefe.Canonazos == 0 && d.Jefe.InicioDeAvisos.Count == 0 && !d.Jefe.FuegoHabilitado, sb, $"con la escuadra adentro del Centro de Datos el jefe no dispara (canonazos={d.Jefe.Canonazos}, avisos={d.Jefe.InicioDeAvisos.Count})");
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Team == TeamId.Player && s.gameObject.activeInHierarchy) { s.transform.position = new Vector3(0f, s.transform.position.y, -24f); ApoyoEnElPiso.Apoyar(s.transform); }
                float t1 = Time.realtimeSinceStartup;
                while (d.Jefe.InicioDeAvisos.Count == 0 && Time.realtimeSinceStartup - t1 < 12f) yield return null;
                W8Ok(ref ok, d.Jefe.FuegoHabilitado && d.Jefe.InicioDeAvisos.Count >= 1, sb, $"al salir empieza a disparar (avisos={d.Jefe.InicioDeAvisos.Count} a los {Time.realtimeSinceStartup - t1:0.0} s)");
            }
            finally { Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 312u el rotulo DEMOLER se va con el muro de la salida norte
        static IEnumerator Bug312u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            var muro = Buscar("Muro_Salida_Norte");
            var rotulo = Buscar("Rotulo_SALIDA NORTE · DEMOLER");
            if (d == null || muro == null) { Fin("FALLO faltan piezas"); yield break; }
            var caja = BoundsDe(muro); caja.Expand(0.2f);
            d.AbrirSalidaNortePorPrueba(true);
            foreach (var x in Esperar(3f)) yield return x;
            W8Ok(ref ok, !muro.activeSelf, sb, "el muro cayo");
            W8Ok(ref ok, rotulo == null || !rotulo.activeInHierarchy, sb, "el rotulo DEMOLER ya no flota");
            int flotando = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || r.GetComponentInParent<Fragmento>() != null || r.bounds.size.y < 0.05f) continue;
                if (!caja.Contains(r.bounds.center) || r.bounds.center.y < 0.3f) continue;
                if (r.GetComponentInParent<Soldier>() != null) continue;
                if (!(r.name.Contains("Rotulo") || r.name == "Placa" || r.name == "Texto" || r.name.Contains("DEMOLER"))) continue;
                flotando++;
            }
            W8Ok(ref ok, flotando == 0, sb, $"nada queda flotando donde estaba el muro (piezas={flotando})");
            foreach (var x in CapturarPantalla("v2_312_despues.png")) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 314u al demoler Rack_5 se va entero (LEDs, tapa y cables incluidos)
        static IEnumerator Bug314u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var rack = Buscar("Rack_5"); var marca = rack != null ? rack.GetComponent<ObstacleMarker>() : null;
            if (marca == null) { Fin("FALLO no esta Rack_5"); yield break; }
            var caja = BoundsDe(rack); caja.Expand(0.3f);
            marca.Demoler();
            foreach (var x in Esperar(2f)) yield return x;
            int sueltos = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || r.GetComponentInParent<Fragmento>() != null) continue;
                if (r.name != "Led" && r.name != "Tapa" && r.name != "Cables") continue;
                if (caja.Contains(r.bounds.center) && r.bounds.center.y > 0.2f) sueltos++;
            }
            W8Ok(ref ok, marca.IsCollapsed && sueltos == 0, sb, $"no quedan LEDs, tapa ni cables flotando donde estaba el rack (sueltos={sueltos})");
            foreach (var x in CapturarPantalla("v2_314_despues.png")) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 315u terminal de mando: arte a escala humana, sin paneles flotantes
        static IEnumerator Bug315u()
        {
            var sb = new StringBuilder(); bool ok = true;
            var arte = Buscar("ArteTerminal");
            W8Ok(ref ok, arte != null && arte.GetComponentsInChildren<Renderer>(true).Length >= 8, sb, $"existe ArteTerminal con >= 8 piezas ({(arte != null ? arte.GetComponentsInChildren<Renderer>(true).Length : 0)})");
            int viejos = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.gameObject.scene.IsValid() && (t.name == "Panel_Central" || t.name == "Panel_Izq" || t.name == "Panel_Der" || t.name == "Soporte_Central")) viejos++;
            W8Ok(ref ok, viejos == 0, sb, $"los paneles flotantes viejos no existen (quedan {viejos})");
            GameObject monitor = null; foreach (var tm in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (tm.name == "Monitor" && tm.parent != null && tm.parent.name == "Computadora") { monitor = tm.gameObject; break; }
            W8Ok(ref ok, monitor != null && monitor.transform.lossyScale.x < 2.6f, sb, $"el monitor central ya no es gigante (ancho {(monitor != null ? monitor.transform.lossyScale.x : 0f):0.0} m)");
            if (arte != null)
            {
                int flota = 0;
                foreach (var r in arte.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds; var o = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
                    bool apoyo = Physics.Raycast(o, Vector3.down, out var h, 0.25f, ~0, QueryTriggerInteraction.Ignore);
                    if (!apoyo)
                        foreach (var otro in arte.GetComponentsInChildren<Renderer>(true))
                            if (otro != r && otro.bounds.Intersects(new Bounds(b.center - Vector3.up * (b.extents.y + 0.1f), new Vector3(b.size.x, 0.25f, b.size.z)))) { apoyo = true; break; }
                    if (!apoyo && b.min.y > 0.4f) flota++;
                }
                W8Ok(ref ok, flota == 0, sb, $"ninguna pieza del arte flota sin apoyo (flotando={flota})");
            }
            foreach (var x in CapturarPantalla("v2_315_despues.png")) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 316u el camino del tanque esta limpio
        static IEnumerator Bug316u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(4, 1, 4);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            if (d == null || d.rutaDelTanque == null) { Fin("FALLO faltan piezas"); yield break; }
            d.DespejarRutaDelTanque();
            int visibles = 0; string ejemplo = ""; var vistosR = new System.Collections.Generic.HashSet<Collider>();
            var prev = d.tanque != null ? d.tanque.transform.position : Vector3.zero;
            foreach (var t in d.rutaDelTanque)
            {
                if (t == null) continue;
                var dir = t.position - prev; float largo = dir.magnitude;
                for (float s = 0f; s <= largo; s += 2f)
                {
                    var centro = prev + dir.normalized * s + Vector3.up * 1.4f;   // mismo corredor que DespejarRutaDelTanque
                    foreach (var r in Physics.OverlapBox(centro, new Vector3(1.7f, 1.1f, 1.4f), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).normalized, Vector3.up)))
                    {
                        string n = r.name;
                        if (r is TerrainCollider || !r.enabled || r.isTrigger || !vistosR.Add(r)) continue;
                        var marca = r.GetComponentInParent<ObstacleMarker>();
                        if (marca != null || n.StartsWith("Barricada") || n.Contains("Auto_Quemado") || n.Contains("Escombro") || n.Contains("Neumatico") || n.Contains("Poste_Caido") || n.Contains("Erizo")) { visibles++; if (ejemplo.Length == 0) ejemplo = n + "@" + r.bounds.center + " act=" + r.gameObject.activeInHierarchy; }
                    }
                }
                prev = t.position;
            }
            W8Ok(ref ok, visibles == 0, sb, $"sin barricadas ni props con colision sobre la ruta del tanque (restos={visibles}{(ejemplo.Length > 0 ? " ej: " + ejemplo : "")})");
            foreach (var x in CapturarPantalla("v2_316_despues.png")) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }
    }
}
