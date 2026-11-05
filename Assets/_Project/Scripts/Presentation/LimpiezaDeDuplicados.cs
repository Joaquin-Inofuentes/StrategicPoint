using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Bugs #002 y #003 (Sheets, Infiltrar): "corregir modelos duplicados o encimados" y "corregir colisiones de objetos (chequear el resto del
    // nivel)". Hay piezas del entorno puestas dos veces en el mismo lugar (misma malla, misma pose, mismo material): se ven como parpadeos de
    // superficies coplanares (z-fighting) y suman un collider de mas. Al cargar cada escena del entorno se apaga el renderer repetido (y su
    // collider, si tambien es una copia exacta). Solo cuenta lo IDENTICO: misma instancia de malla, mismo material, posicion/rotacion/escala
    // iguales al centimetro, y objetos estaticos (sin Rigidbody ni animacion, ni unidades ni vehiculos). Es idempotente.
    public static class LimpiezaDeDuplicados
    {
        public static int Apagados { get; private set; }
        public static int CollidersApagados { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribir()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargar;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode modo)
        {
            if (!s.name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            int n = Limpiar(out int c);
            if (n > 0) GameLog.Line($"Geometria duplicada: {n} piezas repetidas apagadas ({c} colliders) al cargar {s.name}");
            if (SepararClaraboya()) GameLog.Line("Geometria solapada: la claraboya de la azotea se corrio del equipo que la atravesaba");
        }

        // Bug #001 (Sheets, azotea): "Solapamiento de geometrias". La claraboya (cupula de vidrio de 3,2 m) estaba en el mismo punto que la caja del
        // equipo de la azotea (4,4 m): el equipo la atravesaba. Se corre 8 m al costado libre (verificado sin otras piezas ahi).
        public static bool SepararClaraboya()
        {
            // La mansion puede estar apagada (se enciende en el objetivo 5): se busca tambien entre los inactivos y se compara por posicion.
            var claras = new List<Transform>(); var equipos = new List<Transform>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t.gameObject.scene.IsValid()) continue;
                if (t.name == "Claraboya") claras.Add(t); else if (t.name == "Azotea_Equipo") equipos.Add(t);
            }
            bool movida = false;
            foreach (var c in claras)
                foreach (var e in equipos)
                {
                    var d = c.position - e.position;
                    if (Mathf.Abs(d.x) > 3f || Mathf.Abs(d.z) > 3f) continue;   // ya esta separada
                    c.position += Vector3.left * 8f; movida = true; break;
                }
            return movida;
        }

        struct Clave : System.IEquatable<Clave>
        {
            public int malla, material; public Vector3Int pos, esc; public Vector3Int rot;
            public bool Equals(Clave o) => malla == o.malla && material == o.material && pos == o.pos && esc == o.esc && rot == o.rot;
            public override bool Equals(object o) => o is Clave k && Equals(k);
            public override int GetHashCode() { unchecked { return ((malla * 397 ^ material) * 397 ^ pos.GetHashCode()) * 397 ^ esc.GetHashCode() ^ rot.GetHashCode() * 31; } }
        }

        static Vector3Int Q(Vector3 v, float paso) => new Vector3Int(Mathf.RoundToInt(v.x / paso), Mathf.RoundToInt(v.y / paso), Mathf.RoundToInt(v.z / paso));

        public static int Limpiar(out int collidersApagados)
        {
            collidersApagados = 0;
            var vistos = new Dictionary<Clave, MeshRenderer>();
            int apagados = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (mr == null || !mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                if (mr.GetComponentInParent<SP.Actors.Soldier>() != null || mr.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (mr.GetComponentInParent<Rigidbody>() != null || mr.GetComponentInParent<Animator>() != null) continue;
                var t = mr.transform;
                // Mallas combinadas / grandes en el origen (suelo, calles fusionadas): no son piezas sueltas.
                if (t.position.sqrMagnitude < 0.0001f && mf.sharedMesh.vertexCount > 200) continue;
                var k = new Clave
                {
                    malla = mf.sharedMesh.GetEntityId().GetHashCode(), material = mr.sharedMaterial.GetEntityId().GetHashCode(),
                    pos = Q(t.position, 0.01f), esc = Q(t.lossyScale, 0.01f), rot = Q(t.eulerAngles, 0.5f)
                };
                if (!vistos.TryGetValue(k, out var primero)) { vistos[k] = mr; continue; }
                mr.enabled = false; apagados++;
                // El collider solo se apaga si el original tiene uno igual (misma pose): si no, es el unico que hay.
                var cb = mr.GetComponent<Collider>(); var cp = primero.GetComponent<Collider>();
                if (cb != null && cp != null && cb.enabled && cp.enabled && cb.GetType() == cp.GetType() && (cb.bounds.center - cp.bounds.center).sqrMagnitude < 0.0004f && (cb.bounds.size - cp.bounds.size).sqrMagnitude < 0.0004f)
                { cb.enabled = false; collidersApagados++; }
            }
            Apagados += apagados; CollidersApagados += collidersApagados;
            return apagados;
        }
    }
}
