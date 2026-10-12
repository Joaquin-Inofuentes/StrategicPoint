using UnityEngine;
using UnityEngine.Rendering;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Operacion
{
    // Bug #171 ("quiero efecto de circulos vibrantes sobre estas torretas y que tengan sacos de arena"): se agrega en tiempo de juego a cada
    // TorretaFija (TorretaFija.Instalar). Hace dos cosas, las dos solo de presentacion (sin colliders: no cambia la navegacion):
    //   * un parapeto de sacos de arena en arco al frente y a los costados del arma (la parte de atras queda abierta para el artillero);
    //   * dos anillos que laten y se expanden sobre el piso mientras la torreta esta LIBRE (se apagan cuando alguien la ocupa).
    public sealed class EmplazamientoDeTorreta : MonoBehaviour
    {
        public const float RadioDelAnillo = 2.6f, FrecuenciaDeLatido = 1.6f;
        static readonly Color ColorSaco = new Color(0.62f, 0.55f, 0.4f, 1f), ColorSacoOscuro = new Color(0.5f, 0.44f, 0.31f, 1f);

        TorretaFija torreta;
        Transform raizAnillos;
        Transform[] anillos;
        MeshRenderer[] rendsAnillos;
        MaterialPropertyBlock bloque;
        bool anillosVisibles;
        public int SacosCreados { get; private set; }

        public static EmplazamientoDeTorreta Decorar(TorretaFija t)
        {
            if (t == null) return null;
            var e = t.GetComponent<EmplazamientoDeTorreta>();
            if (e != null) return e;
            e = t.gameObject.AddComponent<EmplazamientoDeTorreta>();
            e.torreta = t;
            e.Construir();
            return e;
        }

        void Construir()
        {
            var sacos = new GameObject("SacosDeArena").transform;
            sacos.SetParent(transform, false);
            // Arco al frente (+Z local) de -75 a +75 grados y un saco en cada flanco; dos hileras (la de arriba corrida media pieza).
            var mat = SafeMaterial.Create(ColorSaco);
            var matOsc = SafeMaterial.Create(ColorSacoOscuro);
            const float radio = 2.1f;
            for (int capa = 0; capa < 2; capa++)
                for (int i = 0; i < 9; i++)
                {
                    float grados = -80f + i * 20f + (capa == 1 ? 10f : 0f);
                    if (capa == 1 && i == 8) continue;
                    float rad = grados * Mathf.Deg2Rad;
                    var pos = new Vector3(Mathf.Sin(rad) * radio, 0.18f + capa * 0.34f, Mathf.Cos(rad) * radio);
                    var saco = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    saco.name = "Saco";
                    var col = saco.GetComponent<Collider>(); if (col != null) Destroy(col);
                    saco.transform.SetParent(sacos, false);
                    saco.transform.localPosition = pos;
                    saco.transform.localRotation = Quaternion.Euler(0f, grados + (capa == 1 ? 6f : -4f), 0f);
                    saco.transform.localScale = new Vector3(0.95f, 0.32f, 0.5f);
                    var r = saco.GetComponent<MeshRenderer>();
                    r.sharedMaterial = (i + capa) % 2 == 0 ? mat : matOsc;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    SacosCreados++;
                }

            // Anillos: dos quads planos con el aro de AnilloDeAviso, un solo material, color por MaterialPropertyBlock.
            raizAnillos = new GameObject("AnillosVibrantes").transform;
            raizAnillos.SetParent(transform, false);
            raizAnillos.position = new Vector3(transform.position.x, AnilloDeAviso.AlturaDelPisoEn(transform.position) + 0.12f, transform.position.z);
            bloque = new MaterialPropertyBlock();
            anillos = new Transform[2]; rendsAnillos = new MeshRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var q = new GameObject("Anillo" + i);
                q.transform.SetParent(raizAnillos, false);
                q.AddComponent<MeshFilter>().sharedMesh = AnilloDeAviso.MallaPlana();
                var mr = q.AddComponent<MeshRenderer>();
                mr.sharedMaterial = AnilloDeAviso.MaterialDeAro();
                mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                anillos[i] = q.transform; rendsAnillos[i] = mr;
            }
            Pintar(0f);
        }

        public bool AnillosVisibles => anillosVisibles;

        void Update()
        {
            if (torreta == null || raizAnillos == null) return;
            bool libre = torreta.Libre && !RomboVisibilidad.Suprimidos;
            if (libre != anillosVisibles) { anillosVisibles = libre; raizAnillos.gameObject.SetActive(libre); }
            if (libre) Pintar(Time.time);
        }

        void Pintar(float t)
        {
            // Defensa: tras cargar una partida o recompilar en Play el bloque puede faltar (no se serializa) y los anillos pueden haberse destruido.
            if (anillos == null || rendsAnillos == null) return;
            if (bloque == null) bloque = new MaterialPropertyBlock();
            for (int i = 0; i < anillos.Length; i++)
            {
                if (anillos[i] == null || rendsAnillos[i] == null) continue;
                // Cada anillo nace chico y se expande hasta RadioDelAnillo mientras se desvanece; el segundo va a medio ciclo del primero.
                float ciclo = Mathf.Repeat(t * FrecuenciaDeLatido * 0.5f + i * 0.5f, 1f);
                float escala = Mathf.Lerp(0.35f, 1f, ciclo) * RadioDelAnillo * 2f;
                anillos[i].localScale = new Vector3(escala, 1f, escala);
                float vibra = 0.75f + 0.25f * Mathf.Sin(t * 22f + i);
                var c = DiamondGizmo.ColorTorreta; c.a = Mathf.Clamp01((1f - ciclo) * 1.1f) * vibra;
                bloque.SetColor("_BaseColor", c); bloque.SetColor("_Color", c);
                rendsAnillos[i].SetPropertyBlock(bloque);
            }
        }
    }
}
