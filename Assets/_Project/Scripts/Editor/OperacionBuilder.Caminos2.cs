using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using SP.Operacion;

namespace SP.EditorTools
{
    // Rediseno de caminos, segunda tanda (2026-11-03, pasadas 51-100): a diferencia de la primera (rutas que solo usa el jugador) esta abre
    // pasos a nivel del suelo que usan todos, la IA incluida:
    //   patios       se quitan 4 edificios (uno por manzana) y su lugar pasa a ser un patio con coberturas, puestos y luces, que une el anillo con los callejones
    //   pasarelas    callejon corto del norte y callejon vertical largo del sur
    //   soportales   galeria cubierta sobre la fachada de las manzanas del sur que da a la avenida
    public static partial class OperacionBuilder
    {
        const string GrupoCaminos2 = "13_Caminos2";

        // Numero del edificio (orden de ConstruirCiudadIntegral: manzana, fila, columna) y su huella.
        static (int numero, Rect huella)[] EdificiosAbiertos()
        {
            var l = new List<(int, Rect)>(); int ed = 0;
            for (int mi = 0; mi < Manzanas.Length; mi++)
            {
                var m = Manzanas[mi]; float ancho = m.x1 - m.x0, largo = m.z1 - m.z0, alto = (largo - 8f) / 3f;
                int cols = ancho >= 30f ? 2 : 1; float anc = (ancho - 4f * (cols - 1)) / cols;
                for (int i = 0; i < 3; i++)
                    for (int c = 0; c < cols; c++)
                    {
                        ed++;
                        float z0 = m.z0 + i * (alto + 4f), x0 = m.x0 + c * (anc + 4f);
                        bool abrir = (cols == 1 && i == 1) || (cols == 2 && i == 2 && c == (mi == 2 ? 0 : 1));
                        if (abrir) l.Add((ed, Rect.MinMaxRect(x0, z0, x0 + anc, z0 + alto)));
                    }
            }
            return l.ToArray();
        }

        static void Patio(Transform t, Rect r, int k, StringBuilderLite res)
        {
            float cx = r.center.x, cz = r.center.y; var color = ColorRuta(k); var neon = NeonRuta(k);
            // Piso de losas claras y oscuras sobre la vereda (sin collider).
            int nx = Mathf.FloorToInt(r.width / 2.5f), nz = Mathf.FloorToInt(r.height / 2.5f);
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                    Caja(t, "Patio_Losa", r.xMin + i * 2.5f + (r.width - nx * 2.5f) * 0.5f, r.xMin + (i + 1) * 2.5f + (r.width - nx * 2.5f) * 0.5f, 0.12f, 0.15f, r.yMin + j * 2.5f + (r.height - nz * 2.5f) * 0.5f, r.yMin + (j + 1) * 2.5f + (r.height - nz * 2.5f) * 0.5f, ((i + j) & 1) == 0 ? MAdoquinA : MAdoquinB, false);
            // Cantero central con arbol: cobertura en el medio del patio, se rodea por los dos lados.
            Caja(t, "Patio_Cantero", cx - 1.4f, cx + 1.4f, 0.15f, 0.95f, cz - 1.4f, cz + 1.4f, MMarmolOsc);
            Caja(t, "Patio_Cantero_Borde", cx - 1.5f, cx + 1.5f, 0.95f, 1.05f, cz - 1.5f, cz + 1.5f, MDorado, false);
            ArmarArbol(t, "Patio_Arbol", cx, cz, 1.1f);
            LuzPuntual(t, "Luz_Patio_Centro", new Vector3(cx, 3.2f, cz), 12f, 1.8f, color);
            // Dos puestos de feria en diagonal (cobertura con toldo) y un muro bajo en L al fondo.
            float dx = r.width * 0.27f, dz = r.height * 0.27f;
            foreach (var (sx, sz) in new[] { (-1, -1), (1, 1) })
            {
                float px = cx + sx * dx, pz = cz + sz * dz;
                if (!Libre(px - 1.5f, px + 1.5f, pz - 0.9f, pz + 0.9f)) continue;
                Caja(t, "Patio_Puesto", px - 1.4f, px + 1.4f, 0.15f, 1.1f, pz - 0.7f, pz + 0.7f, MMueble);
                Caja(t, "Patio_Toldo", px - 1.7f, px + 1.7f, 2.35f, 2.5f, pz - 1.2f, pz + 1.0f, MChapa(k + sx + 2), false);
                for (int i = 0; i < 5; i++) Esfera(t, "Patio_Bombilla", new Vector3(px - 1.4f + i * 0.7f, 2.25f, pz - 1.15f), 0.16f, MBombilla);
            }
            foreach (var (sx, sz) in new[] { (1, -1), (-1, 1) })
            {
                float px = cx + sx * dx, pz = cz + sz * dz;
                if (!Libre(px - 1.8f, px + 1.8f, pz - 0.5f, pz + 0.5f)) continue;
                Caja(t, "Patio_Muro", px - 1.7f, px + 1.7f, 0.15f, 1.1f, pz - 0.3f, pz + 0.3f, MHormigon);
                Caja(t, "Patio_Muro_Tapa", px - 1.75f, px + 1.75f, 1.1f, 1.17f, pz - 0.35f, pz + 0.35f, MDorado, false);
            }
            // Bancos y pergola de luces sobre el eje largo; pilar con rotulo de neon visible desde la calle.
            for (int s = -1; s <= 1; s += 2) Banco(t, cx + s * r.width * 0.42f, cz, false);
            Cilindro(t, "Patio_Mastil", cx - r.width * 0.5f + 0.4f, 0.15f, 7f, cz, 0.22f, MBarrote);
            Caja(t, "Patio_Rotulo", cx - r.width * 0.5f + 0.2f, cx - r.width * 0.5f + 0.7f, 5.2f, 6.8f, cz - 1.2f, cz + 1.2f, neon, false);
            LuzPuntual(t, "Luz_Patio_Rotulo", new Vector3(cx - r.width * 0.5f + 1.5f, 5.5f, cz), 9f, 1.5f, color);
            for (float x = r.xMin + 1f; x < r.xMax; x += 1.6f) Esfera(t, "Patio_Guirnalda", new Vector3(x, 4.8f - 0.5f * Mathf.Sin((x - r.xMin) / r.width * Mathf.PI), cz), 0.18f, MBombilla);
            res.Add($"patio {k + 1}: {r.width:0}x{r.height:0} m en ({cx:0},{cz:0})");
        }

