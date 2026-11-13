using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // Pedido 2026-11-13: "un perro que venga saltando a gran velocidad y ataque; su ladrido real y su caminata". Es un soldado enemigo (asi lo ven las
    // balas, los aliados, el minimapa y los conteos de la mision) con el cuerpo humano oculto y un modelo de perro armado por codigo (cubos de la misma
    // estetica del juego), con el cerebro de soldado apagado: PerroEnemigo lo maneja.
    //   MERODEA  camina despacio cerca de donde aparecio (ciclo de caminata de trote).
    //   ALERTA   ve a un aliado/jugador a menos de RangoDeVision: se planta, gruñe y ladra (0,45 s) antes de salir.
    //   CARGA    galopa a gran velocidad por la malla de navegacion hacia el blanco (ciclo de galope con rebote del cuerpo).
    //   SALTO    a menos de AlcanceDeSalto y con linea libre: se lanza en arco y muerde al caer.
    //   MUERDE   pegado al blanco: mordidas cada 0,8 s con la boca abriendose y cerrandose.
    public sealed class PerroEnemigo : MonoBehaviour
    {
        public enum Fase { Merodea, Alerta, Carga, Salto, Muerde, Muerto }

        public const int Vida = 90, DanoDeMordida = 14, DanoDeSalto = 26;
        public const float VelocidadDeCarga = 11f, VelocidadDeMarcha = 1.7f, RangoDeVision = 38f, AlcanceDeSalto = 7.5f, AlcanceMinimoDeSalto = 2.6f;
        public const float SegundosDeAlerta = 0.45f, SegundosEntreMordidas = 0.8f, EsperaDelSalto = 2.2f, AlcanceDeMordida = 1.7f, DuracionMinimaDelSalto = 0.34f;
        public const float AlturaDelPivote = 0.8f, EscalaDelModelo = 1.18f;

        public static int Creados { get; private set; }
        public Fase Estado { get; private set; } = Fase.Merodea;
        public int Ladridos { get; private set; }
        public int Saltos { get; private set; }
        public int Mordidas { get; private set; }
        public int Pasos { get; private set; }
        public float VelocidadActual { get; private set; }
        public float VelocidadMaxima { get; private set; }
        public Transform Modelo { get; private set; }
        public Soldier Blanco { get; private set; }
        public float MetrosPedidos;   // diagnostico: lo que el perro le pidio caminar al motor
        public float ProgresoDelSalto => Estado == Fase.Salto && saltoDur > 0f ? Mathf.Clamp01(saltoT / saltoDur) : 0f;

        Soldier soldado; float t0Fase, proximaMordida, proximoSalto, proximoLadrido, fasePaso, ultimoPasoDeFase;
        Vector3 casa, destinoMerodeo, ultimaPos, saltoDesde, saltoHasta; float saltoT, saltoDur;
        float proximoMerodeo; float bocaAbierta, cabezaArriba, colaFase, muerteT;
        NavMeshPathCache camino;   // se crea en Start: un NavMeshPath no puede construirse en un inicializador de campo de un MonoBehaviour

        // Partes animadas
        Transform cuerpo, cabeza, mandibula, cola;
        readonly Transform[] cadera = new Transform[4];   // 0 DI, 1 DD, 2 TI, 3 TD
        readonly Transform[] rodilla = new Transform[4];
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Creados = 0; mats.Clear(); }

        public static PerroEnemigo Aplicar(Soldier s)
        {
            if (s == null || s.Team != TeamId.Enemy) return null;
            var ya = s.GetComponent<PerroEnemigo>(); if (ya != null) return ya;
            s.gameObject.name = "Perro_" + (++Creados);
            return s.gameObject.AddComponent<PerroEnemigo>();
        }

        // -------------------------------------------------------------- arranque
        System.Collections.IEnumerator Start()
        {
            soldado = GetComponent<Soldier>(); camino = new NavMeshPathCache();
            yield return null; yield return null;   // despues de los Start de Soldier/AiBrain/SoldierLook
            if (soldado == null || soldado.Health == null) yield break;
            soldado.Health.Initialize(soldado.Id, Vida);
            if (soldado.Brain != null) { soldado.Brain.enabled = false; soldado.Brain.Anulado = true; }   // el WorldSimulationDriver ticka los cerebros a mano: apagar el componente no alcanza
            var anim = GetComponentInChildren<Animator>(true); if (anim != null) anim.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = false;   // el cuerpo humano y su arma, antes de armar el modelo del perro
            AjustarColision();
            ConstruirModelo();
            casa = transform.position; ultimaPos = transform.position; proximoMerodeo = Time.time + 1f;
            if (soldado.Motor != null) soldado.Motor.FactorDeClase = 1f;
            GameLog.Line($"[Perro] {name}: vida {Vida}, carga a {VelocidadDeCarga:0} m/s, salto de hasta {AlcanceDeSalto:0.0} m");
        }

        // El collider del cuerpo es el de un soldado de pie: se achica al tamano de un perro (largo y bajo).
        void AjustarColision()
        {
            var c = GetComponent<Collider>();
            if (c is CapsuleCollider cap) { cap.radius = 0.36f; cap.height = 0.95f; cap.center = new Vector3(0f, -0.30f, 0f); }
            else if (c is BoxCollider bx) { bx.size = new Vector3(0.5f, 0.75f, 1.0f); bx.center = new Vector3(0f, -0.30f, 0f); }
        }

        // -------------------------------------------------------------- modelo (cubos; el frente es +Z; los pies quedan en el piso)
        static Material Mat(string clave, Color c, float suave = 0.25f, Color? emision = null)
        {
            if (mats.TryGetValue(clave, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MAT_Perro_" + clave };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", suave); m.SetFloat("_Metallic", 0f);
            if (emision.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emision.Value); }
            mats[clave] = m; return m;
        }

        static Transform Cubo(Transform padre, string n, Vector3 pos, Vector3 esc, Material m, Vector3? rot = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(padre, false); g.transform.localPosition = pos; g.transform.localScale = esc;
            if (rot.HasValue) g.transform.localRotation = Quaternion.Euler(rot.Value);
            var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return g.transform;
        }

        static Transform Nodo(Transform padre, string n, Vector3 pos)
        {
            var g = new GameObject(n).transform; g.SetParent(padre, false); g.localPosition = pos; return g;
        }

        void ConstruirModelo()
        {
            var raiz = Nodo(transform, "Modelo", new Vector3(0f, -AlturaDelPivote, 0f));
            raiz.localScale = Vector3.one * EscalaDelModelo;
            Modelo = raiz;
            // pelaje castano rojizo con manchas fuego claras (que se lea de noche contra el asfalto) y un leve brillo propio, como el resto de los enemigos
            var pelaje = Mat("pelaje", new Color(0.40f, 0.20f, 0.09f), 0.3f, new Color(0.07f, 0.03f, 0.01f));
            var fuego = Mat("fuego", new Color(0.86f, 0.60f, 0.32f), 0.3f, new Color(0.10f, 0.06f, 0.02f));
            var oscuro = Mat("oscuro", new Color(0.09f, 0.06f, 0.05f), 0.5f);
            var collar = Mat("collar", new Color(0.78f, 0.08f, 0.10f), 0.4f, new Color(0.35f, 0.02f, 0.03f));
            var metal = Mat("metal", new Color(0.7f, 0.72f, 0.76f), 0.8f);
            var ojo = Mat("ojo", new Color(1f, 0.55f, 0.05f), 0.9f, new Color(3f, 1.1f, 0.1f));
            var diente = Mat("diente", new Color(0.95f, 0.93f, 0.85f), 0.6f);

            cuerpo = Nodo(raiz, "Cuerpo", Vector3.zero);
            // tronco: pecho profundo, cintura fina, anca
            Cubo(cuerpo, "Torso", new Vector3(0f, 0.60f, 0f), new Vector3(0.30f, 0.30f, 0.62f), pelaje);
            Cubo(cuerpo, "Pecho", new Vector3(0f, 0.61f, 0.27f), new Vector3(0.34f, 0.40f, 0.30f), pelaje);
            Cubo(cuerpo, "PechoClaro", new Vector3(0f, 0.50f, 0.36f), new Vector3(0.22f, 0.20f, 0.16f), fuego);
            Cubo(cuerpo, "Anca", new Vector3(0f, 0.60f, -0.28f), new Vector3(0.32f, 0.34f, 0.30f), pelaje);
            Cubo(cuerpo, "Lomo", new Vector3(0f, 0.78f, 0.02f), new Vector3(0.12f, 0.06f, 0.55f), oscuro);   // franja oscura del lomo
            // cuello y cabeza
            var cuello = Cubo(cuerpo, "Cuello", new Vector3(0f, 0.80f, 0.50f), new Vector3(0.19f, 0.19f, 0.32f), pelaje, new Vector3(-38f, 0f, 0f));
            Cubo(cuerpo, "Collar", new Vector3(0f, 0.78f, 0.46f), new Vector3(0.25f, 0.06f, 0.25f), collar, new Vector3(-38f, 0f, 0f));
            Cubo(cuerpo, "Placa", new Vector3(0f, 0.72f, 0.56f), new Vector3(0.05f, 0.06f, 0.02f), metal);
            for (int i = -1; i <= 1; i++) Cubo(cuerpo, "Pua" + i, new Vector3(i * 0.075f, 0.88f, 0.42f), new Vector3(0.03f, 0.04f, 0.03f), metal, new Vector3(-38f, 0f, 0f));
            cabeza = Nodo(cuerpo, "Cabeza", new Vector3(0f, 0.93f, 0.66f));
            Cubo(cabeza, "Craneo", Vector3.zero, new Vector3(0.20f, 0.19f, 0.22f), pelaje);
            Cubo(cabeza, "Hocico", new Vector3(0f, -0.035f, 0.18f), new Vector3(0.13f, 0.10f, 0.20f), fuego);
            Cubo(cabeza, "Nariz", new Vector3(0f, -0.005f, 0.285f), new Vector3(0.07f, 0.05f, 0.03f), oscuro);
            Cubo(cabeza, "Ceja1", new Vector3(-0.05f, 0.06f, 0.09f), new Vector3(0.05f, 0.03f, 0.04f), fuego);
            Cubo(cabeza, "Ceja2", new Vector3(0.05f, 0.06f, 0.09f), new Vector3(0.05f, 0.03f, 0.04f), fuego);
            Cubo(cabeza, "Ojo1", new Vector3(-0.07f, 0.03f, 0.105f), new Vector3(0.035f, 0.035f, 0.02f), ojo);
            Cubo(cabeza, "Ojo2", new Vector3(0.07f, 0.03f, 0.105f), new Vector3(0.035f, 0.035f, 0.02f), ojo);
            Cubo(cabeza, "Oreja1", new Vector3(-0.075f, 0.14f, -0.03f), new Vector3(0.06f, 0.14f, 0.04f), pelaje, new Vector3(-10f, 0f, 12f));
            Cubo(cabeza, "Oreja2", new Vector3(0.075f, 0.14f, -0.03f), new Vector3(0.06f, 0.14f, 0.04f), pelaje, new Vector3(-10f, 0f, -12f));
            mandibula = Nodo(cabeza, "Mandibula", new Vector3(0f, -0.085f, 0.10f));
            Cubo(mandibula, "MandibulaCubo", new Vector3(0f, -0.01f, 0.09f), new Vector3(0.11f, 0.04f, 0.17f), fuego);
            Cubo(mandibula, "Lengua", new Vector3(0f, 0.012f, 0.09f), new Vector3(0.06f, 0.015f, 0.12f), Mat("lengua", new Color(0.8f, 0.25f, 0.3f), 0.5f));
            for (int i = 0; i < 3; i++) { Cubo(mandibula, "DienteA" + i, new Vector3(-0.045f, 0.02f, 0.05f + i * 0.05f), new Vector3(0.012f, 0.03f, 0.012f), diente); Cubo(mandibula, "DienteB" + i, new Vector3(0.045f, 0.02f, 0.05f + i * 0.05f), new Vector3(0.012f, 0.03f, 0.012f), diente); }
            // cola
            cola = Nodo(cuerpo, "Cola", new Vector3(0f, 0.70f, -0.42f));
            Cubo(cola, "ColaCubo", new Vector3(0f, 0.09f, -0.10f), new Vector3(0.06f, 0.06f, 0.30f), pelaje, new Vector3(35f, 0f, 0f));
            // patas: cadera -> muslo -> rodilla -> pierna -> pata
            float[] px = { -0.13f, 0.13f, -0.13f, 0.13f }, pz = { 0.28f, 0.28f, -0.30f, -0.30f };
            for (int i = 0; i < 4; i++)
            {
                bool trasera = i >= 2;
                cadera[i] = Nodo(raiz, "Cadera" + i, new Vector3(px[i], 0.50f, pz[i]));
                Cubo(cadera[i], "Muslo", new Vector3(0f, -0.13f, trasera ? -0.02f : 0f), new Vector3(0.10f, 0.30f, 0.12f), pelaje, trasera ? new Vector3(-8f, 0f, 0f) : (Vector3?)null);
                rodilla[i] = Nodo(cadera[i], "Rodilla", new Vector3(0f, -0.27f, trasera ? -0.03f : 0f));
                Cubo(rodilla[i], "Pierna", new Vector3(0f, -0.11f, 0f), new Vector3(0.07f, 0.24f, 0.08f), fuego);
                Cubo(rodilla[i], "Pata", new Vector3(0f, -0.225f, 0.03f), new Vector3(0.095f, 0.05f, 0.14f), fuego);
                Cubo(rodilla[i], "Garra", new Vector3(0f, -0.235f, 0.105f), new Vector3(0.06f, 0.03f, 0.03f), oscuro);
            }
            // sombra propia: un disco oscuro bajo el perro (los cubos ya proyectan, esto asienta la silueta)
            ultimoPasoDeFase = 0f;
        }

        // -------------------------------------------------------------- logica
        Soldier BuscarBlanco()
        {
            Soldier mejor = null; float md = RangoDeVision;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(s.transform.position, transform.position);
                if (d < md) { md = d; mejor = s; }
            }
            return mejor;
        }

        void CambiarFase(Fase f) { Estado = f; t0Fase = Time.time; }

        Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

        void Mirar(Vector3 dir, float dt, float grados = 540f)
        {
            dir.y = 0f; if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), grados * dt);
        }

        // Avanza por el camino de la malla hacia 'meta' a 'vel' m/s usando el motor del soldado (colisiones y escalones incluidos).
        void Avanzar(Vector3 meta, float vel, float dt)
        {
            var dir = camino.SiguientePunto(transform.position, meta) - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0004f) return;
            Mirar(dir, dt);
            if (soldado.Motor == null) return;
            soldado.Motor.FactorDeBuff = 1f; soldado.Motor.SpeedMultiplier = vel / 5f;
            float k = Mathf.Clamp01(Vector3.Angle(transform.forward, dir) < 70f ? 1f : 0.25f);
            MetrosPedidos += soldado.Motor.MoveSpeed * k * dt;
            soldado.Motor.Move(transform.forward * k, dt);
        }

        void Update()
        {
            if (soldado == null || soldado.Health == null || Modelo == null) return;
            float dt = Time.deltaTime;
            if (!soldado.Health.IsAlive) { if (Estado != Fase.Muerto) { CambiarFase(Fase.Muerto); muerteT = 0f; } AnimarMuerte(dt); return; }

            Blanco = BuscarBlanco();
            switch (Estado)
            {
                case Fase.Merodea:
                    if (Blanco != null) { CambiarFase(Fase.Alerta); PonerVoz(SfxKind.GruniidoDePerro, 0.7f); break; }
                    if (Time.time >= proximoMerodeo || Vector3.Distance(Plano(transform.position), Plano(destinoMerodeo)) < 0.5f)
                    {
                        proximoMerodeo = Time.time + Random.Range(1.5f, 3.5f);
                        var q = casa + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                        destinoMerodeo = UnityEngine.AI.NavMesh.SamplePosition(q, out var h, 2f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : casa;
                    }
                    if (Vector3.Distance(Plano(transform.position), Plano(destinoMerodeo)) > 0.6f) Avanzar(destinoMerodeo, VelocidadDeMarcha, dt);
                    break;
                case Fase.Alerta:
                    if (Blanco == null) { CambiarFase(Fase.Merodea); break; }
                    Mirar(Blanco.transform.position - transform.position, dt);
                    if (Time.time >= proximoLadrido && Time.time - t0Fase > 0.1f) { Ladrar(false); proximoLadrido = Time.time + 1.2f; }
                    if (Time.time - t0Fase >= SegundosDeAlerta) { CambiarFase(Fase.Carga); proximoSalto = Time.time + 0.3f; }
                    break;
                case Fase.Carga:
                {
                    if (Blanco == null) { CambiarFase(Fase.Merodea); break; }
                    var aBlanco = Blanco.transform.position - transform.position; float d = Plano(aBlanco).magnitude;
                    if (d <= AlcanceDeMordida) { CambiarFase(Fase.Muerde); proximaMordida = Time.time + 0.15f; PonerVoz(SfxKind.GruniidoDePerro, 0.6f); break; }
                    if (d <= AlcanceDeSalto && d >= AlcanceMinimoDeSalto && Time.time >= proximoSalto && LineaLibre(Blanco)) { IniciarSalto(Blanco); break; }
                    Avanzar(Blanco.transform.position, VelocidadDeCarga, dt);
                    if (Time.time >= proximoLadrido) { Ladrar(true); proximoLadrido = Time.time + Random.Range(1.3f, 2.1f); }
                    break;
                }
                case Fase.Salto:
                    TickSalto(dt);
                    break;
                case Fase.Muerde:
                {
                    if (Blanco == null) { CambiarFase(Fase.Merodea); break; }
                    var aBlanco = Blanco.transform.position - transform.position; float d = Plano(aBlanco).magnitude;
                    Mirar(aBlanco, dt);
                    if (d > AlcanceDeMordida + 0.9f) { CambiarFase(Fase.Carga); proximoSalto = Time.time + 0.4f; break; }
                    if (d > 1.0f && soldado.Motor != null) { soldado.Motor.SpeedMultiplier = 2f; soldado.Motor.Move(transform.forward, dt); }
                    if (Time.time >= proximaMordida) { Morder(Blanco, DanoDeMordida); proximaMordida = Time.time + SegundosEntreMordidas; }
                    break;
                }
            }
            Animar(dt);
        }

        bool LineaLibre(Soldier blanco)
        {
            var a = transform.position + Vector3.up * 0.2f; var b = blanco.transform.position;
            return !Physics.Linecast(a, b, out var h, ~0, QueryTriggerInteraction.Ignore) || h.collider.transform.IsChildOf(blanco.transform);
        }

        // -------------------------------------------------------------- salto
        void IniciarSalto(Soldier blanco)
        {
            Saltos++; CambiarFase(Fase.Salto);
            saltoDesde = transform.position;
            var dir = Plano(blanco.transform.position - transform.position).normalized;
            saltoHasta = blanco.transform.position - dir * 0.9f; saltoHasta.y = blanco.transform.position.y;
            saltoDur = Mathf.Max(DuracionMinimaDelSalto, Plano(saltoHasta - saltoDesde).magnitude / 17f);   // ~17 m/s en el aire
            saltoT = 0f; Ladrar(true);
            proximoSalto = Time.time + EsperaDelSalto;
        }

        void TickSalto(float dt)
        {
            saltoT += dt;
            float k = Mathf.Clamp01(saltoT / saltoDur);
            // el destino sigue al blanco hasta la mitad del vuelo (si el aliado se mueve, el perro corrige) y despues se compromete
            if (Blanco != null && k < 0.55f) { var dir = Plano(Blanco.transform.position - saltoDesde).normalized; saltoHasta = Blanco.transform.position - dir * 0.9f; saltoHasta.y = Blanco.transform.position.y; }
            var p = Vector3.Lerp(saltoDesde, saltoHasta, k);
            float altura = 4f * 1.05f * k * (1f - k);   // arco parabolico de ~1,05 m de flecha
            var nueva = new Vector3(p.x, p.y + altura, p.z);
            var antes = transform.position;
            // un muro en el camino corta el vuelo: se cae donde esta
            if (Physics.Linecast(antes + Vector3.up * 0.2f, nueva + Vector3.up * 0.2f, out var h, ~0, QueryTriggerInteraction.Ignore) && !h.collider.transform.IsChildOf(transform) && h.collider.GetComponentInParent<Soldier>() == null) { Aterrizar(false); return; }
            transform.position = nueva;
            Mirar(saltoHasta - saltoDesde, dt, 900f);
            VelocidadActual = Plano(nueva - antes).magnitude / Mathf.Max(0.0001f, dt);
            VelocidadMaxima = Mathf.Max(VelocidadMaxima, VelocidadActual);
            if (k >= 1f) Aterrizar(true);
        }

        void Aterrizar(bool llego)
        {
            ApoyoEnElPiso.Apoyar(transform);
            if (llego && Blanco != null && Plano(Blanco.transform.position - transform.position).magnitude <= AlcanceDeMordida + 0.8f) { Morder(Blanco, DanoDeSalto); CambiarFase(Fase.Muerde); proximaMordida = Time.time + SegundosEntreMordidas; }
            else CambiarFase(Fase.Carga);
        }

        void Morder(Soldier blanco, int dano)
        {
            if (blanco == null || blanco.Health == null || !blanco.Health.IsAlive) return;
            Mordidas++; bocaAbierta = 1f;
            blanco.Health.TakeDamage(dano, soldado.Id);
            AudioDirector.PlayAt(SfxKind.Hit, blanco.transform.position + Vector3.up, 0.8f, 1f);
            ImpactCubes.Spawn(blanco.transform.position + Vector3.up * 1.1f, -transform.forward, ImpactSurface.Soldier, dano);
        }

        // -------------------------------------------------------------- voz
        public void Ladrar(bool corto)
        {
            Ladridos++; bocaAbierta = 1f; cabezaArriba = 1f;
            PonerVoz(corto ? SfxKind.LadridoCorto : SfxKind.Ladrido, 1f);
        }
        void PonerVoz(SfxKind k, float vol) => AudioDirector.PlayAt(k, transform.position + Vector3.up * 0.6f, vol, 1.2f);

        // -------------------------------------------------------------- animacion
        void Animar(float dt)
        {
            var p = transform.position; var mov = p - ultimaPos; mov.y = 0f; ultimaPos = p;   // en el plano: el vaiven vertical del apoyo en el piso no es velocidad
            float vel = Estado == Fase.Salto ? VelocidadActual : mov.magnitude / Mathf.Max(0.0001f, dt);
            if (vel > 40f) vel = 0f;   // un teletransporte no cuenta
            VelocidadActual = Mathf.Lerp(VelocidadActual, vel, 0.35f);
            VelocidadMaxima = Mathf.Max(VelocidadMaxima, VelocidadActual);
            bool galope = VelocidadActual > 4.5f, camina = VelocidadActual > 0.25f;
            // fase de zancada: avanza con la distancia (0,95 m por ciclo caminando, 1,9 m galopando)
            float ciclo = galope ? 1.9f : 0.95f;
            if (Estado != Fase.Salto) fasePaso += (VelocidadActual * dt / ciclo) * Mathf.PI * 2f;
            float amp = galope ? 52f : camina ? 24f : 0f;
            float s0 = Mathf.Sin(fasePaso);
            // patas: al trote en diagonales (DI+TD / DD+TI); al galope los pares delanteros y traseros juntos con desfase
            float[] fase = galope ? new[] { 0f, 0.35f, 2.7f, 3.05f } : new[] { 0f, Mathf.PI, Mathf.PI, 0f };
            for (int i = 0; i < 4; i++)
            {
                float a, rod;
                if (Estado == Fase.Salto)
                {
                    a = i < 2 ? -55f : 50f; rod = i < 2 ? 5f : 35f;   // delanteras adelante, traseras atras (cuerpo estirado)
                }
                else
                {
                    float sn = Mathf.Sin(fasePaso + fase[i]);
                    a = sn * amp * (i >= 2 ? 0.8f : 1f);
                    rod = Mathf.Max(0f, Mathf.Cos(fasePaso + fase[i])) * (galope ? 55f : 38f) * (camina ? 1f : 0f) * (i >= 2 ? 1.2f : 0.8f);   // la rodilla se dobla al levantar la pata
                    if (Estado == Fase.Muerde || Estado == Fase.Alerta) { a *= 0f; rod = 0f; }
                    // paso: una pata apoya cuando el seno cruza hacia abajo
                    if (camina && Estado != Fase.Salto) { int ciclos = Mathf.FloorToInt((fasePaso + fase[i]) / (Mathf.PI * 2f)); if (i == 0 && ciclos != pasosContados) { pasosContados = ciclos; Pasos++; AudioDirector.PlayAt(SfxKind.PasoDePerro, p, galope ? 0.7f : 0.35f, 0.4f); } }
                }
                if (cadera[i] != null) cadera[i].localRotation = Quaternion.Euler(a, 0f, 0f);
                if (rodilla[i] != null) rodilla[i].localRotation = Quaternion.Euler(i >= 2 ? -rod : rod * 0.6f, 0f, 0f);
            }
            // cuerpo: rebote al galope, cabeceo en el salto, agachado al gruñir
            float rebote = galope ? Mathf.Abs(Mathf.Sin(fasePaso * 0.5f + 0.4f)) * 0.07f : (camina ? Mathf.Abs(s0) * 0.015f : 0f);
            float cabeceo = Estado == Fase.Salto ? Mathf.Lerp(-18f, 14f, Mathf.Clamp01(saltoT / Mathf.Max(0.01f, saltoDur))) : (galope ? Mathf.Sin(fasePaso * 0.5f + 0.4f) * 6f : 0f);
            float agachado = Estado == Fase.Alerta ? -0.07f : 0f;
            cuerpo.localPosition = Vector3.Lerp(cuerpo.localPosition, new Vector3(0f, rebote + agachado, 0f), 0.5f);
            cuerpo.localRotation = Quaternion.Slerp(cuerpo.localRotation, Quaternion.Euler(cabeceo + (Estado == Fase.Alerta ? 8f : 0f), 0f, 0f), 0.4f);
            // cabeza y mandibula: ladrido (cabeza arriba y boca abierta), mordida (boca abre y cierra)
            cabezaArriba = Mathf.MoveTowards(cabezaArriba, 0f, dt * 3.2f);
            bocaAbierta = Mathf.MoveTowards(bocaAbierta, 0f, dt * (Estado == Fase.Muerde ? 5f : 3f));
            bool gruñe = Estado == Fase.Alerta;
            float boca = Mathf.Max(bocaAbierta * 32f, gruñe ? 12f : 0f, Estado == Fase.Salto ? 28f : 0f, galope ? 14f : 0f);
            float cab = -cabezaArriba * 24f + (galope ? 8f : 0f) + (gruñe ? 10f : 0f);
            cabeza.localRotation = Quaternion.Slerp(cabeza.localRotation, Quaternion.Euler(cab, camina && !galope ? Mathf.Sin(fasePaso * 0.5f) * 5f : 0f, 0f), 0.45f);
            mandibula.localRotation = Quaternion.Euler(boca, 0f, 0f);
            // cola: se mueve de lado; mas rapido al cargar
            colaFase += dt * (galope ? 16f : camina ? 8f : 3f);
            cola.localRotation = Quaternion.Euler(Estado == Fase.Salto ? -10f : 0f, Mathf.Sin(colaFase) * (galope ? 20f : 30f), 0f);
        }
        int pasosContados;

        void AnimarMuerte(float dt)
        {
            muerteT += dt;
            float k = Mathf.Clamp01(muerteT / 0.35f);
            if (Modelo != null)
            {
                Modelo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 88f, k));
                Modelo.localPosition = new Vector3(0f, -AlturaDelPivote + Mathf.Lerp(0f, 0.27f, k), 0f);
            }
            for (int i = 0; i < 4; i++) { if (cadera[i] != null) cadera[i].localRotation = Quaternion.Euler(Mathf.Lerp(0f, 25f * (i % 2 == 0 ? 1 : -1), k), 0f, 0f); if (rodilla[i] != null) rodilla[i].localRotation = Quaternion.Euler(Mathf.Lerp(0f, 30f, k), 0f, 0f); }
            if (mandibula != null) mandibula.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 18f, k), 0f, 0f);
        }
    }

    // Camino por la malla de navegacion hacia una meta, recalculado cada 0,4 s; devuelve el siguiente punto a seguir.
    public sealed class NavMeshPathCache
    {
        UnityEngine.AI.NavMeshPath camino = new UnityEngine.AI.NavMeshPath(); float proximo; int idx = 1; Vector3 metaVieja = new Vector3(1e9f, 0f, 0f);
        public Vector3 SiguientePunto(Vector3 desde, Vector3 meta)
        {
            if (Time.time >= proximo || (meta - metaVieja).sqrMagnitude > 4f)
            {
                proximo = Time.time + 0.4f; metaVieja = meta; idx = 1;
                if (!UnityEngine.AI.NavMesh.CalculatePath(desde, meta, UnityEngine.AI.NavMesh.AllAreas, camino) || camino.corners.Length < 2) return meta;
            }
            var c = camino.corners;
            while (idx < c.Length - 1 && (new Vector3(c[idx].x - desde.x, 0f, c[idx].z - desde.z)).sqrMagnitude < 0.5f * 0.5f) idx++;
            return c.Length > idx ? c[idx] : meta;
        }
    }
}
