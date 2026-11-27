using System.Collections;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // Pedido 2026-11-13: "un enemigo que tiene un escudo y dispara detras del escudo con una pistola; que demore en girar; si se le dispara desde
    // adelante el escudo lo protege y si no (de costado o por la espalda) recibe el dano". Es un soldado enemigo con el modelo del asalto en grafito
    // y casco plateado, una pistola, un escudo plano delante del cuerpo y el giro mas lento (SoldierMotor.FactorDeGiro): como el AiBrain no dispara hasta
    // estar encarado, tarda en responder si se lo flanquea. El escudo es un filtro del dano (Health.FiltroDeDano): un impacto cuyo origen cae dentro del
    // semiangulo frontal se anula; las explosiones y los vehiculos (origen desconocido) pasan.
    public sealed class LiberarMaterialesAlDestruirse : MonoBehaviour
    {
        public Material[] materiales;
        void OnDestroy() { if (materiales != null) foreach (var m in materiales) if (m != null) Destroy(m); }
    }

    public sealed class EnemigoEscudero : MonoBehaviour
    {
        // 2026-11-13 "que rote muy lento": el soldado comun gira a 220 grados/s; el escudero a 220 x 0,14 = ~31 grados/s (media vuelta en casi 6 s).
        public const float SemianguloDelEscudo = 62f, FactorDeGiro = 0.14f, FactorDeVelocidad = 0.7f;
        public const int MultiplicadorDeVida = 2;
        public static int Creados { get; private set; }
        public int Bloqueados { get; private set; }
        public int Pasaron { get; private set; }
        public Transform Escudo { get; private set; }
        Soldier soldado; Material matVentana; float brillo;
        static Material aceroCompartido, marcoCompartido, franjaCompartida, agarreCompartido;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Creados = 0; aceroCompartido = marcoCompartido = franjaCompartida = agarreCompartido = null; }

        public static EnemigoEscudero Aplicar(Soldier s)
        {
            if (s == null || s.Team != TeamId.Enemy) return null;
            var ya = s.GetComponent<EnemigoEscudero>(); if (ya != null) return ya;
            s.gameObject.name = "Escudero_" + (++Creados);
            return s.gameObject.AddComponent<EnemigoEscudero>();
        }

        IEnumerator Start()
        {
            soldado = GetComponent<Soldier>();
            yield return null; yield return null;   // despues de los Start de Soldier/AiBrain/SoldierLook
            if (soldado == null || soldado.Health == null) yield break;
            // R19: si el soldado ya se habia vestido (SoldierLook aplica en Start) antes de ser escudero, se viste de nuevo con su clase.
            var look = GetComponent<SoldierLook>();
            if (look != null && look.Clase != "ESCUDERO") look.Aplicar();
            soldado.Health.Initialize(soldado.Id, Mathf.Max(1, soldado.Health.MaxHealth) * MultiplicadorDeVida);
            if (soldado.Motor != null) { soldado.Motor.FactorDeGiro = FactorDeGiro; soldado.Motor.FactorDeClase = FactorDeVelocidad; }
            soldado.Health.FiltroDeDano = Filtrar;
            // R17: predicado puro para que Projectile no anuncie HEADSHOT (ni sangre mortal) de un golpe que el escudo absorbe
            soldado.Health.BloqueaGolpeDe = id => { if (id < 0) return false; var a = ActorRegistry.FindById(id); return a != null && ProtegeContra(a.transform.position); };
            subDisparo?.Dispose();
            subDisparo = EventBus.Instance.Subscribe<ShotFiredEvent>(AlDisparar);
            ConstruirEscudo();
            GameLog.Line($"[Escudero] {name}: vida {soldado.Health.MaxHealth}, escudo +-{SemianguloDelEscudo:0} grados, giro x{FactorDeGiro:0.0}");
        }

        // True si un golpe que viene de 'origen' pega contra el escudo (dentro del semiangulo frontal, en el plano).
        public bool ProtegeContra(Vector3 origen)
        {
            var d = origen - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return false;
            return Vector3.Angle(transform.forward, d) <= SemianguloDelEscudo;
        }

        int Filtrar(int dano, int atacanteId, bool cabeza)
        {
            if (atacanteId < 0) return dano;   // explosion, vehiculo o barril: no se conoce el origen
            var atacante = ActorRegistry.FindById(atacanteId);
            if (atacante == null) return dano;
            if (!ProtegeContra(atacante.transform.position)) { Pasaron++; return dano; }
            Bloqueados++; brillo = 1f;
            var punto = Escudo != null ? Escudo.position + (atacante.transform.position - Escudo.position).normalized * 0.12f : transform.position + Vector3.up;
            var haciaElTirador = (atacante.transform.position - punto).normalized;
            AudioDirector.PlayAt(SfxKind.ImpactoEscudo, punto, 0.9f, 1f);
            ImpactCubes.Spawn(punto, haciaElTirador, ImpactSurface.Obstacle, 20);
            // 2026-11-13: "particulas si disparo contra el escudo": lluvia de chispas por donde pego, hacia el que dispara (con algo de abanico al azar)
            ChispasDeMetal.Spawn(punto, (haciaElTirador + Vector3.up * 0.15f + Random.insideUnitSphere * 0.25f).normalized);
            return 0;
        }

        // R14 (#12): "quiero particulas cuando el enemigo de escudo dispare": cada tiro del escudero saca un fogonazo, chispas hacia adelante y una
        // rafaga corta de brasas por la boca de la pistola (con sonido ya lo cubre el arma). Cuenta los tiros para los checks.
        System.IDisposable subDisparo;
        public int DisparosConParticulas { get; private set; }
        void AlDisparar(ShotFiredEvent e)
        {
            if (soldado == null || e.ShooterId != soldado.Id) return;
            var arma = soldado.Weapon;
            var boca = arma != null && arma.Muzzle != null ? arma.Muzzle.position : transform.position + transform.forward * 0.8f + Vector3.up * 1.25f;
            var dir = arma != null && arma.Muzzle != null ? arma.Muzzle.forward : transform.forward;
            ImpactFx.Spawn(boca, new Color(1f, 0.75f, 0.25f), 0.3f, 0.12f);
            ChispasDeMetal.Spawn(boca, dir, 10);
            SparkleBurstFx.Spawn(boca + dir * 0.15f, new Color(1f, 0.6f, 0.15f), 0.35f, 0.7f, 10, 0.8f);
            DisparosConParticulas++;
        }

        void OnDestroy() { subDisparo?.Dispose(); subDisparo = null; if (soldado != null && soldado.Health != null && soldado.Health.FiltroDeDano == (System.Func<int, int, bool, int>)Filtrar) soldado.Health.FiltroDeDano = null; }

        static Material Mat(Color c, float metal, float suave, Color? emision = null, string nombre = "MAT_Escudo")
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", suave);
            if (emision.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emision.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            return m;
        }

        static Transform Pieza(Transform padre, string n, Vector3 pos, Vector3 esc, Material m, float giroZ = 0f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            Destroy(g.GetComponent<Collider>());   // la proteccion es un filtro de dano, no una pared fisica: no estorba al caminar ni a las balas del resto
            g.transform.SetParent(padre, false); g.transform.localPosition = pos; g.transform.localScale = esc;
            if (giroZ != 0f) g.transform.localRotation = Quaternion.Euler(0f, 0f, giroZ);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        // R19 punto 5 ("Contraste enemigo escudo"): la placa de acero claro era lo mas grande que se veia de frente y se perdia contra el hormigon.
        // Contraste interno (R19, medido en #994q: el color de placa y chevrones sale de SoldierLook.PaletaDeEscudero; por defecto placa NARANJA de seguridad
        // con chevrones negros; las paletas A/B/C usan placa gunmetal y chevrones blancos): placa, marco rojo emisivo mas grueso (0,08 -> 0,12), 3 chevrones blancos en la mitad inferior (en vez de
        // las franjas amarillas, que se parecian al dorado aliado), ventana mas grande con brillo base y agarre ambar por detras (de espaldas se lee
        // "lado abierto"). Placa, marco, chevron y agarre son materiales ESTATICOS compartidos (4, sin importar cuantos escuderos haya); la ventana es
        // por instancia porque destella al recibir un impacto (se libera con el escudo).
        public static readonly Color ColorDePlaca = new Color(0.10f, 0.11f, 0.13f);
        public static readonly Color ColorDeMarco = new Color(0.60f, 0.05f, 0.04f), EmisionDeMarco = new Color(1.6f, 0.14f, 0.08f);
        public static readonly Color ColorDeChevron = new Color(0.92f, 0.92f, 0.92f);
        public static readonly Color EmisionDeVentana = new Color(1.2f, 0.40f, 0.10f);
        public const float GrosorDeMarco = 0.12f;
        public static int MaterialesCompartidos => (aceroCompartido != null ? 1 : 0) + (marcoCompartido != null ? 1 : 0) + (franjaCompartida != null ? 1 : 0) + (agarreCompartido != null ? 1 : 0);
        public Material MaterialDeVentana => matVentana;

        // Placa y chevrones toman el color de la paleta vigente (SoldierLook.PaletaDeEscudero). Son materiales compartidos: repintarlos cambia a todos
        // los escuderos a la vez (lo usa el check de paletas #994q).
        public static void RepintarSegunPaleta()
        {
            if (aceroCompartido != null) { aceroCompartido.SetColor("_BaseColor", SoldierLook.ColorDePlacaEscudero); aceroCompartido.SetColor("_EmissionColor", SoldierLook.EmisionDePlacaEscudero); }
            if (franjaCompartida != null) franjaCompartida.SetColor("_BaseColor", SoldierLook.ColorDeChevronEscudero);
        }

        void ConstruirEscudo()
        {
            var raiz = new GameObject("Escudo").transform;
            var liberador = raiz.gameObject.AddComponent<LiberarMaterialesAlDestruirse>();   // ronda 16: los materiales de la pieza se fugaban
            raiz.SetParent(transform, false); raiz.localPosition = new Vector3(0f, 0.12f, 0.62f); raiz.localRotation = Quaternion.identity;
            Escudo = raiz;
            if (aceroCompartido == null)
            {
                aceroCompartido = Mat(ColorDePlaca, 0.3f, 0.45f, Color.black, "MAT_Escudo_Placa");                       // gunmetal, sin emision (la paleta D la aclara)
                marcoCompartido = Mat(ColorDeMarco, 0.2f, 0.4f, EmisionDeMarco, "MAT_Escudo_Marco");                      // rojo de bando: tope 1,6 en un canal
                franjaCompartida = Mat(ColorDeChevron, 0f, 0.3f, new Color(0.10f, 0.10f, 0.10f), "MAT_Escudo_Chevron");  // blanco (tenue de noche)
                agarreCompartido = Mat(new Color(0.95f, 0.55f, 0.08f), 0f, 0.3f, new Color(1.2f, 0.62f, 0.08f), "MAT_Escudo_Agarre");   // ambar por detras
                aceroCompartido.hideFlags = marcoCompartido.hideFlags = franjaCompartida.hideFlags = agarreCompartido.hideFlags = HideFlags.DontSave;
            }
            RepintarSegunPaleta();
            var acero = aceroCompartido; var marco = marcoCompartido; var chevron = franjaCompartida;
            matVentana = Mat(new Color(0.12f, 0.05f, 0.03f), 0f, 0.8f, EmisionDeVentana, "MAT_Escudo_Ventana");
            const float W = 1.1f, H = 1.5f, G = 0.09f, M = GrosorDeMarco;
            liberador.materiales = new[] { matVentana };
            float frente = G * 0.5f + 0.005f;
            Pieza(raiz, "Placa", Vector3.zero, new Vector3(W, H, G), acero);
            Pieza(raiz, "MarcoArriba", new Vector3(0f, H * 0.5f, 0f), new Vector3(W + M, M, G + 0.05f), marco);
            Pieza(raiz, "MarcoAbajo", new Vector3(0f, -H * 0.5f, 0f), new Vector3(W + M, M, G + 0.05f), marco);
            Pieza(raiz, "MarcoIzq", new Vector3(-W * 0.5f, 0f, 0f), new Vector3(M, H, G + 0.05f), marco);
            Pieza(raiz, "MarcoDer", new Vector3(W * 0.5f, 0f, 0f), new Vector3(M, H, G + 0.05f), marco);
            // 3 chevrones (^) en la mitad inferior: cada uno son dos barras de 0,10 m a +-35 grados que se juntan en el centro.
            for (int i = 0; i < 3; i++)
            {
                float y = -0.20f - 0.18f * i;
                Pieza(raiz, "Chevron" + (i + 1) + "_Izq", new Vector3(-0.18f, y, frente), new Vector3(0.44f, 0.10f, 0.02f), chevron, 35f);
                Pieza(raiz, "Chevron" + (i + 1) + "_Der", new Vector3(0.18f, y, frente), new Vector3(0.44f, 0.10f, 0.02f), chevron, -35f);
            }
            Pieza(raiz, "Ventana", new Vector3(0f, 0.42f, G * 0.5f + 0.004f), new Vector3(0.70f, 0.20f, 0.025f), matVentana);
            Pieza(raiz, "Remache1", new Vector3(-0.4f, 0.1f, G * 0.5f + 0.01f), new Vector3(0.06f, 0.06f, 0.03f), marco);
            Pieza(raiz, "Remache2", new Vector3(0.4f, 0.1f, G * 0.5f + 0.01f), new Vector3(0.06f, 0.06f, 0.03f), marco);
            Pieza(raiz, "Agarre", new Vector3(0f, 0f, -0.12f), new Vector3(0.1f, 0.5f, 0.08f), agarreCompartido);   // por detras: donde lo sostiene
        }

        public bool Soltado { get; private set; }
        public Rigidbody EscudoCaido { get; private set; }

        // Pedido 2026-11-13: "cuando muere que el escudo se caiga". El escudo deja de ser hijo del cuerpo, gana collider y cuerpo rigido, sale con un empujon hacia
        // adelante y vuelca sobre el piso; queda tirado un rato y se retira solo. Va en la capa de los fragmentos (sin rayos ni malla de navegacion).
        void SoltarEscudo()
        {
            Soltado = true;
            var e = Escudo; if (e == null) return;
            var pos = e.position;
            e.SetParent(null, true);
            foreach (var t in e.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Fragmentador.LayerSinRayos;
            var caja = e.gameObject.AddComponent<BoxCollider>(); caja.size = new Vector3(1.2f, 1.58f, 0.16f);
            var rb = e.gameObject.AddComponent<Rigidbody>();
            rb.mass = 14f; rb.linearDamping = 0.05f; rb.angularDamping = 0.35f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) Physics.IgnoreCollision(caja, c);
            var adelante = e.forward; adelante.y = 0f;
            rb.AddForce(adelante.normalized * 1.6f + Vector3.up * 1.1f, ForceMode.VelocityChange);
            rb.AddTorque(Vector3.Cross(Vector3.up, adelante.normalized) * -2.6f + Random.insideUnitSphere * 0.8f, ForceMode.VelocityChange);   // vuelca hacia adelante
            EscudoCaido = rb;
            AudioDirector.PlayAt(SfxKind.ImpactoEscudo, pos, 0.8f, 0.7f);
            Destroy(e.gameObject, 40f);
        }

        void Update()
        {
            if (!Soltado && Escudo != null && soldado != null && soldado.Health != null && !soldado.Health.IsAlive) SoltarEscudo();
            if (matVentana == null) return;
            brillo = Mathf.MoveTowards(brillo, 0f, Time.deltaTime * 4f);
            matVentana.SetColor("_EmissionColor", Color.Lerp(EmisionDeVentana, new Color(2.5f, 2.2f, 1.6f), brillo));   // destella al recibir un impacto
        }
    }
}
