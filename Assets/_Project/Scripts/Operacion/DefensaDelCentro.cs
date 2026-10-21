using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SP.Combat;
using SP.Core;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Operacion
{
    // Bug #272: "quiero que haya equipo de torretas para defender este puesto y que los enemigos vengan en helicopteros; los helicopteros son
    // derribables desde lejos y mas accion". Durante el hackeo del centro de datos:
    //   * TorretasAliadas ametralladoras automaticas (TorretaDeDefensa) rodean la computadora y disparan solas a enemigos y helicopteros.
    //   * Dos helicopteros de asalto (HelicopteroEnemigo.IniciarSobre) entran en distintos momentos, orbitan la computadora y ametrallan a la
    //     escuadra; tienen vida reducida y se los derriba a tiros desde lejos.
    public sealed class DefensaDelCentro : MonoBehaviour
    {
        public const int TorretasAliadas = 3;
        public static readonly float[] SegundosDeEntrada = { 5f, 40f };
        // Pedido 2026-10-21: "el helicoptero de la zona de hackeo eliminalo, genera mucha confusion". Sin helicopteros de asalto; quedan las torretas.
        public const bool HelicopterosDeAsalto = false;

        public static DefensaDelCentro Instancia { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; }

        public readonly List<TorretaDeDefensa> Torretas = new List<TorretaDeDefensa>();
        public readonly List<HelicopteroEnemigo> Helicopteros = new List<HelicopteroEnemigo>();
        public int HelicopterosLanzados { get; private set; }
        public int HelicopterosDerribados { get; private set; }

        Transform computadora, plantillaHalcon;
        float inicio = -1f;
        readonly HashSet<int> lanzados = new HashSet<int>();

        // Arma las torretas (visibles desde que se entra a la fase) y deja listo el reloj de los helicopteros.
        public static DefensaDelCentro Preparar(Transform computadora, Transform plantillaHalcon)
        {
            if (Instancia != null) return Instancia;
            if (computadora == null) return null;
            var go = new GameObject("DefensaDelCentro");
            var d = go.AddComponent<DefensaDelCentro>();
            d.computadora = computadora; d.plantillaHalcon = plantillaHalcon;
            Instancia = d;
            d.ArmarTorretas();
            return d;
        }

        void ArmarTorretas()
        {
            var c = computadora.position;
            // En arco por delante y a los flancos de la computadora (la entrada esta al sur, los enemigos llegan del norte y los costados).
            var desvios = new[] { new Vector2(-9f, 7f), new Vector2(9f, 7f), new Vector2(0f, 12f), new Vector2(-12f, -4f), new Vector2(12f, -4f) };
            for (int i = 0; i < desvios.Length && Torretas.Count < TorretasAliadas; i++)
            {
                var p = new Vector3(c.x + desvios[i].x, c.y, c.z + desvios[i].y);
                if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) p = hit.position;
                else continue;
                Torretas.Add(TorretaDeDefensa.Construir(transform, p, "TorretaAliada_" + (Torretas.Count + 1)));
            }
            GameLog.Line($"[Operacion] Defensa del centro: {Torretas.Count} torretas aliadas");
        }

        // El hackeo empezo: corre el reloj de los helicopteros.
        public void Comenzar() { if (inicio < 0f) inicio = Time.time; }

        void Update()
        {
            if (!HelicopterosDeAsalto || inicio < 0f || plantillaHalcon == null) return;
            float t = Time.time - inicio;
            for (int i = 0; i < SegundosDeEntrada.Length; i++)
            {
                if (lanzados.Contains(i) || t < SegundosDeEntrada[i]) continue;
                lanzados.Add(i);
                var h = HelicopteroEnemigo.IniciarSobre(plantillaHalcon, computadora, i * 180f + Random.Range(-30f, 30f));
                if (h == null) continue;
                Helicopteros.Add(h);
                HelicopterosLanzados++;
                h.AlDerribarse += () => HelicopterosDerribados++;
                AvisoCentral.Mostrar("¡HELICOPTEROS ENEMIGOS! DERRIBALOS A TIROS", 2.2f, new Color(0.55f, 0.07f, 0.05f, 0.9f));
            }
        }

        // Se completo el hackeo: los helicopteros se retiran (explotan) y las torretas se desarman.
        public void Terminar()
        {
            foreach (var h in Helicopteros) if (h != null && !h.Derribado) h.ExplotarEnElAire();
            Helicopteros.Clear();
            Instancia = null;
            Destroy(gameObject);
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        // Helicopteros enemigos vivos, para que las torretas los apunten.
        public static IEnumerable<HelicopteroEnemigo> HelicopterosVivos()
        {
            if (Instancia == null) yield break;
            foreach (var h in Instancia.Helicopteros) if (h != null && !h.Derribado) yield return h;
        }
    }

    // Ametralladora automatica aliada: sacos + pedestal + cañon que gira. Dispara rafagas a enemigos y helicopteros en rango.
    public sealed class TorretaDeDefensa : MonoBehaviour
    {
        public const float Alcance = 75f, SegundosEntreRafagas = 0.9f, SegundosEntreBalas = 0.09f, GiroGradosPorSeg = 240f;
        public const int BalasPorRafaga = 6, DanoPorBala = 10;
        public const float MultiplicadorContraVehiculo = 3f;
        static readonly Color ColorSaco = new Color(0.42f, 0.38f, 0.26f), ColorMetal = new Color(0.14f, 0.16f, 0.18f), ColorAliado = new Color(0.3f, 0.62f, 1f);

        public int Disparos { get; private set; }
        public Transform Objetivo { get; private set; }
        Transform pivote, boca;
        float proximoRetarget, proximaBala, proximaRafaga;
        int quedan;

        public static TorretaDeDefensa Construir(Transform padre, Vector3 pos, string nombre)
        {
            var raiz = new GameObject(nombre);
            raiz.transform.SetParent(padre, false);
            raiz.transform.position = pos;
            var t = raiz.AddComponent<TorretaDeDefensa>();
            var sacos = SafeMaterial.Create(ColorSaco);
            var metal = SafeMaterial.Create(ColorMetal);
            var luz = SafeMaterial.Create(ColorAliado);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var b = Pieza(raiz.transform, PrimitiveType.Cube, "Saco", new Vector3(Mathf.Sin(a) * 1.15f, 0.2f, Mathf.Cos(a) * 1.15f), new Vector3(0.85f, 0.4f, 0.5f), sacos);
                b.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            }
            Pieza(raiz.transform, PrimitiveType.Cylinder, "Pedestal", new Vector3(0f, 0.4f, 0f), new Vector3(0.45f, 0.4f, 0.45f), metal);
            var pv = new GameObject("Pivote").transform;
            pv.SetParent(raiz.transform, false);
            pv.localPosition = new Vector3(0f, 1.0f, 0f);
            Pieza(pv, PrimitiveType.Cube, "Cuerpo", Vector3.zero, new Vector3(0.55f, 0.35f, 0.7f), metal);
            Pieza(pv, PrimitiveType.Cube, "Cañon", new Vector3(0f, 0.02f, 0.75f), new Vector3(0.12f, 0.12f, 1.1f), metal);
            Pieza(pv, PrimitiveType.Cube, "LuzAliada", new Vector3(0f, 0.24f, -0.1f), new Vector3(0.2f, 0.08f, 0.2f), luz);
            var bc = new GameObject("Boca").transform;
            bc.SetParent(pv, false);
            bc.localPosition = new Vector3(0f, 0.02f, 1.35f);
            t.pivote = pv; t.boca = bc;
            return t;
        }

        static GameObject Pieza(Transform padre, PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Material mat)
        {
            var g = GameObject.CreatePrimitive(tipo);
            g.name = nombre;
            var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos; g.transform.localScale = escala;
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            return g;
        }

        void Update()
        {
            if (pivote == null) return;
            if (Time.time >= proximoRetarget) { proximoRetarget = Time.time + 0.3f; Objetivo = Elegir(); }
            if (Objetivo == null) return;
            var punto = Objetivo.position + Vector3.up * 1.1f;
            var d = punto - pivote.position;
            if (d.sqrMagnitude < 0.01f) return;
            var meta = Quaternion.LookRotation(d.normalized, Vector3.up);
            pivote.rotation = Quaternion.RotateTowards(pivote.rotation, meta, GiroGradosPorSeg * Time.deltaTime);
            bool alineada = Vector3.Angle(pivote.forward, d) < 6f;
            if (alineada && quedan <= 0 && Time.time >= proximaRafaga) { quedan = BalasPorRafaga; proximaRafaga = Time.time + SegundosEntreRafagas; }
            if (quedan > 0 && Time.time >= proximaBala) { proximaBala = Time.time + SegundosEntreBalas; quedan--; Disparar(punto); }
        }

        void Disparar(Vector3 punto)
        {
            var pool = ProjectilePool.Activo;
            if (pool == null) return;
            var origen = boca.position;
            var dir = (punto - origen).normalized;
            dir = (dir + Random.insideUnitSphere * 0.025f).normalized;
            pool.Spawn(origen, dir, -4, TeamId.Player, DanoPorBala, new Color(0.5f, 0.85f, 1f), 0f, 0f, null, 1f, MultiplicadorContraVehiculo);
            MuzzleLightPool.Flash(origen, new Color(0.6f, 0.85f, 1f), 3f, 6f);
            Disparos++;
            if ((Disparos & 1) == 0) AudioDirector.PlayClipAt(GenericSfx.GetWeaponShot(WeaponKind.Heavy), origen, 0.5f, 1.05f, PerfilEspacial.Disparo);
        }

        // Prioridad: helicoptero enemigo en rango (la amenaza del aire), si no el enemigo de a pie mas cercano.
        Transform Elegir()
        {
            float mejor = Alcance * Alcance; Transform elegido = null;
            foreach (var h in DefensaDelCentro.HelicopterosVivos())
            {
                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d < mejor) { mejor = d; elegido = h.transform; }
            }
            if (elegido != null) return elegido;
            mejor = Alcance * Alcance;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                float d = (s.transform.position - transform.position).sqrMagnitude;
                if (d < mejor) { mejor = d; elegido = s.transform; }
            }
            return elegido;
        }
    }
}

