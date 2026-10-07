using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Actors;
using SP.Operacion;
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

        // ---------------------------------------------------------------
        // 5. Ciudad chica
        // ---------------------------------------------------------------
        // Mueve a las esquinas de la plaza (poste y cabeza) las 4 farolas que Ciudad() deja pegadas a la H.
        static void FarolasDeLaPlazaAEsquinas(Transform g, float cx, float cz)
        {
            float m = 17f - 1.2f;
            // (x, z) de origen -> esquina destino (relativa al centro de la plaza).
            var destinos = new (Vector2 origen, Vector2 esquina)[]
            {
                (new Vector2(313f, 228f), new Vector2(-m, -m)),
                (new Vector2(331f, 228f), new Vector2(m, -m)),
                (new Vector2(324f, 221f), new Vector2(m, m)),
                (new Vector2(324f, 235f), new Vector2(-m, m)),
            };
            foreach (Transform t in g)
            {
                if (t.name != "Farola_Poste" && t.name != "Farola_Cabeza") continue;
                foreach (var d in destinos)
                {
                    if (Mathf.Abs(t.position.x - d.origen.x) > 0.01f || Mathf.Abs(t.position.z - d.origen.y) > 0.01f) continue;
                    t.position = new Vector3(cx + d.esquina.x, t.position.y, cz + d.esquina.y);
                    break;
                }
            }
        }

        static Transform Ciudad(Transform padre, out OleadaDeReserva[] oleadas, out Transform plaza, out Transform helipad)
        {
            var g = Grupo(padre, "6_Ciudad");
            const float cx = 322f, cz = 228f;
            // Calles: principal (E-O) y transversal (N-S).
            Pintura(g, "Calle_Principal", 321f, cz, 118f, 12f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Calle_Transversal", cx, 230f, 12f, 122f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Plaza", cx, cz, 34f, 34f, CementoOsc, 0f, 0.06f, 0.05f);
            for (float x = 270f; x < 380f; x += 9f) if (Mathf.Abs(x - cx) > 20f) Pintura(g, "Linea_P", x, cz, 4f, 0.3f, Amarillo, 0f, 0.02f, 0.1f);
            for (float z = 172f; z < 290f; z += 9f) if (Mathf.Abs(z - cz) > 20f) Pintura(g, "Linea_T", cx, z, 0.3f, 4f, Amarillo, 0f, 0.02f, 0.1f);
            // Helipad pintado (anillo + H).
            Pintura(g, "Helipad_Anillo_N", cx, cz + 8f, 16f, 0.8f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "Helipad_Anillo_S", cx, cz - 8f, 16f, 0.8f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "Helipad_Anillo_E", cx + 8f, cz, 0.8f, 16f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "Helipad_Anillo_O", cx - 8f, cz, 0.8f, 16f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "H_I", cx - 2.2f, cz, 0.9f, 6f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "H_D", cx + 2.2f, cz, 0.9f, 6f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "H_C", cx, cz, 4.4f, 0.9f, Amarillo, 0f, 0.11f, 0.05f);
            // Entrada de la ciudad.
            Rotulo(g, "BIENVENIDOS A\nPUEBLO CHICO", 262f, 6f, 228f, 90f, 1.3f, Color.white, AzulSenal, 18f, 3f);
            Solido(g, "Poste_Entrada", 262f, 219f, 0.5f, 7.5f, 0.5f, CementoOsc);
            Solido(g, "Poste_Entrada_2", 262f, 237f, 0.5f, 7.5f, 0.5f, CementoOsc);
            // Casas (4 por cuadrante) y ayuntamiento.
            float[][] casas =
            {
                new[]{280f,255f,12f,5f,10f}, new[]{298f,256f,10f,6f,12f}, new[]{284f,274f,14f,5f,9f}, new[]{302f,277f,10f,5f,10f},
                new[]{344f,254f,12f,5f,10f}, new[]{362f,256f,12f,6f,12f}, new[]{350f,276f,10f,5f,10f}, new[]{368f,277f,12f,5f,9f},
                new[]{282f,190f,12f,5f,10f}, new[]{300f,192f,12f,6f,10f}, new[]{284f,208f,10f,5f,9f}, new[]{302f,208f,10f,5f,10f},
                new[]{344f,190f,12f,5f,10f}, new[]{362f,192f,12f,5f,10f}, new[]{348f,208f,12f,5f,9f}, new[]{368f,208f,10f,5f,10f},
            };
            var mats3 = new[] { Casa, CasaB, CasaC };
            for (int i = 0; i < casas.Length; i++)
            {
                var c = casas[i];
                Edificio(g, "Casa_" + (i + 1), c[0], c[1], c[2], c[3], c[4], mats3[i % 3]);
                Decor(g, "Techo_" + (i + 1), c[0], c[1], c[2] + 0.6f, 0.5f, c[4] + 0.6f, Techo, 0f, c[3]);
            }
            Edificio(g, "Ayuntamiento", cx, 280f, 24f, 8f, 10f, CementoOsc);
            Rotulo(g, "AYUNTAMIENTO", cx, 6f, 274.8f, 0f, 1.4f, Color.white, Rojo, 12f, 2f);
            // Cobertura: autos abandonados, sacos y barriles en las calles.
            Cubo(g, "Auto_Viejo_1", 296f, 224f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 90f, 0f, true, 500);
            Cubo(g, "Auto_Viejo_2", 346f, 232f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 90f, 0f, true, 500);
            Cubo(g, "Auto_Viejo_3", 319f, 252f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 0f, 0f, true, 500);
            Cubo(g, "Auto_Viejo_4", 325f, 200f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 0f, 0f, true, 500);
            Saco(g, "Saco_Plaza_1", 310f, 214f); Saco(g, "Saco_Plaza_2", 334f, 242f); Saco(g, "Saco_Plaza_3", 310f, 242f, false); Saco(g, "Saco_Plaza_4", 334f, 214f, false);
            Saco(g, "Saco_Calle_1", 276f, 222f); Saco(g, "Saco_Calle_2", 372f, 234f);
            Saco(g, "Saco_Calle_3", 316f, 176f, false); Saco(g, "Saco_Calle_4", 328f, 262f, false);
            for (float z = 176f; z <= 280f; z += 26f) { Farola(g, 313f, z, false); Farola(g, 331f, z, false); }
            for (float x = 270f; x <= 378f; x += 27f) { Farola(g, x, 221f, false); Farola(g, x, 235f, false); }
            // Bug #050: las 4 farolas que rodeaban la H pasan a las esquinas de la plaza (34 x 34 m, 1,2 m hacia adentro) y cada
            // esquina lleva su luz calida (antes dos luces sueltas en el medio). Se hace antes de AmpliarCiudad para que las luces
            // de farola que genera esa ampliacion nazcan ya en las esquinas.
            FarolasDeLaPlazaAEsquinas(g, cx, cz);
            float m = 17f - 1.2f;
            var luzPlaza = new Color(1f, 0.86f, 0.62f);
            LuzPuntual(g, "Luz_Plaza_1", cx - m, 6.6f, cz - m, luzPlaza, 9f, 24f);
            LuzPuntual(g, "Luz_Plaza_2", cx + m, 6.6f, cz - m, luzPlaza, 9f, 24f);
            LuzPuntual(g, "Luz_Plaza_3", cx - m, 6.6f, cz + m, luzPlaza, 9f, 24f);
            LuzPuntual(g, "Luz_Plaza_4", cx + m, 6.6f, cz + m, luzPlaza, 9f, 24f);
            Rotulo(g, "PLAZA >>\nEXTRACCION", 306f, 5.2f, 228f, 90f, 1.2f, Color.black, Amarillo, 11f, 2.6f);
            Solido(g, "Poste_Plaza_S", 306f, 222.2f, 0.4f, 6.8f, 0.4f, CementoOsc);
            Solido(g, "Poste_Plaza_N", 306f, 233.8f, 0.4f, 6.8f, 0.4f, CementoOsc);

            // Puntos de referencia.
            var t1 = new GameObject("Plaza").transform; t1.SetParent(g, false); t1.position = new Vector3(cx, 0f, cz);
            var t2 = new GameObject("Helipad").transform; t2.SetParent(g, false); t2.position = new Vector3(cx, 0.1f, cz);
            plaza = t1; helipad = t2;

            // Oleadas de la ciudad: en los extremos de las calles.
            var res = Grupo(rReservas, "Ciudad");
            var w1 = new List<Soldier>(); var w2 = new List<Soldier>(); var w3 = new List<Soldier>();
            for (int i = 0; i < 5; i++) w1.Add(Enemigo("Reserva_Ciudad1_" + (i + 1), 372f + (i % 2) * 4f, 222f + i * 3f, 270f, res, false, 3f, true));
            for (int i = 0; i < 5; i++) w2.Add(Enemigo("Reserva_Ciudad2_" + (i + 1), 316f + i * 3f, 284f + (i % 2) * 3f, 180f, res, false, 3f, true));
            for (int i = 0; i < 6; i++) w3.Add(Enemigo("Reserva_Ciudad3_" + (i + 1), 316f + (i % 3) * 5f, 170f + (i / 3) * 4f, 0f, res, false, 3f, true));
            var meta = new GameObject("Destino_Ciudad").transform; meta.SetParent(g, false); meta.position = new Vector3(cx, 0f, cz);
            oleadas = new[]
            {
                new OleadaDeReserva { segundo = 2f, aviso = "OLEADA 1/3 · POR LA CALLE ESTE", soldados = w1.ToArray(), destino = meta },
                new OleadaDeReserva { segundo = 14f, aviso = "OLEADA 2/3 · DESDE EL AYUNTAMIENTO", soldados = w2.ToArray(), destino = meta },
                new OleadaDeReserva { segundo = 26f, aviso = "OLEADA 3/3 · POR LA CALLE SUR", soldados = w3.ToArray(), destino = meta },
            };
            AmpliarCiudad(g);
            MandoDeLaCiudad(g);   // WP10 (#101): radio, sectores y sacos del mando
            CierreDeLaCiudad(g);     // #116: muralla de edificios de relleno en los bordes
            return g;
        }
    }
}
