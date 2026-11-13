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
    public sealed class EnemigoEscudero : MonoBehaviour
    {
        public const float SemianguloDelEscudo = 62f, FactorDeGiro = 0.4f, FactorDeVelocidad = 0.7f;
        public const int MultiplicadorDeVida = 2;
        public static int Creados { get; private set; }
        public int Bloqueados { get; private set; }
        public int Pasaron { get; private set; }
        public Transform Escudo { get; private set; }
        Soldier soldado; Material matVentana; float brillo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Creados = 0; }

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
            soldado.Health.Initialize(soldado.Id, Mathf.Max(1, soldado.Health.MaxHealth) * MultiplicadorDeVida);
            if (soldado.Motor != null) { soldado.Motor.FactorDeGiro = FactorDeGiro; soldado.Motor.FactorDeClase = FactorDeVelocidad; }
            soldado.Health.FiltroDeDano = Filtrar;
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
            AudioDirector.PlayAt(SfxKind.ImpactoEscudo, punto, 0.9f, 1f);
            ImpactCubes.Spawn(punto, (atacante.transform.position - punto).normalized, ImpactSurface.Obstacle, 20);
            return 0;
        }

        void OnDestroy() { if (soldado != null && soldado.Health != null && soldado.Health.FiltroDeDano == (System.Func<int, int, bool, int>)Filtrar) soldado.Health.FiltroDeDano = null; }

        static Material Mat(Color c, float metal, float suave, Color? emision = null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MAT_Escudo" };
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", suave);
            if (emision.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emision.Value); }
            return m;
        }

        static Transform Pieza(Transform padre, string n, Vector3 pos, Vector3 esc, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            Destroy(g.GetComponent<Collider>());   // la proteccion es un filtro de dano, no una pared fisica: no estorba al caminar ni a las balas del resto
            g.transform.SetParent(padre, false); g.transform.localPosition = pos; g.transform.localScale = esc;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        void ConstruirEscudo()
        {
            var raiz = new GameObject("Escudo").transform;
            raiz.SetParent(transform, false); raiz.localPosition = new Vector3(0f, 0.12f, 0.62f); raiz.localRotation = Quaternion.identity;
            Escudo = raiz;
            var acero = Mat(new Color(0.66f, 0.72f, 0.80f), 0.15f, 0.5f, new Color(0.05f, 0.06f, 0.08f));   // acero claro: se lee de noche
            var marco = Mat(new Color(0.10f, 0.11f, 0.13f), 0.4f, 0.4f);
            var franja = Mat(new Color(0.95f, 0.72f, 0.08f), 0f, 0.3f, new Color(0.25f, 0.17f, 0f));
            matVentana = Mat(new Color(0.12f, 0.10f, 0.05f), 0f, 0.8f, new Color(0.9f, 0.55f, 0.1f) * 0.8f);
            const float W = 1.1f, H = 1.5f, G = 0.09f;
            Pieza(raiz, "Placa", Vector3.zero, new Vector3(W, H, G), acero);
            Pieza(raiz, "MarcoArriba", new Vector3(0f, H * 0.5f, 0f), new Vector3(W + 0.1f, 0.08f, G + 0.05f), marco);
            Pieza(raiz, "MarcoAbajo", new Vector3(0f, -H * 0.5f, 0f), new Vector3(W + 0.1f, 0.08f, G + 0.05f), marco);
            Pieza(raiz, "MarcoIzq", new Vector3(-W * 0.5f, 0f, 0f), new Vector3(0.08f, H, G + 0.05f), marco);
            Pieza(raiz, "MarcoDer", new Vector3(W * 0.5f, 0f, 0f), new Vector3(0.08f, H, G + 0.05f), marco);
            Pieza(raiz, "Franja", new Vector3(0f, -0.35f, G * 0.5f + 0.005f), new Vector3(W - 0.1f, 0.16f, 0.02f), franja);
            Pieza(raiz, "Franja2", new Vector3(0f, -0.62f, G * 0.5f + 0.005f), new Vector3(W - 0.1f, 0.06f, 0.02f), franja);
            Pieza(raiz, "Ventana", new Vector3(0f, 0.42f, G * 0.5f + 0.004f), new Vector3(0.62f, 0.16f, 0.025f), matVentana);
            Pieza(raiz, "Remache1", new Vector3(-0.4f, 0.1f, G * 0.5f + 0.01f), new Vector3(0.06f, 0.06f, 0.03f), marco);
            Pieza(raiz, "Remache2", new Vector3(0.4f, 0.1f, G * 0.5f + 0.01f), new Vector3(0.06f, 0.06f, 0.03f), marco);
            Pieza(raiz, "Agarre", new Vector3(0f, 0f, -0.12f), new Vector3(0.1f, 0.5f, 0.08f), marco);   // por detras: donde lo sostiene
        }

        void Update()
        {
            if (matVentana == null) return;
            brillo = Mathf.MoveTowards(brillo, 0f, Time.deltaTime * 4f);
            matVentana.SetColor("_EmissionColor", Color.Lerp(new Color(0.9f, 0.55f, 0.1f) * 0.8f, new Color(2.5f, 2.2f, 1.6f), brillo));   // destella al recibir un impacto
        }
    }
}
