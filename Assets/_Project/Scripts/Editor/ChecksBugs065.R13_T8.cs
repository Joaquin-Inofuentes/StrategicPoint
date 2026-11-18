using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T8: enemigos atrapados en los cubiculos de reserva tras la 2.a puerta. Sin sin Barrera_Reserva_SoloEnemigos huerfanas el NavMesh deja
    // salir a las 12 reservas (Reserva_PuestoOeste_1..6 y Reserva_PuestoEste_1..6).
    public static partial class ChecksBugs065
    {
        // Caja de los cubiculos de reserva (x +-43,2..47,8 / z -171,5..-175,9): se esta "adentro" si se cumple en valor absoluto de x.
        static bool R13_T8_EnCubiculo(Vector3 p) => Mathf.Abs(p.x) > 43.2f && Mathf.Abs(p.x) < 47.8f && p.z < -171.5f && p.z > -175.9f;

        // ---------------------------------------------------------------- 977a ningun enemigo sin ruta y las 12 reservas salen del cubiculo
        static IEnumerator Bug977a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(2f)) yield return x;
            Time.timeScale = 1f;
            var d = OperacionDirector.Instancia;
            if (d == null || d.puestos == null || d.puestos.Length < 2) { Fin("FALLO faltan el director o los puestos"); yield break; }

            // el NavMesh vivo tiene que estar horneado
            float t0 = Time.realtimeSinceStartup;
            while (NavMeshViva.Version <= 0 && Time.realtimeSinceStartup - t0 < 25f) yield return null;
            W8Ok(ref ok, NavMeshViva.Version > 0, sb, $"el NavMesh vivo se horneo (version {NavMeshViva.Version})");

            // 1) estatico: ya no quedan las barreras huerfanas
            int barreras = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (t != null && t.name == "Barrera_Reserva_SoloEnemigos") barreras++;
            W8Ok(ref ok, barreras == 0, sb, $"{barreras} Barrera_Reserva_SoloEnemigos en la escena (pide 0)");

            // 2) cada enemigo (activo o no) con y < 5 tiene camino a la zona de salida de SU sector. Los portones cerrados (Porton_Blindado z -170, Muro_Salida_Norte z -40)
            //    parten el nivel en sectores desconectados por diseno: al sur del blindado el destino es la aproximacion sur (z -190); entre el blindado y el muro, (0,0,-160);
            //    al norte del muro (huida, ruta, ciudad) los enemigos no estan atrapados por un cubiculo y solo se cuentan. Con el blindado abierto, los del sector sur
            //    deben llegar tambien a (0,0,-160).
            NavMesh.SamplePosition(new Vector3(0f, 0f, -190f), out var sur, 8f, NavMesh.AllAreas);
            NavMesh.SamplePosition(new Vector3(0f, 0f, -160f), out var norte, 8f, NavMesh.AllAreas);
            bool blindadoAbierto = d.portonBlindado == null || !d.portonBlindado.activeInHierarchy;
            int total = 0, sinRuta = 0, sinRutaNorte = 0, fueraDeSector = 0, artilleros = 0; var malos = new StringBuilder();
            var spawns = new Dictionary<Soldier, Vector3>();
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include))
            {
                if (s == null || s.Team != TeamId.Enemy || s.transform.position.y > 5f) continue;
                spawns[s] = s.transform.position;
                float z0 = s.transform.position.z;
                if (z0 > -42f) { fueraDeSector++; continue; }   // al norte del Muro_Salida_Norte: otro sector
                if (s.name.StartsWith("Enemigo_Nido_") || s.name.StartsWith("Enemigo_Artillero_")) { artilleros++; continue; }   // artilleros fijos (ArtilleroDeTorreta): no caminan, islas por diseno (501a)
                total++;
                bool alSur = z0 < -171f;   // detras del portón blindado (cuartel de reserva y puestos)
                var meta = alSur ? sur.position : norte.position;
                if (!NavMesh.SamplePosition(s.transform.position, out var o, 3f, NavMesh.AllAreas)) { sinRuta++; if (malos.Length < 300) malos.Append(s.name + "(sin NavMesh) "); continue; }
                var path = new NavMeshPath();
                bool hay = NavMesh.CalculatePath(o.position, meta, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                if (!hay) { sinRuta++; if (malos.Length < 300) malos.Append(s.name + " "); }
                if (blindadoAbierto && alSur)
                {
                    var p2 = new NavMeshPath();
                    if (!(NavMesh.CalculatePath(o.position, norte.position, NavMesh.AllAreas, p2) && p2.status == NavMeshPathStatus.PathComplete)) sinRutaNorte++;
                }
            }
            W8Ok(ref ok, total > 0, sb, $"{total} enemigos auditados");
            W8Ok(ref ok, sinRuta == 0, sb, $"{sinRuta} enemigos sin camino a la zona de salida de su sector ({malos}); {fueraDeSector} al norte del Muro_Salida_Norte y {artilleros} artilleros fijos (Nido/TV) no se auditan");
            if (blindadoAbierto) W8Ok(ref ok, sinRutaNorte == 0, sb, $"{sinRutaNorte} enemigos sin camino a (0,0,-160) con el porton blindado abierto");
            else sb.Append("[porton blindado cerrado: no se mide el camino a (0,0,-160)] ");

            // 3) dinamico: plantar la carga del porton (flujo de Bug082c) y a los 12 s las 12 reservas estan fuera de su cubiculo y lejos de donde aparecen
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault);
            var reservas = new List<Soldier>(); var origen = new Dictionary<Soldier, Vector3>();
            foreach (var kv in spawns) if (kv.Key != null && kv.Key.name.StartsWith("Reserva_Puesto")) { reservas.Add(kv.Key); origen[kv.Key] = kv.Value; }
            W8Ok(ref ok, reservas.Count == 12, sb, $"hay 12 reservas Reserva_Puesto* ({reservas.Count})");
            if (vega == null) { ModoDios.Poner(diosAntes); W8Ok(ref ok, false, sb, "hay un soldado de Asalto"); Fin("FALLO " + sb); yield break; }
            try
            {
                Poseer(vega); yield return null;
                var carga = d.puestos[0].carga;
                Junto(vega, carga.transform.position, 2f);
                carga.PlantarPorPrueba(vega);
                carga.AcortarMecha(3f);
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 12f) yield return null;
                int adentro = 0, quietas = 0, vivas = 0; var detalle = new StringBuilder();
                foreach (var s in reservas)
                {
                    if (s == null || !s.Health.IsAlive) continue;   // una reserva muerta salio y combatio: no cuenta contra el check
                    vivas++;
                    bool dentro = R13_T8_EnCubiculo(s.transform.position);
                    bool cerca = Vector3.Distance(s.transform.position, origen[s]) < 4f;
                    if (dentro) adentro++; if (cerca) quietas++;
                    if ((dentro || cerca) && detalle.Length < 300) detalle.Append(s.name + (dentro ? "(adentro) " : "(a menos de 4 m) "));
                }
                W8Ok(ref ok, vivas > 0 && adentro == 0 && quietas == 0, sb, $"a los 12 s de plantar: {adentro} reservas siguen dentro del cubiculo y {quietas} a menos de 4 m de su aparicion, de {vivas} vivas ({detalle})");
            }
            finally { Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
