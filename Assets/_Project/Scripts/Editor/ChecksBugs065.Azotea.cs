using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Bugs #326-#341 (2026-11-04): objetivo 5 termina RESISTIENDO EN LA AZOTEA hasta el rescate, el helipad es el techo y los arreglos de la mansion.
    public static partial class ChecksBugs065
    {
        static readonly System.Reflection.BindingFlags NoPublico = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // ---------------------------------------------------------------- 406u azotea: oleadas, helicopteros de cuerdas, rescate en el techo, victoria desde arriba
        static IEnumerator Bug406u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, d.MansionActiva, sb, "la mansion esta activa en el objetivo 5");
            W8Ok(ref ok, d.heliAterrizaje.position.y > 19f, sb, $"el helipad esta en el techo (y {d.heliAterrizaje.position.y:0.0})");
            d.SaltarALaAzotea();
            foreach (var x in Esperar(1.5f)) yield return x;
            W8Ok(ref ok, d.AzoteaEnCurso && d.MilicianosLiberados, sb, "arranca la azotea con los milicianos liberados");
            var campo = typeof(OperacionDirector).GetField("tAzotea", NoPublico);
            campo.SetValue(d, OperacionDirector.CuandoCuelgan[0] - 0.2f);
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, d.CuerdasLanzadas >= 1 && d.HelisDeLaAzotea.Count >= 1, sb, $"sale el 1.er helicoptero de cuerdas ({d.CuerdasLanzadas})");
            var h = d.HelisDeLaAzotea.Count > 0 ? d.HelisDeLaAzotea[0] : null;
            float limite = Time.realtimeSinceStartup + 40f;
            while (h != null && Time.realtimeSinceStartup < limite && h.SoldadosBajados < h.SoldadosPedidos) yield return null;
            foreach (var x in Esperar(3.0f)) yield return x;
            W8Ok(ref ok, h != null && h.SoldadosBajados == h.SoldadosPedidos && h.SoldadosPedidos > 0, sb, $"bajo a todos por las cuerdas ({(h != null ? h.SoldadosBajados : 0)}/{(h != null ? h.SoldadosPedidos : 0)})");
            int enElTecho = 0;
            if (h != null) foreach (var s in h.SoldadosDelDespliegue) if (s != null && s.transform.position.y > 19f && s.transform.position.y < 22f && G.Dentro(s.transform.position, 1f)) enElTecho++;
            W8Ok(ref ok, h != null && enElTecho == h.SoldadosBajados, sb, $"los {enElTecho} soldados aterrizaron sobre el techo");
            W8Ok(ref ok, h != null && h.Vehiculo != null && !h.Vehiculo.Health.Invulnerable, sb, "terminado el despliegue el helicoptero se puede derribar");
            // El rescate llega al cumplirse el tiempo: queda sobre el techo, los helicopteros enemigos estallan y empieza la extraccion en la azotea.
            campo.SetValue(d, OperacionDirector.SegundosDeResistenciaEnAzotea - 0.2f);
            foreach (var x in Esperar(1.5f)) yield return x;
            W8Ok(ref ok, d.Fase == FaseOperacion.Extraer, sb, $"llega el rescate y pasa a Extraer ({d.Fase})");
            float alto = d.heli.position.y - d.heliAterrizaje.position.y;
            W8Ok(ref ok, alto > -0.5f && alto < OperacionDirector.AlturaEstacionarioHeli + 3f && Vector3.Distance(new Vector3(d.heli.position.x, 0f, d.heli.position.z), new Vector3(d.heliAterrizaje.position.x, 0f, d.heliAterrizaje.position.z)) < 3f, sb, $"el helicoptero de rescate queda sobre el techo ({d.heli.position:0})");
            bool todosCaidos = true; foreach (var e in d.HelisDeLaAzotea) if (e != null && !e.Derribado) todosCaidos = false;
            W8Ok(ref ok, todosCaidos, sb, "los helicopteros enemigos ya no estan");
            // Subida y toma final desde el techo.
            d.SubirAlHeli();
            limite = Time.realtimeSinceStartup + 40f;
            while (d.Fase != FaseOperacion.Victoria && Time.realtimeSinceStartup < limite) yield return null;
            W8Ok(ref ok, d.Fase == FaseOperacion.Victoria, sb, $"se gana subiendo desde la azotea ({d.Fase})");
            W8Ok(ref ok, d.TomaFinalEnLaAzotea && d.TomaFinalActiva, sb, "la toma final es la del techo (camara sobre la azotea)");
            W8Ok(ref ok, SP.Presentation.TomaDeVictoria.Activa == null, sb, "no se pisa con la orbita de la escuadra");
            Time.timeScale = 1f;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 407u arreglos de la mansion: rombos por piso, spawns, franjas, ventanas, tapa de mármol, seguimiento
        static IEnumerator Bug407u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            var d = OperacionDirector.Instancia;
            // Los puntos de aparicion no caen dentro de las torres de escalera (#337).
            int malos = 0;
            foreach (var arr in new[] { G.EnemigosPB, G.EnemigosPrimero, G.EnemigosSegundo }) foreach (var p in arr) if (G.EnTorre(p, out _)) malos++;
            W8Ok(ref ok, malos == 0, sb, $"{malos} puntos de aparicion dentro de una torre de escalera");
            // Seguimiento: la ranura de la formacion y la distancia de parada no quedan pegadas (#332).
            SP.Ai.AjustesDeEscuadra.AsegurarEnEscena().Aplicar();
            W8Ok(ref ok, SP.Ai.AjustesDeEscuadra.DistanciaLateralFormacion >= 4.5f && SP.Ai.AjustesDeEscuadra.DistanciaAtrasFormacion >= 3f && SP.Ai.AjustesDeEscuadra.DistanciaParaDetenerse >= 6f, sb,
                $"formacion al seguir: lateral {SP.Ai.AjustesDeEscuadra.DistanciaLateralFormacion:0.0}, atras {SP.Ai.AjustesDeEscuadra.DistanciaAtrasFormacion:0.0}, se detienen a {SP.Ai.AjustesDeEscuadra.DistanciaParaDetenerse:0.0} m");
            // Franjas doradas del porton (#326): hijas de la hoja, se mueven con ella.
            int franjas = 0, sueltas = 0;
            foreach (var t in Object.FindObjectsByType<Transform>()) if (t.name == "Porton_Dorado") { franjas++; if (t.parent == null || !t.parent.name.StartsWith("Porton_Hoja")) sueltas++; }
            W8Ok(ref ok, franjas == 2 && sueltas == 0, sb, $"{franjas} franjas del porton, {sueltas} sueltas");
            // Ventanas luminosas dentro de las puertas de servicio (#335).
            int flotando = 0;
            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                bool o = t.name == "Ventana_O", e = t.name == "Ventana_E";
                if (!o && !e) continue;
                var b = t.GetComponent<Renderer>().bounds;
                foreach (var pu in OperacionBuilder.PuertasLaterales(o))
                    if (b.max.z > pu.z0 && b.min.z < pu.z1 && b.max.y > pu.y0 && b.min.y < pu.y1) flotando++;
            }
            W8Ok(ref ok, flotando == 0, sb, $"{flotando} ventanas dentro del hueco de una puerta");
            // El marmol del 1.er piso no tapa el hueco de la escalera (#329).
            bool tapa = false;
            var hueco = G.Torres[0].Mundo(G.CarrilB, 4f);
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
                if (r.name.StartsWith("Marmol_Base_1") && r.bounds.Contains(new Vector3(hueco.x, G.Nivel(1) + 0.01f, hueco.y))) tapa = true;
            W8Ok(ref ok, !tapa, sb, "el marmol del 1.er piso deja libre la boca de la escalera A");
            // Rombo del objetivo en el piso donde esta el objetivo (#327/#328/#330).
            var guia = GuiaDeObjetivo.Instancia;
            var yo = PlayerInputDriver.Activo.Brain.Current;
            typeof(OperacionDirector).GetMethod("AbrirElPorton", NoPublico).Invoke(d, null);
            yo.transform.position = new Vector3(322f, 0.9f, 262f);
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, guia != null && guia.ObjetivoActual.y < 1f && Vector3.Distance(guia.ObjetivoActual, G.EntradaDeTorre(0)) < 4f, sb, $"planta baja: el rombo marca la escalera ({(guia != null ? guia.ObjetivoActual : Vector3.zero):0.0})");
            yo.transform.position = new Vector3(322f, G.Nivel(1) + 0.9f, 262f);
            typeof(OperacionDirector).GetMethod("EntrarPrimerPiso", NoPublico).Invoke(d, null);
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, guia != null && Mathf.Abs(guia.ObjetivoActual.y - G.Nivel(1)) < 1.5f, sb, $"1.er piso: el rombo esta a la altura del piso ({(guia != null ? guia.ObjetivoActual.y : 0f):0.0} m)");
            yo.transform.position = new Vector3(322f, G.Nivel(2) + 0.9f, 262f);
            typeof(OperacionDirector).GetMethod("EntrarSegundoPiso", NoPublico).Invoke(d, null);
            foreach (var x in Esperar(8.5f)) yield return x;
            W8Ok(ref ok, guia != null && Mathf.Abs(guia.ObjetivoActual.y - G.Nivel(2)) < 1.5f, sb, $"2.o piso: el rombo marca las celdas a su altura ({(guia != null ? guia.ObjetivoActual.y : 0f):0.0} m)");
            // Las celdas se abren desde 7 m aunque queden guardias (#333) y los liberados siguen al jugador (#338).
            yo.transform.position = new Vector3(310f, G.Nivel(2) + 0.9f, 272f);
            // Sin guardia a la vista (si no, pelean y no siguen): se apagan los enemigos de la mansion y se vacia la cola de apariciones.
            ((System.Collections.IList)typeof(OperacionDirector).GetField("pendientesMansion", NoPublico).GetValue(d)).Clear();
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.name.StartsWith("Mansion_Enemigo")) a.gameObject.SetActive(false);
            ActorRegistry.Invalidate();
            typeof(OperacionDirector).GetMethod("LiberarMilicianos", NoPublico).Invoke(d, null);
            foreach (var x in Esperar(12.0f)) yield return x;
            int siguen = 0, total = 0; var estados = new StringBuilder();
            foreach (var a in ActorRegistry.All)
                if (a != null && a.Team == TeamId.Player && a.gameObject.activeInHierarchy && a.DisplayName.StartsWith("MILICIANO"))
                {
                    total++; estados.Append(a.Brain.State).Append('/').Append(Vector3.Distance(a.transform.position, yo.transform.position).ToString("0")).Append("m ");
                    if (a.Brain.FollowTarget == yo || a.Brain.State == SP.Ai.AiState.Follow) siguen++;   // en pelea con la guardia siguen teniendo al jugador de lider
                }
            int afuera = 0;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Player && a.gameObject.activeInHierarchy && a.DisplayName.StartsWith("MILICIANO") && a.transform.position.z < 278.5f) afuera++;
            var reja = GameObject.Find("P2_Reja");
            W8Ok(ref ok, d.RejaAbierta && (reja == null || !reja.transform.GetChild(0).gameObject.activeSelf) && afuera >= 3, sb, $"reja de las celdas abierta (abierta={d.RejaAbierta}) y {afuera} milicianos ya salieron de las celdas");
            W8Ok(ref ok, total >= 4 && siguen == total, sb, $"{siguen}/{total} milicianos liberados tienen al jugador de lider ({estados})");
            // Recorte de plantas (#336): con la camara cenital se ocultan las plantas de arriba.
            var rig = SP.CameraSystem.CameraRig.Instance;
            rig.SetMode(SP.CameraSystem.ControlMode.Rts, yo.transform.position);
            foreach (var x in Esperar(1.0f)) yield return x;
            var plantas = Object.FindFirstObjectByType<PlantasDeLaMansion>();
            W8Ok(ref ok, plantas != null && plantas.NivelMostrado == 2 && plantas.Ocultos > 20, sb, $"vista tactica: se muestra la planta {(plantas != null ? plantas.NivelMostrado : -9)} y se ocultan {(plantas != null ? plantas.Ocultos : 0)} piezas de arriba");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
