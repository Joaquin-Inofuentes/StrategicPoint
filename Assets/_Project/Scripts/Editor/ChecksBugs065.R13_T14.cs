using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T14: ambientacion de desierto. Arboles de terreno entre 600 y 1500 (escena Ruta, Ola 2), arbustos rodantes entre 8 y 12 en el sector
    // Ruta, emisor de polvo presente y 0 arboles a menos de 13 m de la ruta del tanque.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 983a desierto
        static IEnumerator Bug983a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, 0);   // la huida por la ruta: la camara esta en el sector Ruta
            foreach (var x in Esperar(5f)) yield return x;   // AmbientarLaRuta corre a los 2 s; los rodantes se reparten despues
            var d = OperacionDirector.Instancia; var yo = Poseido();
            if (d == null || yo == null) { Fin("FALLO sin director o jugador"); yield break; }
            ModoDios.Poner(true);
            // colocar la camara/jugador sobre la ruta del tanque
            if (d.rutaDelTanque != null && d.rutaDelTanque.Length > 2 && d.rutaDelTanque[d.rutaDelTanque.Length / 2] != null)
            {
                var p = d.rutaDelTanque[d.rutaDelTanque.Length / 2].position;
                yo.transform.position = p + Vector3.up * 1f; Physics.SyncTransforms();
            }
            foreach (var x in Esperar(4f)) yield return x;
            // 1) arboles de terreno
            Terrain terreno = null;
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t != null && t.name == "Terreno_ArbolesDesierto") { terreno = t; break; }
            int arboles = terreno != null && terreno.terrainData != null ? terreno.terrainData.treeInstanceCount : 0;
            W8Ok(ref ok, terreno != null && arboles >= 600 && arboles <= 1500, sb, $"Terreno_ArbolesDesierto: {(terreno != null ? arboles + " arboles" : "no existe (escena Ruta pendiente)")} (600 a 1500)");
            if (terreno != null)
            {
                W8Ok(ref ok, terreno.GetComponent<TerrainCollider>() == null, sb, "el terreno de arboles no tiene TerrainCollider");
                int cerca = 0;
                var ruta = d.rutaDelTanque;
                if (ruta != null && ruta.Length >= 2)
                    foreach (var ti in terreno.terrainData.treeInstances)
                    {
                        var w = Vector3.Scale(ti.position, terreno.terrainData.size) + terreno.transform.position;
                        if (SectorDeRuta.DistanciaALaRuta(w) < 13f) cerca++;
                    }
                W8Ok(ref ok, cerca == 0, sb, $"{cerca} arboles a menos de 13 m de la ruta del tanque (0)");
            }
            // 2) rodantes en el sector Ruta
            int rod = ArbustoRodante.Activos; int nombres = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t != null && t.name.StartsWith("ArbustoRodante_")) nombres++;
            var cam = Camera.main; bool enSector = cam != null && SectorDeRuta.Contiene(cam.transform.position);
            W8Ok(ref ok, rod >= 8 && rod <= 12, sb, $"{rod} arbustos rodantes activos de {nombres} (8 a 12; camara en el sector Ruta: {enSector})");
            bool sinFisica = true;
            foreach (var a in ArbustoRodante.Pool) if (a != null && (a.GetComponentInChildren<Collider>(true) != null || a.GetComponentInChildren<Rigidbody>(true) != null)) sinFisica = false;
            W8Ok(ref ok, sinFisica, sb, "los rodantes no tienen collider ni Rigidbody");
            // 3) polvo
            var polvo = GameObject.Find("PolvoDelDesierto");
            var ps = polvo != null ? polvo.GetComponent<ParticleSystem>() : null;
            W8Ok(ref ok, ps != null && ps.main.maxParticles <= 150 && ps.main.simulationSpace == ParticleSystemSimulationSpace.World, sb, $"emisor PolvoDelDesierto presente (max {(ps != null ? ps.main.maxParticles : 0)}, espacio mundo)");
            foreach (var x in Esperar(2f)) yield return x;
            W8Ok(ref ok, ps != null && ps.particleCount > 0 && ps.particleCount <= 150, sb, $"el polvo emite ({(ps != null ? ps.particleCount : 0)} particulas vivas)");
            int remolinos = 0; foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t != null && t.name.StartsWith("RemolinoDePolvo_")) remolinos++;
            W8Ok(ref ok, remolinos >= 3 && remolinos <= 4, sb, $"{remolinos} remolinos de polvo (3 o 4)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
