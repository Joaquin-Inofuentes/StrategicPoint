using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Mision
{
    // Bug #076: los que viajan en el helicoptero se ven por las puertas abiertas. Dos artilleros (uno por ametralladora, giran con
    // ella) y dos soldados sentados en el banco trasero; al embarcar, cada soldado que sube aparece sentado adentro. Son COPIAS
    // VISUALES: se clona el modelo ("Visual": malla de clase + esqueleto + Animator) de un soldado aliado, sin cerebro, sin vida
    // y sin colision. Los soldados reales siguen apagados como siempre.
    public class TripulacionDelHeli : MonoBehaviour
    {
        // Posiciones de los pies en el espacio del helicoptero (el piso de la cabina esta en y = 1,12).
        public const float PisoDeCabina = 1.12f;
        static readonly Vector3[] AsientosBase = { new Vector3(-0.5f, PisoDeCabina, -1.95f), new Vector3(0.5f, PisoDeCabina, -1.95f) };
        static readonly Vector3[] AsientosDeEscuadra =
        {
            new Vector3(-0.5f, PisoDeCabina, -1.15f), new Vector3(0.5f, PisoDeCabina, -1.15f),
            new Vector3(-0.38f, PisoDeCabina, -0.4f), new Vector3(0.38f, PisoDeCabina, -0.4f),
            // #128: lugar para los 7 (escuadra de 3 + 4 milicianos): una fila mas adelante y uno al fondo del pasillo.
            new Vector3(-0.38f, PisoDeCabina, 0.35f), new Vector3(0.38f, PisoDeCabina, 0.35f), new Vector3(0f, PisoDeCabina, -2.75f),
        };
        public const int Capacidad = 7;

        readonly List<GameObject> artilleros = new List<GameObject>();
        readonly List<GameObject> sentadosBase = new List<GameObject>();
        readonly List<GameObject> copiasDeEscuadra = new List<GameObject>();
        PivoteMetralleta[] pivotes;
        bool armada;
        float proximoIntento;

        public int Artilleros => Contar(artilleros);
        public int Sentados => Contar(sentadosBase) + Contar(copiasDeEscuadra);
        public int SentadosBase => Contar(sentadosBase);
        public int CopiasDeEscuadra => Contar(copiasDeEscuadra);
        public bool Armada => armada;
        public IReadOnlyList<GameObject> ListaDeArtilleros => artilleros;
        public IReadOnlyList<GameObject> ListaDeSentados => sentadosBase;

        static int Contar(List<GameObject> l) { int n = 0; foreach (var g in l) if (g != null && g.activeInHierarchy) n++; return n; }

        void Awake() { pivotes = GetComponentsInChildren<PivoteMetralleta>(true); }

        void Update()
        {
            if (armada || Time.time < proximoIntento) return;
            proximoIntento = Time.time + 0.5f;
            Armar();
        }

        // Fuente del modelo: un soldado aliado cualquiera (activo si hay, porque ya tiene la malla de su clase puesta).
        static Soldier FuenteDeModelo(int indice = 0)
        {
            var candidatos = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian) continue;
                if (s.GetComponentInChildren<Animator>(true) == null) continue;
                candidatos.Add(s);
            }
            if (candidatos.Count == 0) return null;
            candidatos.Sort((a, b) => (b.gameObject.activeInHierarchy ? 1 : 0).CompareTo(a.gameObject.activeInHierarchy ? 1 : 0));
            return candidatos[indice % candidatos.Count];
        }

        GameObject Clonar(Soldier fuente, Transform padre, Vector3 posLocal, float yaw)
        {
            if (fuente == null) return null;
            var anim = fuente.GetComponentInChildren<Animator>(true);
            if (anim == null) return null;
            var go = Instantiate(anim.gameObject, padre, false);
            go.name = "TripulanteVisual";
            go.transform.localPosition = posLocal;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.SetActive(true);
            var a = go.GetComponent<Animator>();
            if (a != null)
            {
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.applyRootMotion = false;
                a.enabled = true;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // #123: si la fuente estaba oculta (soldados apagados durante el rapel), la copia igual se ve.
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = true;
            return go;
        }

        void Armar()
        {
            if (pivotes == null || pivotes.Length == 0) pivotes = GetComponentsInChildren<PivoteMetralleta>(true);
            if (pivotes.Length == 0) { armada = true; return; }
            var f0 = FuenteDeModelo(0);
            if (f0 == null) return;   // todavia no hay soldados: se reintenta
            armada = true;
            int i = 0;
            foreach (var p in pivotes)
            {
                // El artillero esta de pie en el origen del pivote, mirando hacia donde apunta el arma (+Z del pivote).
                var g = Clonar(FuenteDeModelo(i++), p.transform, new Vector3(0f, -0.1f, 0f), 0f);
                if (g == null) continue;
                var a = g.GetComponent<Animator>();
                if (a != null && a.layerCount > 1) a.SetLayerWeight(1, 1f);   // capa "Disparo": pose de fusil en el hombro
                artilleros.Add(g);
            }
            for (int k = 0; k < AsientosBase.Length; k++)
            {
                var g = Clonar(FuenteDeModelo(i++), transform, AsientosBase[k], 0f);
                if (g == null) continue;
                g.AddComponent<PoseSentada>();
                sentadosBase.Add(g);
            }
        }

        // Al embarcar: una copia visual del soldado, sentada en el proximo asiento libre. Devuelve false si no hay lugar.
        public bool SentarCopia(Soldier s)
        {
            if (s == null) return false;
            int k = copiasDeEscuadra.Count;
            if (k >= AsientosDeEscuadra.Length) return false;
            var g = Clonar(s, transform, AsientosDeEscuadra[k], 0f);
            if (g == null) return false;
            g.AddComponent<PoseSentada>();
            copiasDeEscuadra.Add(g);
            return true;
        }

        public void LimpiarCopias()
        {
            foreach (var g in copiasDeEscuadra) if (g != null) Destroy(g);
            copiasDeEscuadra.Clear();
        }
    }

    // Postura sentada procedural sobre el esqueleto humanoide del soldado (no hay clip de sentarse): despues de que el Animator
    // evalua, baja y retrasa la cadera, lleva los muslos hacia adelante y devuelve las piernas y los pies a su orientacion animada
    // (shins verticales), asi las botas siguen apoyadas sobre el piso de la cabina.
    public class PoseSentada : MonoBehaviour
    {
        public float descenso = 0.24f, atras = 0.40f, angulo = 82f;
        Animator anim;
        Transform cadera, musloI, musloD, piernaI, piernaD, pieI, pieD;
        bool resuelto;

        void Resolver()
        {
            resuelto = true;
            anim = GetComponent<Animator>();
            if (anim == null || !anim.isHuman) return;
            cadera = anim.GetBoneTransform(HumanBodyBones.Hips);
            musloI = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            musloD = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            piernaI = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            piernaD = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            pieI = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            pieD = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        void LateUpdate()
        {
            if (!resuelto) Resolver();
            if (cadera == null || musloI == null || musloD == null || piernaI == null || piernaD == null) return;
            var rpI = piernaI.rotation; var rpD = piernaD.rotation;
            var rfI = pieI != null ? pieI.rotation : Quaternion.identity; var rfD = pieD != null ? pieD.rotation : Quaternion.identity;
            cadera.localPosition += new Vector3(0f, -descenso, -atras);
            var eje = transform.right;
            musloI.rotation = Quaternion.AngleAxis(-angulo, eje) * musloI.rotation;
            musloD.rotation = Quaternion.AngleAxis(-angulo, eje) * musloD.rotation;
            // Rodillas a 90 grados: las piernas vuelven a su orientacion de mundo animada (verticales).
            piernaI.rotation = rpI; piernaD.rotation = rpD;
            if (pieI != null) pieI.rotation = rfI;
            if (pieD != null) pieD.rotation = rfD;
        }
    }
}
