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

        // Bug #318 ("corregi la geometria, mejorala"): la carga era un cajon rojo con una cinta. Ahora es una bomba reconocible: haz de 5 cartuchos
        // de explosivo atados con dos correas, temporizador con pantalla verde, cables, antena, asa, placa de peligro y base. El tinte de armado
        // (CargaExplosiva.caja) pasa al cuerpo del temporizador. Sin colliders: el del cajon original (Caja_Carga) sigue siendo el de la carga.
        static string BombasHero()
        {
            var dinamita = Mat("BombaDinamita", new Color(0.55f, 0.07f, 0.05f));
            var tapa = Mat("BombaTapa", new Color(0.82f, 0.74f, 0.55f));
            var placa = Mat("BombaPlaca", new Color(0.12f, 0.13f, 0.12f));
            var correa = Mat("BombaCorrea", new Color(0.05f, 0.05f, 0.06f));
            var temporizador = Mat("BombaTemporizador", new Color(0.55f, 0.1f, 0.08f));
            var verde = Mat("BombaPantalla", new Color(0.2f, 1f, 0.4f), 1.6f);
            var negro = Negro;
            var amarillo = Mat("PortonPeligroAmarillo", new Color(0.95f, 0.72f, 0.08f));
            var cRoja = Mat("CableRojo", new Color(0.55f, 0.08f, 0.08f));
            var cAzul = Mat("CableAzul", new Color(0.10f, 0.25f, 0.55f));
            var cAmar = Mat("BombaCableAmarillo", new Color(0.85f, 0.7f, 0.1f));
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name != "Caja_Carga" || !t.gameObject.scene.IsValid() || t.parent == null || !t.parent.name.StartsWith("Carga_")) continue;
                var viejo = t.Find("DetalleDeBomba"); if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
                var viejoArte = t.parent.Find("ArteBomba"); if (viejoArte != null) Object.DestroyImmediate(viejoArte.gameObject);
                var ls = t.parent.lossyScale;
                var d = new GameObject("ArteBomba").transform;
                d.SetParent(t.parent, false);
                d.position = t.position; d.rotation = Quaternion.identity;
                d.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
                // el cajon original y su cinta quedan como colision / sin dibujar
                var rc = t.GetComponent<MeshRenderer>(); if (rc != null) rc.enabled = false;
                var cinta = t.parent.Find("Caja_Cinta"); if (cinta != null) { var rr = cinta.GetComponent<MeshRenderer>(); if (rr != null) rr.enabled = false; }

                CajaPB(d, "Base", new Vector3(0f, -0.39f, 0f), new Vector3(1.3f, 0.07f, 1.0f), placa, 0f, false);
                // haz de cartuchos tumbados a lo largo de X: 3 abajo, 2 arriba
                float[] zs = { -0.22f, 0f, 0.22f, -0.11f, 0.11f }; float[] ys = { -0.22f, -0.22f, -0.22f, -0.01f, -0.01f };
                for (int i = 0; i < 5; i++)
                {
                    FormaPBc(PrimitiveType.Cylinder, d, "Cartucho", new Vector3(0f, ys[i], zs[i]), new Vector3(0.2f, 0.48f, 0.2f), dinamita, new Vector3(0f, 0f, 90f));
                    for (int e = -1; e <= 1; e += 2)
                        FormaPBc(PrimitiveType.Cylinder, d, "Tapa", new Vector3(e * 0.485f, ys[i], zs[i]), new Vector3(0.17f, 0.012f, 0.17f), tapa, new Vector3(0f, 0f, 90f));
                }
                for (int s = -1; s <= 1; s += 2)
                    CajaPB(d, "Correa", new Vector3(s * 0.27f, -0.11f, 0f), new Vector3(0.07f, 0.5f, 0.68f), correa, 0f, false);
                // temporizador al frente (el que se tine al armarse), con pantalla verde, botones y placa de peligro atras
                var cuerpo = CajaPB(d, "Temporizador", new Vector3(0f, -0.06f, -0.45f), new Vector3(0.62f, 0.38f, 0.2f), temporizador, 0f, false);
                cuerpo.name = "Temporizador";
                CajaPB(d, "Pantalla", new Vector3(0f, -0.01f, -0.555f), new Vector3(0.42f, 0.16f, 0.02f), verde, 0f, false);
                CajaPB(d, "Boton_Rojo", new Vector3(-0.15f, -0.15f, -0.555f), new Vector3(0.1f, 0.05f, 0.025f), cRoja, 0f, false);
                CajaPB(d, "Boton_Verde", new Vector3(0.15f, -0.15f, -0.555f), new Vector3(0.1f, 0.05f, 0.025f), verde, 0f, false);
                CajaPB(d, "Placa_Peligro", new Vector3(0f, -0.1f, 0.45f), new Vector3(0.5f, 0.24f, 0.02f), amarillo, 0f, false);
                // cables del temporizador a los cartuchos
                CajaPB(d, "Cable_Rojo", new Vector3(-0.18f, 0.12f, -0.3f), new Vector3(0.025f, 0.025f, 0.3f), cRoja, 0f, false);
                CajaPB(d, "Cable_Azul", new Vector3(0.0f, 0.12f, -0.3f), new Vector3(0.025f, 0.025f, 0.3f), cAzul, 0f, false);
                CajaPB(d, "Cable_Amarillo", new Vector3(0.18f, 0.12f, -0.3f), new Vector3(0.025f, 0.025f, 0.3f), cAmar, 0f, false);
                CajaPB(d, "Cable_Rojo_Tendido", new Vector3(-0.18f, 0.13f, -0.05f), new Vector3(0.3f, 0.025f, 0.025f), cRoja, 0f, false);
                // asa y antena
                for (int s = -1; s <= 1; s += 2) CajaPB(d, "Asa_Poste", new Vector3(s * 0.24f, 0.19f, 0f), new Vector3(0.05f, 0.14f, 0.05f), negro, 0f, false);
                CajaPB(d, "Asa", new Vector3(0f, 0.27f, 0f), new Vector3(0.53f, 0.05f, 0.05f), negro, 0f, false);
                FormaPBc(PrimitiveType.Cylinder, d, "Antena", new Vector3(0.32f, 0.3f, -0.45f), new Vector3(0.025f, 0.32f, 0.025f), negro);
                FormaPBc(PrimitiveType.Sphere, d, "Antena_Punta", new Vector3(0.32f, 0.62f, -0.45f), new Vector3(0.06f, 0.06f, 0.06f), cRoja);
                // el LED de estado deja de flotar sobre el cajon: pasa a la cara superior del temporizador
                var led = t.parent.Find("Led");
                if (led != null) { led.position = d.position + new Vector3(0.2f, 0.17f, -0.45f); led.localScale = new Vector3(0.12f, 0.06f, 0.12f); }
                // el tinte de armado va al cuerpo del temporizador
                var ce = t.parent.GetComponent<SP.Operacion.CargaExplosiva>();
                if (ce != null) { ce.caja = cuerpo.GetComponent<MeshRenderer>(); EditorUtility.SetDirty(ce); }
                n++;
            }
            return $"Bombas rehechas: {n}";
        }
    }
}
