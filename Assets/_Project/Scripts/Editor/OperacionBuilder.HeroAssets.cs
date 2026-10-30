using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido "mejorar hero assets: bomba, portones, generador". El portón vive en OperacionBuilder.PortonBlindado.cs. Aca:
    //   - Generador del cuartel: lo que colgaba de la raiz (rejilla, pilotos, techo amarillo, escape, tanque, rotulo) pasa a "Adjuntos" dentro de
    //     Cuerpo, asi se va con el cuando cae (antes quedaban flotando); y gana persianas, rejillas de techo, puerta de servicio, caño y franjas.
    //   - Cargas explosivas (Carga_*): bloques de explosivo, modulo temporizador con pantalla, cables, antena, asa y placa de peligro.
    // Arte sin colliders. Idempotente: rehace "Adjuntos" y "DetalleDeBomba".
    public static partial class OperacionBuilder
    {
        [MenuItem("Strategic Point/Arte/Hero assets: generador y bombas (SC_Operacion)")]
        static void HeroAssetsMenu() => Debug.Log(HeroAssetsEscenaAbierta());

        static GameObject FormaPBc(PrimitiveType tipo, Transform p, string n, Vector3 local, Vector3 s, Material m, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(tipo);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = n; go.transform.SetParent(p, false);
            go.transform.localPosition = local; go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero); go.transform.localScale = s;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Transform ContenedorInverso(Transform padre, string nombre)
        {
            var viejo = padre.Find(nombre);
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var o = new GameObject(nombre).transform;
            o.SetParent(padre, false);
            var ls = padre.lossyScale;
            o.localPosition = Vector3.zero; o.localRotation = Quaternion.identity;
            o.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            return o;
        }

        static Transform BuscarEnEscena(string nombre, string padre = null)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == nombre && t.gameObject.scene.IsValid() && (padre == null || (t.parent != null && t.parent.name == padre))) return t;
            return null;
        }

        public static string HeroAssetsEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            string r1 = GeneradorHero(), r2 = BombasHero();
            EditorSceneManager.MarkSceneDirty(escena);
            return r1 + " | " + r2;
        }

        static string GeneradorHero()
        {
            var grupo = BuscarEnEscena("Generador_Cuartel");
            var cuerpo = grupo != null ? grupo.Find("Cuerpo") : null;
            if (cuerpo == null) return "sin Generador_Cuartel/Cuerpo";
            // Lo que colgaba de la raiz y debe irse con el cuerpo. La Base queda como la plataforma quemada.
            var viejoAd = cuerpo.Find("Adjuntos");
            var aMover = new System.Collections.Generic.List<Transform>();
            foreach (var n in new[] { "Rejilla", "Franja_Aviso", "Piloto_0", "Piloto_1", "Piloto_2", "Escape", "Tanque", "Rotulo_GENERADOR" })
            {
                var t = grupo.Find(n); if (t != null) aMover.Add(t);
                else if (viejoAd != null) { var h = viejoAd.Find(n); if (h != null) aMover.Add(h); }
            }
            // Si ya existia "Adjuntos", sus hijos originales se conservan: se arma uno nuevo, se mudan y se borra el viejo.
            var ad = ContenedorInverso(cuerpo, "Adjuntos_tmp");
            foreach (var t in aMover) t.SetParent(ad, true);
            if (viejoAd != null) Object.DestroyImmediate(viejoAd.gameObject);
            ad.name = "Adjuntos";

            var oscuro = Mat("GenMetalOscuro", new Color(0.09f, 0.10f, 0.11f));
            var metal = Mat("GenMetal", new Color(0.22f, 0.25f, 0.24f));
            var amarillo = Mat("PortonPeligroAmarillo", new Color(0.95f, 0.72f, 0.08f));
            var negro = Negro;
            var cobre = Mat("GenCobre", new Color(0.55f, 0.32f, 0.15f));
            // coordenadas locales del contenedor = mundo relativo al centro del cuerpo (5 x 2.2 x 2.6)
            for (int lado = -1; lado <= 1; lado += 2)
                for (int i = 0; i < 8; i++)
                    CajaPB(ad, "Persiana", new Vector3(lado * 2.53f, -0.7f + i * 0.2f, 0f), new Vector3(0.07f, 0.09f, 1.9f), oscuro, 0f, false);
            for (int i = 0; i < 3; i++)
                CajaPB(ad, "Ventila", new Vector3(-1.4f + i * 1.4f, 1.22f, 0.15f), new Vector3(1.0f, 0.1f, 0.9f), oscuro, 0f, false);
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < 4; i++)
                    CajaPB(ad, "Ventila_Barra", new Vector3(-1.4f + k * 1.4f, 1.29f, -0.3f + i * 0.2f), new Vector3(0.9f, 0.04f, 0.05f), metal, 0f, false);
            CajaPB(ad, "Puerta_Servicio", new Vector3(0.6f, -0.1f, 1.33f), new Vector3(1.3f, 1.7f, 0.05f), metal, 0f, false);
            CajaPB(ad, "Puerta_Asa", new Vector3(0.1f, -0.1f, 1.38f), new Vector3(0.06f, 0.45f, 0.05f), negro, 0f, false);
            CajaPB(ad, "Placa_Peligro", new Vector3(-1.2f, 0.45f, 1.34f), new Vector3(0.7f, 0.5f, 0.03f), amarillo, 0f, false);
            for (int b = 0; b < 10; b++)
                CajaPB(ad, "Franja_Base", new Vector3(-2.25f + b * 0.5f, -0.93f, -1.34f), new Vector3(0.5f, 0.28f, 0.03f), b % 2 == 0 ? amarillo : negro, 0f, false);
            FormaPBc(PrimitiveType.Cylinder, ad, "Conducto", new Vector3(2.2f, -0.55f, -1.45f), new Vector3(0.16f, 0.55f, 0.16f), cobre);
            CajaPB(ad, "Conducto_Codo", new Vector3(2.2f, 0.05f, -1.4f), new Vector3(0.18f, 0.18f, 0.25f), cobre, 0f, false);
            FormaPBc(PrimitiveType.Cylinder, ad, "Tapa_Escape", new Vector3(1.8f, 3.1f - 1.1f + 0.98f, -0.8f), new Vector3(0.5f, 0.05f, 0.5f), oscuro);
            return $"Generador: {ad.childCount} piezas en Adjuntos";
        }

        static string BombasHero()
        {
            var verde = Mat("BombaPantalla", new Color(0.2f, 1f, 0.4f), 1.6f);
            var tan = Mat("BombaExplosivo", new Color(0.55f, 0.50f, 0.32f));
            var negro = Negro;
            var correa = Mat("BombaCorrea", new Color(0.12f, 0.13f, 0.12f));
            var amarillo = Mat("PortonPeligroAmarillo", new Color(0.95f, 0.72f, 0.08f));
            var cRoja = Mat("CableRojo", new Color(0.55f, 0.08f, 0.08f));
            var cAzul = Mat("CableAzul", new Color(0.10f, 0.25f, 0.55f));
            var cAmar = Mat("BombaCableAmarillo", new Color(0.85f, 0.7f, 0.1f));
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "Caja_Carga" || !t.gameObject.scene.IsValid() || t.parent == null || !t.parent.name.StartsWith("Carga_")) continue;
                var d = ContenedorInverso(t, "DetalleDeBomba");
                // caja de 1.1 x 0.85 x 0.8 centrada en el contenedor
                for (int s = -1; s <= 1; s += 2)
                    CajaPB(d, "Correa", new Vector3(s * 0.3f, 0f, 0f), new Vector3(0.08f, 0.9f, 0.84f), correa, 0f, false);
                for (int s = -1; s <= 1; s += 2)
                    CajaPB(d, "Explosivo", new Vector3(s * 0.34f, 0.5f, 0.05f), new Vector3(0.3f, 0.12f, 0.5f), tan, 0f, false);
                CajaPB(d, "Temporizador", new Vector3(0f, 0.47f, 0f), new Vector3(0.36f, 0.07f, 0.26f), negro, 0f, false);
                CajaPB(d, "Pantalla", new Vector3(0f, 0.52f, 0f), new Vector3(0.28f, 0.02f, 0.14f), verde, 0f, false);
                CajaPB(d, "Cable_Rojo", new Vector3(-0.17f, 0.5f, 0.03f), new Vector3(0.18f, 0.025f, 0.025f), cRoja, 0f, false);
                CajaPB(d, "Cable_Azul", new Vector3(0.17f, 0.5f, -0.03f), new Vector3(0.18f, 0.025f, 0.025f), cAzul, 0f, false);
                CajaPB(d, "Cable_Amarillo", new Vector3(0f, 0.5f, -0.13f), new Vector3(0.025f, 0.025f, 0.1f), cAmar, 0f, false);
                FormaPBc(PrimitiveType.Cylinder, d, "Antena", new Vector3(0.45f, 0.62f, 0.28f), new Vector3(0.025f, 0.2f, 0.025f), negro);
                CajaPB(d, "Asa", new Vector3(-0.58f, 0.1f, 0f), new Vector3(0.05f, 0.05f, 0.4f), negro, 0f, false);
                CajaPB(d, "Placa_Peligro", new Vector3(0f, -0.1f, -0.41f), new Vector3(0.5f, 0.3f, 0.02f), amarillo, 0f, false);
                n++;
            }
            return $"Bombas con detalle: {n}";
        }
    }
}
