using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Presentation;

namespace SP.EditorTools
{
    // Bug #063: "mejora el diseño del nivel ... mas casas, que sea una ciudad (esta aislada) ... mejora las luminarias y quiero
    // puntitos de luminarias chiquitas donde aterriza el helicoptero". La ciudad eran 16 casas en cruz alrededor de la plaza,
    // con el borde del mapa a la vista. Se le agrega (en el grupo 6_Ciudad/Ampliacion, que se rehace entero en cada corrida):
    //   - un anillo de calles (norte, sur, oeste y este) que cierra la cuadricula
    //   - casas del otro lado de esas calles (una y dos plantas) y una fila de edificios altos contra el borde este del mapa
    //   - ventanas (algunas encendidas) y puertas en todas las casas, para que de noche se lea como un pueblo habitado
    //   - luz real en las farolas de la ciudad (antes eran solo cabezas emisivas) y farolas nuevas en las calles nuevas
    //   - balizas chiquitas alrededor del helipuerto que parpadean en secuencia (LucesDelHelipuerto)
    public static partial class OperacionBuilder
    {
        const float CiudadX = 322f, CiudadZ = 228f;

        // Calles nuevas (centro x, centro z, ancho x, largo z).
        static readonly float[][] CallesNuevas =
        {
            new[]{317f, 296f, 134f, 10f},   // Calle_Norte
            new[]{317f, 162f, 134f, 10f},   // Calle_Sur
            new[]{266f, 229f, 8f, 144f},    // Calle_Oeste
            new[]{380f, 229f, 7f, 144f},    // Calle_Este
        };

        [MenuItem("Strategic Point/Operacion/Ampliar la ciudad (bug 063)")]
        public static void AmpliarCiudadEnEscena()
        {
            var ciudad = GameObject.Find("6_Ciudad");
            if (ciudad == null) { Debug.LogError("[Operacion] No hay 6_Ciudad en la escena abierta"); return; }
            mats.Clear();
            int n = AmpliarCiudad(ciudad.transform);
            EditorSceneManager.MarkSceneDirty(ciudad.scene);
            Debug.Log("[Operacion] Ciudad ampliada: " + n + " casas nuevas");
        }

