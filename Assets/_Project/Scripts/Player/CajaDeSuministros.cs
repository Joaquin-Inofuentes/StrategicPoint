using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Presentation;

namespace SP.Player
{
    // Caja de suministros: antes las granadas (3) y la vida no se reponian NUNCA durante la partida.
    // Caminar hasta la caja con el soldado que manejas repone las granadas al maximo y cura el 60 % de
    // su vida. La caja se vuelve a llenar a los 45 s, asi que no se puede abusar de ella.
    public class CajaDeSuministros : MonoBehaviour
    {
        public const float Radio = 2.2f;
        public const float SegundosDeReposicion = 45f;
        public const float FraccionDeCuracion = 0.6f;

        public static readonly List<CajaDeSuministros> Todas = new List<CajaDeSuministros>();
        public static int Recogidas { get; private set; }

        Renderer[] renderers;
        float disponibleDesde;
        PlayerInputDriver driver;
        Vector3 basePos;

        public bool Disponible => Time.time >= disponibleDesde;

        // Crea una caja en el piso mas cercano a 'pos' (si algo la bloquea, prueba unos metros mas alla).
        public static CajaDeSuministros Crear(Vector3 pos)
        {
            for (int i = 0; i < 8; i++)
            {
                var p = pos + new Vector3(i * 2.5f, 0f, 0f);
                var desde = new Vector3(p.x, 60f, p.z);
                if (!Physics.Raycast(desde, Vector3.down, out var golpe, 120f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckBox(golpe.point + Vector3.up * 0.6f, new Vector3(0.6f, 0.4f, 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                return Construir(golpe.point);
            }
            return Construir(pos);
        }

        static CajaDeSuministros Construir(Vector3 suelo)
        {
            var raiz = new GameObject("CajaDeSuministros");
            raiz.transform.position = suelo + Vector3.up * 0.45f;
            Pieza(raiz.transform, "Caja", new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.6f, 0.6f), new Color(0.32f, 0.42f, 0.22f));
            Pieza(raiz.transform, "CruzH", new Vector3(0f, 0.31f, 0f), new Vector3(0.5f, 0.03f, 0.14f), Color.white);
            Pieza(raiz.transform, "CruzV", new Vector3(0f, 0.31f, 0f), new Vector3(0.14f, 0.03f, 0.5f), Color.white);
            var caja = raiz.AddComponent<CajaDeSuministros>();
            caja.renderers = raiz.GetComponentsInChildren<Renderer>();
            caja.basePos = raiz.transform.position;
            return caja;
        }

        static void Pieza(Transform padre, string nombre, Vector3 local, Vector3 escala, Color color)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = nombre;
            var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);   // no bloquea a la IA ni a las balas
            g.transform.SetParent(padre, false);
            g.transform.localPosition = local;
            g.transform.localScale = escala;
            g.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(color);
        }

        void OnEnable() { Todas.Add(this); }
        void OnDisable() { Todas.Remove(this); }

        void Update()
        {
            bool visible = Disponible;
            foreach (var r in renderers) if (r != null && r.enabled != visible) r.enabled = visible;
            if (visible)
            {
                // Se ve de lejos: sube y baja y gira despacio.
                transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.08f);
                transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
            }
            if (!visible) return;
            if (driver == null) driver = FindAnyObjectByType<PlayerInputDriver>();
            var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
            if (yo == null || !yo.Health.IsAlive) return;
            var d = yo.transform.position - basePos; d.y = 0f;
            if (d.sqrMagnitude > Radio * Radio) return;

            var arma = yo.Weapon;
            int antes = arma != null ? arma.Granadas : 0;
            if (arma != null) arma.ReponerGranadas();
            int cura = Mathf.RoundToInt(yo.Health.MaxHealth * FraccionDeCuracion);
            int vidaAntes = yo.Health.Current;
            yo.Health.Heal(cura);
            int granadasNuevas = arma != null ? arma.Granadas - antes : 0;
            int vidaNueva = yo.Health.Current - vidaAntes;
            if (granadasNuevas <= 0 && vidaNueva <= 0)
            {
                // Ya tenia todo: no se gasta la caja, pero se avisa (feedback siempre).
                Feedback.Accion(SfxKind.EmptyClick, "SUMINISTROS: YA ESTAS COMPLETO", basePos, Feedback.Warn, aviso: true, pulso: false, volumen: 0.4f);
                disponibleDesde = Time.time + 2f;
                return;
            }
            Recogidas++;
            disponibleDesde = Time.time + SegundosDeReposicion;
            string texto = "SUMINISTROS";
            if (granadasNuevas > 0) texto += $"  +{granadasNuevas} GRANADAS";
            if (vidaNueva > 0) texto += $"  +{vidaNueva} VIDA";
            Feedback.Accion(SfxKind.HealDone, texto, basePos, Feedback.Ok, aviso: true, pulso: true, volumen: 0.7f);
        }

        // Escenas de mision: tres cajas repartidas entre la salida y la plaza. Las pruebas y el tutorial las crean a mano.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CrearEnMision()
        {
            var mision = FindAnyObjectByType<SP.Mision.MisionDirector>();
            if (mision == null || Todas.Count > 0) return;
            var plaza = mision.Plaza;
            Crear(new Vector3(9f, 0f, 8f));
            Crear(new Vector3(plaza.x - 10f, 0f, plaza.z * 0.5f));
            Crear(new Vector3(plaza.x - 14f, 0f, plaza.z - 16f));
        }
    }
}
