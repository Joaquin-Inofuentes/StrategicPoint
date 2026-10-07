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
    // Zona 2 (WP9a, #082/#097/#085): MURALLA DE CONTENCION. Una sola capa transversal en z = -170 (x -61..61, 5 m de alto) con el porton blindado
    // en el centro (x -8..8, cerrado) y dos bunkers sobresalientes en los flancos (Puesto Oeste x -38, Puesto Este x 38; 10 x 4 x 8). Cada bunker
    // tiene un nido de ametralladora en el techo, una garita, una puerta lateral por donde sale el contraataque y su PUNTO DE CARGA en la cara sur
    // (placa amarilla y negra con una caja). Al sur, la zona de aproximacion (z -218..-173): campo abierto con 2 lineas de Jersey en diagonal hacia
    // cada bunker, sacos, 2 autos quemados y una trinchera; el centro (frente al porton) es zona de muerte cubierta por los dos nidos.
    // Al norte, el CORREDOR DE TRINCHERAS (z -165..-100) con 3 guardias rezagados y coberturas (no hace falta matarlos para avanzar).
    // Guardias: 12 a cubierto (5 por bunker detras de sacos o Jersey + 2 en la trinchera) + 2 artilleros en los nidos + 3 del corredor + 12 de reserva.
    public static partial class OperacionBuilder
    {
        const float ZMuralla = -170f;
        static Material Blindado => Mat("Blindado", new Color(0.20f, 0.24f, 0.28f));
        static Material CargaCaja => Mat("CargaCaja", new Color(0.55f, 0.1f, 0.08f), 0.2f);
        static Material PisoAproximacion => Mat("PisoAproximacion", new Color(0.46f, 0.47f, 0.45f));
        static Material PisoTrinchera => Mat("PisoTrinchera", new Color(0.22f, 0.20f, 0.17f));
        static Material AutoQuemado => Mat("AutoQuemado", new Color(0.10f, 0.10f, 0.11f));

        // Cobertura baja que nunca se rompe sola (Jersey): solido sin marcador; la IA la reconoce igual por el collider.
        static GameObject Jersey(Transform p, string n, float x, float z, float largo, float yaw = 0f)
            => Solido(p, n, x, z, largo, 1.1f, 1.2f, Hormigon, yaw);

        static PuestoDeVoladura[] Puestos(Transform padre, out GameObject portonBlindado, out Soldier[] soldadosDeTrinchera, out Soldier[] soldadosDelCorredor)
        {
            var g = Grupo(padre, "2_MurallaDeContencion");
            var camino = Grupo(g, "Camino");
            var est = Grupo(g, "Muralla");
            var zona = Grupo(g, "ZonaDeAproximacion");
            var corredor = Grupo(g, "CorredorDeTrincheras");

            // --- Camino que conecta el porton del cuartel con el centro de datos, y piso de la zona ---
            Pintura(camino, "Asfalto_Corredor", 0f, -150f, 14f, 150f, Asfalto, 0f, 0f, 0.08f);
            for (int k = 0; k < 17; k++)
            {
                float zl = -216f + k * 8f;
                if (Mathf.Abs(zl - ZMuralla) < 3f) continue;
                Pintura(camino, "Linea_Corredor_" + k, 0f, zl, 0.3f, 3.5f, Amarillo);
            }
            for (float z = -210f; z <= -80f; z += 26f) { Farola(camino, -8.4f, z, false); Farola(camino, 8.4f, z, false); }
            for (float z = -205f; z <= -120f; z += 22f) FlechaPiso(camino, 0f, z, 0f);
            Pintura(zona, "Piso_Aproximacion", 0f, -196f, 120f, 46f, PisoAproximacion, 0f, 0f, 0.05f);

            // --- La muralla: una capa, el porton blindado en el centro ---
            MuroX(est, "Muralla_O", -61f, -8f, ZMuralla, 5f, 3f, CementoOsc);
            MuroX(est, "Muralla_E", 8f, 61f, ZMuralla, 5f, 3f, CementoOsc);
            Solido(est, "Porton_Pilar_O", -8.9f, ZMuralla, 1.8f, 7f, 3.4f, Cemento);
            Solido(est, "Porton_Pilar_E", 8.9f, ZMuralla, 1.8f, 7f, 3.4f, Cemento);
            Decor(est, "Porton_Dintel", 0f, ZMuralla, 19.6f, 0.8f, 3f, CementoOsc, 0f, 6.3f);
            portonBlindado = Solido(est, "Porton_Blindado", 0f, ZMuralla, 16f, 4.6f, 2.2f, Blindado);
            for (int k = -3; k <= 3; k++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                f.name = "Franja"; f.transform.SetParent(portonBlindado.transform, false);
                f.transform.localPosition = new Vector3(k / 8f * 1f, 0f, 0f);
                f.transform.localScale = new Vector3(0.07f, 1.02f, 1.1f);
                f.GetComponent<MeshRenderer>().sharedMaterial = Amarillo;
                Object.DestroyImmediate(f.GetComponent<BoxCollider>());
            }
            Rotulo(est, "MURALLA DE CONTENCION", 0f, 6.5f, ZMuralla - 1.6f, 0f, 1.3f, Color.white, AzulSenal, 17f, 1.9f);
            Rotulo(est, "PORTON BLINDADO\nSE ABRE AL VOLAR LOS 2 PUESTOS", 0f, 3.3f, ZMuralla - 1.2f, 0f, 0.8f, Color.white, Rojo, 12f, 1.6f);
            LuzPuntual(est, "Luz_Porton_Blindado", 0f, 6f, ZMuralla - 8f, new Color(1f, 0.93f, 0.8f), 3.2f, 24f);

            // --- Dos bunkers ---
            var en = Grupo(rEnemigos, "Muralla");
            var res = Grupo(rReservas, "Muralla");
            var puestos = new PuestoDeVoladura[2];
            puestos[0] = Bunker(g, zona, en, res, "OESTE", -38f);
            puestos[1] = Bunker(g, zona, en, res, "ESTE", 38f);

            // --- Zona de aproximacion: coberturas del campo abierto ---
            foreach (float s in new[] { -1f, 1f })
            {
                string l = s < 0f ? "O" : "E";
                // 2 lineas de Jersey en diagonal hacia el bunker (segmentos de 6,5 m con un hueco entre cada uno).
                for (int k = 0; k < 3; k++)
                {
                    Jersey(zona, $"Jersey_Diag_A{k + 1}_{l}", s * (10f + k * 9f), -204f + k * 7.5f, 6.5f, s * 50f);
                    Jersey(zona, $"Jersey_Diag_B{k + 1}_{l}", s * (20f + k * 8.5f), -212f + k * 6.5f, 6.5f, s * 52f);
                }
                // Trinchera: dos muros bajos con el piso oscuro entre ellos, a ambos lados del camino (z -195).
                Solido(zona, $"Trinchera_N_{l}", s * 10f, -193f, 8f, 1.1f, 0.6f, Hormigon);
                Solido(zona, $"Trinchera_S_{l}", s * 10f, -197f, 8f, 1.1f, 0.6f, Hormigon);
                Pintura(zona, $"Trinchera_Piso_{l}", s * 10f, -195f, 8f, 3.4f, PisoTrinchera, 0f, 0f, 0.07f);
                // Sacos sueltos y cajas como cobertura del campo.
                Saco(zona, $"Saco_Campo_1_{l}", s * 16f, -186f);
                Saco(zona, $"Saco_Campo_2_{l}", s * 4f, -207f, true);
                Destruible(zona, $"Caja_Campo_1_{l}", s * 25f, -211f, 1.4f, 1.2f, 1.4f, Cobertura, 250);
                Destruible(zona, $"Caja_Campo_2_{l}", s * 33f, -184.5f, 1.4f, 1.2f, 1.4f, Cobertura, 250);
            }
            AutoQuemadoDe(zona, "Auto_Quemado_1", -14f, -201f, 15f);
            AutoQuemadoDe(zona, "Auto_Quemado_2", 13f, -184f, -20f);
            Solido(zona, "Poste_Zona_O", -8.4f, -207f, 0.4f, 6.5f, 0.4f, CementoOsc);
            Solido(zona, "Poste_Zona_E", 8.4f, -207f, 0.4f, 6.5f, 0.4f, CementoOsc);
            Rotulo(zona, "ZONA DE APROXIMACION\nSOLO EL ASALTO COLOCA LAS CARGAS", 0f, 5.4f, -207.2f, 0f, 0.9f, Color.white, Negro, 16.4f, 1.9f);
            LuzPuntual(zona, "Luz_Zona_1", -18f, 7f, -196f, new Color(1f, 0.93f, 0.8f), 3.2f, 26f);
            LuzPuntual(zona, "Luz_Zona_2", 18f, 7f, -196f, new Color(1f, 0.93f, 0.8f), 3.2f, 26f);

            // --- Trinchera: 2 guardias a cubierto ---
            var trinchera = new List<Soldier>();
            trinchera.Add(Enemigo("Enemigo_Trinchera_O", -10f, -195f, 180f, en, true, 2.5f));
            trinchera.Add(Enemigo("Enemigo_Trinchera_E", 10f, -195f, 180f, en, true, 2.5f));
            soldadosDeTrinchera = trinchera.ToArray();

            // --- Corredor de trincheras (norte de la muralla) ---
            foreach (float s in new[] { -1f, 1f })
            {
                string l = s < 0f ? "O" : "E";
                Solido(corredor, $"Trinchera_C1_N_{l}", s * 20f, -150f, 18f, 1.1f, 0.6f, Hormigon);
                Solido(corredor, $"Trinchera_C1_S_{l}", s * 20f, -153.5f, 18f, 1.1f, 0.6f, Hormigon);
                Pintura(corredor, $"Trinchera_C1_Piso_{l}", s * 20f, -151.75f, 18f, 2.9f, PisoTrinchera, 0f, 0f, 0.07f);
                Solido(corredor, $"Trinchera_C2_N_{l}", s * 20f, -125f, 18f, 1.1f, 0.6f, Hormigon);
                Solido(corredor, $"Trinchera_C2_S_{l}", s * 20f, -128.5f, 18f, 1.1f, 0.6f, Hormigon);
                Pintura(corredor, $"Trinchera_C2_Piso_{l}", s * 20f, -126.75f, 18f, 2.9f, PisoTrinchera, 0f, 0f, 0.07f);
                Saco(corredor, $"Saco_Corredor_{l}", s * 40f, -140f, false);
            }
            Jersey(corredor, "Jersey_Corredor_1", -9f, -140f, 6f, 20f);
            Jersey(corredor, "Jersey_Corredor_2", 9f, -113.9f, 6f, -15f);
            Solido(corredor, "Poste_Corredor_O", -7.6f, -160f, 0.4f, 7.5f, 0.4f, CementoOsc);
            Solido(corredor, "Poste_Corredor_E", 7.6f, -160f, 0.4f, 7.5f, 0.4f, CementoOsc);
            Rotulo(corredor, "CORREDOR DE TRINCHERAS", 0f, 7f, -160f, 0f, 1.2f, Color.white, Naranja, 15f, 1.7f);
            LuzPuntual(corredor, "Luz_Corredor_1", 0f, 7f, -138f, new Color(1f, 0.93f, 0.8f), 3f, 26f);
            var rez = new List<Soldier>();
            rez.Add(Enemigo("Enemigo_Corredor_1", -20f, -151.7f, 180f, en, true, 2.5f));      // en la trinchera C1 (cobertura: el muro sur)
            rez.Add(Enemigo("Enemigo_Corredor_2", 20f, -126.7f, 180f, en, true, 2.5f));       // en la trinchera C2
            rez.Add(Enemigo("Enemigo_Corredor_3", 9f, -112f, 180f, en, true, 2.5f));          // detras del Jersey de las 112
            soldadosDelCorredor = rez.ToArray();
            return puestos;
        }

        // Auto quemado: carroceria destruible (cobertura) y cabina oscura sin colision.
        static void AutoQuemadoDe(Transform p, string nombre, float x, float z, float yaw)
        {
            var raizA = Grupo(p, nombre);
            Destruible(raizA, nombre + "_Carroceria", x, z, 1.9f, 1.1f, 4.4f, AutoQuemado, 500, yaw);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var c = rot * new Vector3(0f, 0f, -0.3f);
            Decor(raizA, nombre + "_Cabina", x + c.x, z + c.z, 1.6f, 0.8f, 2.2f, AutoQuemado, yaw, 1.1f);
            Decor(raizA, nombre + "_Brasas", x, z, 1.3f, 0.12f, 3.4f, Naranja, yaw, 1.1f);
        }

        // Un bunker: cuerpo, nido de ametralladora en el techo (con su artillero), garita, puerta lateral, escalera, punto de carga y reservas.
        static PuestoDeVoladura Bunker(Transform g, Transform zona, Transform en, Transform res, string lado, float xb)
        {
            float s = xb < 0f ? -1f : 1f;                       // -1 oeste, +1 este (x del centro hacia afuera)
            string cap = lado == "OESTE" ? "Oeste" : "Este";
            const float z0 = ZMuralla;
            var b = Grupo(g, "Bunker_" + cap);

            // Cuerpo (se hunde en trozos al volar) y garita: edificios indestructibles salvo por la carga.
            Edificio(b, "Bunker_Cuerpo", xb, z0, 10f, 4f, 8f, Hormigon);
            Edificio(b, "Bunker_Garita", xb - s * 9.5f, z0 - 6.2f, 3f, 3f, 3f, Cemento);
            Decor(b, "Bunker_Garita_Techo", xb - s * 9.5f, z0 - 6.2f, 3.5f, 0.3f, 3.5f, Techo, 0f, 3f);
            // Nido en el techo: parapeto (frente al sur y costados) y ametralladora apuntando al sur.
            Decor(b, "Nido_Parapeto_S", xb, z0 - 3.6f, 10f, 1.1f, 0.5f, Cemento, 0f, 4f);
            Decor(b, "Nido_Parapeto_L1", xb - 4.75f, z0, 0.5f, 1.1f, 7.2f, Cemento, 0f, 4f);
            Decor(b, "Nido_Parapeto_L2", xb + 4.75f, z0, 0.5f, 1.1f, 7.2f, Cemento, 0f, 4f);
            Decor(b, "MG_Tripode", xb, z0 - 2.9f, 0.4f, 0.7f, 0.4f, MetalOsc, 180f, 4f);
            Decor(b, "MG_Cajon", xb, z0 - 3.1f, 0.35f, 0.35f, 0.8f, MetalOsc, 180f, 4.65f);
            Decor(b, "MG_Canon", xb, z0 - 3.9f, 0.12f, 0.12f, 1.2f, Negro, 180f, 4.75f);
            Rotulo(b, "PUESTO " + lado, xb, 2.9f, z0 - 4.12f, 0f, 1.0f, Color.white, Rojo, 7f, 1.3f);
            // Escalera (visual) en la cara de adentro, al sur.
            float ex = xb - s * 5.25f;
            Decor(b, "Escalera_Riel_1", ex, z0 - 2.3f, 0.1f, 4f, 0.1f, MetalOsc, 0f, 0f);
            Decor(b, "Escalera_Riel_2", ex, z0 - 3.3f, 0.1f, 4f, 0.1f, MetalOsc, 0f, 0f);
            for (int k = 0; k < 6; k++) Decor(b, "Escalera_Peldano_" + k, ex, z0 - 2.8f, 0.1f, 0.08f, 1f, MetalOsc, 0f, 0.5f + k * 0.62f);
            // Puerta lateral (la hoja se apaga cuando sale el contraataque).
            var puerta = Decor(b, "Bunker_Puerta", xb + s * 5.06f, z0 - 2.85f, 0.2f, 2.6f, 2.0f, Naranja, 0f, 0f);
            Rotulo(b, "PUERTA", xb + s * 5.2f, 3.1f, z0 - 2.85f, s > 0f ? 90f : 270f, 0.45f, Color.white, Negro, 2.6f, 0.7f);
            LuzPuntual(b, "Luz_Bunker", xb, 6.5f, z0 - 9f, new Color(1f, 0.9f, 0.7f), 3.4f, 22f);

            // Punto de carga: placa amarilla y negra en el piso, caja con LED y etiqueta.
            float zc = z0 - 7f;
            var carga = Grupo(g, "Carga_" + cap);
            carga.position = new Vector3(xb, 0f, zc);
            Pintura(carga, "Placa", xb, zc, 3.6f, 3.6f, Amarillo, 0f, 0.075f, 0.05f);
            for (int k = -1; k <= 1; k++) Pintura(carga, "Banda_" + (k + 1), xb + k * 1.15f, zc, 0.5f, 3.0f, Negro, 45f, 0.12f, 0.03f);
            var caja = Cubo(carga, "Caja_Carga", xb, zc, 1.1f, 0.85f, 0.8f, CargaCaja, 0f, 0f, true, 0);
            Decor(carga, "Caja_Cinta", xb, zc, 1.16f, 0.12f, 0.86f, Amarillo, 0f, 0.5f);
            var led = Decor(carga, "Led", xb + 0.3f, zc - 0.2f, 0.2f, 0.1f, 0.2f, Pantalla, 0f, 0.85f);
            var rotulo = Rotulo(carga, "CARGA " + lado, xb, 2.3f, zc, 0f, 0.8f, new Color(1f, 0.82f, 0.1f), Negro, 4.8f, 0.95f);
            LuzPuntual(carga, "Luz_Carga", xb, 3f, zc - 1.5f, new Color(1f, 0.75f, 0.3f), 2.6f, 14f);
            var ce = carga.gameObject.AddComponent<CargaExplosiva>();
            ce.lado = lado;
            ce.bunker = b;
            ce.led = led.GetComponent<MeshRenderer>();
            ce.caja = caja.GetComponent<MeshRenderer>();
            ce.etiqueta = rotulo.GetComponentInChildren<TextMesh>(true);

            // Nido: el artillero nace en el suelo (sobre el NavMesh) y sube al techo al empezar.
            var nido = Enemigo("Enemigo_Nido_" + cap, xb, z0 - 13f, 180f, en, false);
            var art = b.gameObject.AddComponent<ArtilleroDeTorreta>();
            art.arcoGrados = 90f;
            art.soldado = nido; art.pies = new Vector3(xb, 4f, z0 - 2.3f); art.yawBase = 180f;
            ce.nido = nido;

            // Guardias a cubierto, mirando al sur: 4 detras de sacos (z -181,6) y 1 detras de un Jersey exterior. 1,9 m detras de la cobertura.
            float zs = z0 - 11.6f;
            float[] dx = { -9.5f, -4f, 4.5f, 10f };
            var guardias = new List<Soldier>();
            for (int k = 0; k < dx.Length; k++)
            {
                Saco(zona, $"Saco_Puesto_{cap}_{k + 1}", xb + dx[k], zs);
                guardias.Add(Enemigo($"Enemigo_Puesto{cap}_{k + 1}", xb + dx[k], zs + 1.9f, 180f, en, true, 2.5f));
            }
            Jersey(zona, $"Jersey_Puesto_{cap}", xb + s * 15f, z0 - 16f, 8f);
            guardias.Add(Enemigo($"Enemigo_Puesto{cap}_5", xb + s * 15f, z0 - 14.1f, 180f, en, true, 2.5f));

            // Reservas del contraataque: 6 detras de la puerta lateral, apagadas hasta que se coloca la carga.
            var rs = new List<Soldier>();
            for (int k = 0; k < 6; k++)
            {
                float rx = xb + s * (7.5f + (k / 3) * 1.6f);
                float rz = z0 - 3.5f - (k % 3) * 1.5f;
                rs.Add(Enemigo($"Reserva_Puesto{cap}_{k + 1}", rx, rz, 180f, res, false, 3f, true));
            }
            var salida = new GameObject("Salida_Camioneta").transform; salida.SetParent(b, false); salida.position = new Vector3(s * 55f, 0.6f, -214f);
            var parada = new GameObject("Parada_Camioneta").transform; parada.SetParent(b, false); parada.position = new Vector3(s * 50f, 0.6f, -196f);

            return new PuestoDeVoladura
            {
                nombre = "PUESTO " + lado, lado = lado, bunker = b, carga = ce, guardias = guardias.ToArray(), nido = nido,
                contraataque = new OleadaDeReserva { segundo = 0f, aviso = "", soldados = rs.ToArray(), destino = null },
                puerta = puerta.transform, salidaCamioneta = salida, paradaCamioneta = parada,
            };
        }
    }
}