        static int AmpliarCiudad(Transform ciudad)
        {
            var vieja = ciudad.Find("Ampliacion");
            if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
            var g = Grupo(ciudad, "Ampliacion");
            var rnd = new System.Random(63);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // ---- Calles ----
            string[] nombres = { "Calle_Norte", "Calle_Sur", "Calle_Oeste", "Calle_Este" };
            for (int i = 0; i < CallesNuevas.Length; i++)
            {
                var c = CallesNuevas[i];
                Pintura(g, nombres[i], c[0], c[1], c[2], c[3], Asfalto, 0f, 0f, 0.075f);
            }
            for (float x = 258f; x < 378f; x += 9f) { Pintura(g, "Linea_N", x, 296f, 4f, 0.3f, Amarillo, 0f, 0.02f, 0.1f); Pintura(g, "Linea_S", x, 162f, 4f, 0.3f, Amarillo, 0f, 0.02f, 0.1f); }

            // ---- Casas nuevas ----
            var zonasLibres = new List<Rect>();   // calles y la llegada de la autopista: ahi no va nada
            foreach (var c in CallesNuevas) zonasLibres.Add(new Rect(c[0] - c[2] * 0.5f - 1f, c[1] - c[3] * 0.5f - 1f, c[2] + 2f, c[3] + 2f));
            zonasLibres.Add(new Rect(262f - 1f, 222f - 1f, 120f, 14f));      // calle principal
            zonasLibres.Add(new Rect(316f - 1f, 169f - 1f, 14f, 124f));      // transversal
            zonasLibres.Add(new Rect(200f, 210f, 64f, 36f));                 // autopista llegando a la entrada
            var mats3 = new[] { Casa, CasaB, CasaC };
            var casas = new List<Transform>();
            int idx = 0;
            Physics.SyncTransforms();

            void Casa_(float x, float z, float w, float h, float d, Vector3 frente, Material m = null, string prefijo = "Casa_A", bool contraElMuro = false)
            {
                var r = new Rect(x - w * 0.5f, z - d * 0.5f, w, d);
                foreach (var zl in zonasLibres) if (zl.Overlaps(r)) return;
                if (!contraElMuro && Physics.CheckBox(new Vector3(x, h * 0.5f + 0.3f, z), new Vector3(w * 0.5f + 0.6f, h * 0.5f, d * 0.5f + 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return;
                idx++;
                var casa = Edificio(g, prefijo + idx, x, z, w, h, d, m ?? mats3[rnd.Next(3)]);
                CopiarModificadorDeNavegacion(casa);
                Decor(g, "Techo_A" + idx, x, z, w + 0.6f, 0.5f, d + 0.6f, Techo, 0f, h);
                Physics.SyncTransforms();
                Fachada(g, casa.transform, frente, rnd);
                Fachada(g, casa.transform, -frente, rnd);
                casas.Add(casa.transform);
            }

            // Fila de casas a lo largo de X (frente = hacia donde mira la fachada principal).
            void FilaX(float x0, float x1, float zc, Vector3 frente)
            {
                float x = x0;
                while (x < x1)
                {
                    float w = R(9f, 13f), d = R(8f, 11f);
                    float h = rnd.NextDouble() < 0.25 ? R(8f, 9f) : R(4.5f, 6f);
                    Casa_(x + w * 0.5f, zc + frente.z * -d * 0.5f, w, h, d, frente);
                    x += w + R(2f, 4.5f);
                }
            }
            void FilaZ(float z0, float z1, float xc, Vector3 frente)
            {
                float z = z0;
                while (z < z1)
                {
                    float d = R(9f, 13f), w = R(8f, 11f);
                    float h = rnd.NextDouble() < 0.25 ? R(8f, 9f) : R(4.5f, 6f);
                    Casa_(xc + frente.x * -w * 0.5f, z + d * 0.5f, w, h, d, frente);
                    z += d + R(2f, 4.5f);
                }
            }
            // Del otro lado de las calles nuevas, mirando hacia la calle.
            FilaX(250f, 376f, 302.5f, Vector3.back);       // norte (fachada al sur, frente en z=302.5)
            FilaX(250f, 376f, 155.5f, Vector3.forward);    // sur (fachada al norte)
            FilaZ(150f, 312f, 260.5f, Vector3.right);        // oeste (fachada al este), saltea la entrada
            // Manzanas del sur: hay lugar entre las casas viejas y la calle nueva (al norte no: ahi quedan 8 m).
            FilaX(272f, 312f, 168.5f, Vector3.back);
            FilaX(334f, 376f, 168.5f, Vector3.back);
            // Contra el borde este del mapa: fachadas de edificios altos apoyadas en el muro perimetral (Borde_Este, x 388-390,
            // 8 m), que asoman por encima y dejan de mostrar el muro pelado al final de las calles.
            {
                float z = 148f;
                while (z < 314f)
                {
                    float d = R(11f, 17f), h = R(10f, 14f);
                    Casa_(386.45f, z + d * 0.5f, 3.7f, h, d, Vector3.left, CementoOsc, "Edificio_Borde_", true);
                    z += d + R(1f, 2.5f);
                }
            }

            // Ventanas y puertas de las casas viejas (las 16 de la cruz y el ayuntamiento).
            foreach (Transform t in ciudad)
            {
                if (!(t.name.StartsWith("Casa_") || t.name == "Ayuntamiento")) continue;
                var frente = t.position.z > CiudadZ ? Vector3.back : Vector3.forward;
                Fachada(g, t, frente, rnd);
                Fachada(g, t, -frente, rnd);
            }

            // ---- Luminarias ----
            // Las farolas de la ciudad tenian solo la cabeza emisiva (de noche no iluminaban nada): luz calida real en cada una.
            var luzCalida = new Color(1f, 0.84f, 0.6f);
            foreach (Transform t in ciudad)
                if (t.name == "Farola_Cabeza") LuzDeFarola(g, t.position + Vector3.down * 0.45f, luzCalida);
            // Farolas nuevas en el anillo de calles (al costado, cada ~30 m; una de cada dos con luz para cuidar el rendimiento).
            int k = 0;
            for (float x = 262f; x <= 376f; x += 28.5f)
            {
                FarolaConLuz(g, x, 290.2f, (k++ & 1) == 0, luzCalida);
                FarolaConLuz(g, x + 14f, 167.8f, (k & 1) == 0, luzCalida);
            }
            k = 0;
            for (float z = 168f; z <= 292f; z += 31f)
            {
                if (Mathf.Abs(z - CiudadZ) < 10f) continue;
                FarolaConLuz(g, 270.6f, z, (k++ & 1) == 0, luzCalida);
                FarolaConLuz(g, 376.2f, z + 15f, (k & 1) == 0, luzCalida);
            }

            // ---- Balizas del helipuerto ----
            var balizas = new GameObject("LucesDelHelipuerto").transform;
            balizas.SetParent(g, false);
            balizas.position = new Vector3(CiudadX, 0f, CiudadZ);
            var comp = balizas.gameObject.AddComponent<LucesDelHelipuerto>();
            comp.encendida = Mat("BalizaHeliOn", new Color(0.35f, 1f, 0.45f), 10f);
            comp.apagada = Mat("BalizaHeliOff", new Color(0.2f, 0.55f, 0.25f), 1.6f);
            comp.esquina = Mat("BalizaHeliEsquina", new Color(1f, 0.72f, 0.2f), 8f);
            const float lado = 9.4f;
            int porLado = 7;
            var puntos = new List<Vector3>();
            for (int s = 0; s < 4; s++)
                for (int i = 0; i < porLado; i++)
                {
                    float u = -lado + 2f * lado * i / porLado;
                    var p = s == 0 ? new Vector3(u, 0f, -lado) : s == 1 ? new Vector3(lado, 0f, u) : s == 2 ? new Vector3(-u, 0f, lado) : new Vector3(-lado, 0f, -u);
                    puntos.Add(p);
                }
            for (int i = 0; i < puntos.Count; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.name = "Baliza_" + i;
                b.transform.SetParent(balizas, false);
                b.transform.localPosition = puntos[i] + Vector3.up * 0.12f;
                b.transform.localScale = new Vector3(0.38f, 0.22f, 0.38f);
                var mr = b.GetComponent<MeshRenderer>();
                mr.sharedMaterial = i % porLado == 0 ? comp.esquina : comp.apagada;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }

            return casas.Count;
        }

        static void CopiarModificadorDeNavegacion(GameObject casa)
        {
            var modelo = GameObject.Find("Casa_1");
            var src = modelo != null ? modelo.GetComponent<NavMeshModifierVolume>() : null;
            if (src == null) return;
            var dst = casa.AddComponent<NavMeshModifierVolume>();
            EditorUtility.CopySerialized(src, dst);
        }

        static Material Ventana => Mat("Ventana", new Color(1f, 0.78f, 0.45f), 1.7f);
        static Material VentanaApagada => Mat("VentanaApagada", new Color(0.13f, 0.16f, 0.2f));
        static Material Puerta => Mat("Puerta", new Color(0.24f, 0.17f, 0.12f));

        // Ventanas (y una puerta abajo) sobre la cara de la casa que mira hacia "frente".
        static void Fachada(Transform g, Transform casa, Vector3 frente, System.Random rnd)
        {
            var s = casa.localScale;
            var c = casa.position;
            float ancho = Mathf.Abs(frente.z) > 0.5f ? s.x : s.z;
            float prof = Mathf.Abs(frente.z) > 0.5f ? s.z : s.x;
            var lateral = Vector3.Cross(Vector3.up, frente);
            float yaw = Quaternion.LookRotation(frente).eulerAngles.y;
            int cuantas = Mathf.Max(1, Mathf.FloorToInt((ancho - 1.5f) / 3.2f));
            int pisos = s.y >= 7.5f ? 2 : 1;
            int puerta = rnd.Next(cuantas);
            for (int piso = 0; piso < pisos; piso++)
                for (int i = 0; i < cuantas; i++)
                {
                    float u = (i - (cuantas - 1) * 0.5f) * (ancho - 1.5f) / cuantas;
                    var p = c + frente * (prof * 0.5f + 0.03f) + lateral * u;
                    bool esPuerta = piso == 0 && i == puerta && casa.name != "Ayuntamiento" && !casa.name.StartsWith("Edificio_Borde");
                    if (esPuerta)
                    {
                        Decor(g, "Puerta", p.x, p.z, 1.3f, 2.2f, 0.06f, Puerta, yaw, 0f);
                        continue;
                    }
                    bool encendida = rnd.NextDouble() < 0.45;
                    Decor(g, encendida ? "Ventana" : "Ventana_Apagada", p.x, p.z, 1.25f, 1.15f, 0.06f, encendida ? Ventana : VentanaApagada, yaw, 1.6f + piso * 3.4f);
                }
        }

        static void LuzDeFarola(Transform g, Vector3 pos, Color color)
        {
            var l = new GameObject("Farola_Luz_Ciudad");
            l.transform.SetParent(g, false);
            l.transform.position = pos;
            var li = l.AddComponent<Light>();
            li.type = LightType.Point; li.color = color; li.intensity = 7f; li.range = 16f; li.shadows = LightShadows.None;
        }

        static void FarolaConLuz(Transform g, float x, float z, bool conLuz, Color color)
        {
            Solido(g, "Farola_Poste", x, z, 0.3f, 6f, 0.3f, CementoOsc);
            Decor(g, "Farola_Cabeza", x, z, 1.1f, 0.3f, 0.7f, Lampara, 0f, 6f);
            if (conLuz) LuzDeFarola(g, new Vector3(x, 5.6f, z), color);
        }
    }
}