        // Pasarela elevada recta sobre un callejon, pegada a la pared (lado = signo en el eje corto).
        static void PasarelaCorta(Transform t, Callejon c, float yP, float rampa, bool ladoSur, int id)
        {
            var color = ColorRuta(c.id);
            if (c.ew)
            {
                float zc = ladoSur ? c.z0 + 0.8f : c.z1 - 0.8f, xa = c.x0 + 1f, xb = c.x1 - 1f;
                Escalera(t, "Pasarela2_Subida", new Vector3(xa, 0.05f, zc), new Vector3(xa + rampa, yP, zc), 1.4f);
                Elevado(t, "Pasarela2_Tramo", xa + rampa, xb - rampa, yP - 0.2f, yP, zc - 0.7f, zc + 0.7f, MAndamio);
                Escalera(t, "Pasarela2_Bajada", new Vector3(xb - rampa, yP, zc), new Vector3(xb, 0.05f, zc), 1.4f);
                Caja(t, "Pasarela2_Baranda", xa + rampa, xb - rampa, yP, yP + 1f, ladoSur ? zc + 0.66f : zc - 0.7f, ladoSur ? zc + 0.7f : zc - 0.66f, MBarrote, false);
                LuzPuntual(t, "Luz_Pasarela2", new Vector3((xa + xb) * 0.5f, yP + 1.2f, zc), 8f, 1.3f, color);
            }
            else
            {
                float xc = ladoSur ? c.x0 + 0.8f : c.x1 - 0.8f, za = c.z0 + 1f, zb = c.z1 - 1f;
                Escalera(t, "Pasarela2_Subida", new Vector3(xc, 0.05f, za), new Vector3(xc, yP, za + rampa), 1.4f);
                Elevado(t, "Pasarela2_Tramo", xc - 0.7f, xc + 0.7f, yP - 0.2f, yP, za + rampa, zb - rampa, MAndamio);
                Escalera(t, "Pasarela2_Bajada", new Vector3(xc, yP, zb - rampa), new Vector3(xc, 0.05f, zb), 1.4f);
                Caja(t, "Pasarela2_Baranda", ladoSur ? xc + 0.66f : xc - 0.7f, ladoSur ? xc + 0.7f : xc - 0.66f, yP, yP + 1f, za + rampa, zb - rampa, MBarrote, false);
                LuzPuntual(t, "Luz_Pasarela2", new Vector3(xc, yP + 1.2f, (za + zb) * 0.5f), 8f, 1.3f, color);
            }
        }

