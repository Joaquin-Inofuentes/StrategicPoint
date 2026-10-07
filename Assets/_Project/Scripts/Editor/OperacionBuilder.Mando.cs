using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // WP10 (#101): "DIRIGIR LA DEFENSA DESDE LA RADIO". Lo que el objetivo 5 necesita en el nivel (en el grupo 6_Ciudad/Mando, que Ciudad() rehace
    // entero en cada corrida):
    //   - la radio de campaña en las escalinatas del ayuntamiento (322, 268), detras de un anillo de sacos
    //   - tres sectores de defensa (A calle norte, B mercado oeste, C calle este) con su letra gigante pintada en el piso y sacos que sirven de
    //     cobertura (#074: los aliados que llegan se cubren solos)
    //   - los cuatro puntos de salida de los milicianos (frente al ayuntamiento)
    //   - (opcional) el campanario: una plataforma elevada con rampa
    // Los milicianos NO se ponen en la escena (un soldado Player inactivo igual se registra en el SpatialGrid y cuenta para EstadoDePartida): el
    // director los instancia al tomar la radio desde el prefab de aliado.
    public static partial class OperacionBuilder
    {
        const string PrefabAliado = "Assets/_Project/Prefabs/P_Soldier_Ally.prefab";

        // Lo deja armado Ciudad() y lo cablea Construir() al director.
        static Transform radioDeCampana, campanarioMando;
        static SectorDeDefensa[] sectoresMando;
        static Transform[] salidasMiliciano;
        static GameObject[] prefabsMiliciano;
        public static int SacosDeMando { get; private set; }

        static readonly (string letra, string nombre, float x, float z, Vector2 amenaza)[] SectoresDeLaDefensa =
        {
            ("A", "CALLE NORTE", 322f, 292f, new Vector2(-1f, 0f)),     // los enemigos llegan por el oeste de la calle norte
            ("B", "MERCADO OESTE", 272f, 228f, new Vector2(0f, 1f)),    // por el norte de la calle oeste
            ("C", "CALLE ESTE", 372f, 228f, new Vector2(0f, -1f)),      // por el sur de la calle este
        };

        static void MandoDeLaCiudad(Transform ciudad)
        {
            var vieja = ciudad.Find("Mando");
            if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
            var g = Grupo(ciudad, "Mando");
            SacosDeMando = 0;
            Physics.SyncTransforms();

            // ---- La radio ----
            const float rx = 322f, rz = 268f;
            var radio = new GameObject("Radio_De_Campana").transform;
            radio.SetParent(g, false);
            radio.position = new Vector3(rx, 0f, rz);
            radio.rotation = Quaternion.identity;     // mira al ayuntamiento (norte): el operador se arrodilla al sur
            radioDeCampana = radio;
            Solido(g, "Radio_Mesa", rx, rz + 0.9f, 1.8f, 1.0f, 0.8f, CementoOsc);
            Decor(g, "Radio_Equipo", rx - 0.35f, rz + 0.9f, 0.9f, 0.34f, 0.5f, Negro, 0f, 1.0f);
            Decor(g, "Radio_Pantalla", rx + 0.5f, rz + 0.62f, 0.5f, 0.3f, 0.06f, Pantalla, 0f, 1.12f);
            Decor(g, "Radio_Antena", rx + 0.75f, rz + 1.0f, 0.07f, 3.4f, 0.07f, MetalOsc, 0f, 1.0f);
            Decor(g, "Radio_Antena_Punta", rx + 0.75f, rz + 1.0f, 0.2f, 0.2f, 0.2f, Pantalla, 0f, 4.4f);
            Rotulo(g, "RADIO DE CAMPAÑA", rx, 3.5f, rz + 1.6f, 0f, 0.8f, Color.black, Amarillo, 6.4f, 1.1f);
            Solido(g, "Radio_Poste_Rotulo_I", rx - 2.6f, rz + 1.6f, 0.2f, 3.6f, 0.2f, MetalOsc);
            Solido(g, "Radio_Poste_Rotulo_D", rx + 2.6f, rz + 1.6f, 0.2f, 3.6f, 0.2f, MetalOsc);
            // Sacos: dos a los costados y uno al sudoeste (la rampa del campanario sale por el sudeste); abierto al sur, por donde entra el operador.
            SacoLibre(g, "Saco_Radio_O", rx - 3.4f, rz + 1.4f, false);
            SacoLibre(g, "Saco_Radio_E", rx + 3.4f, rz + 1.4f, false);
            SacoLibre(g, "Saco_Radio_SO", rx - 3.4f, rz - 3.2f, true);

            // ---- Salidas de los milicianos (frente al ayuntamiento, a los costados de la radio) ----
            // P7 (#122): 4 milicianos (antes 2): las dos salidas de siempre (316,5 y 327,5) y dos mas hacia afuera (311,5 y 332,5), todas sobre z 273,2.
            float[] salidasX = { 316.5f, 327.5f, 311.5f, 332.5f };
            salidasMiliciano = new Transform[salidasX.Length];
            prefabsMiliciano = new GameObject[salidasX.Length];
            var prefabAliado = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAliado);
            for (int i = 0; i < salidasX.Length; i++)
            {
                var s = new GameObject("Salida_Miliciano_" + (i + 1)).transform;
                s.SetParent(g, false);
                s.position = new Vector3(salidasX[i], 0f, 273.2f);
                salidasMiliciano[i] = s;
                prefabsMiliciano[i] = prefabAliado;
            }

            // ---- Sectores ----
            sectoresMando = new SectorDeDefensa[SectoresDeLaDefensa.Length];
            for (int i = 0; i < SectoresDeLaDefensa.Length; i++)
            {
                var d = SectoresDeLaDefensa[i];
                var go = new GameObject("Sector_" + d.letra);
                go.transform.SetParent(g, false);
                go.transform.position = new Vector3(d.x, 0f, d.z);
                var sec = go.AddComponent<SectorDeDefensa>();
                sec.letra = d.letra; sec.nombre = d.nombre; sec.radio = SectorDeDefensa.RadioPorDefecto;
                sec.haciaLaAmenaza = new Vector3(d.amenaza.x, 0f, d.amenaza.y);
                sectoresMando[i] = sec;

                // Letra gigante pintada en el piso (se lee desde la vista tactica).
                LetraEnElPiso(g, d.letra, d.x, d.z, Color.white);

                // Sacos de cobertura: tres de cara a la amenaza (dos en las puntas, uno al medio) y uno de costado.
                var t = new Vector3(d.amenaza.x, 0f, d.amenaza.y);
                var l = new Vector3(-t.z, 0f, t.x);
                bool amenazaEnX = Mathf.Abs(t.x) > 0.5f;
                var c = new Vector3(d.x, 0f, d.z);
                var p1 = c + t * 5f + l * 3.3f; var p2 = c + t * 5f - l * 3.3f; var p3 = c + t * 1.5f; var p4 = c + l * 5.5f;
                SacoLibre(g, $"Saco_{d.letra}_1", p1.x, p1.z, !amenazaEnX);
                SacoLibre(g, $"Saco_{d.letra}_2", p2.x, p2.z, !amenazaEnX);
                SacoLibre(g, $"Saco_{d.letra}_3", p3.x, p3.z, !amenazaEnX);
                SacoLibre(g, $"Saco_{d.letra}_4", p4.x, p4.z, amenazaEnX);
            }

            // ---- Campanario (opcional) ----
            campanarioMando = Campanario(g);
        }

        // Un saco solo si el lugar esta libre (no adentro de una casa ni de otro saco).
        static void SacoLibre(Transform g, string nombre, float x, float z, bool horizontal)
        {
            var ext = horizontal ? new Vector3(1.6f, 0.6f, 0.6f) : new Vector3(0.6f, 0.6f, 1.6f);
            if (Physics.CheckBox(new Vector3(x, 0.7f, z), ext, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning($"[Operacion] Mando: no hay lugar para {nombre} en ({x:0.0}, {z:0.0})");
                return;
            }
            Saco(g, nombre, x, z, horizontal);
            Physics.SyncTransforms();
            SacosDeMando++;
        }

        // Letra plana sobre el asfalto, legible desde arriba (el "arriba" del texto es el norte).
        static void LetraEnElPiso(Transform g, string letra, float x, float z, Color color)
        {
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var raizL = new GameObject("Letra_" + letra).transform;
            raizL.SetParent(g, false);
            raizL.position = new Vector3(x, 0.09f, z);   // #112: pegada al asfalto (0,08), no flotando a 0,5
            raizL.rotation = Quaternion.Euler(90f, 0f, 0f);
            var tm = raizL.gameObject.AddComponent<TextMesh>();
            tm.font = fuente; tm.text = letra; tm.fontSize = 64; tm.characterSize = 1.55f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            var mr = raizL.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MatTexto();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            raizL.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        // El campanario: una plataforma de 6 x 6 a 6,5 m entre dos casas del este de la plaza, con una rampa de subida desde el sur. El horneado del
        // NavMesh (LevelBlockoutBuilder.BakeNavMesh) la deja caminable (excluye estas piezas del volumen "no caminable" de los BoxCollider altos).
        public const string NombreDelCampanario = "Campanario";
        const float AlturaCampanario = 6.5f;
        static Transform Campanario(Transform g)
        {
            var c = new GameObject(NombreDelCampanario).transform;
            c.SetParent(g, false);
            const float px = 340f, pz = 263.5f;
            c.position = new Vector3(px, AlturaCampanario, pz);

            GameObject Pieza(string n, Vector3 centro, Vector3 escala, Quaternion rot, Material m, bool colision)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = NombreDelCampanario + "_" + n;
                go.transform.SetParent(c, false);
                go.transform.SetPositionAndRotation(centro, rot);
                go.transform.localScale = escala;
                go.GetComponent<MeshRenderer>().sharedMaterial = m;
                if (!colision) { Object.DestroyImmediate(go.GetComponent<BoxCollider>()); go.AddComponent<NavMeshModifier>().ignoreFromBuild = true; }
                return go;
            }
            // Plataforma: 6 x 6 m, 0,45 m de espesor (menos de 0,5: no cuenta como cobertura), cara superior a 6,5 m.
            Pieza("Plataforma", new Vector3(px, AlturaCampanario - 0.225f, pz), new Vector3(6f, 0.45f, 6f), Quaternion.identity, CementoOsc, true);
            // Cuatro pilares de las esquinas.
            foreach (var (dx, dz) in new[] { (-2.8f, -2.8f), (2.8f, -2.8f), (-2.8f, 2.8f), (2.8f, 2.8f) })
                Pieza("Pilar", new Vector3(px + dx, (AlturaCampanario - 0.45f) * 0.5f, pz + dz), new Vector3(0.5f, AlturaCampanario - 0.45f, 0.5f), Quaternion.identity, CementoOsc, true);
            // Parapeto bajo (solo visual) en los tres lados que no tienen la rampa (la rampa llega por el oeste).
            Pieza("Parapeto_N", new Vector3(px, AlturaCampanario + 0.55f, pz + 2.9f), new Vector3(6f, 1.1f, 0.2f), Quaternion.identity, Casa, false);
            Pieza("Parapeto_S", new Vector3(px, AlturaCampanario + 0.55f, pz - 2.9f), new Vector3(6f, 1.1f, 0.2f), Quaternion.identity, Casa, false);
            Pieza("Parapeto_E", new Vector3(px + 2.9f, AlturaCampanario + 0.55f, pz), new Vector3(0.2f, 1.1f, 6f), Quaternion.identity, Casa, false);
            // Rampa: 2 m de ancho que sube hacia el este desde el piso (x 323) hasta el borde oeste de la plataforma (x 337).
            const float run = 14f, rz = 265.3f;
            float ang = Mathf.Atan2(AlturaCampanario, run) * Mathf.Rad2Deg;
            float largo = Mathf.Sqrt(run * run + AlturaCampanario * AlturaCampanario);
            var rampa = Pieza("Rampa", new Vector3(px - 3f - run * 0.5f, AlturaCampanario * 0.5f - 0.15f * Mathf.Cos(ang * Mathf.Deg2Rad), rz),
                new Vector3(largo, 0.3f, 2f), Quaternion.Euler(0f, 0f, ang), Cemento, true);
            var marca = rampa.AddComponent<RampaCaminable>();
            marca.abajo = new Vector3(px - 3f - run, 0f, rz); marca.arriba = new Vector3(px - 3f, 0f, rz); marca.alturaDelFinal = AlturaCampanario; marca.anchoMedio = 1.15f;
            // Barandas de la rampa (visuales).
            Pieza("Baranda_N", rampa.transform.position + new Vector3(0f, 0.55f, 0.95f), new Vector3(largo, 0.12f, 0.1f), Quaternion.Euler(0f, 0f, ang), MetalOsc, false);
            Pieza("Baranda_S", rampa.transform.position + new Vector3(0f, 0.55f, -0.95f), new Vector3(largo, 0.12f, 0.1f), Quaternion.Euler(0f, 0f, ang), MetalOsc, false);
            return c;
        }
    }
}
