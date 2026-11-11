using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;
using SP.Vehicles;
using G = SP.Operacion.MansionGeometria;

namespace SP.Presentation
{
    // Revision Opus 5.5 del diseno del nivel (2026-11-11). Dos ajustes que se aplican en juego (el nivel se arma por codigo y no se reconstruye la escena
    // para esto; el builder de la mansion crea los mismos bancos con el mismo nombre y entonces este parche no agrega nada):
    //   1. GALERIA DEL 1.er PISO: es un pasillo de 14 x 50 m con tres muebles; las emboscadas de los seis cuartos caian sobre una pista vacia, sin
    //      cobertura. Se suman 6 bancos bajos (3 x 1 x 0,9 m) alternados a ambos lados de la alfombra: dejan un carril libre de ~8 m, no tapan las puertas
    //      de los cuartos ni los puntos de aparicion de la guardia, son destructibles y la navegacion los rodea (NavMeshObstacle con carving).
    //   2. RUTA DEL TANQUE: un recorrido con una caja del tamano del tanque encontro 4 neumaticos (P_Env_Neumaticos, ambientacion) sobre la ruta en
    //      (206, 231). Se apagan los props P_Env_* chicos que caigan sobre la ruta; las estructuras no se tocan.
    public sealed class AjustesDeNivelOpus : MonoBehaviour
    {
        public const string NombreBanco = "P1_Banco_Galeria";
        public static readonly Vector2[] BancosDeGaleria =
        {
            new Vector2(299f, 261.8f), new Vector2(321f, 261.8f), new Vector2(336f, 261.8f),   // lado sur (los cuartos del sur abren en x 303, 316, 329; la torre B entra por x 344,7)
            new Vector2(310f, 270.4f), new Vector2(319f, 270.4f), new Vector2(342f, 270.4f),   // lado norte (puertas en x 313, 326, 340; la torre A sale en 305,1 / 270,8: deja >= 3 m)
        };
        public static readonly Vector3 TamanoBanco = new Vector3(3f, 1f, 0.9f);
        public const float AnchoDelTanque = 3.4f, LargoDelTanque = 7f, AltoDelTanque = 2.2f, RealceDelSuelo = 0.7f;

        public static int BancosCreados { get; private set; }
        public static int ObjetosDeRutaApagados { get; private set; }
        public static bool ListoGaleria { get; private set; }
        public static bool ListoRuta { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { BancosCreados = ObjetosDeRutaApagados = 0; ListoGaleria = ListoRuta = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            var go = new GameObject("AjustesDeNivelOpus") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<AjustesDeNivelOpus>();
        }

        OperacionDirector ultimo; float proximo;

        void Update()
        {
            if (Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 2f;
            var d = OperacionDirector.Instancia;
            if (d == null || !UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            if (d != ultimo) { ultimo = d; ListoGaleria = ListoRuta = false; }
            Aplicar(d);
        }

        public static void Aplicar(OperacionDirector d)
        {
            if (!ListoGaleria) ArmarGaleria();
            if (!ListoRuta && d != null) DespejarRutaDelTanque(d);
        }

        // ---------------------------------------------------------------- 1. bancos de la galeria
        static void ArmarGaleria()
        {
            var mansion = GameObject.Find("P_Op_8_Mansion");
            if (mansion == null) return;   // la mansion no esta (o esta apagada): se reintenta
            ListoGaleria = true;
            if (GameObject.Find(NombreBanco) != null) return;   // ya los trae el builder
            var mat = SafeMaterial.Create(new Color(0.30f, 0.17f, 0.10f));
            float y = G.Nivel(1);
            foreach (var p in BancosDeGaleria)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = NombreBanco;
                b.transform.SetParent(mansion.transform, false);
                b.transform.position = new Vector3(p.x, y + TamanoBanco.y * 0.5f, p.y);
                b.transform.localScale = TamanoBanco;
                b.GetComponent<Renderer>().sharedMaterial = mat;
                var m = b.AddComponent<ObstacleMarker>(); m.ConfigurarVida(250);
                var ob = b.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                ob.shape = UnityEngine.AI.NavMeshObstacleShape.Box; ob.size = Vector3.one; ob.center = Vector3.zero; ob.carving = true;
                BancosCreados++;
            }
            Physics.SyncTransforms();
            if (Application.isPlaying) Coberturas.Registrar();   // los bancos pasan a ser puntos de cobertura de la IA
        }

        // ---------------------------------------------------------------- 2. ruta del tanque
        // Colliders solidos (no vehiculos, soldados, cuerpos ni barreras de un solo bando) que toca una caja del tamano del tanque recorrida a lo largo de la ruta.
        // Lo usan el parche y el check Bug714a.
        public static List<Collider> BloqueosDeLaRuta(OperacionDirector d, float paso = 3f)
        {
            var res = new List<Collider>(); var visto = new HashSet<Collider>();
            if (d == null || d.rutaDelTanque == null) return res;
            var pts = new List<Vector3>();
            foreach (var r in d.rutaDelTanque) if (r != null) pts.Add(r.position);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1]; var dir = b - a; dir.y = 0f; float len = dir.magnitude; if (len < 0.1f) continue; dir /= len;
                for (float t = 0f; t < len; t += paso)
                {
                    var p = a + dir * t;
                    if (!Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out var suelo, 120f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var centro = new Vector3(p.x, suelo.point.y + RealceDelSuelo + AltoDelTanque * 0.5f, p.z);
                    foreach (var c in Physics.OverlapBox(centro, new Vector3(AnchoDelTanque * 0.5f, AltoDelTanque * 0.5f, LargoDelTanque * 0.5f), Quaternion.LookRotation(dir), ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c == null || c.GetComponentInParent<Vehicle>() != null || c.GetComponentInParent<Soldier>() != null || c.attachedRigidbody != null) continue;
                        if (BarreraSoloEnemigos.Contiene(c)) continue;
                        if (visto.Add(c)) res.Add(c);
                    }
                }
            }
            return res;
        }

        static void DespejarRutaDelTanque(OperacionDirector d)
        {
            if (d.rutaDelTanque == null || d.rutaDelTanque.Length < 2) return;
            ListoRuta = true;
            foreach (var c in BloqueosDeLaRuta(d))
            {
                if (c == null) continue;
                // Solo el objeto del collider (nunca un padre: "P_Env_*" tambien nombra grupos enteros) y solo si es un prop chico de ambientacion.
                if (!c.gameObject.activeSelf || !c.name.StartsWith("P_Env_", System.StringComparison.Ordinal)) continue;
                var b = c.bounds; if (Mathf.Max(b.size.x, b.size.z) > 8f) continue;
                c.gameObject.SetActive(false); ObjetosDeRutaApagados++;
            }
            Physics.SyncTransforms();
        }
    }
}
