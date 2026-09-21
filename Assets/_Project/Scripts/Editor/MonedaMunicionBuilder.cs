using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Ronda 12: la municion que sueltan los enemigos era un cubo amarillo. Ahora es una MONEDA 3D con una bala en relieve,
    // como las monedas de un juego de plataformas: disco dorado con borde elevado, una bala de laton en relieve en cada cara.
    // Se genera por codigo (mallas de revolucion) y se guarda como prefab en Resources/Pickups; la moneda gira y flota en
    // MunicionPickup/MonedaGiratoria. Menu: Strategic Point > Arte > 10 Moneda de municion.
    public static class MonedaMunicionBuilder
    {
        public const string CarpetaMallas = "Assets/_Project/Arte/Pickups";
        public const string RutaPrefab = "Assets/_Project/Resources/Pickups/P_MunicionMoneda.prefab";

        [MenuItem("Strategic Point/Arte/10 Moneda de municion")]
        public static void Construir()
        {
            Directory.CreateDirectory(CarpetaMallas);
            Directory.CreateDirectory(Path.GetDirectoryName(RutaPrefab));

            // Perfil de la moneda (radio, y) medido desde el centro de una cara hacia el borde y de vuelta por la otra cara.
            // Eje de revolucion = Z local: la cara mira a +Z/-Z y la moneda "de canto" queda parada como en un juego.
            var perfilMoneda = new List<Vector2>
            {
                new Vector2(0.00f, 0.035f), new Vector2(0.36f, 0.035f), new Vector2(0.385f, 0.05f),   // cara hundida (fondo del relieve)
                new Vector2(0.41f, 0.075f), new Vector2(0.47f, 0.075f), new Vector2(0.50f, 0.055f),   // aro elevado con bisel
                new Vector2(0.50f, -0.055f), new Vector2(0.47f, -0.075f), new Vector2(0.41f, -0.075f),
                new Vector2(0.385f, -0.05f), new Vector2(0.36f, -0.035f), new Vector2(0.00f, -0.035f)
            };
            var mallaMoneda = Revolucion(perfilMoneda, 40, Vector3.forward, "SM_MonedaMunicion_Cuerpo", true);

            // Bala: casquillo + punta ojival, de pie (eje Y), 0,42 de alto. Se achata en Z para que sea un relieve.
            var perfilBala = new List<Vector2>
            {
                new Vector2(0.00f, -0.21f), new Vector2(0.075f, -0.21f), new Vector2(0.085f, -0.19f),   // culote con ranura
                new Vector2(0.085f, -0.16f), new Vector2(0.065f, -0.15f), new Vector2(0.065f, -0.02f), // cuello del casquillo
                new Vector2(0.068f, -0.02f), new Vector2(0.068f, 0.05f),                                // proyectil
                new Vector2(0.060f, 0.11f), new Vector2(0.040f, 0.16f), new Vector2(0.015f, 0.195f), new Vector2(0.00f, 0.205f)
            };
            var mallaBala = Revolucion(perfilBala, 20, Vector3.up, "SM_MonedaMunicion_Bala", false);

            var oro = Material("MAT_MonedaMunicion_Oro", new Color(1f, 0.74f, 0.14f), 0.85f, 0.62f);
            var laton = Material("MAT_MonedaMunicion_Laton", new Color(0.74f, 0.36f, 0.12f), 0.9f, 0.5f);
            var punta = Material("MAT_MonedaMunicion_Punta", new Color(0.95f, 0.93f, 0.85f), 0.9f, 0.7f);

            var raiz = new GameObject("P_MunicionMoneda");
            var cuerpo = Parte(raiz.transform, "Cuerpo", mallaMoneda, oro, Vector3.zero, Vector3.one);
            Parte(raiz.transform, "Bala_Frente", mallaBala, laton, new Vector3(0f, 0f, 0.075f), new Vector3(1f, 1f, 0.32f));
            Parte(raiz.transform, "Bala_Dorso", mallaBala, laton, new Vector3(0f, 0f, -0.075f), new Vector3(1f, 1f, 0.32f));
            // Pequenas puntas claras (la punta de la bala) para leer el "proyectil" aunque este lejos.
            var puntaMalla = Revolucion(new List<Vector2> { new Vector2(0f, 0.11f), new Vector2(0.061f, 0.11f), new Vector2(0.040f, 0.16f), new Vector2(0.015f, 0.195f), new Vector2(0f, 0.205f) }, 20, Vector3.up, "SM_MonedaMunicion_Punta", false);
            Parte(raiz.transform, "Punta_Frente", puntaMalla, punta, new Vector3(0f, 0f, 0.0765f), new Vector3(1.02f, 1.02f, 0.33f));
            Parte(raiz.transform, "Punta_Dorso", puntaMalla, punta, new Vector3(0f, 0f, -0.0765f), new Vector3(1.02f, 1.02f, 0.33f));

            // Sin sombra propia (es un objeto chico que gira) y con trigger para recogerla.
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var sc = raiz.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 0.55f;
            // La moneda mide 1 m de diametro en el prefab; la escala real la pone MunicionPickup (0,45).
            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            Object.DestroyImmediate(raiz);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MONEDA] Prefab de moneda de municion creado: " + RutaPrefab + (prefab != null ? " (ok)" : " (FALLO)"));
        }

        static GameObject Parte(Transform padre, string nombre, Mesh malla, Material mat, Vector3 pos, Vector3 escala)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos; g.transform.localScale = escala;
            g.AddComponent<MeshFilter>().sharedMesh = malla;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        static Material Material(string nombre, Color c, float metalico, float suavidad)
        {
            string ruta = CarpetaMallas + "/" + nombre + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Metallic", metalico); m.SetFloat("_Smoothness", suavidad);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.16f);   // un brillo propio leve: se ve de lejos en el pasto
            EditorUtility.SetDirty(m);
            return m;
        }

        // Malla de revolucion: gira el perfil (x = radio, y = altura sobre el eje) alrededor de 'eje'. Caras planas por franja.
        static Mesh Revolucion(List<Vector2> perfil, int lados, Vector3 eje, string nombre, bool horario)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            Quaternion aEje = Quaternion.FromToRotation(Vector3.up, eje);
            for (int s = 0; s < lados; s++)
            {
                float a0 = s / (float)lados * Mathf.PI * 2f, a1 = (s + 1) / (float)lados * Mathf.PI * 2f;
                for (int p = 0; p < perfil.Count - 1; p++)
                {
                    Vector2 P = perfil[p], Q = perfil[p + 1];
                    if (Mathf.Abs(P.x) < 1e-5f && Mathf.Abs(Q.x) < 1e-5f) continue;
                    Vector3 A = Punto(P, a0, aEje), B = Punto(Q, a0, aEje), C = Punto(Q, a1, aEje), D = Punto(P, a1, aEje);
                    int i0 = v.Count;
                    v.Add(A); v.Add(B); v.Add(C); v.Add(D);
                    uv.Add(new Vector2(s / (float)lados, p / (float)perfil.Count)); uv.Add(new Vector2(s / (float)lados, (p + 1) / (float)perfil.Count));
                    uv.Add(new Vector2((s + 1) / (float)lados, (p + 1) / (float)perfil.Count)); uv.Add(new Vector2((s + 1) / (float)lados, p / (float)perfil.Count));
                    // Normal hacia afuera esperada: perfil horario -> (-dy, dx); antihorario -> (dy, -dx). Se compara con la cara real.
                    Vector2 t = Q - P;
                    Vector2 n2 = horario ? new Vector2(-t.y, t.x) : new Vector2(t.y, -t.x);
                    float am = (a0 + a1) * 0.5f;
                    Vector3 esperada = aEje * new Vector3(Mathf.Cos(am) * n2.x, n2.y, Mathf.Sin(am) * n2.x);
                    Vector3 nCara = Vector3.Cross(B - A, C - A);
                    if (nCara.sqrMagnitude < 1e-12f) nCara = Vector3.Cross(C - A, D - A);
                    bool ok = Vector3.Dot(nCara, esperada) >= 0f;
                    if (ok) { tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2); tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3); }
                    else { tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 1); tri.Add(i0); tri.Add(i0 + 3); tri.Add(i0 + 2); }
                }
            }
            var m = new Mesh { name = nombre };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            string ruta = CarpetaMallas + "/" + nombre + ".asset";
            var previo = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (previo != null) { EditorUtility.CopySerialized(m, previo); Object.DestroyImmediate(m); return previo; }
            AssetDatabase.CreateAsset(m, ruta);
            return m;
        }

        static Vector3 Punto(Vector2 pf, float ang, Quaternion aEje)
            => aEje * new Vector3(Mathf.Cos(ang) * pf.x, pf.y, Mathf.Sin(ang) * pf.x);
    }
}
