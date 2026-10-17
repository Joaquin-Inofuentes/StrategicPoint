using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pulido final del Nivel 2 (pedido: "modelos de edificio y PC, mas arte en la parte de hackear, rework salida norte, mas arte en el
    // tramo final"). Todo es arte PROCEDURAL sin colision (cajas y cilindros con materiales del builder), asi que no toca el NavMesh ni
    // el combate. Se corre sobre la escena ABIERTA y es idempotente: reemplaza el grupo "9_PulidoFinal".
    //   - Centro de datos: LEDs en los racks, bandejas de cables, puestos de trabajo con PC y monitor, mesa holografica en la terminal,
    //     franjas de luz en los muros, mastiles y antenas.
    //   - Ciudad: ventanas (algunas encendidas), puertas con toldo, cornisas, pilastras y equipos en los techos de cada casa; el
    //     Ayuntamiento lleva columnas, escalera y mastil con bandera.
    //   - Salida norte: portico con pilares rayados, dintel con luces direccionales y embudo de barreras.
    //   - Tramo final: autos quemados, barricadas, escombros y faroles a los lados del camino hacia la ciudad.
    public static partial class OperacionBuilder
    {
        public static int PulidoFinalObjetos { get; private set; }

        static GameObject CajaPF(Transform p, string n, Vector3 c, Vector3 s, Material m, float yaw = 0f, bool sombra = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = n; go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = s;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = sombra ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            PulidoFinalObjetos++;
            return go;
        }

        static GameObject FormaPF(PrimitiveType tipo, Transform p, string n, Vector3 c, Vector3 s, Material m, bool sombra = false)
        {
            var go = GameObject.CreatePrimitive(tipo);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = n; go.transform.SetParent(p, false);
            go.transform.position = c; go.transform.localScale = s;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = sombra ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            PulidoFinalObjetos++;
            return go;
        }

        static Bounds LimitesDe(GameObject o)
        {
            var b = new Bounds(o.transform.position, Vector3.zero); bool alguno = false;
            foreach (var r in o.GetComponentsInChildren<Renderer>(true))
            {
                if (!alguno) { b = r.bounds; alguno = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        public static string PulidoFinalEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            var viejo = GameObject.Find("9_PulidoFinal");
            if (viejo != null) Object.DestroyImmediate(viejo);
            PulidoFinalObjetos = 0;
            var raiz = new GameObject("9_PulidoFinal");
            var g = raiz.transform;
            Physics.SyncTransforms();
            var rnd = new System.Random(20261018);

            int cd = ArteCentroDeDatos(g, rnd);
            int ciudad = ArteCiudadPF(g, rnd);
            int salida = ArteSalidaNorte(g, rnd);
            int tramo = ArteTramoFinal(g, rnd);
            EditorSceneManager.MarkSceneDirty(escena);
            return $"PulidoFinal: centro de datos={cd}, ciudad={ciudad}, salida norte={salida}, tramo final={tramo}, objetos totales={PulidoFinalObjetos}";
        }

        // ------------------------------------------------------------------ centro de datos
        static int ArteCentroDeDatos(Transform g, System.Random rnd)
        {
            var raiz = Grupo(g, "CentroDeDatos");
            int n0 = PulidoFinalObjetos;
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var mRack = Mat("RackMetal", new Color(0.10f, 0.12f, 0.16f));
            var mNegro = Negro;
            var mCable = Mat("CableRojo", new Color(0.55f, 0.08f, 0.08f));
            var mCableAzul = Mat("CableAzul", new Color(0.10f, 0.25f, 0.55f));
            var mMesa = Mat("MesaOficina", new Color(0.30f, 0.32f, 0.36f));
            var mSilla = Mat("SillaOficina", new Color(0.12f, 0.13f, 0.15f));
            var leds = new[]
            {
                Mat("LedVerde", new Color(0.2f, 1f, 0.4f), 2.2f),
                Mat("LedCian", new Color(0.2f, 0.8f, 1f), 2.2f),
                Mat("LedAmbar", new Color(1f, 0.65f, 0.1f), 2.2f),
            };
            var pantalla = Mat("PantallaAzul", new Color(0.35f, 0.75f, 1f), 1.8f);
            var pantallaB = Mat("PantallaBlanca", new Color(0.85f, 0.95f, 1f), 1.5f);
            var holo = Mat("HoloCian", new Color(0.2f, 0.75f, 1f), 1.4f);
            var luzRoja = Mat("LuzRojaMastil", new Color(1f, 0.15f, 0.1f), 2.5f);

            // a) racks: tiras de LEDs en las dos caras largas, tapa y haz de cables
            var gRack = Grupo(raiz, "Racks");
            for (int i = 1; i <= 6; i++)
            {
                var o = GameObject.Find("Rack_" + i);
                if (o == null) continue;
                var b = LimitesDe(o);
                bool largoZ = b.size.z > b.size.x;
                float baseY = Mathf.Max(0f, b.min.y) + 0.55f;
                for (int lado = -1; lado <= 1; lado += 2)
                    for (int fila = 0; fila < 4; fila++)
                    {
                        float y = baseY + fila * 0.5f;
                        var m = leds[(fila + i + (lado > 0 ? 1 : 0)) % leds.Length];
                        if (largoZ) CajaPF(gRack, "Led", new Vector3(b.center.x + lado * (b.extents.x + 0.03f), y, b.center.z), new Vector3(0.05f, 0.07f, b.size.z * 0.86f), m);
                        else CajaPF(gRack, "Led", new Vector3(b.center.x, y, b.center.z + lado * (b.extents.z + 0.03f)), new Vector3(b.size.x * 0.86f, 0.07f, 0.05f), m);
                    }
                CajaPF(gRack, "Tapa", new Vector3(b.center.x, b.max.y + 0.06f, b.center.z), new Vector3(b.size.x * 0.96f, 0.12f, b.size.z * 0.96f), mRack, 0f, true);
                CajaPF(gRack, "Cables", new Vector3(b.center.x, b.max.y + 0.2f, b.center.z), new Vector3(largoZ ? 0.18f : b.size.x * 0.6f, 0.12f, largoZ ? b.size.z * 0.6f : 0.18f), i % 2 == 0 ? mCable : mCableAzul);
            }

            // b) bandejas de cables en el piso, de los racks a la terminal
            var gCab = Grupo(raiz, "Cables");
            CajaPF(gCab, "Bandeja_Principal", new Vector3(0f, 0.1f, -52f), new Vector3(40f, 0.05f, 0.55f), mNegro);
            CajaPF(gCab, "Cable_Rojo", new Vector3(0f, 0.14f, -52.15f), new Vector3(40f, 0.04f, 0.08f), mCable);
            CajaPF(gCab, "Cable_Azul", new Vector3(0f, 0.14f, -51.85f), new Vector3(40f, 0.04f, 0.08f), mCableAzul);
            foreach (float x in new[] { -9f, 9f })
                CajaPF(gCab, "Bandeja_Rack", new Vector3(x, 0.1f, -60f), new Vector3(0.55f, 0.05f, 16f), mNegro);
            CajaPF(gCab, "Bandeja_Terminal", new Vector3(-1f, 0.1f, -48.5f), new Vector3(0.55f, 0.05f, 7f), mNegro);

            // c) puestos de trabajo con PC pegados a los muros oeste y este
            var gPc = Grupo(raiz, "PuestosPC");
            float[] zs = { -76f, -72.5f, -69f, -65.5f, -62f };
            for (int lado = -1; lado <= 1; lado += 2)
            {
                float xMuro = lado * 23.7f;
                float haciaAdentro = -lado;
                foreach (float z in zs)
                {
                    if (lado > 0 && z == -62f) continue;
                    CajaPF(gPc, "Escritorio", new Vector3(xMuro, 0.4f, z), new Vector3(1.0f, 0.8f, 2.0f), mMesa, 0f, true);
                    CajaPF(gPc, "Monitor", new Vector3(xMuro + lado * 0.3f, 1.15f, z), new Vector3(0.05f, 0.55f, 0.9f), rnd.Next(3) == 0 ? pantallaB : pantalla);
                    CajaPF(gPc, "Monitor_Pie", new Vector3(xMuro + lado * 0.3f, 0.86f, z), new Vector3(0.12f, 0.12f, 0.25f), mRack);
                    CajaPF(gPc, "Teclado", new Vector3(xMuro + haciaAdentro * 0.28f, 0.82f, z), new Vector3(0.25f, 0.03f, 0.55f), mNegro);
                    CajaPF(gPc, "Torre_PC", new Vector3(xMuro + lado * 0.1f, 0.25f, z + 1.15f), new Vector3(0.5f, 0.5f, 0.28f), mRack);
                    CajaPF(gPc, "Torre_Led", new Vector3(xMuro + haciaAdentro * 0.14f, 0.4f, z + 1.15f), new Vector3(0.02f, 0.05f, 0.05f), leds[rnd.Next(leds.Length)]);
                    CajaPF(gPc, "Silla_Asiento", new Vector3(xMuro + haciaAdentro * 1.15f, 0.5f, z), new Vector3(0.5f, 0.08f, 0.5f), mSilla);
                    CajaPF(gPc, "Silla_Respaldo", new Vector3(xMuro + haciaAdentro * 1.38f, 0.82f, z), new Vector3(0.07f, 0.55f, 0.5f), mSilla);
                    FormaPF(PrimitiveType.Cylinder, gPc, "Silla_Pie", new Vector3(xMuro + haciaAdentro * 1.15f, 0.25f, z), new Vector3(0.1f, 0.25f, 0.1f), mSilla);
                }
            }

            // d) terminal: plataforma con luz y paneles holograficos
            var gT = Grupo(raiz, "Terminal");
            var term = GameObject.Find("Computadora");
            Vector3 tc = term != null ? LimitesDe(term).center : new Vector3(-1f, 1f, -45f);
            CajaPF(gT, "Plataforma", new Vector3(tc.x, 0.11f, tc.z - 1.8f), new Vector3(6.5f, 0.1f, 4.2f), mRack, 0f, true);
            CajaPF(gT, "Plataforma_Borde", new Vector3(tc.x, 0.17f, tc.z - 3.85f), new Vector3(6.5f, 0.05f, 0.1f), leds[1]);
            CajaPF(gT, "Panel_Central", new Vector3(tc.x, 3.0f, tc.z + 1.05f), new Vector3(3.6f, 1.9f, 0.05f), holo);
            CajaPF(gT, "Panel_Izq", new Vector3(tc.x - 3.0f, 2.7f, tc.z + 0.95f), new Vector3(2.0f, 1.4f, 0.05f), pantalla, 25f);
            CajaPF(gT, "Panel_Der", new Vector3(tc.x + 3.0f, 2.7f, tc.z + 0.95f), new Vector3(2.0f, 1.4f, 0.05f), pantalla, -25f);
            CajaPF(gT, "Soporte_Central", new Vector3(tc.x, 1.5f, tc.z + 1.1f), new Vector3(0.2f, 3.0f, 0.2f), mRack);

            // e) muros interiores: zocalo oscuro y linea de luz cian
            var gMuro = Grupo(raiz, "Muros");
            void Tramo(float cx, float cz, float largo, bool largoX, float lado)
            {
                if (largoX)
                {
                    CajaPF(gMuro, "Zocalo", new Vector3(cx, 0.55f, cz + lado * 0.09f), new Vector3(largo, 1.1f, 0.12f), mRack);
                    CajaPF(gMuro, "LineaLuz", new Vector3(cx, 2.7f, cz + lado * 0.06f), new Vector3(largo, 0.08f, 0.05f), leds[1]);
                }
                else
                {
                    CajaPF(gMuro, "Zocalo", new Vector3(cx + lado * 0.09f, 0.55f, cz), new Vector3(0.12f, 1.1f, largo), mRack);
                    CajaPF(gMuro, "LineaLuz", new Vector3(cx + lado * 0.06f, 2.7f, cz), new Vector3(0.05f, 0.08f, largo), leds[1]);
                }
            }
            Tramo(-15f, -78.95f, 22f, true, 1f); Tramo(15f, -78.95f, 22f, true, 1f);
            Tramo(-15f, -41.05f, 22f, true, -1f); Tramo(15f, -41.05f, 22f, true, -1f);
            Tramo(-24.95f, -71f, 18f, false, 1f); Tramo(-24.95f, -48f, 16f, false, 1f);
            Tramo(24.95f, -71f, 18f, false, -1f); Tramo(24.95f, -48f, 16f, false, -1f);

            // f) mastiles con antena y luz roja, y equipos de aire en los remates del muro
            var gTecho = Grupo(raiz, "Remates");
            foreach (float x in new[] { -26f, 26f })
                foreach (float z in new[] { -80f, -40f })
                {
                    CajaPF(gTecho, "Mastil", new Vector3(x, 8.2f, z), new Vector3(0.16f, 4.4f, 0.16f), mRack, 0f, true);
                    CajaPF(gTecho, "Mastil_Luz", new Vector3(x, 10.5f, z), new Vector3(0.28f, 0.28f, 0.28f), luzRoja);
                    FormaPF(PrimitiveType.Sphere, gTecho, "Antena_Plato", new Vector3(x + (x < 0 ? 0.6f : -0.6f), 7.4f, z), new Vector3(1.3f, 0.35f, 1.3f), mMesa, true);
                    CajaPF(gTecho, "Equipo_Aire", new Vector3(x, 6.45f, z + (z < -60f ? 2.5f : -2.5f)), new Vector3(1.8f, 0.9f, 1.3f), mMesa, 0f, true);
                }
            return PulidoFinalObjetos - n0;
        }

        // ------------------------------------------------------------------ ciudad
        static int ArteCiudadPF(Transform g, System.Random rnd)
        {
            var raiz = Grupo(g, "Ciudad");
            int n0 = PulidoFinalObjetos;
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var vOscura = Mat("VentanaOscura", new Color(0.05f, 0.09f, 0.14f));
            var vLuz = Mat("VentanaLuz", new Color(1f, 0.78f, 0.42f), 1.6f);
            var vLuzFria = Mat("VentanaLuzFria", new Color(0.6f, 0.85f, 1f), 1.3f);
            var marco = Mat("MarcoVentana", new Color(0.18f, 0.17f, 0.16f));
            var cornisa = Mat("Cornisa", new Color(0.58f, 0.54f, 0.48f));
            var pilastra = Mat("Pilastra", new Color(0.30f, 0.27f, 0.24f));
            var puerta = Mat("PuertaMadera", new Color(0.30f, 0.18f, 0.10f));
            var toldoA = Mat("ToldoRojo", new Color(0.62f, 0.14f, 0.12f));
            var toldoB = Mat("ToldoAzul", new Color(0.14f, 0.26f, 0.52f));
            var techoEq = Mat("EquipoTecho", new Color(0.36f, 0.38f, 0.40f));
            var luzRoja = Mat("LuzRojaMastil", new Color(1f, 0.15f, 0.1f), 2.5f);

            var ciudad = GameObject.Find("6_Ciudad");
            if (ciudad == null) return 0;
            foreach (Transform t in ciudad.transform)
            {
                bool casa = t.name.StartsWith("Casa_");
                bool ayto = t.name == "Ayuntamiento";
                if (!casa && !ayto) continue;
                var b = LimitesDe(t.gameObject);
                if (b.size.y < 3f) continue;
                var gc = Grupo(raiz, t.name + "_Arte");
                float alto = b.max.y;
                int pisos = Mathf.Max(1, Mathf.FloorToInt((alto - 1.2f) / 3.2f));
                float cx = b.center.x, cz = b.center.z;

                // ventanas en las 4 caras
                for (int cara = 0; cara < 4; cara++)
                {
                    bool largoX = cara < 2;
                    float largo = largoX ? b.size.x : b.size.z;
                    int cuantas = Mathf.Max(1, Mathf.FloorToInt((largo - 2.4f) / 2.8f));
                    float paso = (largo - 2.4f) / Mathf.Max(1, cuantas);
                    for (int k = 0; k < cuantas; k++)
                    {
                        float u = -largo * 0.5f + 1.2f + paso * (k + 0.5f);
                        for (int p = 0; p < pisos; p++)
                        {
                            float y = 1.9f + p * 3.0f;
                            if (y + 0.8f > alto - 0.4f) continue;
                            bool esPuertaDeFrente = cara == 1 && p == 0 && k == cuantas / 2;
                            if (esPuertaDeFrente) continue;
                            Material mv = rnd.NextDouble() < 0.3 ? (rnd.Next(4) == 0 ? vLuzFria : vLuz) : vOscura;
                            Vector3 c, tam, cm;
                            if (cara == 0) { c = new Vector3(cx + u, y, b.max.z + 0.03f); tam = new Vector3(1.1f, 1.3f, 0.05f); cm = new Vector3(1.3f, 1.5f, 0.04f); }
                            else if (cara == 1) { c = new Vector3(cx + u, y, b.min.z - 0.03f); tam = new Vector3(1.1f, 1.3f, 0.05f); cm = new Vector3(1.3f, 1.5f, 0.04f); }
                            else if (cara == 2) { c = new Vector3(b.max.x + 0.03f, y, cz + u); tam = new Vector3(0.05f, 1.3f, 1.1f); cm = new Vector3(0.04f, 1.5f, 1.3f); }
                            else { c = new Vector3(b.min.x - 0.03f, y, cz + u); tam = new Vector3(0.05f, 1.3f, 1.1f); cm = new Vector3(0.04f, 1.5f, 1.3f); }
                            CajaPF(gc, "Marco", c + (cara == 0 ? Vector3.back : cara == 1 ? Vector3.forward : cara == 2 ? Vector3.left : Vector3.right) * 0.0f, cm, marco);
                            CajaPF(gc, "Ventana", c + (cara == 0 ? Vector3.forward : cara == 1 ? Vector3.back : cara == 2 ? Vector3.right : Vector3.left) * 0.025f, tam, mv);
                        }
                    }
                }

                // puerta con toldo en la cara sur (la que mira a la calle/plaza)
                float zPuerta = b.min.z - 0.03f;
                CajaPF(gc, "Puerta", new Vector3(cx, 1.1f, zPuerta - 0.02f), new Vector3(1.3f, 2.2f, 0.08f), puerta);
                var toldo = CajaPF(gc, "Toldo", new Vector3(cx, 2.55f, zPuerta - 0.65f), new Vector3(2.6f, 0.1f, 1.4f), rnd.Next(2) == 0 ? toldoA : toldoB, 0f, true);
                toldo.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
                CajaPF(gc, "Escalon", new Vector3(cx, 0.1f, zPuerta - 0.5f), new Vector3(2.0f, 0.2f, 0.8f), cornisa);

                // cornisa y pilastras en las esquinas
                CajaPF(gc, "Cornisa", new Vector3(cx, alto - 0.12f, cz), new Vector3(b.size.x + 0.45f, 0.28f, b.size.z + 0.45f), cornisa, 0f, true);
                CajaPF(gc, "Zocalo", new Vector3(cx, 0.45f, cz), new Vector3(b.size.x + 0.2f, 0.9f, b.size.z + 0.2f), pilastra);
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sz in new[] { -1, 1 })
                        CajaPF(gc, "Pilastra", new Vector3(cx + sx * (b.extents.x + 0.08f), alto * 0.5f, cz + sz * (b.extents.z + 0.08f)), new Vector3(0.45f, alto, 0.45f), pilastra, 0f, true);

                // equipos en el techo
                float rx = Mathf.Max(0.5f, b.extents.x - 1.6f), rz = Mathf.Max(0.5f, b.extents.z - 1.6f);
                CajaPF(gc, "Techo_Aire", new Vector3(cx + R(-rx, rx), alto + 0.35f, cz + R(-rz, rz)), new Vector3(1.6f, 0.8f, 1.2f), techoEq, R(0f, 40f), true);
                if (rnd.Next(2) == 0)
                {
                    float tx = cx + R(-rx, rx), tz = cz + R(-rz, rz);
                    FormaPF(PrimitiveType.Cylinder, gc, "Techo_Tanque", new Vector3(tx, alto + 1.4f, tz), new Vector3(1.6f, 0.9f, 1.6f), techoEq, true);
                    foreach (var o in new[] { new Vector2(-0.6f, -0.6f), new Vector2(0.6f, -0.6f), new Vector2(-0.6f, 0.6f), new Vector2(0.6f, 0.6f) })
                        CajaPF(gc, "Tanque_Pata", new Vector3(tx + o.x, alto + 0.5f, tz + o.y), new Vector3(0.1f, 1.0f, 0.1f), marco);
                }
                float ax = cx + R(-rx, rx), az = cz + R(-rz, rz);
                CajaPF(gc, "Antena", new Vector3(ax, alto + 1.8f, az), new Vector3(0.08f, 3.6f, 0.08f), marco, 0f, true);
                CajaPF(gc, "Antena_Luz", new Vector3(ax, alto + 3.65f, az), new Vector3(0.16f, 0.16f, 0.16f), luzRoja);

                if (ayto)
                {
                    // columnas, escalera y mastil con bandera en el frente
                    for (int k = -2; k <= 2; k++)
                        FormaPF(PrimitiveType.Cylinder, gc, "Columna", new Vector3(cx + k * (b.size.x / 5.2f), alto * 0.36f, b.min.z - 0.9f), new Vector3(0.9f, alto * 0.36f, 0.9f), cornisa, true);
                    for (int e = 0; e < 3; e++)
                        CajaPF(gc, "Escalera", new Vector3(cx, 0.12f + e * 0.2f, b.min.z - 1.5f - (2 - e) * 0.6f), new Vector3(b.size.x * 0.7f, 0.24f + e * 0.2f, 0.7f), cornisa, 0f, true);
                    CajaPF(gc, "Frontis", new Vector3(cx, alto * 0.74f, b.min.z - 0.9f), new Vector3(b.size.x * 0.92f, 0.6f, 1.2f), cornisa, 0f, true);
                    CajaPF(gc, "Mastil", new Vector3(b.max.x - 1.5f, alto + 3.2f, cz), new Vector3(0.1f, 6.4f, 0.1f), marco, 0f, true);
                    CajaPF(gc, "Bandera", new Vector3(b.max.x - 1.5f, alto + 5.5f, cz + 0.9f), new Vector3(0.04f, 1.1f, 1.8f), toldoA);
                }
            }
            return PulidoFinalObjetos - n0;
        }

        // ------------------------------------------------------------------ salida norte
        static int ArteSalidaNorte(Transform g, System.Random rnd)
        {
            var raiz = Grupo(g, "SalidaNorte");
            int n0 = PulidoFinalObjetos;
            var amarillo = Amarillo;
            var negro = Negro;
            var hormigon = Mat("CementoPortico", new Color(0.50f, 0.50f, 0.52f));
            var ambar = Mat("LuzAmbarPortico", new Color(1f, 0.6f, 0.08f), 2.6f);
            var luzRoja = Mat("LuzRojaMastil", new Color(1f, 0.15f, 0.1f), 2.5f);
            const float zMuro = -40f;

            // pilares rayados a los lados de la abertura (el muro central es lo que se demuele)
            foreach (float x in new[] { -6.6f, 6.6f })
            {
                CajaPF(raiz, "Pilar", new Vector3(x, 3.8f, zMuro), new Vector3(1.5f, 7.6f, 1.7f), hormigon, 0f, true);
                for (int i = 0; i < 9; i++)
                    CajaPF(raiz, "Franja", new Vector3(x, 0.45f + i * 0.8f, zMuro + 0.88f), new Vector3(1.54f, 0.4f, 0.04f), i % 2 == 0 ? amarillo : negro);
                CajaPF(raiz, "Lampara", new Vector3(x, 7.9f, zMuro + 0.2f), new Vector3(0.5f, 0.4f, 0.5f), ambar);
                CajaPF(raiz, "Base", new Vector3(x, 0.25f, zMuro), new Vector3(2.0f, 0.5f, 2.2f), hormigon, 0f, true);
            }
            // dintel alto con chevrones de luz que apuntan al norte
            CajaPF(raiz, "Dintel", new Vector3(0f, 7.4f, zMuro), new Vector3(15.0f, 0.9f, 1.5f), hormigon, 0f, true);
            CajaPF(raiz, "Dintel_Franja", new Vector3(0f, 7.4f, zMuro + 0.78f), new Vector3(15.0f, 0.12f, 0.04f), ambar);
            for (int i = -2; i <= 2; i++)
            {
                CajaPF(raiz, "Chevron_A", new Vector3(i * 1.8f - 0.25f, 7.55f, zMuro + 0.8f), new Vector3(0.9f, 0.16f, 0.05f), ambar, 35f);
                CajaPF(raiz, "Chevron_B", new Vector3(i * 1.8f + 0.25f, 7.55f, zMuro + 0.8f), new Vector3(0.9f, 0.16f, 0.05f), ambar, -35f);
            }
            // embudo de barreras y luces de balizas hacia el norte
            for (int lado = -1; lado <= 1; lado += 2)
                for (int i = 0; i < 4; i++)
                {
                    float z = zMuro + 4f + i * 4.5f, x = lado * (7.5f + i * 1.4f);
                    PonerArte(raiz, "P_Env_Barricada_Concreto", new Vector3(x, 0f, z), lado * 18f, 1.2f, true);
                    CajaPF(raiz, "Baliza", new Vector3(x, 1.5f, z), new Vector3(0.18f, 0.18f, 0.18f), ambar);
                    CajaPF(raiz, "Baliza_Poste", new Vector3(x, 0.75f, z), new Vector3(0.07f, 1.5f, 0.07f), negro);
                }
            // lado sur de la abertura: conos de aviso y luz roja
            CajaPF(raiz, "Aviso_Luz", new Vector3(0f, 7.95f, zMuro - 0.5f), new Vector3(0.3f, 0.3f, 0.3f), luzRoja);
            return PulidoFinalObjetos - n0;
        }

        // ------------------------------------------------------------------ tramo final (camino hacia la ciudad)
        static int ArteTramoFinal(Transform g, System.Random rnd)
        {
            var raiz = Grupo(g, "TramoFinal");
            int n0 = PulidoFinalObjetos;
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var evitar = new List<Vector3>();
            foreach (var nombre in new[] { "Waypoints", "Enemies", "PlayerSquad", "Vehicles", "UI_World", "Mision", "CaminoLights", "Estrategia" })
            {
                var o = GameObject.Find(nombre);
                if (o == null) continue;
                foreach (var t in o.GetComponentsInChildren<Transform>(true)) evitar.Add(t.position);
            }
            bool Cerca(Vector3 p, float radio)
            {
                float r2 = radio * radio;
                for (int i = 0; i < evitar.Count; i++)
                {
                    float dx = evitar[i].x - p.x, dz = evitar[i].z - p.z;
                    if (dx * dx + dz * dz < r2) return true;
                }
                return false;
            }
            bool EsTierra(Vector3 p, out float y)
            {
                y = 0f;
                if (!Physics.Raycast(new Vector3(p.x, 4f, p.z), Vector3.down, out var h, 8f, ~0, QueryTriggerInteraction.Ignore)) return false;
                y = h.point.y;
                if (h.normal.y < 0.9f || h.point.y > 0.6f) return false;
                var r = h.collider.GetComponentInParent<Renderer>();
                var nombre = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "";
                return !nombre.Contains("Asfalto") && !nombre.Contains("Amarillo") && !nombre.Contains("Blanco");
            }

            string[] kit = { "P_Env_Auto_Quemado", "P_Env_Barricada_Sacos", "P_Env_Barricada_Concreto", "P_Env_Escombro_Pila_A", "P_Env_Escombro_Pila_B", "P_Env_Neumaticos", "P_Env_Barril", "P_Env_Farol", "P_Env_Barricada_Erizo", "P_Env_Muro_Caido", "P_Env_Poste_Caido" };
            float[] esc = { 1.1f, 1.2f, 1.2f, 1.2f, 1.2f, 1.1f, 0.6f, 1.1f, 1.1f, 1.2f, 1.1f };
            int puestos = 0, intentos = 0;
            const float x0 = -46f, x1 = 46f, z0 = -34f, z1 = 170f;
            while (puestos < 70 && intentos++ < 3000)
            {
                var p = new Vector3(R(x0, x1), 0f, R(z0, z1));
                if (Cerca(p, 9f) || !EsTierra(p, out float y)) continue;
                int ix = rnd.Next(kit.Length);
                float yaw = R(0f, 360f);
                if (!LugarLibre(p, 1.3f * esc[ix], 1.3f * esc[ix], yaw, 1.4f)) continue;
                p.y = y;
                if (PonerArte(raiz, kit[ix], p, yaw, esc[ix], true) == null) continue;
                puestos++;
                Physics.SyncTransforms();
            }
            return PulidoFinalObjetos - n0 + puestos;
        }
    }
}
