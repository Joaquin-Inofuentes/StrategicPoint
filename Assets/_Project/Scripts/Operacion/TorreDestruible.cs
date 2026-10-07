using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // WP8 (#078): torre del cuartel con alguien arriba (francotirador de 12 m o torreta vigia de 5 m).
    //
    // Va en la RAIZ de la torre, junto a un ObstacleMarker con la vida (450 / 600) en modo SoloExplosiones: las balas no la bajan, solo un cohete,
    // una granada, el canon o una carga. Todo lo que se ve y todo lo que tiene collider cuelga de `pivote`: cuando la vida llega a cero el marcador
    // dispara Derrumbado (antes de apagar la raiz), la torre suelta el pivote al escenario, apaga sus colliders (para que el NavMesh que se
    // rehace los vea libres), mata a quien este arriba y lo vuelca 80 grados en 1,2 s hacia el patio. Al tocar el piso se parte en trozos
    // (Fragmentador) con escombros, polvo y estruendo.
    [DisallowMultipleComponent]
    public class TorreDestruible : MonoBehaviour
    {
        public enum Tipo { Francotirador, Torreta }

        public const int VidaFrancotirador = 450;
        public const int VidaTorreta = 600;
        public const float GradosDeVuelco = 80f;
        public const float SegundosDeVuelco = 1.2f;

        public Tipo tipo;
        public Transform pivote;          // lo que vuelca: visuales, colliders y la silueta
        public Soldier ocupante;          // el tirador de arriba (sale con el vuelco)
        public float mediaBase = 1.5f;    // distancia del centro al borde de la base: el eje del vuelco

        static readonly List<TorreDestruible> todas = new List<TorreDestruible>();
        public static IReadOnlyList<TorreDestruible> Todas => todas;
        public static event Action<TorreDestruible> Cayo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { todas.Clear(); Cayo = null; }

        ObstacleMarker marca;
        GameObject silueta;
        public ObstacleMarker Marca => marca != null ? marca : (marca = GetComponent<ObstacleMarker>());
        public bool EstaCaida { get; private set; }
        public bool Cayendo { get; private set; }
        public int Vida => Marca != null ? Marca.CurrentHealth : 0;
        public int VidaMaxima => Marca != null ? Marca.MaxHealth : 0;
        public Vector3 Cima => (pivote != null ? pivote.position : transform.position) + Vector3.up * AlturaDeLaCima;
        public float AlturaDeLaCima { get; set; } = 12f;

        void Awake()
        {
            if (!todas.Contains(this)) todas.Add(this);
            marca = GetComponent<ObstacleMarker>();
        }

        void OnDestroy() { todas.Remove(this); }

        void OnEnable() { ObstacleMarker.Derrumbado += AlDerrumbar; }
        void OnDisable() { ObstacleMarker.Derrumbado -= AlDerrumbar; }

        void Start()
        {
            if (Application.isPlaying) CrearSiluetaDeMinimapa();
        }

        // Silueta naranja en el minimapa (capa Minimap, sobre el icono gris de obstaculo): se ve donde hay torres en pie.
        void CrearSiluetaDeMinimapa()
        {
            int capa = LayerMask.NameToLayer("Minimap"); if (capa < 0) capa = 8;
            silueta = GameObject.CreatePrimitive(PrimitiveType.Quad);
            silueta.name = "SiluetaDeTorre";
            var col = silueta.GetComponent<Collider>(); if (col != null) Destroy(col);
            silueta.layer = capa;
            silueta.transform.SetParent(transform, false);
            silueta.transform.position = new Vector3(transform.position.x, 41f, transform.position.z);
            silueta.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float lado = tipo == Tipo.Francotirador ? 7f : 6f;
            silueta.transform.localScale = new Vector3(lado, lado, 1f);
            var mr = silueta.GetComponent<MeshRenderer>();
            mr.sharedMaterial = DiamondGizmo.NuevoMaterial(new Color(1f, 0.5f, 0.08f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // La silueta cuelga de la raiz y el ObstacleMarker rompe la raiz en trozos con Fragmentador apenas termina el evento Derrumbado: si
        // seguia ahi (aunque apagada) se rompia ella tambien y caian laminas naranjas enormes desde y=41. Se suelta del arbol y se destruye.
        // El laser del francotirador (un LineRenderer de 50-90 m colgado de la raiz) hace lo mismo: ensanchaba la caja a romper.
        void QuitarSilueta()
        {
            if (silueta != null)
            {
                silueta.transform.SetParent(null, true);
                silueta.SetActive(false);
                Destroy(silueta);
                silueta = null;
            }
            foreach (var lr in GetComponentsInChildren<LineRenderer>(true))
            {
                lr.enabled = false;
                lr.transform.SetParent(null, true);
                Destroy(lr.gameObject);
            }
        }

        void AlDerrumbar(ObstacleMarker m)
        {
            if (m != Marca || EstaCaida) return;
            Colapsar();
        }

        // Quien se lleva la baja de los que mueren arriba: el soldado que maneja el jugador (fue su explosion).
        static int IdDelJugador()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            var yo = d != null && d.Brain != null ? d.Brain.Current : null;
            return yo != null ? yo.Id : -1;
        }

        public static void Matar(Soldier s, int atacante)
        {
            if (s == null || s.Health == null || !s.Health.IsAlive) return;
            bool dios = ModoDios.Activo;
            if (dios) ModoDios.Poner(false);
            for (int i = 0; i < 40 && s.Health.IsAlive; i++) s.Health.TakeDamage(s.Health.Current + s.Health.MaxHealth, atacante);
            if (dios) ModoDios.Poner(true);
        }

        void Colapsar()
        {
            EstaCaida = true;
            Cayendo = true;
            Cayo?.Invoke(this);
            QuitarSilueta();

            // Hacia donde cae: hacia el centro del cuartel (el patio), no contra el muro perimetral.
            var basePos = transform.position;
            var haciaElPatio = new Vector3(-basePos.x, 0f, 0f);
            if (haciaElPatio.sqrMagnitude < 4f) haciaElPatio = Vector3.right;
            var dir = haciaElPatio.normalized;

            // El pivote se suelta de la raiz (que el marcador apaga en cuanto termine este evento) y deja de ser pared.
            Transform padreDelOcupante = null;
            if (pivote != null)
            {
                pivote.SetParent(null, true);
                foreach (var c in pivote.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            }
            if (ocupante != null)
            {
                padreDelOcupante = ocupante.transform.parent;
                int yo = IdDelJugador();
                Matar(ocupante, yo);
                if (pivote != null) ocupante.transform.SetParent(pivote, true);
            }
            AudioDirector.PlayAt(SfxKind.Explosion, basePos + Vector3.up * 2f, 1f, 1f);
            ImpactFx.SpawnExplosion(basePos + Vector3.up * 1.5f, 3.5f, false);
            if (Application.isPlaying && pivote != null)
            {
                var corredor = new GameObject("TorreCayendo_" + name).AddComponent<VuelcoDeTorre>();
                corredor.StartCoroutine(Vuelco(corredor, basePos, dir, padreDelOcupante));
            }
            else if (pivote != null) pivote.gameObject.SetActive(false);
            GameLog.Line($"[Operacion] cae la torre {name}");
        }

        // Para restaurar un estado guardado: la torre ya estaba caida (sin animacion).
        public void CaerYa()
        {
            if (EstaCaida) return;
            var m = Marca;
            if (m != null && !m.IsCollapsed) m.Demoler();
            else if (!EstaCaida) { EstaCaida = true; if (pivote != null) pivote.gameObject.SetActive(false); }
        }

        IEnumerator Vuelco(VuelcoDeTorre corredor, Vector3 basePos, Vector3 dir, Transform padreOcupante)
        {
            var eje = Vector3.Cross(Vector3.up, dir).normalized;
            var punto = new Vector3(basePos.x, 0f, basePos.z) + dir * mediaBase;
            float t = 0f, anterior = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / SegundosDeVuelco;
                float k = Mathf.Clamp01(t);
                float angulo = GradosDeVuelco * k * k;   // acelera: cae, no se inclina
                if (pivote == null) break;
                pivote.RotateAround(punto, eje, angulo - anterior);
                anterior = angulo;
                yield return null;
            }
            if (pivote != null)
            {
                var centro = pivote.position + dir * (AlturaDeLaCima * 0.5f);
                var origen = punto + Vector3.up * 0.5f;
                AudioDirector.PlayAt(SfxKind.Explosion, centro, 1f, 1f);
                ImpactFx.SpawnExplosion(centro + Vector3.up * 0.5f, 4.5f, false);
                ImpactFx.SpawnShockwaveRing(new Vector3(centro.x, 0.3f, centro.z), new Color(0.85f, 0.8f, 0.7f), 7f, 0.8f);
                // Las piezas grandes se parten en trozos con fisica; las chicas se apagan con el resto.
                var piezas = new List<Transform>();
                foreach (var mr in pivote.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (mr.GetComponent<TextMesh>() != null) continue;
                    var s = mr.transform.lossyScale;
                    if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= 2.5f) piezas.Add(mr.transform);
                }
                int cuantas = 0;
                foreach (var p in piezas)
                {
                    if (cuantas++ >= 4) break;
                    Fragmentador.Romper(p, origen, 6f, 10);
                    p.gameObject.SetActive(false);
                }
                if (ocupante != null) ocupante.transform.SetParent(padreOcupante, true);
                pivote.gameObject.SetActive(false);
            }
            Cayendo = false;
            if (corredor != null) Destroy(corredor.gameObject);
        }
    }

    // Soporte de la corrutina del vuelco: la raiz de la torre se apaga en cuanto el marcador termina de derrumbarse.
    public class VuelcoDeTorre : MonoBehaviour { }

    // Coloca a un soldado enemigo en su puesto elevado (torre, reflector) y lo deja quieto: el cerebro normal no corre (MontadoEnVehiculo, el
    // mismo mecanismo que usan los asientos de un vehiculo), el agente de navegacion se apaga (esta a metros de la malla) y el tirador lo maneja
    // el componente de la torre. El soldado se instancia en el suelo (en la malla, para que su NavMeshAgent nazca bien) y sube en Start.
    public static class PuestoElevado
    {
        public static void Montar(Soldier s, Vector3 pies, float yaw)
        {
            if (s == null) return;
            var agente = s.GetComponent<NavMeshAgent>();
            if (agente != null) agente.enabled = false;
            s.transform.SetPositionAndRotation(pies + Vector3.up * SoldierMotor.PivoteSobrePiso, Quaternion.Euler(0f, yaw, 0f));
            if (s.Brain != null)
            {
                s.Brain.SetPatrolWaypoints(null);
                s.Brain.MontadoEnVehiculo = true;
            }
            if (s.Motor != null) { s.Motor.SetCrouching(false); s.Motor.SetRunning(false); }
            LuzDeSilueta(s);
        }

        // #104: de noche el enemigo en altura (azul oscuro sobre cielo oscuro) no se distinguia. Luz de rebote debil delante del pecho
        // (como el resplandor de la propia lampara/arma): recorta la silueta sin ser un resaltado a traves de paredes.
        public const string NombreLuzDeSilueta = "LuzDeSilueta";
        static void LuzDeSilueta(Soldier s)
        {
            if (s.transform.Find(NombreLuzDeSilueta) != null) return;
            var go = new GameObject(NombreLuzDeSilueta);
            go.transform.SetParent(s.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.55f, 1.1f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 5.5f; l.intensity = 2.2f;
            l.color = new Color(0.78f, 0.86f, 1f);
            l.shadows = LightShadows.None;
        }
    }
}
