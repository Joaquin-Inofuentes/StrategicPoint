using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Operacion
{
    // Gente y autos DECORATIVOS de la ciudad de cubos (OperacionBuilder.CiudadDeCubos): civiles-cubo que caminan por las veredas y autos-cubo que
    // dan vueltas a las manzanas. No son Soldier ni vehiculos del juego: no tienen colliders, no entran al ActorRegistry, no cuentan como
    // enemigos ni como bajas y ninguna IA los ve. Solo se mueven los mas cercanos a la camara (maxActivos, 60 por defecto); el resto queda
    // apagado donde estaba. Se mueve con Time.deltaTime: con la pausa (timeScale 0) se queda quieto.
    public class CivilesDeAmbiente : MonoBehaviour
    {
        [System.Serializable]
        public class Ruta
        {
            public Vector3[] puntos;
            public bool circuito;   // true = da la vuelta y repite; false = va y vuelve
            [System.NonSerialized] public float[] acumulado;
            [System.NonSerialized] public float largo;
        }

        [System.Serializable]
        public class Figura
        {
            public Transform t;
            public int ruta;
            public float distancia, velocidad, fase;
            public bool auto, vuelta;       // vuelta = ya recorre la ruta de ida y vuelta al reves
            [System.NonSerialized] public bool activa;
        }

        public Ruta[] rutas = new Ruta[0];
        public Figura[] figuras = new Figura[0];
        public int maxActivos = 60;
        public float radioDeActividad = 150f;

        // Para las pruebas.
        public int ActivosAhora { get; private set; }
        public static int MaximoActivosVisto { get; private set; }

        float proximoRepaso;
        readonly List<(float d, int i)> cercanos = new List<(float, int)>();

        void Awake() { Preparar(); }
        void OnEnable() { Preparar(); proximoRepaso = 0f; }

        void Preparar()
        {
            if (rutas == null) return;
            foreach (var r in rutas)
            {
                if (r == null || r.puntos == null || r.puntos.Length < 2) continue;
                int n = r.puntos.Length;
                int cuantos = r.circuito ? n + 1 : n;
                r.acumulado = new float[cuantos];
                float a = 0f;
                for (int i = 1; i < cuantos; i++)
                {
                    a += Vector3.Distance(r.puntos[i % n], r.puntos[i - 1]);
                    r.acumulado[i] = a;
                }
                r.largo = a;
            }
        }

        // Deja a cada figura en su lugar de partida (lo llama el builder; el resto del tiempo las mueve Update).
        public void ColocarInicial()
        {
            Preparar();
            if (figuras == null) return;
            foreach (var f in figuras) if (f != null && f.t != null) Mover(f, 0f, true);
        }

        void Update()
        {
            if (figuras == null || figuras.Length == 0) return;
            float dt = Time.deltaTime;
            // Repaso (3 por segundo en tiempo real, aunque la partida este en pausa): quienes estan activos son los maxActivos mas cercanos a la camara.
            if (Time.unscaledTime >= proximoRepaso)
            {
                proximoRepaso = Time.unscaledTime + 0.35f;
                Repasar();
            }
            if (dt <= 0f) return;
            for (int i = 0; i < figuras.Length; i++)
            {
                var f = figuras[i];
                if (f == null || !f.activa || f.t == null) continue;
                Mover(f, dt, false);
            }
        }

        void Repasar()
        {
            var cam = CamaraPrincipal.Actual;
            Vector3 c = cam != null ? cam.transform.position : Vector3.zero;
            cercanos.Clear();
            for (int i = 0; i < figuras.Length; i++)
            {
                var f = figuras[i];
                if (f == null || f.t == null) continue;
                float d = (f.t.position - c).sqrMagnitude;
                if (d <= radioDeActividad * radioDeActividad) cercanos.Add((d, i));
            }
            cercanos.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < figuras.Length; i++) if (figuras[i] != null) figuras[i].activa = false;
            int n = Mathf.Min(maxActivos, cercanos.Count);
            for (int k = 0; k < n; k++) figuras[cercanos[k].i].activa = true;
            ActivosAhora = n;
            if (n > MaximoActivosVisto) MaximoActivosVisto = n;
            for (int i = 0; i < figuras.Length; i++)
            {
                var f = figuras[i];
                if (f == null || f.t == null) continue;
                if (f.t.gameObject.activeSelf != f.activa) f.t.gameObject.SetActive(f.activa);
            }
        }

        void Mover(Figura f, float dt, bool snap)
        {
            if (f.ruta < 0 || f.ruta >= rutas.Length) return;
            var r = rutas[f.ruta];
            if (r == null || r.puntos == null || r.largo <= 0.1f) return;
            f.distancia += f.velocidad * dt;
            float d, dirSigno = 1f;
            if (r.circuito) d = Mathf.Repeat(f.distancia, r.largo);
            else
            {
                float ciclo = Mathf.Repeat(f.distancia, r.largo * 2f);
                if (ciclo <= r.largo) d = ciclo; else { d = r.largo * 2f - ciclo; dirSigno = -1f; }
            }
            Posicion(r, d, out var pos, out var tang);
            if (dirSigno < 0f) tang = -tang;
            var t = f.t;
            if (f.auto)
            {
                t.position = pos;
                if (tang.sqrMagnitude > 0.0001f) t.rotation = snap ? Quaternion.LookRotation(tang, Vector3.up) : Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(tang, Vector3.up), 120f * dt);
            }
            else
            {
                pos.y += Mathf.Abs(Mathf.Sin(f.distancia * 2.6f + f.fase)) * 0.07f;   // paso
                t.position = pos;
                if (tang.sqrMagnitude > 0.0001f) t.rotation = snap ? Quaternion.LookRotation(tang, Vector3.up) : Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(tang, Vector3.up), 240f * dt);
            }
        }

        static void Posicion(Ruta r, float d, out Vector3 pos, out Vector3 tang)
        {
            int n = r.puntos.Length;
            int tramos = r.acumulado.Length - 1;
            int k = 0;
            while (k < tramos - 1 && r.acumulado[k + 1] < d) k++;
            var a = r.puntos[k % n]; var b = r.puntos[(k + 1) % n];
            float largo = r.acumulado[k + 1] - r.acumulado[k];
            float u = largo > 0.0001f ? Mathf.Clamp01((d - r.acumulado[k]) / largo) : 0f;
            pos = Vector3.Lerp(a, b, u);
            tang = b - a; tang.y = 0f;
            if (tang.sqrMagnitude > 0.0001f) tang.Normalize();
        }
    }
}
