using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Ronda 13 · T9: calles con sentido y un comienzo sin ruta.
    //  - Comienzo: fuera la calle de aproximacion (asfalto, lineas, postes, farolas) y en su lugar tierra con huellas, sacos y cajas de municion
    //    (SIN bidones de gasolina) y un muro de contencion caido.
    //  - Salida de los servidores: Calle_Salida se acorta (z -40..-24) y desemboca en una calle TRANSVERSAL en T (z -24..-12, x -48..48) con veredas,
    //    cordones y lineas; entre la T y el patio del tanque queda un acceso con isleta central.
    //  - La plaza final la resuelve T15 (solado y sendas) y T16 (mobiliario): aqui solo se informa la continuidad calle -> plaza.
    // Todo lo nuevo va en el grupo R13_Calles (escena Cuarteles) y SIN collider (los cordones no tocan el NavMesh).
    public static partial class OperacionBuilder
    {
        [MenuItem("Strategic Point/Operacion/R13 T9 · Calles en T y comienzo")]
        static void MenuCallesR13() => Debug.Log(CallesR13EscenaAbierta());

        static bool RegionDeLineasQuitadasR13(Vector3 c)
        {
            if (Mathf.Abs(c.x) > 0.6f) return false;
            if (c.z > -381f && c.z < -338.5f) return true;     // Linea_Aprox_0..4
            if (c.z > -26f && c.z < -1f) return true;          // Linea_Salida_2..4 (cruzaban la T y la isleta)
            return false;
        }

        /// <summary>
        /// T9. Firma: OperacionBuilder.CallesR13EscenaAbierta(bool soloInforme = false, bool coberturaSolida = false)
        /// coberturaSolida = true le pone collider al muro caido del comienzo (cobertura real; obliga a rehornear y a revisar Bug409u).
        /// </summary>
        public static string CallesR13EscenaAbierta(bool soloInforme = false, bool coberturaSolida = false)
        {
            var sb = new StringBuilder("== R13 T9 calles" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t9.txt");
            var todos = EscenasR13.Todos();
            var spawn = new Vector3(0f, 0f, -373f);

            // ---------------------------------------------------------------------------------- 1) apagar el comienzo viejo
            var apagar = new List<Transform>();
            foreach (var t in todos)
            {
                if (t == null || !t.gameObject.activeSelf) continue;
                string n = t.name; var p = t.position;
                bool zonaInicio = p.z < -338f && p.z > -385f && Mathf.Abs(p.x) < 10f;
                bool coincide = n == "Camino_Aproximacion" || n.StartsWith("Linea_Aprox_")
                    || ((n == "Poste_Zona_L" || n == "Poste_Zona_R" || n == "Poste_Brecha_L" || n == "Poste_Brecha_R") && zonaInicio)
                    || ((n.StartsWith("Farola_")) && zonaInicio && Mathf.Abs(p.x) > 5f && Mathf.Abs(p.x) < 9f)
                    || ((n == "Linea_Salida_2" || n == "Linea_Salida_3" || n == "Linea_Salida_4") && Mathf.Abs(p.x) < 1f);
                if (coincide) apagar.Add(t);
            }
            foreach (var t in apagar) sb.AppendLine($"APAGAR {EscenasR13.Ruta(t)} en {EscenasR13.P(t.position)}");
            sb.AppendLine($"objetos del comienzo/salida viejos a apagar: {apagar.Count}");

            // lineas fusionadas (celdas Amarillo_x0_z-400 y z-360, y la del tramo de salida): se recortan los triangulos de la region
            int recortados = RecortarFusionR13("M_Op_Amarillo", RegionDeLineasQuitadasR13, !soloInforme, sb);
            sb.AppendLine($"triangulos de lineas fusionadas recortados: {recortados}");

            // piezas del embudo/props que quedan sobre la T
            var enRuta = new List<(Transform t, string accion)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled) continue;
                var b = r.bounds; var c = b.center;
                bool enT = c.x > -49f && c.x < 49f && c.z > -24.5f && c.z < -11.5f;
                bool enAcceso = Mathf.Abs(c.x) < 6.5f && c.z >= -12f && c.z < -1f;
                if (!enT && !enAcceso) continue;
                if (b.size.y < 0.3f) continue;                                      // pintura, cordones, decals
                string n = r.name; string rp = EscenasR13.Ruta(r.transform);
                if (rp.Contains("R13_") || n.StartsWith("Calle") || n.StartsWith("Fus") || n.StartsWith("Amarillo")) continue;
                string acc = "REVISAR A MANO";
                var raiz = r.transform;
                if (n.Contains("Barricada_Concreto") || n.StartsWith("Baliza")) acc = "APAGAR (embudo ArteSalidaNorte)";
                else if (n.StartsWith("Saco_Salida") || (raiz.parent != null && raiz.parent.name.StartsWith("Saco_Salida"))) acc = "MOVER 3,5 m al norte";
                enRuta.Add((raiz, acc));
            }
            foreach (var e in enRuta) sb.AppendLine($"PROP SOBRE LA T/ACCESO {EscenasR13.Ruta(e.t)} en {EscenasR13.P(e.t.position)}: {e.accion}");

            // continuidad calle -> plaza (informe)
            foreach (var t in todos)
                if (t != null && t.gameObject.activeInHierarchy && (t.name == "Calle_Principal" || t.name == "Calle_Transversal" || t.name == "Calle_Salida" || t.name == "Destino_Huida"))
                    sb.AppendLine($"CONTINUIDAD {t.name} en {EscenasR13.P(t.position)} escala={t.lossyScale}");

            if (soloInforme)
            {
                sb.AppendLine("se crearia R13_Calles: R13_Comienzo (huellas, sacos, cajas, muro caido), Calle_T_Transversal, R13_Calle_Acceso_Patio, veredas, cordones, lineas e isleta");
                return EscenasR13.Cerrar(sb, "r13_t9.txt");
            }

            // ---------------------------------------------------------------------------------- aplicar
            foreach (var t in apagar) if (t != null) { t.gameObject.SetActive(false); EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene); }
            foreach (var e in enRuta)
            {
                if (e.t == null) continue;
                if (e.accion.StartsWith("APAGAR")) { e.t.gameObject.SetActive(false); EscenasR13.Tocar(e.t.gameObject); EscenasR13.Sucia(e.t.gameObject.scene); }
                else if (e.accion.StartsWith("MOVER"))
                {
                    var objetivo = e.t.name.StartsWith("Saco_Salida") ? e.t : e.t.parent;
                    if (objetivo != null) { objetivo.position += new Vector3(0f, 0f, 3.5f); EscenasR13.Tocar(objetivo); EscenasR13.Sucia(objetivo.gameObject.scene); }
                }
            }

            // Calle_Salida: z -40..-24 (centro -32, largo 16)
            foreach (var t in todos)
            {
                if (t == null || t.name != "Calle_Salida") continue;
                var p = t.position; var s = t.localScale;
                t.position = new Vector3(p.x, p.y, -32f); t.localScale = new Vector3(s.x, s.y, 16f);
                EscenasR13.Tocar(t); EscenasR13.Sucia(t.gameObject.scene);
                sb.AppendLine("Calle_Salida acortada a z -40..-24");
            }

            var escena = EscenasR13.Sector("Cuarteles");
            var g = EscenasR13.GrupoNuevo(escena, "R13_Calles");
            var vereda = MatVeredaR13(); var guarda = MatGuardaR13();
            var tierra = Mat("R13_Tierra", new Color(0.36f, 0.27f, 0.18f)); var cantero = Mat("R13_Cantero", new Color(0.30f, 0.24f, 0.16f));
            var huella = Mat("R13_Huella", new Color(0.20f, 0.15f, 0.10f));

            // ---- comienzo
            var gc = EscenasR13.Hijo(g, "R13_Comienzo");
            Pintura(gc, "R13_Huella_Rueda_I", -1.5f, -358f, 0.45f, 34f, huella, 0.6f, 0.03f, 0.02f);
            Pintura(gc, "R13_Huella_Rueda_D", 1.5f, -358f, 0.45f, 34f, huella, -0.6f, 0.03f, 0.02f);
            for (int i = 0; i < 5; i++) PonerDecal(gc, "Huellas", new Vector3(((i % 2) * 2 - 1) * 2.6f, 0.05f, -376f + i * 7.5f), Vector3.up, 8f + i * 23f, 3.2f, 3.2f);
            Pintura(gc, "R13_TierraBatida", 0f, -357f, 9f, 36f, tierra, 0f, 0.025f, 0.02f);
            var mCaja = Cobertura;
            for (int i = 0; i < 6; i++)
            {
                float sx = (i % 2 == 0 ? -1f : 1f) * (8.5f + (i / 2) * 1.3f), sz = -366f + (i / 2) * 7f;
                Decor(gc, "R13_CajaMunicion_" + (i + 1), sx, sz, 0.95f, 0.55f, 0.65f, mCaja, i * 17f);
                if (i % 3 == 0) Decor(gc, "R13_CajaMunicion_Alta_" + (i + 1), sx + 0.1f, sz, 0.9f, 0.5f, 0.6f, mCaja, i * 17f + 8f, 0.55f);
            }
            foreach (var (sx, sz, yaw) in new[] { (-9f, -352f, 20f), (9f, -354f, -15f), (-8f, -372f, -8f), (8f, -370f, 12f) })
            {
                var saco = PonerArte(gc, "P_Env_Barricada_Sacos", new Vector3(sx, 0f, sz), yaw, 1f, true);
                if (saco != null) foreach (var c in saco.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            // muro de contencion caido: cobertura de entrada (decorado salvo coberturaSolida)
            Cubo(gc, "R13_MuroCaido", -3.8f, -350f, 6f, 0.7f, 0.8f, CementoOsc, 14f, 0.05f, coberturaSolida);
            Cubo(gc, "R13_MuroCaido_Pedazo", 2.6f, -347.5f, 2.2f, 0.45f, 0.8f, CementoOsc, -24f, 0.02f, false);

            // ---- T tras las computadoras
            var gt = EscenasR13.Hijo(g, "R13_T");
            Pintura(gt, "Calle_T_Transversal", 0f, -18f, 96f, 12f, Asfalto, 0f, 0f, 0.08f);
            Pintura(gt, "R13_Calle_Acceso_Patio", 0f, -6.5f, 12f, 11f, Asfalto, 0f, 0f, 0.08f);
            // veredas de 2 m (con el corte de 12 m de las calles que desembocan)
            Cubo(gt, "R13_Vereda_S_O", -27f, -25f, 42f, 0.12f, 2f, vereda, 0f, 0f, false); Cubo(gt, "R13_Vereda_S_E", 27f, -25f, 42f, 0.12f, 2f, vereda, 0f, 0f, false);
            Cubo(gt, "R13_Vereda_N_O", -27f, -11f, 42f, 0.12f, 2f, vereda, 0f, 0f, false); Cubo(gt, "R13_Vereda_N_E", 27f, -11f, 42f, 0.12f, 2f, vereda, 0f, 0f, false);
            Cubo(gt, "R13_Vereda_Acceso_O", -7f, -5.5f, 2f, 0.12f, 9f, vereda, 0f, 0f, false); Cubo(gt, "R13_Vereda_Acceso_E", 7f, -5.5f, 2f, 0.12f, 9f, vereda, 0f, 0f, false);
            // cordones de 0,15 m SIN collider
            void Cordon(string n, float x, float z, float w, float d) => Cubo(gt, "R13_Cordon_" + n, x, z, w, 0.15f, d, guarda, 0f, 0f, false);
            Cordon("S_O", -27f, -24.075f, 42f, 0.15f); Cordon("S_E", 27f, -24.075f, 42f, 0.15f);
            Cordon("N_O", -27f, -11.925f, 42f, 0.15f); Cordon("N_E", 27f, -11.925f, 42f, 0.15f);
            Cordon("Salida_O", -6.075f, -33f, 0.15f, 14f); Cordon("Salida_E", 6.075f, -33f, 0.15f, 14f);
            Cordon("Acceso_O", -6.075f, -5.5f, 0.15f, 9f); Cordon("Acceso_E", 6.075f, -5.5f, 0.15f, 9f);
            Cordon("Fin_O", -48.075f, -18f, 0.15f, 12f); Cordon("Fin_E", 48.075f, -18f, 0.15f, 12f);   // cierre de los brazos de la T (Bug978a: bordes cortos conectados)
            // lineas de borde blancas y eje punteado amarillo
            for (int s = -1; s <= 1; s += 2)
            {
                Pintura(gt, "R13_LineaBorde_S_" + (s < 0 ? "O" : "E"), s * 28f, -23.6f, 40f, 0.15f, Blanco, 0f, 0.075f, 0.02f);
                Pintura(gt, "R13_LineaBorde_N_" + (s < 0 ? "O" : "E"), s * 28f, -12.4f, 40f, 0.15f, Blanco, 0f, 0.075f, 0.02f);
                for (int i = 0; i < 8; i++) Pintura(gt, "R13_EjeT_" + (s < 0 ? "O_" : "E_") + i, s * (10f + i * 5f), -18f, 2f, 0.2f, Amarillo, 0f, 0.075f, 0.02f);
            }
            // isleta central frente al patio (z -10..-3): cantero con arbusto, sin collider
            Cubo(gt, "R13_Isleta_Borde", 0f, -6.5f, 2.7f, 0.2f, 7.7f, guarda, 0f, 0f, false);
            Cubo(gt, "R13_Isleta_Cantero", 0f, -6.5f, 2.3f, 0.25f, 7.3f, cantero, 0f, 0f, false);
            foreach (float zz in new[] { -9f, -4f })
            {
                var arb = PonerArte(gt, "P_Env_Arbusto_Medio", new Vector3(0f, 0.2f, zz), zz * 13f, 1f, true);
                if (arb != null) foreach (var c in arb.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }

            AssetDatabase.SaveAssets();
            EscenasR13.Guardar(sb);
            sb.Insert(0, $"R13_Calles creado en {escena.name}; Calle_Salida acortada. Pendiente: rehornear NavMesh (Ola 2, paso 10) y portar sin ejecutar a Perimetro/PatioDelTanque/Cuartel.cs.\n");
            return EscenasR13.Cerrar(sb, "r13_t9.txt");
        }

        // Quita los triangulos cuyo centroide (en el mundo) cae en 'region' de las mallas FUSIONADAS de ese material. Devuelve la cantidad de triangulos.
        // La malla recortada se guarda como asset propio (Generated/R13) y se asigna a la instancia: no se toca el prefab Fus_*.
        static int RecortarFusionR13(string nombreMaterial, System.Func<Vector3, bool> region, bool aplicar, StringBuilder sb)
        {
            int total = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.name != nombreMaterial) continue;
                var m = mf.sharedMesh; if (m == null || m.vertexCount < 120) continue;
                string ruta = AssetDatabase.GetAssetPath(m);
                bool fusionada = mf.name.StartsWith("Amarillo_") || mf.name.StartsWith("Fus_") || ruta.Contains("FusionOperacion") || ruta.Contains("/Generated/R13/");
                if (!fusionada) continue;
                if (m.subMeshCount != 1) { sb.AppendLine("AVISO malla con varios submeshes, no se recorta: " + EscenasR13.Ruta(mf.transform)); continue; }
                if (!m.isReadable) { sb.AppendLine("AVISO malla no legible, no se recorta: " + EscenasR13.Ruta(mf.transform)); continue; }
                var vs = m.vertices; var tr = m.triangles; var mat = mf.transform.localToWorldMatrix;
                var keep = new List<int>(tr.Length); int quitados = 0;
                for (int i = 0; i < tr.Length; i += 3)
                {
                    var c = (mat.MultiplyPoint3x4(vs[tr[i]]) + mat.MultiplyPoint3x4(vs[tr[i + 1]]) + mat.MultiplyPoint3x4(vs[tr[i + 2]])) / 3f;
                    if (region(c)) quitados++; else { keep.Add(tr[i]); keep.Add(tr[i + 1]); keep.Add(tr[i + 2]); }
                }
                if (quitados == 0) continue;
                total += quitados;
                sb.AppendLine($"RECORTE {EscenasR13.Ruta(mf.transform)}: {quitados} de {tr.Length / 3} triangulos");
                if (!aplicar) continue;
                Mesh destino;
                if (ruta.StartsWith(EscenasR13.CarpetaGenerada)) destino = m;
                else
                {
                    EscenasR13.AsegurarCarpetaGenerada();
                    destino = Object.Instantiate(m); destino.name = "R13_" + m.name;
                    AssetDatabase.CreateAsset(destino, AssetDatabase.GenerateUniqueAssetPath($"{EscenasR13.CarpetaGenerada}/R13_{mf.name}_{mf.gameObject.scene.name}.asset"));
                }
                destino.SetTriangles(keep, 0); destino.RecalculateBounds(); EditorUtility.SetDirty(destino);
                mf.sharedMesh = destino; EscenasR13.Tocar(mf); EscenasR13.Sucia(mf.gameObject.scene);
            }
            return total;
        }
    }
}