        public static string ConstruirCaminos2()
        {
            var res = new StringBuilderLite();
            var esc = SceneManager.GetSceneByPath(RutaEscenaDeSector("Ciudad"));
            if (!esc.IsValid() || !esc.isLoaded) return "La escena Ciudad no esta abierta (aisl.sh Completa).";
            Transform ciudad = null; foreach (var r in esc.GetRootGameObjects()) if (r.name == "Ciudad") ciudad = r.transform;
            if (ciudad == null) return "Sin raiz Ciudad.";
            var viejo = ciudad.Find(GrupoCaminos2); if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var grupo = new GameObject(GrupoCaminos2).transform; grupo.SetParent(ciudad, false);
            AsegurarCarpeta(CarpetaElementos);

            // 51-54: se apagan los cuatro edificios que abren los patios.
            var abiertos = EdificiosAbiertos(); int quitados = 0;
            foreach (var tr in ciudad.GetComponentsInChildren<Transform>(true))
                foreach (var (num, _) in abiertos)
                    if (tr.name == "P_Op_Ciudad_Edificio_" + num) { if (tr.gameObject.activeSelf) { tr.gameObject.SetActive(false); quitados++; } }
            res.Add($"51-54 edificios quitados para abrir patios: {quitados} (de {abiertos.Length})");

            void Instanciar(GameObject escenaria, string nombrePrefab)
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(escenaria, CarpetaElementos + "/" + nombrePrefab + ".prefab");
                Object.DestroyImmediate(escenaria);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, esc);
                inst.transform.SetParent(grupo, false); inst.transform.position = Vector3.zero;
            }

            // 55-78: los cuatro patios (losas, cantero con arbol, puestos, muros, bancos, rotulo, guirnalda y luces: seis pasadas por patio).
            var p = new GameObject("P_Op_Cam2_Patios"); int k = 0;
            foreach (var (num, huella) in abiertos) Patio(p.transform, huella, k++, res);
            Instanciar(p, "P_Op_Cam2_Patios");

            // 79-90: pasarelas cortas (callejon norte, lado sur) y largas en los callejones verticales del sur.
            var q = new GameObject("P_Op_Cam2_Pasarelas"); int pas = 0;
            foreach (var c in Callejones())
            {
                float largo = c.ew ? c.x1 - c.x0 : c.z1 - c.z0;
                bool norte = (c.x0 + c.x1) * 0.5f < 322f ? c.z0 > 236f : c.z0 > 236f;
                if (c.ew && largo < 29f && c.z0 < 258f && c.z0 > 236f) { PasarelaCorta(q.transform, c, 3.4f, 6.5f, true, c.id); pas++; }
                else if (!c.ew && largo > 40f) { PasarelaCorta(q.transform, c, 4.4f, 10.5f, (c.x0 + c.x1) * 0.5f > 322f, c.id); pas++; }
            }
            Instanciar(q, "P_Op_Cam2_Pasarelas");
            res.Add($"79-90 pasarelas nuevas: {pas}");

            // 91-100: soportales (galeria cubierta con columnas) sobre las fachadas de las manzanas del sur que dan a la avenida.
            var s = new GameObject("P_Op_Cam2_Soportales"); int col = 0;
            foreach (bool oeste in new[] { true, false })
            {
                float xc = oeste ? 303.2f : 340.8f, xr0 = oeste ? 302.2f : 338.8f, xr1 = oeste ? 305.2f : 341.8f;
                Caja(s.transform, "Soportal_Techo", xr0, xr1, 3.8f, 3.95f, 174f, 218f, MMarmolOsc, false);
                Caja(s.transform, "Soportal_Cornisa", xr0, xr1, 3.95f, 4.1f, 174f, 218f, MDorado, false);
                for (float z = 175f; z <= 217f; z += 6f)
                {
                    if (!Libre(xc - 0.3f, xc + 0.3f, z - 0.3f, z + 0.3f)) continue;
                    Caja(s.transform, "Soportal_Columna", xc - 0.25f, xc + 0.25f, 0.12f, 3.8f, z - 0.25f, z + 0.25f, MMarmol); col++;
                    if (((int)z / 6) % 2 == 0) LuzPuntual(s.transform, "Luz_Soportal", new Vector3(xc + (oeste ? 1f : -1f), 3.4f, z + 3f), 7f, 1.3f, new Color(1f, 0.85f, 0.55f));
                }
            }
            Instanciar(s, "P_Op_Cam2_Soportales");
            res.Add($"91-100 soportales: {col} columnas en las dos fachadas de la avenida");
            EditorSceneManager.MarkSceneDirty(esc);
            AssetDatabase.SaveAssets();
            return res.ToString();
        }
    }
}
