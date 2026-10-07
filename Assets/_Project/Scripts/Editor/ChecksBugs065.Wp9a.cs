using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // WP9a: rediseno de los Puestos (#082, #085 parte puestos, #084 con la carga) y la cinematica inicial (#097 parte 1).
    // Hay que estar en Play sobre SC_Operacion y conviene un Play fresco por check (ChecksBugs065.Correr(82) corre a, b y c en orden).
    //   082a  estructura: 2 puestos con carga, 5 guardias, nido y 6 reservas inactivas cada uno; 2 de trinchera y 3 de corredor; 1 sola capa
    //         (nadie del contraataque activo al empezar); cargas, camioneta y entrada alcanzables; el Centro de Datos no (porton blindado).
    //   082b  el puesto Oeste de punta a punta con [E] real: el Flanqueador no la coloca, el Asalto si (3,5 s), arde 20 s, contraataque de 4,
    //         un enemigo pegado a la carga NO la desactiva, estalla: bunker colapsado, nido y guardias del radio muertos.
    //   082c  cadena: Este primero y Oeste despues (orden libre); la segunda trae 6 y una camioneta; al volar los dos se hunde el porton
    //         y la fase pasa al Centro de Datos con camino libre.
    //   082d  la orden [Q] (EnviarA): solo el Asalto puede; el aliado camina, coloca solo y sale el contraataque.
    //   085b  los 12 guardias de los puestos estan atrincherados, en cobertura de verdad, pegados a un solido, y ninguno sale a cazar sin ver.
    //   097a  cinematica: bloquea teclas (W, Tab, E, Q...), muestra titulo y el paso 2 con su texto, [ESPACIO] 1 s la salta y devuelve el control.
    //   097b  HUD de pasos de la Operacion: lista de 6 con el actual marcado y plegado/desplegado con la tecla V.
    //   097i  cinematica completa (sin saltar): los 6 pasos en orden con su texto, "presiona una tecla", bloqueo hasta el final.
    public static partial class ChecksBugs065
    {
        const string W9Aereas = "v2_097a_";

        static Soldier W9Yo()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        static string W9Nav(Vector3 desde, Vector3 p, out NavMeshPathStatus estado)
        {
            estado = NavMeshPathStatus.PathInvalid;
            if (!NavMesh.SamplePosition(p, out var h, 1.5f, NavMesh.AllAreas)) return "sin-navmesh";
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(desde, h.position, NavMesh.AllAreas, path)) return "sin-ruta";
            estado = path.status;
            return path.status.ToString();
        }

        static int W9Vivos(IEnumerable<Soldier> l)
        {
            int n = 0;
            if (l != null) foreach (var s in l) if (s != null && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) n++;
            return n;
        }

        static void W9Aerea(Vector3 centro, string archivo, float altura = 42f, float atras = 30f, float fov = 60f)
        {
            var pos = centro + new Vector3(0f, altura, -atras);
            CapturarDesde(pos, Quaternion.LookRotation(centro + Vector3.up * 2f - pos, Vector3.up), archivo, 1600, 900, fov);
        }

        // ---------------------------------------------------------------- 082a estructura
        static IEnumerator Bug082a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var sb = new StringBuilder(); bool ok = true;
            if (d == null || d.puestos == null) { Fin("FALLO sin director o sin puestos"); yield break; }
            W8Ok(ref ok, d.puestos.Length == OperacionDirector.TotalDePuestos && OperacionDirector.TotalDePuestos == 2, sb, $"2 puestos ({d.puestos.Length})");
            var entrada = d.entradas[1].position;
            for (int i = 0; i < d.puestos.Length; i++)
            {
                var p = d.puestos[i];
                bool piezas = p != null && p.carga != null && p.nido != null && p.guardias != null && p.contraataque != null && p.contraataque.soldados != null;
                W8Ok(ref ok, piezas, sb, $"{p?.nombre}: carga+nido+guardias+reservas");
                if (!piezas) continue;
                bool rolAsalto = p.carga.rol == RoleType.Assault;
                int vivos = W9Vivos(p.guardias);
                int reservasActivas = 0; foreach (var s in p.contraataque.soldados) if (s != null && s.gameObject.activeSelf) reservasActivas++;
                W8Ok(ref ok, rolAsalto && vivos == 5 && p.contraataque.soldados.Length == 6 && reservasActivas == 0, sb,
                    $"{p.lado}: rol={p.carga.rol} guardias vivos={vivos}/5 reservas={p.contraataque.soldados.Length} (activas {reservasActivas}, pide 0: una sola capa) mecha={p.carga.segundosDeMecha:0} s plantado={p.carga.duracion:0.0} s");
                var punto = p.carga.transform.position;
                string alc = W9Nav(entrada, punto + Vector3.back * 1.8f, out var est);
                W8Ok(ref ok, est == NavMeshPathStatus.PathComplete, sb, $"{p.lado}: carga alcanzable desde la entrada ({alc})");
                string cam1 = W9Nav(entrada, p.salidaCamioneta.position, out var e1), cam2 = W9Nav(entrada, p.paradaCamioneta.position, out var e2);
                W8Ok(ref ok, e1 == NavMeshPathStatus.PathComplete && e2 == NavMeshPathStatus.PathComplete, sb, $"{p.lado}: salida/parada de la camioneta alcanzables ({cam1}/{cam2})");
            }
            float sep = Mathf.Abs(d.puestos[0].carga.transform.position.x - d.puestos[1].carga.transform.position.x);
            W8Ok(ref ok, sep > 60f && sep < 100f, sb, $"puestos separados {sep:0} m (flancos opuestos de la muralla)");
            W8Ok(ref ok, d.GuardiasDeLosPuestos().Count == 12 && d.soldadosDelCorredor.Length == 3, sb, $"guardias de los puestos={d.GuardiasDeLosPuestos().Count} (12) corredor={d.soldadosDelCorredor.Length} (3)");
            W8Ok(ref ok, d.ContraatacantesActivados == 0 && d.CamionetasDePuestosCreadas == 0, sb, $"nada lanzado al empezar (contraatacantes {d.ContraatacantesActivados}, camionetas {d.CamionetasDePuestosCreadas})");
            string cd = W9Nav(entrada, new Vector3(0f, 0f, -98f), out var ecd);
            W8Ok(ref ok, ecd != NavMeshPathStatus.PathComplete && d.portonBlindado != null && d.portonBlindado.activeSelf, sb, $"Centro de Datos bloqueado por el porton blindado ({cd})");
            var hud = OperacionHud.Instancia;
            string det = hud != null ? hud.TextoDetalle : "(sin hud)";
            W8Ok(ref ok, det.Contains("Puesto Oeste: intacto") && det.Contains("Puesto Este: intacto") && (hud.TextoTitulo ?? "").StartsWith("OBJETIVO 2/6", StringComparison.Ordinal), sb, $"HUD '{hud?.TextoTitulo}' / '{det}'");
            W9Aerea(new Vector3(0f, 0f, -178f), W9Aereas + "muralla_aerea.png", 95f, 60f, 62f);
            W9Aerea(d.puestos[0].carga.transform.position, "v2_082_puesto_oeste_aerea.png", 34f, 26f, 60f);
            W9Aerea(d.puestos[1].carga.transform.position, "v2_082_puesto_este_aerea.png", 34f, 26f, 60f);
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 082b el puesto Oeste de punta a punta
        static IEnumerator Bug082b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var kes = Escuadrista(RoleType.Flanker);
            var p = d != null && d.puestos != null && d.puestos.Length > 0 ? d.puestos[0] : null;
            if (vega == null || kes == null || p == null || p.carga == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas (vega/kes/puesto)"); yield break; }
            var carga = p.carga;
            var guardiasAntes = new List<Soldier>(p.guardias);
            try
            {
                // A) El Flanqueador manteniendo [E] junto a la carga: no avanza (solo el Asalto).
                Poseer(kes); yield return null;
                Junto(kes, carga.transform.position, 2f);
                Junto(vega, carga.transform.position, 7f, vega.transform.position);
                OperacionTerminal.PruebaMantenerE = true;
                foreach (var x in Esperar(1.0f)) yield return x;
                bool quieto = carga.Progreso01 <= 0.0001f && !carga.EstaPlantada;
                OperacionTerminal.PruebaMantenerE = false;
                W8Ok(ref ok, quieto, sb, $"A) Kes (Flanqueador) manteniendo [E]: la carga no avanza (progreso {carga.Progreso01:0.00})");

                // B) El Asalto manteniendo [E]: avanza, reporta DETONANDO, planta a los ~3,5 s y la mecha arranca en 20 s.
                Poseer(vega); yield return null;
                Junto(vega, carga.transform.position, 2f);
                OperacionTerminal.PruebaMantenerE = true;
                foreach (var x in Esperar(1.2f)) yield return x;
                var lista = new List<AccionesEnCurso.Accion>(); AccionesEnCurso.Vigentes(lista);
                bool reporta = false; foreach (var a in lista) if (a.Actor == vega && a.VerboEs == "DETONANDO" && a.Progreso01 > 0f) reporta = true;
                float progMedio = carga.Progreso01;
                foreach (var x in CapturarPantalla("v2_082_plantando.png")) yield return x;
                float t0 = Time.realtimeSinceStartup;
                while (!carga.EstaPlantada && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                OperacionTerminal.PruebaMantenerE = false;
                float tPlanto = 1.2f + (Time.realtimeSinceStartup - t0);
                bool planto = carga.Situacion == CargaExplosiva.Estado.Ardiendo && carga.MechaRestante > 17f && carga.MechaRestante <= 20f && tPlanto > 2.3f && tPlanto < 4.6f;
                W8Ok(ref ok, reporta && progMedio > 0.15f && progMedio < 0.6f, sb, $"B) Vega manteniendo [E]: reporta DETONANDO={reporta}, progreso a los 1,2 s={progMedio:0.00}");
                W8Ok(ref ok, planto, sb, $"B) planto a los ~{tPlanto:0.0} s (pide 3,5), mecha={carga.MechaRestante:0.0} s, estado {carga.Situacion}");

                // C) Contraataque: 4 enemigos de la reserva se activan por la puerta lateral; la mecha va por HUD y por la etiqueta 3D.
                foreach (var x in Esperar(1.0f)) yield return x;
                int activos = 0; foreach (var s in p.contraataque.soldados) if (s != null && s.gameObject.activeSelf) activos++;
                var hud = OperacionHud.Instancia;
                string timer = hud != null ? hud.TextoTimer : "";
                var etq = carga.etiqueta != null ? carga.etiqueta.text : "";
                W8Ok(ref ok, activos == OperacionDirector.ContraatacantesPrimera && d.ContraatacantesActivados == 4 && d.CamionetasDePuestosCreadas == 0, sb,
                    $"C) contraataque: {activos} reservas activas (pide 4), camionetas={d.CamionetasDePuestosCreadas} (pide 0 en la primera)");
                W8Ok(ref ok, etq.Contains("CARGA OESTE"), sb, $"C) etiqueta 3D='{etq}' (el timer del HUD se aparta mientras dura el cartel de aviso: #060)");
                W9Aerea(carga.transform.position, "v2_082_contraataque_aerea.png", 30f, 22f, 60f);
                foreach (var x in CapturarPantalla("v2_082_mecha.png")) yield return x;

                // D) #082: un enemigo pegado a la carga NO la desactiva; la mecha sigue bajando.
                var enemigo = p.guardias[0];
                if (enemigo != null && enemigo.Health.IsAlive) { Junto(enemigo, carga.transform.position, 0.8f); }
                float m0 = carga.MechaRestante;
                foreach (var x in Esperar(3f)) yield return x;
                bool sigue = carga.Situacion == CargaExplosiva.Estado.Ardiendo && carga.MechaRestante < m0 - 2f;
                W8Ok(ref ok, sigue, sb, $"D) con un enemigo al lado la carga sigue ardiendo: mecha {m0:0.0} a {carga.MechaRestante:0.0} s");
                timer = hud != null ? hud.TextoTimer : "";
                W8Ok(ref ok, timer.Contains("MECHA · PUESTO OESTE"), sb, $"D) HUD timer pasado el cartel='{timer}'");

                // E) Los ultimos 5 s el bip se acelera: se cuentan los bips en la 1.a mitad y en los 5 s finales.
                int bipsAntes = carga.Bips;
                while (carga.Situacion == CargaExplosiva.Estado.Ardiendo && carga.MechaRestante > 5.2f) yield return null;
                int bipsEn5 = carga.Bips;
                float tFinal0 = Time.realtimeSinceStartup;
                bool captura5 = false;
                while (carga.Situacion == CargaExplosiva.Estado.Ardiendo && carga.MechaRestante > 0.4f)
                {
                    if (!captura5 && carga.MechaRestante < 3f) { captura5 = true; foreach (var x in CapturarPantalla("v2_082_mecha_final.png")) yield return x; }
                    yield return null;
                }
                int bipsFinal = carga.Bips - bipsEn5;
                W8Ok(ref ok, bipsFinal >= 8, sb, $"E) bips: {bipsEn5 - bipsAntes} entre el aviso y los 5 s, {bipsFinal} en los ultimos 5 s (se aceleran: pide >=8)");

                // F) Estalla: bunker colapsado, nido muerto, guardias del radio muertos.
                float t1 = Time.realtimeSinceStartup;
                while (!carga.EstaVolada && Time.realtimeSinceStartup - t1 < 3f) yield return null;
                foreach (var x in Esperar(0.5f)) yield return x;
                int cols = 0, tot = 0;
                foreach (var m in p.bunker.GetComponentsInChildren<ObstacleMarker>(true)) { tot++; if (m.IsCollapsed || !m.gameObject.activeSelf) cols++; }
                bool nidoMuerto = p.nido == null || !p.nido.Health.IsAlive;
                int vivosRadio = 0;
                foreach (var g in guardiasAntes) if (g != null && g.Health.IsAlive && g.gameObject.activeInHierarchy && Vector3.Distance(g.transform.position, carga.transform.position) < CargaExplosiva.RadioDeExplosion) vivosRadio++;
                W8Ok(ref ok, carga.EstaVolada && d.ContarVolados() == 1 && d.Fase == FaseOperacion.Puestos, sb, $"F) volada={carga.EstaVolada} volados={d.ContarVolados()}/2 fase={d.Fase} (sigue en Puestos)");
                W8Ok(ref ok, cols == tot && tot > 0 && nidoMuerto && vivosRadio == 0, sb, $"F) bunker colapsado {cols}/{tot} marcas, nido muerto={nidoMuerto}, guardias vivos en el radio={vivosRadio} (pide 0)");
                string det = hud != null ? hud.TextoDetalle : "";
                W8Ok(ref ok, det.Contains("Puesto Oeste: VOLADO") && det.Contains("Puesto Este: intacto"), sb, $"F) HUD '{det}'");
                W9Aerea(carga.transform.position, "v2_082_volado_aerea.png", 34f, 26f, 60f);
                foreach (var x in CapturarPantalla("v2_082_volado.png")) yield return x;
            }
            finally
            {
                OperacionTerminal.PruebaMantenerE = false;
                Poseer(vega); ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 082c cadena Puestos -> Centro de Datos
        static IEnumerator Bug082c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault);
            if (d == null || vega == null || d.puestos == null || d.puestos.Length < 2) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            var este = d.puestos[1]; var oeste = d.puestos[0];
            try
            {
                // 1) El orden es libre: se coloca primero la del ESTE (Vega la coloca con la costura de pruebas, sin los 3,5 s).
                Poseer(vega); yield return null;
                Junto(vega, este.carga.transform.position, 2f);
                este.carga.PlantarPorPrueba(vega);
                este.carga.AcortarMecha(2.5f);
                foreach (var x in Esperar(1.2f)) yield return x;
                int act1 = 0; foreach (var s in este.contraataque.soldados) if (s != null && s.gameObject.activeSelf) act1++;
                W8Ok(ref ok, act1 == OperacionDirector.ContraatacantesPrimera && d.CamionetasDePuestosCreadas == 0, sb, $"1) Este primero: {act1} contraatacantes (pide 4), camionetas={d.CamionetasDePuestosCreadas} (pide 0)");
                float t0 = Time.realtimeSinceStartup;
                while (!este.carga.EstaVolada && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                foreach (var x in Esperar(0.4f)) yield return x;
                W8Ok(ref ok, este.carga.EstaVolada && d.ContarVolados() == 1 && d.Fase == FaseOperacion.Puestos && d.portonBlindado.activeSelf, sb,
                    $"1) Este volado, fase {d.Fase}, porton blindado sigue en pie={d.portonBlindado.activeSelf}");

                // 2) Despues el OESTE: la segunda carga trae 6 y una camioneta de asalto.
                Junto(vega, oeste.carga.transform.position, 2f);
                oeste.carga.PlantarPorPrueba(vega);
                oeste.carga.AcortarMecha(4f);
                foreach (var x in Esperar(1.5f)) yield return x;
                int act2 = 0; foreach (var s in oeste.contraataque.soldados) if (s != null && s.gameObject.activeSelf) act2++;
                W8Ok(ref ok, act2 == OperacionDirector.ContraatacantesSegunda && d.CamionetasDePuestosCreadas == 1 && d.CamionetasDePuestosVivas == 1, sb,
                    $"2) Oeste segundo: {act2} contraatacantes (pide 6), camionetas creadas={d.CamionetasDePuestosCreadas} vivas={d.CamionetasDePuestosVivas} (pide 1)");
                W9Aerea(new Vector3(0f, 0f, -190f), W9Aereas + "segunda_carga_aerea.png", 80f, 50f, 62f);
                t0 = Time.realtimeSinceStartup;
                while (!oeste.carga.EstaVolada && Time.realtimeSinceStartup - t0 < 8f) yield return null;
                foreach (var x in Esperar(0.5f)) yield return x;

                // 3) Los dos volados: la muralla cae, fase Centro de Datos y camino abierto.
                var hud = OperacionHud.Instancia;
                W8Ok(ref ok, d.ContarVolados() == 2 && d.Fase == FaseOperacion.CentroDeDatos, sb, $"3) volados={d.ContarVolados()}/2 fase={d.Fase} (pide CentroDeDatos)");
                foreach (var x in Esperar(3.5f)) yield return x;
                W8Ok(ref ok, d.portonBlindado != null && !d.portonBlindado.activeSelf, sb, $"3) el porton blindado se hundio (activo={d.portonBlindado != null && d.portonBlindado.activeSelf})");
                W8Ok(ref ok, d.CamionetasDePuestosVivas == 0, sb, $"3) sin camionetas de los puestos (vivas {d.CamionetasDePuestosVivas})");
                string cd = W9Nav(d.entradas[1].position, new Vector3(0f, 0f, -98f), out var ecd);
                W8Ok(ref ok, ecd == NavMeshPathStatus.PathComplete, sb, $"3) camino a la entrada del Centro de Datos abierto ({cd})");
                string tit = hud != null ? hud.TextoTitulo : "(sin hud)";
                W8Ok(ref ok, tit.StartsWith("OBJETIVO 3/6", StringComparison.Ordinal), sb, $"3) HUD '{tit}'");
                foreach (var x in CapturarPantalla("v2_082_cadena_centro.png")) yield return x;
            }
            finally
            {
                Poseer(vega); ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 082d orden [Q]: el aliado ASALTO va solo a colocar la carga
        static IEnumerator Bug082d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var kes = Escuadrista(RoleType.Flanker);
            if (d == null || vega == null || kes == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            var carga = d.puestos[0].carga;
            try
            {
                Poseer(kes); yield return null;
                Junto(kes, carga.transform.position, 9f, kes.transform.position);
                // El Flanqueador y el Medico no pueden: el motivo dice que solo el ASALTO.
                bool kesNo = !carga.EnviarA(kes, out string motivoKes) && motivoKes.Contains("ASALTO");
                var doc = Escuadrista(RoleType.Medic);
                bool docNo = doc != null && !carga.EnviarA(doc, out string motivoDoc) && motivoDoc.Contains("ASALTO");
                W8Ok(ref ok, kesNo && docNo, sb, $"A) EnviarA(Kes)='{motivoKes}', EnviarA(Doc)='{(doc != null ? "no" : "sin doc")}': solo el Asalto puede");
                // Con [Q] (la misma ruta del radial: EnviarA) el Asalto camina, coloca solo y la carga arde.
                Junto(vega, carga.transform.position, 12f, vega.transform.position);
                string motivo;
                bool acepto = carga.EnviarA(vega, out motivo);
                float t0 = Time.realtimeSinceStartup;
                while (!carga.EstaPlantada && Time.realtimeSinceStartup - t0 < 25f) yield return null;
                float tardo = Time.realtimeSinceStartup - t0;
                W8Ok(ref ok, acepto && carga.EstaPlantada && carga.Plantador == vega, sb, $"B) EnviarA(Vega) acepto={acepto}, plantada por {(carga.Plantador != null ? carga.Plantador.name : "nadie")} a los {tardo:0.0} s (12 m de caminata + 3,5 s)");
                int act = 0; foreach (var s in d.puestos[0].contraataque.soldados) if (s != null && s.gameObject.activeSelf) act++;
                W8Ok(ref ok, act == OperacionDirector.ContraatacantesPrimera, sb, $"B) y el contraataque salio igual ({act}/4)");
                foreach (var x in CapturarPantalla("v2_082_orden_q.png")) yield return x;
            }
            finally { Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 085b guardias de los puestos
        static IEnumerator Bug085b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            var d = OperacionDirector.Instancia;
            // WP11: el conteo de guardias se toma AL ARRANCAR (cuantos hay puestos) y no 4 s despues: sin modo dios la escuadra pelea con ellos en esos
            // 4 s y a veces cae uno (11 de 12 en 2 de 3 corridas), lo que no es un defecto de la colocacion.
            int n0 = 0; foreach (var g0 in d.GuardiasDeLosPuestos()) if (g0 != null && g0.Health.IsAlive && g0.gameObject.activeInHierarchy && g0.Brain != null) n0++;
            foreach (var x in Esperar(4f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var guardias = d.GuardiasDeLosPuestos();
            int n = 0, atr = 0, enCob = 0, valido = 0, agachados = 0, pegadoASolido = 0, cazadores = 0, peleando = 0;
            float peor = 0f; var sin = new StringBuilder();
            foreach (var s in guardias)
            {
                if (s == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy || s.Brain == null) continue;
                n++;
                if (s.Brain.Atrincherado) atr++;
                if (s.Brain.EnCobertura) enCob++; else sin.Append(s.name + " ");
                if (s.Brain.EnCobertura && s.Brain.CoberturaPunto != Vector3.zero && (s.Brain.CoberturaPunto - s.transform.position).magnitude < 3f) valido++;
                if (s.Motor != null && s.Motor.IsCrouching) agachados++;
                if (s.Brain.CurrentTarget != null) peleando++;
                if (CazaDeEnemigos.EsCazador(s) && s.Brain.CurrentTarget == null && s.Health.Current >= s.Health.MaxHealth) cazadores++;
                // "buen puesto": un solido (muro, saco, auto, jersey) a menos de 2,5 m, a la altura del pecho.
                float dmin = 99f;
                foreach (var c in Physics.OverlapSphere(s.transform.position + Vector3.up * 0.8f, 2.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    if (c.isTrigger || c.GetComponentInParent<Soldier>() != null || c.bounds.size.y < 0.6f) continue;
                    float dd = Vector3.Distance(c.ClosestPoint(s.transform.position + Vector3.up * 0.8f), s.transform.position + Vector3.up * 0.8f);
                    if (dd < dmin) dmin = dd;
                }
                if (dmin <= 2.5f) pegadoASolido++; else sin.Append(s.name + "(solido " + dmin.ToString("0.0") + ") ");
                peor = Mathf.Max(peor, dmin < 99f ? dmin : 0f);
            }
            W8Ok(ref ok, n0 == 12 && n >= 10, sb, $"guardias de los puestos: {n0} al arrancar (pide 12), {n} vivos a los 4 s (pide >= 10; la escuadra pelea con ellos)");
            W8Ok(ref ok, atr == n && enCob == n && valido == n, sb, $"atrincherados={atr} en cobertura={enCob} con punto valido={valido}");
            W8Ok(ref ok, pegadoASolido == n, sb, $"pegados a un solido (<=2,5 m)={pegadoASolido}/{n}");
            W8Ok(ref ok, cazadores == 0 || peleando > 0, sb, $"cazadores sin haber visto a nadie={cazadores} (pide 0), peleando={peleando}");
            sb.Append($"agachados={agachados}; sin cobertura/solido: [{sin}]");
            W9Aerea(new Vector3(-38f, 0f, -183f), "v2_085b_guardias_oeste_aerea.png", 24f, 18f, 60f);
            W9Aerea(new Vector3(0f, 0f, -195f), "v2_085b_trinchera_aerea.png", 28f, 20f, 60f);
            Fin((ok ? "OK " : "FALLO ") + sb + $" (arranque: {r0})");
        }

        // ---------------------------------------------------------------- 097: cinematica inicial
        static Vector3[] W9Posiciones()
        {
            var l = new List<Vector3>();
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Player && s.gameObject.activeInHierarchy) l.Add(s.transform.position);
            return l.ToArray();
        }

        static float W9Desvio(Vector3[] a, Vector3[] b)
        {
            float m = 0f;
            for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++) m = Mathf.Max(m, Vector3.Distance(a[i], b[i]));
            return m;
        }

        static IEnumerator Bug097a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            if (Keyboard.current == null) { Fin("FALLO no hay Keyboard.current"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            // En un Play fresco la cinematica arranca sola al entrar (y todavia no se vio): hay que encontrarla activa, con el titulo o un paso.
            bool fresco = !CinematicaDeOperacion.YaVista;
            // P9 (#123): en un Play fresco primero corre el rapel (5 s) y despues encadena la cinematica de los pasos: se espera el empalme.
            if (CinematicaDeRapel.Activa)
            {
                float tr = Time.realtimeSinceStartup;
                while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - tr < 12f) yield return null;
                yield return null;
            }
            bool sola = CinematicaDeOperacion.Activa && CinematicaDeOperacion.Instancia != null;
            string estadoInicial = sola ? $"paso {CinematicaDeOperacion.Instancia.Paso}, {CinematicaDeOperacion.Instancia.Tiempo:0.0} s" : "inactiva";
            W8Ok(ref ok, !fresco || sola, sb, $"al entrar en Play la cinematica arranca sola ({(fresco ? estadoInicial : "no es un Play fresco: se omite")})");
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.0f)) yield return x;
            var drv = PlayerInputDriver.Activo;
            var yo = W9Yo();
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                var cin = CinematicaDeOperacion.ReiniciarPorPrueba();
                yield return null; yield return null;
                W8Ok(ref ok, CinematicaDeOperacion.Activa && cin != null, sb, "arranca la cinematica (Activa)");
                W8Ok(ref ok, !drv.enabled && SP.Ai.AiBrain.IAPausada && (drv.Rig == null || !drv.Rig.enabled), sb, $"bloqueo: driver apagado={!drv.enabled}, IA pausada={SP.Ai.AiBrain.IAPausada}, rig apagado={(drv.Rig == null || !drv.Rig.enabled)}");

                // A) Teclas virtuales (movimiento, correr, agacharse, rts, interactuar, ordenes): nada se mueve en 1,5 s.
                var antes = W9Posiciones();
                var camAntes = CamaraPrincipal.Actual != null ? CamaraPrincipal.Actual.transform.position : Vector3.zero;
                Teclas(Key.W, Key.D, Key.LeftShift, Key.E, Key.Q, Key.Tab, Key.R, Key.Digit2, Key.F);
                foreach (var x in Esperar(1.5f)) yield return x;
                Teclas();
                var despues = W9Posiciones();
                float desvio = W9Desvio(antes, despues);
                W8Ok(ref ok, desvio < 0.02f && antes.Length > 0, sb, $"A) 9 teclas (W D Shift E Q Tab R 2 F) durante 1,5 s: desvio maximo de la escuadra {desvio:0.000} m ({antes.Length} soldados)");
                W8Ok(ref ok, CinematicaDeOperacion.Activa && yo != null && yo.Health.IsAlive, sb, "A) la cinematica sigue activa y el poseido vivo");
                foreach (var x in CapturarPantalla(W9Aereas + "titulo.png")) yield return x;

                // B) Titulo y el paso 2 (muralla): el cartel dice el titulo y el texto del plan; el panel marca ✓ el 1 y ▶ el 2.
                float t0 = Time.realtimeSinceStartup;
                bool vioTitulo = !string.IsNullOrEmpty(cin.TextoDelTitulo) || cin.Paso == -1;
                while (cin.Paso < 1 && Time.realtimeSinceStartup - t0 < 14f) yield return null;
                foreach (var x in Esperar(2.4f)) yield return x;
                string cartel = cin.TextoDelCartel, panel = cin.TextoDelPanel;
                bool paso2 = cin.Paso == 1 && cartel.Contains("PASO 2/6") && cartel.Contains("VOLÁ LOS DOS PUESTOS") && cartel.Contains("Solo el ASALTO coloca las cargas") && cartel.Contains("20 s");
                bool panelOk = panel.Contains("✓") && panel.Contains("▶") && panel.Contains("VOLÁ LOS DOS PUESTOS") && panel.Contains("EXTRACCIÓN");
                var anc = d_anclas(1);
                var camPos = cin.PosicionDeCamara;
                float dh = new Vector3(camPos.x - anc.x, 0f, camPos.z - anc.z).magnitude;
                bool camOk = camPos.y > 25f && dh > 20f && dh < 70f;
                W8Ok(ref ok, vioTitulo, sb, "B) el titulo OPERACIÓN CUARTEL se mostro primero");
                W8Ok(ref ok, paso2, sb, $"B) paso 2: cartel='{cartel.Replace('\n', '|')}'");
                W8Ok(ref ok, panelOk, sb, $"B) panel lateral: '{panel.Replace('\n', '|')}'");
                W8Ok(ref ok, camOk, sb, $"B) camara aerea sobre la muralla (altura {camPos.y:0} m, a {dh:0} m del ancla)");
                foreach (var x in CapturarPantalla("v2_097_intro_paso2.png")) yield return x;
                foreach (var x in CapturarPantalla(W9Aereas + "paso2.png")) yield return x;

                // C) Mantener [ESPACIO]: a los 0,5 s el anillo va por la mitad; al segundo se salta y se devuelve el control.
                CinematicaDeOperacion.PruebaMantenerEspacio = true;
                foreach (var x in Esperar(0.5f)) yield return x;
                float mitad = cin != null ? cin.ProgresoDeSalto : 0f;
                foreach (var x in CapturarPantalla("v2_097_intro_saltar.png")) yield return x;
                foreach (var x in Esperar(0.8f)) yield return x;
                CinematicaDeOperacion.PruebaMantenerEspacio = false;
                yield return null; yield return null;
                W8Ok(ref ok, mitad > 0.3f && mitad < 0.8f, sb, $"C) anillo de salto a los 0,5 s = {mitad:0.00} (pide ~0,5)");
                W8Ok(ref ok, !CinematicaDeOperacion.Activa && CinematicaDeOperacion.YaVista && drv.enabled && !SP.Ai.AiBrain.IAPausada && (drv.Rig == null || drv.Rig.enabled), sb,
                    $"C) saltada: Activa={CinematicaDeOperacion.Activa}, driver activo={drv.enabled}, IA pausada={SP.Ai.AiBrain.IAPausada}");

                // D) Control devuelto: con W solo el poseido camina.
                yo = W9Yo();
                var p0 = yo.transform.position;
                Teclas(Key.W);
                foreach (var x in Esperar(0.6f)) yield return x;
                Teclas();
                float avanzo = Vector3.Distance(p0, yo.transform.position);
                W8Ok(ref ok, avanzo > 0.5f, sb, $"D) control devuelto: con W avanzo {avanzo:0.00} m");
            }
            finally
            {
                Teclas();
                CinematicaDeOperacion.PruebaMantenerEspacio = false; CinematicaDeOperacion.PruebaTecla = false;
                CinematicaDeOperacion.Saltar();
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static Vector3 d_anclas(int k)
        {
            var d = OperacionDirector.Instancia;
            return d != null && d.anclasDeZona != null && k < d.anclasDeZona.Length && d.anclasDeZona[k] != null ? d.anclasDeZona[k].position : Vector3.zero;
        }

        static IEnumerator Bug097b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(5.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var hud = OperacionHud.Instancia;
            if (hud == null) { Fin("FALLO sin OperacionHud"); yield break; }
            bool plegadoAntes = hud.PasosPlegados;
            try
            {
                if (hud.PasosPlegados) hud.AlternarPasos();
                yield return null; yield return null;
                string t = hud.TextoPasos;
                bool seis = true; foreach (var s in OperacionDirector.PasosCortos) if (!t.Contains(s)) seis = false;
                bool marca = t.Contains("▶") && t.Contains("✓") && t.Contains("•");
                W8Ok(ref ok, seis && marca, sb, $"desplegado: 6 pasos y marcas ✓▶• en el objetivo 2: '{t.Replace('\n', '|')}'");
                foreach (var x in CapturarPantalla("v2_097_pasos_hud.png")) yield return x;
                hud.AlternarPasos();
                yield return null; yield return null;
                string pl = hud.TextoPasos;
                bool una = !pl.Contains("\n") && pl.Contains("2/6") && pl.Contains("[V]");
                W8Ok(ref ok, hud.PasosPlegados && una, sb, $"plegado con [V]: una linea '{pl}'");
                foreach (var x in CapturarPantalla("v2_097_pasos_hud_plegado.png")) yield return x;
                // La tecla real: [V] inyectada alterna.
                if (Keyboard.current != null)
                {
                    bool antes = hud.PasosPlegados;
                    Teclas(KeyBindings.Get(KeyBindings.PlegarPasos));
                    yield return null; yield return null;
                    Teclas();
                    yield return null; yield return null;
                    W8Ok(ref ok, hud.PasosPlegados != antes, sb, $"tecla [V] real alterna (plegado {antes} a {hud.PasosPlegados})");
                }
            }
            finally
            {
                Teclas();
                if (hud.PasosPlegados != plegadoAntes) hud.AlternarPasos();
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static IEnumerator Bug097i()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            if (Keyboard.current == null) { Fin("FALLO no hay Keyboard.current"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.0f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var drv = PlayerInputDriver.Activo;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                var cin = CinematicaDeOperacion.ReiniciarPorPrueba();
                float t0 = Time.realtimeSinceStartup;
                var antes = W9Posiciones();
                var vistos = new List<string>(); var camaras = new List<Vector3>(); var capturados = new HashSet<int>();
                int pasoPrev = -2; bool bloqueoSiempre = true;
                while (CinematicaDeOperacion.Activa && !cin.EsperandoTecla && Time.realtimeSinceStartup - t0 < 60f)
                {
                    if (cin.Paso != pasoPrev)
                    {
                        pasoPrev = cin.Paso;
                        if (cin.Paso >= 0 && cin.Paso < CinematicaDeOperacion.TotalDePasos) vistos.Add(cin.TextoDelCartel);
                    }
                    if (drv.enabled || !SP.Ai.AiBrain.IAPausada) bloqueoSiempre = false;
                    // A mitad de cada paso: captura del Game View y posicion de camara.
                    float enPaso = cin.Tiempo - (0.5f + CinematicaDeOperacion.SegundosDeTitulo + Mathf.Max(0, cin.Paso) * CinematicaDeOperacion.SegundosPorPaso);
                    if (cin.Paso >= 0 && cin.Paso < CinematicaDeOperacion.TotalDePasos && enPaso > 2.2f && capturados.Add(cin.Paso))
                    {
                        camaras.Add(cin.PosicionDeCamara);
                        foreach (var x in CapturarPantalla(W9Aereas + "paso" + (cin.Paso + 1) + ".png")) yield return x;
                    }
                    // Con W+E apretadas a ratos (se suelta 2 s antes del cierre: en "presiona una tecla" CUALQUIER tecla termina la cinematica).
                    if (cin.Tiempo < 27f && ((int)((Time.realtimeSinceStartup - t0) * 2f)) % 3 == 0) Teclas(Key.W, Key.E); else Teclas();
                    yield return null;
                }
                Teclas();
                float dur = Time.realtimeSinceStartup - t0;
                bool completa = cin.EsperandoTecla && vistos.Count == CinematicaDeOperacion.TotalDePasos;
                bool textos = completa;
                for (int k = 0; k < vistos.Count && k < CinematicaDeOperacion.TotalDePasos; k++)
                    if (!vistos[k].Contains($"PASO {k + 1}/6") || !vistos[k].Contains(CinematicaDeOperacion.Titulos[k]) || !vistos[k].Contains(CinematicaDeOperacion.Textos[k])) textos = false;
                float dist = 0f; for (int i = 1; i < camaras.Count; i++) dist += Vector3.Distance(camaras[i - 1], camaras[i]);
                W8Ok(ref ok, completa, sb, $"llego a 'presiona una tecla' tras {dur:0.0} s con {vistos.Count}/6 pasos vistos (~29 s)");
                W8Ok(ref ok, textos, sb, "cada paso muestra 'PASO k/6', su titulo y su texto");
                W8Ok(ref ok, camaras.Count == 6 && dist > 100f, sb, $"la camara recorre las 6 zonas (capturas {camaras.Count}, recorrido entre tomas {dist:0} m)");
                W8Ok(ref ok, bloqueoSiempre && W9Desvio(antes, W9Posiciones()) < 0.02f, sb, $"bloqueo total durante toda la cinematica con W+E apretadas (desvio {W9Desvio(antes, W9Posiciones()):0.000} m)");
                foreach (var x in CapturarPantalla(W9Aereas + "final.png")) yield return x;
                // Esperando la tecla (sin tocar nada) 1,2 s: no se cierra sola, nadie se mueve y el driver sigue apagado.
                foreach (var x in Esperar(1.2f)) yield return x;
                W8Ok(ref ok, CinematicaDeOperacion.Activa && cin.EsperandoTecla && !drv.enabled && W9Desvio(antes, W9Posiciones()) < 0.02f, sb,
                    $"esperando la tecla 1,2 s sin tocar nada: sigue activa={CinematicaDeOperacion.Activa}, driver apagado={!drv.enabled}, desvio {W9Desvio(antes, W9Posiciones()):0.000} m");

                // Cierra con una tecla REAL (Enter) y se devuelve el control (si la tecla inyectada no llegara, la costura PruebaTecla).
                Teclas(Key.Enter);
                foreach (var x in Esperar(0.3f)) yield return x;
                Teclas();
                bool cerroConTeclaReal = !CinematicaDeOperacion.Activa;
                if (!cerroConTeclaReal) { CinematicaDeOperacion.PruebaTecla = true; foreach (var x in Esperar(0.3f)) yield return x; }
                foreach (var x in Esperar(0.4f)) yield return x;
                var yo = W9Yo(); var p0 = yo.transform.position;
                Teclas(Key.W);
                foreach (var x in Esperar(0.6f)) yield return x;
                Teclas();
                float avanzo = Vector3.Distance(p0, yo.transform.position);
                W8Ok(ref ok, !CinematicaDeOperacion.Activa && CinematicaDeOperacion.YaVista && drv.enabled && !SP.Ai.AiBrain.IAPausada && avanzo > 0.5f, sb,
                    $"al apretar una tecla ({(cerroConTeclaReal ? "Enter real" : "costura PruebaTecla")}) termina: Activa={CinematicaDeOperacion.Activa}, YaVista={CinematicaDeOperacion.YaVista}, driver activo={drv.enabled}, IA pausada={SP.Ai.AiBrain.IAPausada}, con W avanzo {avanzo:0.00} m");
                var hud = OperacionHud.Instancia;
                W8Ok(ref ok, hud != null && hud.TextoTitulo.StartsWith("OBJETIVO 1/6", StringComparison.Ordinal), sb, $"el HUD vuelve: '{hud?.TextoTitulo}'");
                foreach (var x in CapturarPantalla(W9Aereas + "despues.png")) yield return x;
            }
            finally
            {
                Teclas();
                CinematicaDeOperacion.PruebaMantenerEspacio = false; CinematicaDeOperacion.PruebaTecla = false;
                CinematicaDeOperacion.Saltar();
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static bool cin_saltada() => false;
    }
}
