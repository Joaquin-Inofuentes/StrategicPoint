using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Mision;
using SP.Operacion;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Zona 4: patio del tanque, autopista, plantilla de la camioneta enemiga y helicoptero (OperacionDirector.Huida/Extraer).
    public static partial class OperacionBuilder
    {
        // ---------------------------------------------------------------
        // 4a. Patio del tanque (salida del centro de datos)
        // ---------------------------------------------------------------
        // WP9b: resultados extra del patio (el Construir los pasa al director).
        static Soldier[] tripulacionJefe;
        static OleadaDeReserva[] oleadasReparacion;
        static Transform[] patrullaJefe, cajasJefe;

        static Vehicle PatioDelTanque(Transform padre, Transform vehiculos, out OleadaDeReserva oleada)
        {
            var g = Grupo(padre, "4_PatioDelTanque");
            Pintura(g, "Calle_Salida", 0f, -15f, 12f, 50f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Patio_Tanque", 0f, 22f, 52f, 46f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Plaza_Cemento", 0f, 22f, 30f, 30f, CementoOsc, 0f, 0.08f, 0.04f);
            for (int k = 0; k < 6; k++) Pintura(g, "Linea_Salida_" + k, 0f, -36f + k * 8f, 0.3f, 3.5f, Amarillo);
            for (int k = 0; k < 4; k++) FlechaPiso(g, 0f, -30f + k * 12f, 0f);
            // Cocheras laterales y barricadas (cobertura para el combate de salida).
            Edificio(g, "Cochera_O", -30f, 24f, 14f, 5f, 28f, Oliva);
            Edificio(g, "Cochera_E", 30f, 24f, 14f, 5f, 28f, Oliva);
            Decor(g, "Techo_Cochera_O", -30f, 24f, 14.6f, 0.5f, 28.6f, Techo, 0f, 5f);
            Decor(g, "Techo_Cochera_E", 30f, 24f, 14.6f, 0.5f, 28.6f, Techo, 0f, 5f);
            Saco(g, "Saco_Salida_1", -10f, -12f); Saco(g, "Saco_Salida_2", 10f, -4f);   // bug #070: corridos (la camioneta de asalto quedaba dentro del primero) Saco(g, "Saco_Patio_1", -12f, 8f, false); Saco(g, "Saco_Patio_2", 12f, 10f, false);
            Cubo(g, "Camion_Patio", -16f, 40f, 3f, 2.8f, 7f, Oliva, 0f, 0f, true, 700);
            Cubo(g, "Caja_Patio", 15f, 38f, 3f, 2f, 3f, Cobertura, 0f, 0f, true, 400);
            Rotulo(g, "TANQUE\nPUNTO DE FUGA ^", 0f, 5.2f, 3f, 0f, 1.5f, Color.white, VerdeSenal, 12f, 3f);
            Solido(g, "Poste_Tanque_O", -6.2f, 3f, 0.4f, 5f, 0.4f, CementoOsc);
            Solido(g, "Poste_Tanque_E", 6.2f, 3f, 0.4f, 5f, 0.4f, CementoOsc);
            Farola(g, -8f, -20f, true); Farola(g, 8f, -20f, true); Farola(g, -14f, 14f, true); Farola(g, 14f, 14f, true);
            LuzPuntual(g, "Luz_Tanque", 0f, 7f, 22f, new Color(1f, 0.95f, 0.85f), 3.5f, 28f);

            // Tanque amigo: maneja un aliado (autopiloto por la carretera) y el jugador va de artillero.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTanque);
            var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, vehiculos);
            t.name = "Tanque_Fuga";
            t.transform.SetPositionAndRotation(new Vector3(0f, 0.6f, 22f), Quaternion.identity);
            var v = t.GetComponent<Vehicle>();
            var so = new SerializedObject(v);
            so.FindProperty("maxHealth").intValue = TanqueJefe.VidaMaxima;   // WP9b (#097): el BLINDADO GOLIAT aguanta 1600
            so.ApplyModifiedPropertiesWithoutUndo();
            // Pedido: "quiero que uses el tanque que tenes, el que tiene torreta y orugas" (el mismo Vehiculo_Blindado de
            // SC_Gameplay). Antes aca se apagaban los VisualMundo y se prendian los cubos (blockout); ahora queda el arte del
            // prefab tal cual: cuerpo con orugas, torreta, cañon y metralleta reales (SM_Veh_Tanque_*, trimsheet).
            PrefabUtility.RecordPrefabInstancePropertyModifications(v);
            EditorUtility.SetDirty(v);

            // Los que "abren el paso" hasta el tanque: un grupo que sale del patio de la fabrica en cuanto termina la descarga.
            var res = Grupo(rReservas, "Huida");
            var lista = new List<Soldier>();
            for (int i = 0; i < 6; i++) lista.Add(Enemigo("Reserva_Huida_" + (i + 1), -22f + i * 9f, 52f + (i % 2) * 6f, 180f, res, false, 3f, true));
            var destino = new GameObject("Destino_Huida").transform;
            destino.SetParent(g, false);
            destino.position = new Vector3(0f, 0f, 24f);
            oleada = new OleadaDeReserva { segundo = 0f, aviso = "REFUERZOS ENEMIGOS EN EL PATIO", soldados = lista.ToArray(), destino = destino };

            // WP9b (#097): tripulacion del BLINDADO (2, suben al tanque en runtime), oleadas de la reparacion (5 del este, 6 del oeste, inactivas),
            // puntos de patrulla del jefe y cajas de suministros del patio (se crean al empezar la pelea: las cajas son runtime).
            var resJ = Grupo(rReservas, "Jefe");
            tripulacionJefe = new[]
            {
                Enemigo("Tripulante_Blindado_1", 0f, 18f, 0f, resJ, false, 3f, true),
                Enemigo("Tripulante_Blindado_2", 1.5f, 18f, 0f, resJ, false, 3f, true),
            };
            var este = new List<Soldier>(); var oeste = new List<Soldier>();
            for (int i = 0; i < 5; i++) este.Add(Enemigo("Reserva_Reparacion_E" + (i + 1), 44f + (i % 2) * 3f, 12f + i * 5f, -90f, resJ, false, 3f, true));
            for (int i = 0; i < 6; i++) oeste.Add(Enemigo("Reserva_Reparacion_O" + (i + 1), -44f - (i % 2) * 3f, 10f + i * 5f, 90f, resJ, false, 3f, true));
            oleadasReparacion = new[]
            {
                new OleadaDeReserva { segundo = 9f, aviso = "¡REFUERZOS POR EL ESTE!", soldados = este.ToArray(), destino = destino },
                new OleadaDeReserva { segundo = 18f, aviso = "¡REFUERZOS POR EL OESTE Y UNA CAMIONETA!", soldados = oeste.ToArray(), destino = destino },
            };
            var gj = Grupo(g, "Jefe");
            Vector3[] patr = { new Vector3(-14f, 0f, 12f), new Vector3(14f, 0f, 12f), new Vector3(14f, 0f, 30f), new Vector3(-14f, 0f, 30f) };
            patrullaJefe = new Transform[patr.Length];
            for (int i = 0; i < patr.Length; i++)
            {
                var tp = new GameObject("Patrulla_Jefe_" + (i + 1)).transform; tp.SetParent(gj, false); tp.position = patr[i]; patrullaJefe[i] = tp;
            }
            Vector3[] cj = { new Vector3(-21f, 0f, 3f), new Vector3(21f, 0f, 3f) };
            cajasJefe = new Transform[cj.Length];
            for (int i = 0; i < cj.Length; i++)
            {
                var tc = new GameObject("Caja_Cohetes_" + (i + 1)).transform; tc.SetParent(gj, false); tc.position = cj[i]; cajasJefe[i] = tc;
            }
            return v;
        }

        // ---------------------------------------------------------------
        // 4b. Autopista (ruta del tanque)
        // ---------------------------------------------------------------
        static Transform[] Autopista(Transform padre)
        {
            var g = Grupo(padre, "5_Autopista");
            // Ruta del doble de largo (~870 m): recta inicial, gran curva a la izquierda, bajada en "S" por el norte y entrada a la
            // ciudad por el oeste. A ~17 m/s son ~52 s de trayecto frenetico.
            // WP9b-2 (#073/#097): 18 puntos, ~950 m, para ~70 s a ~14 m/s: recta de salida, CANADON en "S" (muros de roca de 10 m, 4 barricadas,
            // 2 cornisas con tiradores de cohetes), curva al oeste, PUENTE (-130,342)..(10,345) con trafico abandonado y recta final a la ciudad.
            Vector2[] pts =
            {
                new Vector2(0f, 40f), new Vector2(0f, 105f), new Vector2(24f, 140f), new Vector2(-18f, 172f), new Vector2(22f, 205f),
                new Vector2(-30f, 232f), new Vector2(-95f, 244f), new Vector2(-160f, 262f), new Vector2(-170f, 322f), new Vector2(-130f, 342f),
                new Vector2(-55f, 349f), new Vector2(10f, 345f), new Vector2(100f, 332f), new Vector2(150f, 318f), new Vector2(190f, 298f),
                new Vector2(222f, 268f), new Vector2(238f, 228f), new Vector2(262f, 228f),
            };
            float longitudTotal = 0f;
            for (int q = 0; q < pts.Length - 1; q++) longitudTotal += (pts[q + 1] - pts[q]).magnitude;
            longitudTotal += 18f;   // del tanque (0,22) al primer punto
            var rutaRaiz = Grupo(g, "RutaTanque");
            var ruta = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var t = new GameObject("Ruta_" + (i + 1)).transform;
                t.SetParent(rutaRaiz, false);
                t.position = new Vector3(pts[i].x, 0.6f, pts[i].y);
                ruta[i] = t;
            }
            float acumulado = 0f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var d = b - a; float largo = d.magnitude;
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                var mid = (a + b) * 0.5f;
                Pintura(g, "Asfalto_" + i, mid.x, mid.y, 14f, largo + 1.5f, Asfalto, yaw, 0f, 0.08f);
                // Banquinas blancas y linea central discontinua.
                var lado = new Vector2(d.y, -d.x).normalized;
                Pintura(g, "Borde_I_" + i, mid.x + lado.x * 6.6f, mid.y + lado.y * 6.6f, 0.3f, largo, Blanco, yaw, 0.02f, 0.1f);
                Pintura(g, "Borde_D_" + i, mid.x - lado.x * 6.6f, mid.y - lado.y * 6.6f, 0.3f, largo, Blanco, yaw, 0.02f, 0.1f);
                var dn = d.normalized;
                for (float s = 2f; s < largo - 2f; s += 9f)
                {
                    var p = a + dn * s;
                    Pintura(g, "Linea", p.x, p.y, 0.3f, 4f, Amarillo, yaw, 0.02f, 0.1f);
                }
                // Farolas alternadas, defensa de cemento (con huecos) y senales de km.
                for (float s = 10f; s < largo - 4f; s += 30f)
                {
                    var p = a + dn * s;
                    float sign = ((int)((acumulado + s) / 30f) % 2 == 0) ? 1f : -1f;
                    Farola(g, p.x + lado.x * 9.5f * sign, p.y + lado.y * 9.5f * sign, false, 7f, 120);   // bug #090: poste que cae
                    var p2 = a + dn * (s + 15f);
                    if (s + 15f < largo - 3f) FlechaPiso(g, p2.x, p2.y, yaw);
                }
                for (float s = 6f; s < largo - 6f; s += 22f)
                {
                    var p = a + dn * s;
                    Destruible(g, "Jersey_I", p.x + lado.x * 8.4f, p.y + lado.y * 8.4f, 0.7f, 1.0f, 8f, Cemento, 180, yaw);   // bug #090
                    Destruible(g, "Jersey_D", p.x - lado.x * 8.4f, p.y - lado.y * 8.4f, 0.7f, 1.0f, 8f, Cemento, 180, yaw);
                }
                acumulado += largo;
            }
            ObstaculosDeLaRuta(g, pts);
            CanadonPuenteYEmboscadas(g, pts);
            // Cartelera y senales de la autopista.
            Rotulo(g, $"AUTOPISTA ^\nCIUDAD ESTE {Mathf.RoundToInt(longitudTotal / 10f) * 10} m", -17.5f, 4.6f, 62f, 0f, 1.3f, Color.white, VerdeSenal, 12f, 3f);
            Solido(g, "Poste_Autopista_O", -22.5f, 62f, 0.4f, 6f, 0.4f, CementoOsc);
            Solido(g, "Poste_Autopista_E", -12.5f, 62f, 0.4f, 6f, 0.4f, CementoOsc);
            Rotulo(g, "CANADON ^\nCUIDADO: CORNISAS", -17.5f, 3.8f, 92f, 0f, 1.2f, Color.black, Amarillo, 12f, 1.8f);
            Solido(g, "Poste_Canadon_O", -22.5f, 92f, 0.3f, 4f, 0.3f, CementoOsc);
            Solido(g, "Poste_Canadon_E", -12.5f, 92f, 0.3f, 4f, 0.3f, CementoOsc);
            // Chevrons en cada curva cerrada y carteles de km cada ~200 m, a la derecha del sentido de la marcha.
            float recorrido = 0f, proximoKm = 200f; int km = 1;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var d0 = pts[i + 1] - pts[i]; float l0 = d0.magnitude; var n0 = d0.normalized;
                var der = new Vector2(n0.y, -n0.x);
                if (i >= 1 && i < pts.Length - 2)
                {
                    var d1 = (pts[i + 2] - pts[i + 1]).normalized;
                    if (Vector2.Angle(n0, d1) > 25f)
                    {
                        var esq = pts[i + 1] + (n0 - d1).normalized * 10.5f;   // por fuera de la curva
                        Destruible(g, "Chevron_" + i, esq.x, esq.y, 0.4f, 2.4f, 2.4f, Naranja, 80);
                    }
                }
                while (proximoKm >= recorrido && proximoKm < recorrido + l0 - 6f)
                {
                    var pk = pts[i] + n0 * (proximoKm - recorrido) + der * 11f;
                    float yawK = Mathf.Atan2(-n0.x, n0.y) * Mathf.Rad2Deg + 180f;
                    Rotulo(g, "KM " + km, pk.x, 2.6f, pk.y, yawK, 1.2f, Color.white, AzulSenal, 4f, 1.5f);
                    Destruible(g, "Poste_Km" + km, pk.x, pk.y, 0.3f, 2.6f, 0.3f, CementoOsc, 60);
                    km++; proximoKm += 200f;
                }
                recorrido += l0;
            }
            Rotulo(g, "CIUDAD >>\nEXTRACCION EN LA PLAZA", 232f, 4.8f, 206f, 90f, 1.3f, Color.white, VerdeSenal, 10f, 3f);
            Solido(g, "Poste_Ciudad_O", 232f, 201.4f, 0.4f, 6f, 0.4f, CementoOsc);
            Solido(g, "Poste_Ciudad_E", 232f, 210.6f, 0.4f, 6f, 0.4f, CementoOsc);

            // Escenario a los costados: depositos, graneros y rocas, siempre a mas de 24 m del eje de la ruta (para no cerrar el paso).
            var esc = Grupo(g, "Escenario");
            System.Func<float, float, float> distRuta = (px, pz) =>
            {
                float mejor = 1e9f;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    var a2 = pts[i]; var b2 = pts[i + 1]; var ab = b2 - a2;
                    float t = Mathf.Clamp01(Vector2.Dot(new Vector2(px, pz) - a2, ab) / ab.sqrMagnitude);
                    mejor = Mathf.Min(mejor, (new Vector2(px, pz) - (a2 + ab * t)).magnitude);
                }
                return mejor;
            };
            float[][] edificios =
            {
                new[]{-40f,90f,24f,6f,16f,0}, new[]{44f,140f,20f,6f,14f,0}, new[]{-50f,165f,18f,7f,14f,1}, new[]{70f,200f,20f,6f,12f,2},
                new[]{-110f,190f,22f,6f,14f,0}, new[]{-60f,275f,20f,6f,12f,1}, new[]{-215f,290f,24f,7f,14f,0}, new[]{-125f,300f,18f,6f,12f,2},
                new[]{-100f,395f - 30f,22f,6f,12f,1}, new[]{40f,300f,20f,6f,12f,0}, new[]{110f,372f - 30f,20f,6f,10f,2}, new[]{150f,270f,18f,5f,12f,1},
                new[]{205f,330f,16f,5f,10f,0}, new[]{250f,300f,14f,5f,10f,2}, new[]{160f,215f,16f,5f,10f,1},
            };
            var matsE = new[] { Oliva, Casa, CasaC };
            int ne = 0;
            foreach (var e in edificios)
            {
                if (distRuta(e[0], e[1]) < 24f + Mathf.Max(e[2], 8f) * 0.5f) continue;
                Edificio(esc, "Edificio_" + (ne++), e[0], e[1], e[2], e[3], e[4], matsE[(int)e[5]]);
            }
            Edificio(esc, "Silo_1", 190f, 170f, 10f, 12f, 10f, CementoOsc);
            Edificio(esc, "Silo_2", 205f, 170f, 10f, 12f, 10f, CementoOsc);
            int rocas = 0;
            for (int i = 0; rocas < 18 && i < 200; i++)
            {
                float rx = -200f + ((i * 97) % 420), rz = 40f + ((i * 53) % 330);
                if (distRuta(rx, rz) < 14f || distRuta(rx, rz) > 60f) continue;
                Cubo(esc, "Roca_" + rocas, rx, rz, 4f, 2.5f, 4f, CementoOsc, i * 20f, 0f, true, Indestructible);
                rocas++;
            }
            return ruta;
        }

        // Punto y direccion de la ruta a "dist" metros del inicio.
        static Vector2 PuntoDeLaRuta(Vector2[] pts, float dist, out Vector2 dir)
        {
            float acum = 0f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float l = (pts[i + 1] - pts[i]).magnitude;
                if (dist <= acum + l || i == pts.Length - 2)
                {
                    dir = (pts[i + 1] - pts[i]).normalized;
                    return pts[i] + dir * Mathf.Clamp(dist - acum, 0f, l);
                }
                acum += l;
            }
            dir = Vector2.up;
            return pts[0];
        }

        // Bug #090: "el tanque tiene que destruir obstaculos". Sobre la calzada: barreras de obra de punta a punta, grupos de barriles
        // explosivos y cajas. Todos con vida (el tanque los aplasta con Atropello; el canon los revienta) y fuera del NavMesh.
        public const int BarrerasDeRuta = 14, GruposDeBarriles = 5, BarrilesPorGrupo = 4, CajasDeRuta = 10;

        static void ObstaculosDeLaRuta(Transform g, Vector2[] pts)
        {
            var rr = Grupo(g, "ObstaculosDeLaRuta");
            // Barreras de obra: cruzan la calzada (8 m de ancho sobre 14), una cada ~52 m desde el primer tramo.
            for (int k = 0; k < BarrerasDeRuta; k++)
            {
                var p = PuntoDeLaRuta(pts, 62f + k * 52f, out var d);
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                var lado = new Vector2(d.y, -d.x);
                float jitter = ((k * 7) % 5 - 2) * 0.6f;
                p += lado * jitter;
                SobreLaCalzada(Destruible(rr, "Jersey_Ruta_" + k, p.x, p.y, 8f, 1.0f, 0.7f, Cemento, 180, yaw));
            }
            // Barriles en grupos de 4 (2 x 2): explotan al ser tocados por fuego (radio 3).
            float[] distBarriles = { 96f, 214f, 338f, 466f, 606f };
            float[] latBarriles = { 2.6f, -3.2f, 1.2f, -2.0f, 3.6f };
            for (int gI = 0; gI < GruposDeBarriles; gI++)
            {
                var c = PuntoDeLaRuta(pts, distBarriles[gI], out var d);
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                var lado = new Vector2(d.y, -d.x);
                for (int b = 0; b < BarrilesPorGrupo; b++)
                {
                    var pos = c + lado * (latBarriles[gI] + (b % 2) * 0.95f) + d * ((b / 2) * 0.95f);
                    var go = Destruible(rr, "Barril_" + gI + "_" + b, pos.x, pos.y, 0.7f, 1.0f, 0.7f, Rojo, 40, yaw);
                    go.GetComponent<ObstacleMarker>().ConfigurarExplosivo(3f, 45);
                    SobreLaCalzada(go);
                }
            }
            // Cajas en el centro de la calzada.
            for (int k = 0; k < CajasDeRuta; k++)
            {
                var p = PuntoDeLaRuta(pts, 145f + k * 71f, out var d);
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                var lado = new Vector2(d.y, -d.x);
                p += lado * (((k * 3) % 5) - 2) * 0.55f;
                SobreLaCalzada(Destruible(rr, "Caja_Ruta_" + k, p.x, p.y, 1.4f, 1.2f, 1.4f, Cobertura, 40, yaw));
            }
        }

        // ---------------------------------------------------------------
        // WP9b-2: canadon, barricadas, cornisas con tiradores de cohetes, puente y Halcon
        // ---------------------------------------------------------------
        public const int BarricadasDelCanadon = 4, EmboscadasDelCanadon = 2, TiradoresPorEmboscada = 3, CornisaAltura = 6;
        public const float MuroAltura = 10f;
        static EmboscadaDeCohetes[] emboscadasEscena;
        static Transform halconPlantilla;

        static float DistanciaAPolilinea(Vector2[] pts, Vector2 p)
        {
            float mejor = 1e9f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var ab = pts[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                mejor = Mathf.Min(mejor, (p - (a + ab * t)).magnitude);
            }
            return mejor;
        }

        static float DistanciaAcumulada(Vector2[] pts, int idx)
        {
            float l = 0f;
            for (int i = 0; i < idx; i++) l += (pts[i + 1] - pts[i]).magnitude;
            return l;
        }

        static void CanadonPuenteYEmboscadas(Transform g, Vector2[] pts)
        {
            var rg = Grupo(g, "Canadon");
            float ini = DistanciaAcumulada(pts, 2), fin = DistanciaAcumulada(pts, 7);
            var roca = Mat("RocaCanadon", new Color(0.40f, 0.35f, 0.30f));
            var rocaOsc = Mat("RocaCanadonOsc", new Color(0.30f, 0.27f, 0.24f));
            // Muros de roca de 10 m a los dos lados: bloques de ~5 m cada 5,5 m, solo donde no pisan la calzada (distancia al eje >= 10 m).
            int n = 0;
            for (float d = ini; d <= fin; d += 5.5f)
            {
                var c = PuntoDeLaRuta(pts, d, out var dir);
                var lado = new Vector2(dir.y, -dir.x);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    float lat = 11.8f + ((n * 7) % 3) * 0.6f;
                    var p = c + lado * (sg * lat);
                    if (DistanciaAPolilinea(pts, p) < 10.2f) continue;
                    float w = 4.6f + ((n * 5) % 3) * 0.6f, h = MuroAltura - 1f + ((n * 3) % 3) * 1f;
                    Cubo(rg, "Roca_Canadon_" + n, p.x, p.y, w, h, 5f, (n & 1) == 0 ? roca : rocaOsc, yaw + ((n * 11) % 7 - 3) * 4f, 0f, true, Indestructible);
                    n++;
                }
            }
            // Barricadas sobre la calzada (4): madera y cemento alternadas; el tanque las aplasta.
            float[] distBar = { 150f, 215f, 290f, 355f };
            for (int k = 0; k < BarricadasDelCanadon; k++)
            {
                var p = PuntoDeLaRuta(pts, distBar[k], out var dir);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                if ((k & 1) == 0) SobreLaCalzada(Destruible(rg, "Barricada_Madera_" + k, p.x, p.y, 8.5f, 1.6f, 1.3f, Cobertura, 300, yaw + 90f));
                else SobreLaCalzada(Destruible(rg, "Barricada_Cemento_" + k, p.x, p.y, 8.5f, 1.4f, 1.4f, Cemento, 400, yaw + 90f));
            }
            // Cornisas de 6 m con 3 tiradores de cohetes cada una (a la izquierda y a la derecha de la marcha).
            float[] distEmb = { 188f, 332f };
            emboscadasEscena = new EmboscadaDeCohetes[EmboscadasDelCanadon];
            var resE = Grupo(rReservas, "Emboscadas");
            for (int k = 0; k < EmboscadasDelCanadon; k++)
            {
                var p = PuntoDeLaRuta(pts, distEmb[k], out var dir);
                var lado = new Vector2(dir.y, -dir.x);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                int sg = (k & 1) == 0 ? -1 : 1;
                var cc = p + lado * (sg * 10.2f);
                Cubo(rg, "Cornisa_" + (k + 1), cc.x, cc.y, 3.2f, CornisaAltura, 9f, rocaOsc, yaw, 0f, true, Indestructible);
                var gE = Grupo(resE, "Emboscada_" + (k + 1));
                var tir = new Soldier[TiradoresPorEmboscada];
                for (int t = 0; t < TiradoresPorEmboscada; t++)
                {
                    var q = cc + dir * ((t - 1) * 2.6f);
                    tir[t] = Enemigo($"Tirador_Cohetes_{k + 1}_{t + 1}", q.x, q.y, yaw + 90f * -sg, gE, false, 3f, true);
                    tir[t].transform.position = new Vector3(q.x, CornisaAltura + 0.85f, q.y);
                }
                var emb = gE.gameObject.AddComponent<EmboscadaDeCohetes>();
                emb.tiradores = tir;
                emboscadasEscena[k] = emb;
            }
            // Puente: del punto 9 al 11 (cruza el rio). Rio a los lados, barandas solo visuales, 4 autos abandonados destructibles.
            float pIni = DistanciaAcumulada(pts, 9), pFin = DistanciaAcumulada(pts, 11);
            var rio = Mat("Rio", new Color(0.09f, 0.20f, 0.28f), 0.15f);
            for (float d = pIni; d < pFin; d += 18f)
            {
                var c = PuntoDeLaRuta(pts, d + 9f, out var dir);
                var lado = new Vector2(dir.y, -dir.x);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                foreach (int sg in new[] { -1, 1 })
                {
                    var pr = c + lado * (sg * 19f);
                    Decor(rg, "Rio", pr.x, pr.y, 24f, 0.05f, 20f, rio, yaw, 0.02f);
                    var pb = c + lado * (sg * 7.4f);
                    Decor(rg, "Baranda", pb.x, pb.y, 0.25f, 1.1f, 18f, Cemento, yaw);
                }
            }
            var pCartel = PuntoDeLaRuta(pts, pIni - 8f, out var dirP);
            pCartel += new Vector2(dirP.y, -dirP.x) * 9.5f;   // a la derecha de la marcha, de cara a quien llega
            Rotulo(g, "PUENTE\nSIN GUARDARRAILES", pCartel.x, 3.4f, pCartel.y, Mathf.Atan2(dirP.x, dirP.y) * Mathf.Rad2Deg, 1.1f, Color.black, Amarillo, 12f, 1.8f);
            float[] distAutos = { pIni + 22f, pIni + 55f, pIni + 90f, pIni + 118f };
            float[] latAutos = { 2.6f, -2.2f, 3.0f, -2.8f };
            var matsAuto = new[] { AutoRojo, Casa, CasaB, Oliva };
            for (int k = 0; k < distAutos.Length; k++)
            {
                var p = PuntoDeLaRuta(pts, distAutos[k], out var dir);
                var lado = new Vector2(dir.y, -dir.x);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                var pc = p + lado * latAutos[k];
                SobreLaCalzada(Destruible(rg, "Auto_Abandonado_" + k, pc.x, pc.y, 2.2f, 1.7f, 4.8f, matsAuto[k], 220, yaw + (k * 13 - 20)));
            }
            // El HALCON: plantilla inactiva en la escena.
            halconPlantilla = CrearHalcon(rReservas);
        }

        // Helicoptero enemigo (plantilla INACTIVA): oscuro, luces rojas, rotores con los nombres RotorPrincipal / RotorCola. Vive en la escena; el director lo clona.
        public const int VidaDelHalcon = 900;
        static Transform CrearHalcon(Transform padre)
        {
            var h = new GameObject("Halcon_Plantilla").transform;
            h.SetParent(padre, false);
            h.position = new Vector3(0f, 60f, 0f);
            var oscuro = Mat("HalconCuerpo", new Color(0.13f, 0.14f, 0.16f));
            var mas = Mat("HalconDetalle", new Color(0.07f, 0.07f, 0.08f));
            var rojo = Mat("HalconLuz", new Color(1f, 0.1f, 0.06f), 2.4f);
            void P(string n, float x, float y, float z, float w, float hh, float d, Material m) { var c = Decor(h, n, 0, 0, w, hh, d, m); c.transform.localPosition = new Vector3(x, y, z); }
            P("Fuselaje", 0f, 1.7f, 0f, 2.4f, 2.0f, 5.4f, oscuro);
            P("Cabina", 0f, 2.0f, 2.9f, 2.0f, 1.4f, 1.8f, Cristal);
            P("Nariz", 0f, 1.5f, 3.9f, 1.4f, 1.0f, 1.0f, mas);
            P("Cola", 0f, 2.3f, -5.0f, 0.8f, 0.9f, 5.6f, oscuro);
            P("Aleta", 0f, 3.1f, -7.6f, 0.18f, 1.9f, 1.2f, mas);
            P("Patin_I", -1.4f, 0.35f, 0.2f, 0.2f, 0.2f, 5.0f, mas);
            P("Patin_D", 1.4f, 0.35f, 0.2f, 0.2f, 0.2f, 5.0f, mas);
            P("Ala_I", -2.1f, 1.6f, 0.4f, 1.8f, 0.2f, 1.2f, mas);
            P("Ala_D", 2.1f, 1.6f, 0.4f, 1.8f, 0.2f, 1.2f, mas);
            P("Cohetera_I", -3.0f, 1.45f, 0.5f, 0.4f, 0.4f, 1.8f, oscuro);
            P("Cohetera_D", 3.0f, 1.45f, 0.5f, 0.4f, 0.4f, 1.8f, oscuro);
            P("Luz_Nariz", 0f, 1.05f, 4.45f, 0.35f, 0.25f, 0.2f, rojo);
            P("Luz_Cola", 0f, 3.6f, -8.2f, 0.25f, 0.25f, 0.25f, rojo);
            P("Luz_Techo", 0f, 2.85f, 0f, 0.3f, 0.2f, 0.3f, rojo);
            var luz = new GameObject("Luz_Roja"); luz.transform.SetParent(h, false); luz.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            var li = luz.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.15f, 0.1f); li.intensity = 3f; li.range = 12f;
            var rp = new GameObject("RotorPrincipal").transform; rp.SetParent(h, false); rp.localPosition = new Vector3(0f, 3.35f, 0f);
            var b1 = Decor(rp, "Pala_1", 0, 0, 8.6f, 0.07f, 0.4f, mas); b1.transform.localPosition = Vector3.zero;
            var b2 = Decor(rp, "Pala_2", 0, 0, 0.4f, 0.07f, 8.6f, mas); b2.transform.localPosition = Vector3.zero;
            var rc = new GameObject("RotorCola").transform; rc.SetParent(h, false); rc.localPosition = new Vector3(0.3f, 3.1f, -7.6f);
            var c1 = Decor(rc, "Pala_C1", 0, 0, 0.08f, 2.0f, 0.3f, mas); c1.transform.localPosition = Vector3.zero;
            var col = h.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0f, 1.8f, -1.0f); col.size = new Vector3(3.2f, 3.0f, 10.5f);
            var rb = h.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var v = h.gameObject.AddComponent<Vehicle>();
            var so = new SerializedObject(v);
            so.FindProperty("maxHealth").intValue = VidaDelHalcon;
            so.ApplyModifiedPropertiesWithoutUndo();
            h.gameObject.AddComponent<HelicopteroEnemigo>();
            h.gameObject.SetActive(false);
            return h;
        }

        // ---------------------------------------------------------------
        // Auto perseguidor (plantilla inactiva; el director la clona)
        // ---------------------------------------------------------------
        public const int VidaDeCamioneta = 260;
        static GameObject CrearPlantillaDeAuto()
        {
            var raizA = new GameObject("AutoPlantilla");
            raizA.transform.SetParent(rReservas, false);
            raizA.transform.position = new Vector3(0f, 0.6f, 0f);
            var col = raizA.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, -0.2f); col.size = new Vector3(2.3f, 2.2f, 5.6f);
            var rb = raizA.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var vis = Grupo(raizA.transform, "Visual");
            // CAMIONETA ("technical"): los enemigos manejan camionetas, vos manejas el tanque. Capot + cabina adelante (el frente es +Z),
            // caja abierta atras con barral y la metralleta montada sobre la caja.
            var vidrio = Mat("AutoVidrio", new Color(0.10f, 0.13f, 0.17f));
            var metal = Mat("AutoCaja", new Color(0.20f, 0.18f, 0.16f));
            Decor(vis, "Chasis", 0, 0, 2.1f, 0.45f, 5.2f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 0.62f, 0f);
            Decor(vis, "Capot", 0, 0, 2.0f, 0.55f, 1.5f, AutoRojo, 0f, 0f).transform.localPosition = new Vector3(0f, 1.1f, 1.75f);
            Decor(vis, "Parrilla", 0, 0, 1.7f, 0.4f, 0.12f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 1.05f, 2.55f);
            Decor(vis, "Cabina", 0, 0, 2.0f, 0.85f, 1.7f, AutoRojo, 0f, 0f).transform.localPosition = new Vector3(0f, 1.62f, 0.3f);
            Decor(vis, "Parabrisas", 0, 0, 1.8f, 0.5f, 0.1f, vidrio, 0f, 0f).transform.localPosition = new Vector3(0f, 1.75f, 1.16f);
            Decor(vis, "Techo", 0, 0, 2.05f, 0.1f, 1.8f, AutoRojo, 0f, 0f).transform.localPosition = new Vector3(0f, 2.1f, 0.3f);
            Decor(vis, "Caja_Piso", 0, 0, 2.0f, 0.12f, 2.5f, metal, 0f, 0f).transform.localPosition = new Vector3(0f, 0.95f, -1.7f);
            Decor(vis, "Caja_I", 0, 0, 0.1f, 0.55f, 2.5f, metal, 0f, 0f).transform.localPosition = new Vector3(-0.95f, 1.25f, -1.7f);
            Decor(vis, "Caja_D", 0, 0, 0.1f, 0.55f, 2.5f, metal, 0f, 0f).transform.localPosition = new Vector3(0.95f, 1.25f, -1.7f);
            Decor(vis, "Caja_Fondo", 0, 0, 2.0f, 0.55f, 0.1f, metal, 0f, 0f).transform.localPosition = new Vector3(0f, 1.25f, -2.9f);
            Decor(vis, "Barral_I", 0, 0, 0.1f, 1.1f, 0.1f, Negro, 0f, 0f).transform.localPosition = new Vector3(-0.8f, 1.75f, -0.75f);
            Decor(vis, "Barral_D", 0, 0, 0.1f, 1.1f, 0.1f, Negro, 0f, 0f).transform.localPosition = new Vector3(0.8f, 1.75f, -0.75f);
            Decor(vis, "Barral_Tope", 0, 0, 1.7f, 0.1f, 0.1f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 2.3f, -0.75f);
            foreach (var sx in new[] { -1.1f, 1.1f })
                foreach (var sz in new[] { -1.8f, 1.6f })
                    Decor(vis, "Rueda", 0, 0, 0.4f, 0.98f, 0.98f, Negro, 0f, 0f).transform.localPosition = new Vector3(sx, 0.49f, sz);
            Decor(vis, "Sirena", 0, 0, 1.0f, 0.16f, 0.28f, Mat("AutoSirena", new Color(1f, 0.1f, 0.05f), 2f), 0f, 0f).transform.localPosition = new Vector3(0f, 2.22f, 0.5f);
            // Metralleta del techo: pivote que gira hacia el tanque.
            var pivote = new GameObject("MetralletaPivote").transform;
            pivote.SetParent(raizA.transform, false);
            pivote.localPosition = new Vector3(0f, 2.2f, -1.4f);
            Decor(pivote, "Soporte", 0, 0, 0.5f, 0.4f, 0.5f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, -0.1f, 0f);
            Decor(pivote, "Canon", 0, 0, 0.14f, 0.14f, 1.4f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 0.1f, 0.8f);
            var boca = new GameObject("Boca").transform;
            boca.SetParent(pivote, false);
            boca.localPosition = new Vector3(0f, 0.1f, 1.55f);

            var v = raizA.AddComponent<Vehicle>();
            var so = new SerializedObject(v);
            so.FindProperty("maxHealth").intValue = VidaDeCamioneta;   // bug #073: eran 110 (moria de un obus); ahora 2 obuses
            so.ApplyModifiedPropertiesWithoutUndo();
            var a = raizA.AddComponent<OperacionAuto>();
            a.metralleta = pivote;
            a.boca = boca;
            raizA.SetActive(false);
            return raizA;
        }

        // ---------------------------------------------------------------
        // Helicoptero (cubos): llega volando, no esta estacionado. Rotores con los nombres que espera Helicoptero.cs.
        // ---------------------------------------------------------------
        static Transform CrearHelicoptero(Transform padre, out Transform inicio)
        {
            var h = new GameObject("Helicoptero_Extraccion").transform;
            h.SetParent(padre, false);
            h.position = new Vector3(330f, 70f, 380f);
            var verde = Mat("HeliCuerpo", new Color(0.26f, 0.32f, 0.22f));
            var verdeOsc = Mat("HeliOscuro", new Color(0.16f, 0.2f, 0.14f));
            void P(string n, float x, float y, float z, float w, float hh, float d, Material m) { var c = Decor(h, n, 0, 0, w, hh, d, m); c.transform.localPosition = new Vector3(x, y, z); }
            // Bug #076: fuselaje abierto a los lados (antes un cubo macizo): piso, techo, mamparo, umbrales, pilares y cabina con huecos
            // laterales de 2,4 x 1,4 m (z -2,2..0,2; y 1,62..3,02) por donde se ven los artilleros y los sentados. El piso de la
            // cabina esta en y = 1,12 (TripulacionDelHeli.PisoDeCabina). Proa = +Z, derecha = +X.
            P("Piso", 0f, 1.05f, -1.15f, 2.4f, 0.14f, 3.9f, verdeOsc);
            P("Techo", 0f, 3.15f, -1.0f, 2.4f, 0.16f, 4.1f, verde);
            P("Mamparo_Trasero", 0f, 2.1f, -3.05f, 2.4f, 1.9f, 0.15f, verde);
            foreach (var sx in new[] { -1f, 1f })
            {
                string l = sx < 0f ? "I" : "D";
                P("Umbral_" + l, sx * 1.15f, 1.37f, -1.1f, 0.1f, 0.5f, 3.8f, verde);
                P("Pilar_Tras_" + l, sx * 1.15f, 2.32f, -2.6f, 0.1f, 1.4f, 0.8f, verde);
                P("Pilar_Del_" + l, sx * 1.15f, 2.32f, 0.5f, 0.1f, 1.4f, 0.6f, verde);
            }
            P("Cabina_Base", 0f, 1.75f, 1.8f, 2.2f, 1.25f, 2.0f, verde);
            P("Cabina_Cristal", 0f, 2.95f, 1.8f, 2.2f, 1.15f, 2.0f, Cristal);
            P("Nariz", 0f, 1.85f, 3.2f, 1.9f, 1.45f, 1.6f, verdeOsc);
            P("Cola", 0f, 2.6f, -5.3f, 0.8f, 0.9f, 5.6f, verde);
            P("Aleta", 0f, 3.4f, -7.9f, 0.18f, 2.0f, 1.2f, verdeOsc);
            P("Patin_I", -1.4f, 0.35f, 0.2f, 0.2f, 0.2f, 5.2f, Negro);
            P("Patin_D", 1.4f, 0.35f, 0.2f, 0.2f, 0.2f, 5.2f, Negro);
            P("Puntal_I", -1.3f, 0.85f, 0.2f, 0.15f, 1.0f, 0.15f, Negro);
            P("Puntal_D", 1.3f, 0.85f, 0.2f, 0.15f, 1.0f, 0.15f, Negro);
            P("Mastil", 0f, 3.5f, 0f, 0.35f, 0.7f, 0.35f, Negro);
            // Bancos (la parte de arriba del asiento a 0,45 m del piso). Los soldados sentados son copias visuales en runtime.
            var banco = Mat("HeliBanco", new Color(0.12f, 0.13f, 0.12f));
            P("Banco_Trasero", 0f, 1.51f, -2.35f, 1.9f, 0.12f, 0.55f, banco);
            P("Respaldo_Trasero", 0f, 1.95f, -2.68f, 1.9f, 0.75f, 0.08f, banco);
            P("Banco_Medio", 0f, 1.51f, -1.55f, 1.9f, 0.12f, 0.55f, banco);
            P("Banco_Delantero", 0f, 1.51f, -0.8f, 1.5f, 0.12f, 0.55f, banco);
            // Dos ametralladoras de puerta: un pivote por lado que gira hacia el blanco (PivoteMetralleta). El pivote esta en el piso
            // de la cabina: el artillero (copia visual) se para en su origen y el arma queda 0,6 m adelante, a 1,2 m de altura.
            var negroArma = Negro;
            foreach (var sx in new[] { -1f, 1f })
            {
                var pv = new GameObject(sx < 0f ? "PivoteMetralleta_I" : "PivoteMetralleta_D").transform;
                pv.SetParent(h, false);
                pv.localPosition = new Vector3(sx * 0.8f, TripulacionDelHeli.PisoDeCabina, -0.3f);
                var pm = pv.gameObject.AddComponent<PivoteMetralleta>();
                pm.lado = (int)sx;
                void Q(Transform padreQ, string n, Vector3 pos, Vector3 esc)
                {
                    var c = Decor(padreQ, n, 0, 0, esc.x, esc.y, esc.z, negroArma);
                    c.transform.localPosition = pos; c.transform.localRotation = Quaternion.identity;
                }
                Q(pv, "Tripode", new Vector3(0f, 0.55f, 0.6f), new Vector3(0.1f, 1.1f, 0.1f));
                var arma = new GameObject("Arma").transform;
                arma.SetParent(pv, false);
                arma.localPosition = new Vector3(0f, TripulacionDelHeli.PisoDeCabina * 0f + PivoteMetralleta.AlturaDelArma, 0.6f);
                Q(arma, "Cajon", new Vector3(0f, 0f, 0.05f), new Vector3(0.28f, 0.24f, 0.5f));
                Q(arma, "Canon", new Vector3(0f, 0.02f, 0.85f), new Vector3(0.1f, 0.1f, 1.1f));
                Q(arma, "Mira", new Vector3(0f, 0.17f, 0.1f), new Vector3(0.06f, 0.1f, 0.2f));
                var boca = new GameObject("Boca").transform;
                boca.SetParent(arma, false);
                boca.localPosition = new Vector3(0f, 0.02f, 1.45f);
                pm.arma = arma; pm.boca = boca;
            }
            var rp = new GameObject("RotorPrincipal").transform; rp.SetParent(h, false); rp.localPosition = new Vector3(0f, 3.95f, 0f);
            var b1 = Decor(rp, "Pala_1", 0, 0, 8.4f, 0.08f, 0.45f, Negro); b1.transform.localPosition = Vector3.zero;
            var b2 = Decor(rp, "Pala_2", 0, 0, 0.45f, 0.08f, 8.4f, Negro); b2.transform.localPosition = Vector3.zero;
            var rc = new GameObject("RotorCola").transform; rc.SetParent(h, false); rc.localPosition = new Vector3(0.3f, 3.6f, -7.9f);
            var c1 = Decor(rc, "Pala_C1", 0, 0, 0.08f, 2.2f, 0.3f, Negro); c1.transform.localPosition = Vector3.zero;
            h.gameObject.AddComponent<Helicoptero>();
            h.gameObject.SetActive(false);
            var ini = new GameObject("Heli_Inicio").transform; ini.SetParent(padre, false); ini.position = new Vector3(330f, 75f, 385f);
            inicio = ini;
            return h;
        }
    }
}
