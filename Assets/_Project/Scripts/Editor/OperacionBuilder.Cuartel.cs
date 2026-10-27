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
    // Zona 1: cuartel militar (OperacionDirector.Infiltrar). Rediseno WP8 (#078): cuatro sectores con su color, divisiones internas, chicana
    // de acceso, laberinto de contenedores, patio de armas, comando y parque motor; cuatro torres con tiradores y cuatro reflectores.
    //
    //   z -339..-312  A · CONTROL DE ACCESO (gris hormigon): 3 lineas de Jersey en zigzag, nidos de sacos, garita. Reflector R1.
    //   z = -312      muro interno con 3 aberturas de 6 m (x -35, 0, +35)
    //   oeste  B · BARRACAS (ocre):  3 barracas, 2 callejones, muro bajo en x -14. Torre de francotirador TF1.
    //   centro PATIO DE ARMAS:       mastil y 4 cajas bajas, abierto: zona de muerte cubierta por TV1 y TF1.
    //   este   C · ARMERIA Y DEPOSITO (verde oliva): laberinto de 8 contenedores. Torreta vigia TV1 y reflector R2.
    //   z = -260      muro interno con 2 aberturas de 6 m (x -30 y +30): el centro queda tapado por el Comando.
    //   z -259..-221  D · COMANDO Y PARQUE MOTOR (azul acero): comando, comedor, parque motor con 3 camiones. TF2, TV2, R3 y R4.
    // La brecha sur (x -8..8) y el porton norte (x -7..7) conservan nombre y posicion. 28 enemigos: 20 guardias a cubierto (A4 B6 C5 D5),
    // 2 francotiradores, 2 artilleros de torreta y 4 operadores de reflector.
    public static partial class OperacionBuilder
    {
        static TorreDestruible[] torresDelCuartel;       // las arma Cuartel() y Construir() las cablea al director
        static ReflectorVigia[] reflectoresDelCuartel;

        static Material Hormigon => Mat("Hormigon", new Color(0.58f, 0.59f, 0.61f));
        static Material PisoHormigon => Mat("PisoHormigon", new Color(0.50f, 0.51f, 0.53f));
        static Material Ocre => Mat("Ocre", new Color(0.74f, 0.58f, 0.33f));
        static Material PisoOcre => Mat("PisoOcre", new Color(0.62f, 0.52f, 0.33f));
        static Material OlivaClaro => Mat("OlivaClaro", new Color(0.44f, 0.50f, 0.30f));
        static Material OlivaOsc => Mat("OlivaOsc", new Color(0.20f, 0.26f, 0.15f));
        static Material PisoOliva => Mat("PisoOliva", new Color(0.36f, 0.43f, 0.27f));
        static Material AzulAcero => Mat("AzulAcero", new Color(0.30f, 0.41f, 0.56f));
        static Material PisoAzul => Mat("PisoAzul", new Color(0.30f, 0.37f, 0.48f));
        static Material TechoOcre => Mat("TechoOcre", new Color(0.50f, 0.38f, 0.20f));
        static Material TechoAzul => Mat("TechoAzul", new Color(0.18f, 0.25f, 0.38f));
        static Material MetalOsc => Mat("MetalOsc", new Color(0.16f, 0.17f, 0.19f));
        static Material MetalClaro => Mat("MetalClaro", new Color(0.55f, 0.57f, 0.60f));

        static Transform Cuartel(Transform padre, out Soldier[] enemigos, out GameObject porton)
        {
            var g = Grupo(padre, "1_Cuartel");
            var estructura = Grupo(g, "Estructuras");
            // Muro sur con la brecha (x -8..8) y muro norte con el porton (x -7..7).
            MuroX(estructura, "Cuartel_Sur_O", -61f, -8f, -340f, 4f, 2f);
            MuroX(estructura, "Cuartel_Sur_E", 8f, 61f, -340f, 4f, 2f);
            MuroX(estructura, "Cuartel_Norte_O", -61f, -7f, -220f, 4f, 2f);
            MuroX(estructura, "Cuartel_Norte_E", 7f, 61f, -220f, 4f, 2f);
            Solido(estructura, "Brecha_Pilar_O", -8.6f, -340f, 1.6f, 6f, 1.6f, CementoOsc);
            Solido(estructura, "Brecha_Pilar_E", 8.6f, -340f, 1.6f, 6f, 1.6f, CementoOsc);
            Solido(estructura, "Porton_Pilar_O", -7.6f, -220f, 1.8f, 7f, 1.8f, CementoOsc);
            Solido(estructura, "Porton_Pilar_E", 7.6f, -220f, 1.8f, 7f, 1.8f, CementoOsc);
            Decor(estructura, "Porton_Dintel", 0f, -220f, 16f, 0.8f, 1.6f, CementoOsc, 0f, 6.2f);
            Rotulo(estructura, "SALIDA NORTE", 0f, 6.6f, -220.95f, 0f, 1.2f, Color.white, Rojo, 9f, 1.6f);
            Rotulo(estructura, "SALIDA NORTE", 0f, 6.6f, -219.05f, 180f, 1.2f, Color.white, Rojo, 9f, 1.6f);
            Rotulo(estructura, "BRECHA", 0f, 6.6f, -341.15f, 0f, 1.2f, Color.white, Naranja, 7f, 1.6f);

            // Porton: barrera roja y blanca que cierra el paso hasta que cae el ultimo enemigo.
            porton = Solido(estructura, "Porton_Cuartel", 0f, -220f, 14f, 3.4f, 1.2f, Rojo);
            for (int i = -3; i <= 3; i++) Decor(porton.transform, "Franja", 0f, 0f, 1f, 1f, 1f, Blanco).transform.SetPositionAndRotation(new Vector3(i * 1.9f, 1.7f, -220f), Quaternion.identity);
            foreach (Transform t in porton.transform) t.localScale = new Vector3(0.9f / 14f, 1.02f, 1.1f);

            // Pisos de blocking por sector (pintura sin collider, debajo del asfalto del camino).
            var pisos = Grupo(estructura, "Pisos");
            Pintura(pisos, "Piso_A", 0f, -326f, 120f, 26f, PisoHormigon, 0f, 0f, 0.05f);
            Pintura(pisos, "Piso_B", -36f, -287f, 48f, 48f, PisoOcre, 0f, 0f, 0.05f);
            Pintura(pisos, "Piso_C", 36f, -287f, 48f, 48f, PisoOliva, 0f, 0f, 0.05f);
            Pintura(pisos, "Piso_Patio", 0f, -287f, 24f, 48f, CementoOsc, 0f, 0f, 0.06f);
            Pintura(pisos, "Piso_D", 0f, -240f, 120f, 38f, PisoAzul, 0f, 0f, 0.05f);

            SectorA(estructura);
            MuroInterno(estructura);
            SectorB(estructura);
            PatioDeArmas(estructura);
            SectorC(estructura);
            SectorD(estructura);

            // Camino iluminado hasta el porton (por el centro: pasa por las tres aberturas centrales y rodea el comando).
            Pintura(estructura, "Camino_Cuartel", 0f, -280f, 8f, 120f, Asfalto, 0f, 0.0f, 0.08f);
            for (int k = 0; k < 12; k++) Pintura(estructura, "Linea_Cuartel_" + k, 0f, -336f + k * 9.5f, 0.3f, 3.5f, Amarillo);
            foreach (float z in new[] { -330f, -310f, -290f, -270f, -232f }) { Farola(estructura, -6.5f, z, false); Farola(estructura, 6.5f, z, false); }
            LuzPuntual(estructura, "Luz_Patio_1", -20f, 7f, -300f, new Color(1f, 0.93f, 0.8f), 3f, 24f);
            LuzPuntual(estructura, "Luz_Patio_2", 20f, 7f, -300f, new Color(1f, 0.93f, 0.8f), 3f, 24f);
            LuzPuntual(estructura, "Luz_Porton", 0f, 7f, -228f, new Color(1f, 0.93f, 0.8f), 3f, 22f);
            FlechaPiso(estructura, 0f, -232f, 0f);
            Rotulo(estructura, "^ PORTON NORTE", 0f, 5f, -224f, 180f, 1.0f, Color.white, AzulSenal, 10f, 1.4f);

            // Torres y reflectores (con sus tiradores y operadores).
            var en = Grupo(rEnemigos, "Cuartel");
            var torres = new List<TorreDestruible>();
            var reflectores = new List<ReflectorVigia>();
            var arriba = new List<Soldier>();
            var tf1 = TorreDeFrancotirador(estructura, en, "Torre_TF1", -56.5f, -295.5f, Ocre, 0f, arriba); torres.Add(tf1);
            var tf2 = TorreDeFrancotirador(estructura, en, "Torre_TF2", 55f, -228f, AzulAcero, 0f, arriba); torres.Add(tf2);
            var tv1 = TorreDeTorreta(estructura, en, "Torre_TV1", 45f, -272f, OlivaClaro, arriba); torres.Add(tv1);
            var tv2 = TorreDeTorreta(estructura, en, "Torre_TV2", -45f, -232f, AzulAcero, arriba); torres.Add(tv2);
            reflectores.Add(Reflector(estructura, en, "Reflector_R1", -22f, -315.5f, Hormigon, new Vector3(0f, 0f, -292f), arriba));
            reflectores.Add(Reflector(estructura, en, "Reflector_R2", 56f, -304f, OlivaClaro, new Vector3(8f, 0f, -290f), arriba));
            reflectores.Add(Reflector(estructura, en, "Reflector_R3", -56f, -226f, AzulAcero, new Vector3(0f, 0f, -258f), arriba));
            reflectores.Add(Reflector(estructura, en, "Reflector_R4", 16f, -232f, AzulAcero, new Vector3(-2f, 0f, -262f), arriba));
            torresDelCuartel = torres.ToArray();
            reflectoresDelCuartel = reflectores.ToArray();

            // Guardias a cubierto: cada uno 1,9 m detras de su cobertura, mirando hacia la entrada del sector (Atrincherar los deja EN la cobertura).
            var lista = new List<Soldier>();
            int n = 0;
            void Guardia(float x, float z) => lista.Add(Enemigo("Enemigo_Cuartel_" + (++n), x, z, 180f, en, true, 2.5f));
            // A (4): nidos y detras de los Jersey.
            Guardia(-14f, -321.9f); Guardia(14f, -321.9f); Guardia(26f, -325.2f); Guardia(-30f, -317.6f);
            // B (6): sacos y cajas del patio de las barracas.
            Guardia(-24f, -304.1f); Guardia(-30f, -294.1f); Guardia(-20f, -285.5f); Guardia(-28f, -278.1f); Guardia(-18f, -270.1f); Guardia(-27f, -266.1f);
            // C (5): detras de los contenedores.
            Guardia(22f, -301.4f); Guardia(34f, -301.4f); Guardia(26f, -289.4f); Guardia(42f, -287.4f); Guardia(34f, -273.2f);
            // D (5): camiones y sacos junto al porton.
            Guardia(26f, -230.6f); Guardia(36f, -230.6f); Guardia(-30f, -227.6f); Guardia(-6f, -236.1f); Guardia(6f, -236.1f);
            lista.AddRange(arriba);
            enemigos = lista.ToArray();
            return g;
        }

        // ---------------------------------------------------------------
        // Sectores
        // ---------------------------------------------------------------
        static void SectorA(Transform e)
        {
            // Chicana: tres lineas de Jersey de 50 m (segmentos de 8,3 m: cada uno da cobertura) con el hueco alternado.
            float[] zs = { -335f, -327.5f, -320f };
            float[] x0 = { -34f, -16f, -34f };
            for (int l = 0; l < 3; l++)
                for (int k = 0; k < 6; k++)
                    Solido(e, $"Barrera_Jersey_{l + 1}_{k + 1}", x0[l] + (k + 0.5f) * (50f / 6f), zs[l], 50f / 6f - 0.1f, 1.1f, 1.2f, Hormigon);
            // Nidos de sacos entre las lineas 2 y 3, garita al oeste.
            Saco(e, "Saco_NidoA_O", -14f, -323.75f); Saco(e, "Saco_NidoA_E", 14f, -323.75f);
            Edificio(e, "Garita_A", -50f, -328f, 6f, 3.2f, 5f, Hormigon);
            Decor(e, "Garita_A_Techo", -50f, -328f, 6.6f, 0.4f, 5.6f, Techo, 0f, 3.2f);
            Rotulo(e, "SECTOR A", -16f, 2.6f, -313.12f, 0f, 1.0f, Color.white, Negro, 10f, 2.2f);
            // Franjas de hazard en las puntas de cada linea.
            for (int l = 0; l < 3; l++)
            {
                float extremo = l == 1 ? x0[l] + 50f : x0[l];
                Decor(e, "Jersey_Aviso_" + l, extremo, zs[l], 0.4f, 1.2f, 1.3f, Amarillo);
            }
        }

        static void MuroInterno(Transform e)
        {
            // z = -312 (aberturas de 6 m en x -35, 0, 35) y z = -260 (aberturas en x -30 y 30). Muros de 4 m, 2 m de espesor.
            MuroX(e, "Cuartel_Interno_1a", -60f, -38f, -312f, 4f, 2f, Hormigon);
            MuroX(e, "Cuartel_Interno_1b", -32f, -3f, -312f, 4f, 2f, Hormigon);
            MuroX(e, "Cuartel_Interno_1c", 3f, 32f, -312f, 4f, 2f, Hormigon);
            MuroX(e, "Cuartel_Interno_1d", 38f, 60f, -312f, 4f, 2f, Hormigon);
            MuroX(e, "Cuartel_Interno_2a", -60f, -33f, -260f, 4f, 2f, Hormigon);
            MuroX(e, "Cuartel_Interno_2b", -27f, 27f, -260f, 4f, 2f, Hormigon);
            MuroX(e, "Cuartel_Interno_2c", 33f, 60f, -260f, 4f, 2f, Hormigon);
            // Dinteles bajos sobre las aberturas (solo visual) con el color del sector al que llevan.
            Decor(e, "Dintel_B", -35f, -312f, 6.4f, 0.5f, 2.4f, Ocre, 0f, 4f);
            Decor(e, "Dintel_Patio", 0f, -312f, 6.4f, 0.5f, 2.4f, CementoOsc, 0f, 4f);
            Decor(e, "Dintel_C", 35f, -312f, 6.4f, 0.5f, 2.4f, OlivaClaro, 0f, 4f);
            Decor(e, "Dintel_D1", -30f, -260f, 6.4f, 0.5f, 2.4f, AzulAcero, 0f, 4f);
            Decor(e, "Dintel_D2", 30f, -260f, 6.4f, 0.5f, 2.4f, AzulAcero, 0f, 4f);
            Rotulo(e, "SECTOR B", -35f, 4.9f, -313.2f, 0f, 1.0f, Color.white, Ocre, 8f, 1.9f);
            Rotulo(e, "PATIO DE ARMAS", 0f, 4.9f, -313.2f, 0f, 1.0f, Color.white, Negro, 8f, 1.5f);
            Rotulo(e, "SECTOR C", 35f, 4.9f, -313.2f, 0f, 1.0f, Color.white, OlivaOsc, 10f, 1.9f);
            Rotulo(e, "SECTOR D", 0f, 2.6f, -261.12f, 0f, 1.0f, Color.white, AzulAcero, 12f, 2.0f);
        }

        static void SectorB(Transform e)
        {
            float[] zb = { -304f, -287f, -270f };
            string[] letras = { "A", "B", "C" };
            for (int i = 0; i < 3; i++)
            {
                Edificio(e, "Barraca_O_" + letras[i], -43f, zb[i], 20f, 5f, 12f, Ocre);
                Decor(e, "Techo_O_" + letras[i], -43f, zb[i], 20.6f, 0.5f, 12.6f, TechoOcre, 0f, 5f);
                Rotulo(e, "BARRACA " + letras[i], -32.8f, 3.2f, zb[i], 270f, 1.1f, Color.white, Ocre, 8f, 1.5f);
            }
            // Muro bajo de 1,2 m en x -14 con dos pasos (z -300..-292 y -278..-270) y el extremo norte abierto.
            MuroZ(e, "Cuartel_MuroBajo_B1", -14f, -311f, -300f, 1.2f, 0.8f, Hormigon);
            MuroZ(e, "Cuartel_MuroBajo_B2", -14f, -292f, -278f, 1.2f, 0.8f, Hormigon);
            MuroZ(e, "Cuartel_MuroBajo_B3", -14f, -270f, -263f, 1.2f, 0.8f, Hormigon);
            // Coberturas del patio de las barracas.
            Saco(e, "Saco_B1", -24f, -306f); Saco(e, "Saco_B2", -30f, -296f); Saco(e, "Saco_B4", -28f, -280f);
            Saco(e, "Saco_B5", -18f, -272f); Saco(e, "Saco_B6", -27f, -268f);
            Cubo(e, "Caja_B1", -20f, -287.5f, 3f, 2f, 3f, Cobertura, 0f, 0f, true, 400);
            Destruible(e, "Caja_B2", -20f, -300f, 2f, 1.1f, 2f, Cobertura, 300);
        }

        static void PatioDeArmas(Transform e)
        {
            Solido(e, "Mastil_Pata", 0f, -287f, 0.7f, 0.6f, 0.7f, CementoOsc);
            Decor(e, "Mastil_Poste", 0f, -287f, 0.25f, 14f, 0.25f, Blanco, 0f, 0.6f);
            Decor(e, "Mastil_Bandera", 1.4f, -287f, 2.4f, 1.5f, 0.06f, Rojo, 0f, 12.6f);
            Destruible(e, "Caja_P1", -7f, -298f, 2f, 1.1f, 2f, Cobertura, 400);
            Destruible(e, "Caja_P2", 7f, -298f, 2f, 1.1f, 2f, Cobertura, 400);
            Destruible(e, "Caja_P3", -7f, -276f, 2f, 1.1f, 2f, Cobertura, 400);
            Destruible(e, "Caja_P4", 7f, -276f, 2f, 1.1f, 2f, Cobertura, 400);
        }

        static void SectorC(Transform e)
        {
            // Laberinto de 8 contenedores de 6 x 2,6 x 2,4 (algunos de dos pisos). yaw 90 = el largo va sobre Z.
            (float x, float z, float yaw, bool doble, Material m)[] c =
            {
                (22f, -304f, 0f, false, OlivaClaro), (34f, -304f, 0f, false, Oliva), (50f, -296f, 90f, false, OlivaOsc),
                (26f, -292f, 0f, true, Oliva), (42f, -290f, 0f, true, OlivaClaro), (54f, -282f, 90f, false, Oliva),
                (20f, -278f, 90f, false, OlivaOsc), (34f, -276f, 0f, true, OlivaClaro),
            };
            for (int i = 0; i < c.Length; i++)
            {
                Edificio(e, "Contenedor_" + (i + 1), c[i].x, c[i].z, 6f, 2.6f, 2.4f, c[i].m).transform.rotation = Quaternion.Euler(0f, c[i].yaw, 0f);
                if (c[i].doble)
                {
                    var alto = Edificio(e, "Contenedor_" + (i + 1) + "_Alto", c[i].x, c[i].z, 6f, 2.6f, 2.4f, c[i].m);
                    alto.transform.rotation = Quaternion.Euler(0f, c[i].yaw, 0f);
                    alto.transform.position = new Vector3(c[i].x, 2.6f + 1.3f, c[i].z);
                }
            }
            Rotulo(e, "ARMERIA", 22f, 2.6f, -302.7f, 0f, 1.0f, Color.white, OlivaOsc, 5f, 1.2f);
            Saco(e, "Saco_C1", 42f, -300f); Saco(e, "Saco_C2", 28f, -284f, false);
        }

        static void SectorD(Transform e)
        {
            Edificio(e, "Comando", 0f, -248f, 18f, 8.4f, 12f, AzulAcero);
            Decor(e, "Comando_Piso2", 0f, -248f, 18.6f, 0.3f, 12.6f, Hormigon, 0f, 4.2f);
            Decor(e, "Techo_Comando", 0f, -248f, 18.8f, 0.5f, 12.8f, TechoAzul, 0f, 8.4f);
            Decor(e, "Comando_Antena", 5f, -248f, 0.3f, 6f, 0.3f, MetalOsc, 0f, 8.9f);
            Rotulo(e, "COMANDO", 0f, 6.2f, -254.2f, 0f, 1.4f, Color.white, Rojo, 9f, 1.8f);
            Edificio(e, "Comedor", -31f, -248f, 20f, 4.5f, 12f, Casa);
            Rotulo(e, "COMEDOR", -31f, 3f, -254.2f, 0f, 1.2f, Color.white, VerdeSenal, 8f, 1.6f);
            Edificio(e, "Parque_Motor", 31f, -248f, 20f, 4.5f, 12f, Oliva);
            Rotulo(e, "PARQUE MOTOR", 31f, 3f, -254.2f, 0f, 1.2f, Color.white, Naranja, 10f, 1.6f);
            // Camiones de cobertura (vida 700) en el patio norte.
            Cubo(e, "Camion_1", 26f, -233f, 7f, 2.8f, 3f, Oliva, 0f, 0f, true, 700);
            Cubo(e, "Camion_2", 36f, -233f, 7f, 2.8f, 3f, Oliva, 0f, 0f, true, 700);
            Cubo(e, "Camion_3", -30f, -230f, 7f, 2.8f, 3f, Oliva, 0f, 0f, true, 700);
            Saco(e, "Saco_D1", -6f, -238f); Saco(e, "Saco_D2", 6f, -238f);
        }

        // ---------------------------------------------------------------
        // Torres y reflectores
        // ---------------------------------------------------------------
        static Soldier TiradorEnSuelo(Transform en, string nombre, float x, float z, float yaw)
            => Enemigo(nombre, x, z, yaw, en, false);

        // #257: la baranda de la torre mide la MITAD (0,5 m): se ve al tirador entero y sirve de cobertura baja (el tirador se agacha detras).
        public const float AlturaDeLaBarandaDeTorre = 0.5f;

        // Torre de francotirador de 12 m. El soldado nace en el suelo (en la malla de navegacion) y sube a la plataforma al empezar la partida.
        static TorreDestruible TorreDeFrancotirador(Transform e, Transform en, string nombre, float x, float z, Material mat, float yawSuelo, List<Soldier> arriba)
        {
            var raizT = new GameObject(nombre);
            raizT.transform.SetParent(e, false);
            raizT.transform.position = new Vector3(x, 0f, z);
            var pivote = Grupo(raizT.transform, "Pivote");
            const float h = 12f;
            Cubo(pivote, "Torre_Pata", x, z, 3f, h, 3f, mat);
            Decor(pivote, "Torre_Cinto_1", x, z, 3.6f, 0.4f, 3.6f, mat, 0f, 4f);
            Decor(pivote, "Torre_Cinto_2", x, z, 3.6f, 0.4f, 3.6f, mat, 0f, 8f);
            Decor(pivote, "Torre_Plataforma", x, z, 6f, 0.6f, 6f, Techo, 0f, h);
            Decor(pivote, "Torre_Baranda_N", x, z + 2.9f, 6f, AlturaDeLaBarandaDeTorre, 0.2f, mat, 0f, h + 0.6f);
            Decor(pivote, "Torre_Baranda_S", x, z - 2.9f, 6f, AlturaDeLaBarandaDeTorre, 0.2f, mat, 0f, h + 0.6f);
            Decor(pivote, "Torre_Baranda_O", x - 2.9f, z, 0.2f, AlturaDeLaBarandaDeTorre, 6f, mat, 0f, h + 0.6f);
            Decor(pivote, "Torre_Baranda_E", x + 2.9f, z, 0.2f, AlturaDeLaBarandaDeTorre, 6f, mat, 0f, h + 0.6f);
            foreach (var sx in new[] { -2.8f, 2.8f }) foreach (var sz in new[] { -2.8f, 2.8f }) Decor(pivote, "Torre_Poste", x + sx, z + sz, 0.25f, 3.2f, 0.25f, MetalOsc, 0f, h + 0.6f);
            Decor(pivote, "Torre_Techo", x, z, 6.6f, 0.4f, 6.6f, Techo, 0f, h + 3.8f);
            Rotulo(pivote, "FRANCOTIRADOR", x, 8f, z - 1.62f, 0f, 0.9f, Color.white, Rojo, 5f, 1.1f);
            var marca = raizT.AddComponent<ObstacleMarker>();
            marca.ConfigurarVida(TorreDestruible.VidaFrancotirador);
            marca.ConfigurarSoloExplosiones();
            var torre = raizT.AddComponent<TorreDestruible>();
            torre.tipo = TorreDestruible.Tipo.Francotirador; torre.pivote = pivote; torre.mediaBase = 1.5f; torre.AlturaDeLaCima = h + 0.6f;
            // El tirador: nace 4 m al lado de la torre, en suelo libre, y mira hacia el patio.
            float lado = x < 0f ? 4.5f : -4.5f;
            var s = TiradorEnSuelo(en, "Enemigo_Francotirador_" + nombre.Substring(nombre.Length - 3), x + lado, z, x < 0f ? 90f : 270f);
            var tirador = raizT.AddComponent<FrancotiradorEnTorre>();
            tirador.soldado = s; tirador.pies = new Vector3(x, h + 0.6f, z); tirador.yawBase = x < 0f ? 90f : 270f;
            torre.ocupante = s;
            arriba.Add(s);
            return torre;
        }

        // #104: parapeto de la torreta a la cintura (antes 1,2 m: tapaba al artillero entero desde el suelo y solo asomaba la cabeza).
        const float AlturaDelParapeto = 0.5f;
        // #104: el artillero se para sobre un estrado bajo para asomar entero por encima del parapeto.
        const float AlturaDelEstrado = 0.45f;

        // Torreta ANTIAEREA (bug #152/#153): nido bajo de hormigon con dos canones gemelos apuntando al cielo y un artillero. No se trepa (sin escalera,
        // sin colision arriba): solo se destruye con explosivos o se DEMUELE con [Q] (collider disparador en la raiz, bug #154).
        static TorreDestruible TorreDeTorreta(Transform e, Transform en, string nombre, float x, float z, Material mat, List<Soldier> arriba)
        {
            var raizT = new GameObject(nombre);
            raizT.transform.SetParent(e, false);
            raizT.transform.position = new Vector3(x, 0f, z);
            var pivote = Grupo(raizT.transform, "Pivote");
            const float h = 1.1f;
            float yaw = Mathf.Atan2(-x, 0f) * Mathf.Rad2Deg;   // mira hacia el eje x = 0 (el patio): +90 si esta al oeste, -90 si esta al este
            if (Mathf.Abs(x) < 1f) yaw = 180f;
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Pisable(Cubo(pivote, "Torre_Pata", x, z, 3.4f, h, 3.4f, mat));
            Decor(pivote, "Torre_Cinto", x, z, 3.7f, 0.3f, 3.7f, Hormigon, 0f, 0.35f);
            // Nido bajo: la plataforma y el parapeto son decorado sin colision (nadie sube: es una pieza antiaerea, no un mirador).
            Decor(pivote, "Torre_Plataforma", x, z, 5f, 0.5f, 5f, Techo, 0f, h);
            Decor(pivote, "Torre_Parapeto_N", x, z + 2.4f, 5f, AlturaDelParapeto, 0.4f, mat, 0f, h + 0.5f);
            Decor(pivote, "Torre_Parapeto_S", x, z - 2.4f, 5f, AlturaDelParapeto, 0.4f, mat, 0f, h + 0.5f);
            Decor(pivote, "Torre_Parapeto_O", x - 2.4f, z, 0.4f, AlturaDelParapeto, 5f, mat, 0f, h + 0.5f);
            Decor(pivote, "Torre_Parapeto_E", x + 2.4f, z, 0.4f, AlturaDelParapeto, 5f, mat, 0f, h + 0.5f);
            Rotulo(pivote, "ANTIAEREA", x, 0.62f, z - 1.72f, 0f, 0.5f, Color.white, Rojo, 2.8f, 0.6f);
            // Ametralladora visual (tripode, cajon y canon) mirando hacia el patio.
            var arma = Grupo(pivote, "Ametralladora");
            Decor(arma, "MG_Tripode", x + dir.x * 0.4f, z + dir.z * 0.4f, 0.4f, 0.7f, 0.4f, MetalOsc, yaw, h + 0.5f);
            Decor(arma, "MG_Cajon", x + dir.x * 0.5f, z + dir.z * 0.5f, 0.35f, 0.35f, 0.8f, MetalOsc, yaw, h + 1.15f);
            // Dos canones gemelos de 3,2 m inclinados 55 grados hacia el cielo (se arma de frente y se gira entera sobre el eje lateral).
            var gAA = Grupo(arma, "Canones_AA");
            var perpAA = new Vector3(dir.z, 0f, -dir.x);
            foreach (float l in new[] { -0.2f, 0.2f })
            {
                float px = x + dir.x * 0.9f + perpAA.x * l, pz = z + dir.z * 0.9f + perpAA.z * l;
                Decor(gAA, "AA_Tubo", px + dir.x * 1.2f, pz + dir.z * 1.2f, 0.13f, 0.13f, 3.2f, Negro, yaw, h + 1.2f);
                Decor(gAA, "AA_Camisa", px + dir.x * 0.3f, pz + dir.z * 0.3f, 0.22f, 0.22f, 1.0f, MetalClaro, yaw, h + 1.17f);
                Decor(gAA, "AA_Freno", px + dir.x * 2.75f, pz + dir.z * 2.75f, 0.2f, 0.2f, 0.3f, MetalOsc, yaw, h + 1.2f);
            }
            gAA.transform.RotateAround(new Vector3(x + dir.x * 0.9f, h + 1.2f, z + dir.z * 0.9f), perpAA, -55f);
            DetalleDeTorreta(pivote, x, z, h, yaw, dir, mat);
            var marca = raizT.AddComponent<ObstacleMarker>();
            marca.ConfigurarVida(TorreDestruible.VidaTorreta);
            marca.ConfigurarSoloExplosiones();
            var torre = raizT.AddComponent<TorreDestruible>();
            torre.tipo = TorreDestruible.Tipo.Torreta; torre.pivote = pivote; torre.mediaBase = 1.5f; torre.AlturaDeLaCima = h + 0.5f;
            // Bug #154: Demolicion.EsDemolible pide un Collider en el objeto con la marca; sin el, la radial no ofrecia DEMOLER. Disparador = no estorba.
            var cd = raizT.AddComponent<BoxCollider>(); cd.isTrigger = true; cd.center = new Vector3(0f, 1.6f, 0f); cd.size = new Vector3(3.6f, 3.2f, 3.6f);
            Pisable(raizT);
            float lado = x < 0f ? 4.5f : -4.5f;
            var s = TiradorEnSuelo(en, "Enemigo_Artillero_" + nombre.Substring(nombre.Length - 3), x + lado, z, yaw);
            var art = raizT.AddComponent<ArtilleroDeTorreta>();
            Decor(pivote, "Torre_Estrado", x - dir.x * 0.5f, z - dir.z * 0.5f, 1.4f, AlturaDelEstrado, 1.4f, mat, 0f, h + 0.5f);
            art.soldado = s; art.pies = new Vector3(x - dir.x * 0.5f, h + 0.5f + AlturaDelEstrado, z - dir.z * 0.5f); art.yawBase = yaw;
            torre.ocupante = s;
            arriba.Add(s);
            return torre;
        }

        static GameObject Pisable(GameObject go)
        {
            var mod = go.GetComponent<NavMeshModifier>(); if (mod == null) mod = go.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            return go;
        }

        // Pedido: "quiero su geometria ... mejoralo": la torreta de artilleria es ahora un puesto de cañon completo (todo cuelga del pivote: cae con la
        // torre). Escudo de dos placas con tronera, cañon con camisa y freno de boca, cajones de municion con cinta, bolsas de arena en los tres
        // lados traseros, techo de red de camuflaje sobre postes, escalera, antena y reflector chico.
        static void DetalleDeTorreta(Transform pv, float x, float z, float h, float yaw, Vector3 dir, Material mat)
        {
            var perp = new Vector3(dir.z, 0f, -dir.x);
            float baseY = h + AlturaDelParapeto;
            Vector3 P(float f, float l) => new Vector3(x + dir.x * f + perp.x * l, 0f, z + dir.z * f + perp.z * l);   // f = hacia adelante, l = lateral
            var mg = Grupo(pv, "Detalle");
            // Escudo: dos placas con una tronera de 0,35 m para el cañon.
            foreach (float l in new[] { -0.62f, 0.62f })
            {
                var q = P(1.8f, l);
                Decor(mg, "Escudo", q.x, q.z, 0.9f, 1.15f, 0.12f, MetalOsc, yaw, baseY);
                var q2 = P(1.8f, l * 1.55f); Decor(mg, "Escudo_Ala", q2.x, q2.z, 0.5f, 1.05f, 0.1f, MetalOsc, yaw + (l < 0f ? 28f : -28f), baseY);
            }
            var cab = P(1.8f, 0f); Decor(mg, "Escudo_Dintel", cab.x, cab.z, 0.4f, 0.12f, 0.14f, MetalOsc, yaw, baseY + 1.2f);
            // Cañon: camisa refrigerante gruesa, tubo y freno de boca.
            var camisa = P(1.05f, 0f); Decor(mg, "MG_Camisa", camisa.x, camisa.z, 0.24f, 0.24f, 0.8f, MetalClaro, yaw, baseY + 0.56f);
            var freno = P(2.0f, 0f); Decor(mg, "MG_Freno", freno.x, freno.z, 0.22f, 0.22f, 0.3f, MetalOsc, yaw, baseY + 0.6f);
            var mira = P(0.45f, 0.18f); Decor(mg, "MG_Mira", mira.x, mira.z, 0.1f, 0.18f, 0.3f, Negro, yaw, baseY + 1.0f);
            // Cajones de municion con cinta hacia el arma.
            foreach (float l in new[] { -0.95f, 0.95f })
            {
                var cj = P(0.5f, l); Decor(mg, "Cajon_Municion", cj.x, cj.z, 0.5f, 0.38f, 0.32f, OlivaOsc, yaw, baseY);
                var cn = P(0.5f, l * 0.55f); Decor(mg, "Cinta", cn.x, cn.z, 0.5f, 0.05f, 0.09f, Cobertura, yaw + (l < 0f ? 20f : -20f), baseY + 0.5f);
            }
            // Bolsas de arena sobre el parapeto: atras y a los costados (el frente queda libre para tirar).
            for (int i = -2; i <= 2; i++)
            {
                var a = P(-2.3f, i * 0.95f); Decor(mg, "Saco_Atras", a.x, a.z, 1.0f, 0.4f, 0.55f, Cobertura, yaw, baseY);
            }
            foreach (float l in new[] { -2.3f, 2.3f })
                for (int i = 0; i < 3; i++)
                {
                    var a = P(-1.1f + i * 1.15f, l); Decor(mg, "Saco_Lado", a.x, a.z, 0.55f, 0.4f, 1.0f, Cobertura, yaw, baseY);
                    if (i == 1) { var b = P(-0.55f, l); Decor(mg, "Saco_Lado_Alto", b.x, b.z, 0.5f, 0.38f, 0.95f, Cobertura, yaw, baseY + 0.4f); }
                }
            // Techo de red de camuflaje sobre cuatro postes (alto: no tapa el tiro).
            foreach (float f in new[] { -2.3f, 2.3f }) foreach (float l in new[] { -2.3f, 2.3f })
            {
                var pp = P(f, l); Decor(mg, "Red_Poste", pp.x, pp.z, 0.12f, 3.3f, 0.12f, MetalOsc, 0f, baseY);
            }
            var rc = P(0f, 0f); Decor(mg, "Red_Camuflaje", rc.x, rc.z, 5.8f, 0.1f, 5.8f, RedCamuflaje, yaw, baseY + 3.3f);
            Decor(mg, "Red_Faldon", P(-2.3f, 0f).x, P(-2.3f, 0f).z, 5.8f, 1.1f, 0.08f, RedCamuflaje, yaw, baseY + 2.2f);
            // Sin escalera: la torreta antiaerea no se trepa (bug #152). Antena en una esquina.
            var an = P(-2.2f, 2.2f); Decor(mg, "Antena", an.x, an.z, 0.06f, 4.2f, 0.06f, MetalClaro, 0f, baseY);
            var lm = P(1.2f, -2.2f); Decor(mg, "Faro", lm.x, lm.z, 0.5f, 0.3f, 0.4f, Lampara, yaw, baseY + 1.2f);
            Decor(mg, "Faro_Soporte", lm.x, lm.z, 0.08f, 1.2f, 0.08f, MetalOsc, 0f, baseY);
            // Cinturon de hormigon en la base (la pata deja de verse como un cubo pelado).
            Decor(mg, "Base_Zocalo", x, z, 3.9f, 0.7f, 3.9f, Hormigon);
        }

        // Bug #285: sombra dura, near plane, bias bajos y resolucion alta (tier High = 1024) para que la luz del reflector no se escape por el pie de los muros.
        // Se puede llamar sobre una luz ya existente (retoque en sitio). El tier se escribe por SerializedObject: fuera de Play el setter de URP lanza excepcion.
        public static void SombraDeReflector(Light luz)
        {
            luz.shadows = LightShadows.Hard; luz.shadowNearPlane = 0.5f; luz.shadowBias = 0.05f; luz.shadowNormalBias = 0.2f;
            if (!luz.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(out var datos)) datos = luz.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            var so = new SerializedObject(datos);
            var usa = so.FindProperty("m_UsePipelineSettings"); if (usa != null) usa.boolValue = false;
            var tier = so.FindProperty("m_AdditionalLightsShadowResolutionTier"); if (tier != null) tier.intValue = UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(luz); EditorUtility.SetDirty(datos);
        }

        // Reflector: torre de 6 m, plataforma, lampara (Luminaria + SpotLight) y operador.
        static ReflectorVigia Reflector(Transform e, Transform en, string nombre, float x, float z, Material mat, Vector3 haciaPunto, List<Soldier> arriba)
        {
            var raizR = new GameObject(nombre);
            raizR.transform.SetParent(e, false);
            raizR.transform.position = new Vector3(x, 0f, z);
            const float h = 6f;
            var d = new Vector3(haciaPunto.x - x, 0f, haciaPunto.z - z).normalized;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Cubo(raizR.transform, "Reflector_Pata", x, z, 1.4f, h, 1.4f, mat);
            Decor(raizR.transform, "Reflector_Plataforma", x, z, 3.4f, 0.4f, 3.4f, Techo, 0f, h);
            Decor(raizR.transform, "Reflector_Baranda_A", x, z - 1.6f, 3.4f, 0.55f, 0.15f, mat, 0f, h + 0.4f);
            Decor(raizR.transform, "Reflector_Baranda_B", x, z + 1.6f, 3.4f, 0.55f, 0.15f, mat, 0f, h + 0.4f);
            Decor(raizR.transform, "Reflector_Baranda_C", x - 1.6f, z, 0.15f, 0.55f, 3.4f, mat, 0f, h + 0.4f);
            Decor(raizR.transform, "Reflector_Baranda_D", x + 1.6f, z, 0.15f, 0.55f, 3.4f, mat, 0f, h + 0.4f);
            // Brazo y lampara al borde de la plataforma, del lado hacia donde mira.
            var pos = new Vector3(x, h + 1.15f, z) + d * 1.2f;
            Decor(raizR.transform, "Reflector_Brazo", x + d.x * 0.6f, z + d.z * 0.6f, 0.2f, 0.9f, 0.2f, MetalOsc, 0f, h + 0.4f);
            // #104: faro de 1,4 m (antes 0,8): carcasa metalica, aro claro y lente emisiva grande, visible de lejos de noche.
            var lampara = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lampara.name = "Lampara";
            lampara.transform.SetParent(raizR.transform, false);
            lampara.transform.position = pos;
            lampara.transform.rotation = Quaternion.Euler(14f, yaw, 0f);
            lampara.transform.localScale = new Vector3(1.4f, 1.4f, 1.1f);
            lampara.GetComponent<MeshRenderer>().sharedMaterial = MetalOsc;
            var aro = GameObject.CreatePrimitive(PrimitiveType.Cube);
            aro.name = "Aro";
            aro.transform.SetParent(lampara.transform, false);
            aro.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            aro.transform.localScale = new Vector3(0.95f, 0.95f, 0.09f);
            aro.GetComponent<MeshRenderer>().sharedMaterial = MetalClaro;
            Object.DestroyImmediate(aro.GetComponent<BoxCollider>());
            var lente = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lente.name = "Lente";
            lente.transform.SetParent(lampara.transform, false);
            lente.transform.localPosition = new Vector3(0f, 0f, 0.535f);
            lente.transform.localScale = new Vector3(0.78f, 0.78f, 0.06f);
            lente.GetComponent<MeshRenderer>().sharedMaterial = Lampara;
            Object.DestroyImmediate(lente.GetComponent<BoxCollider>());
            var luz = lampara.AddComponent<Light>();
            luz.type = LightType.Spot; luz.range = 45f; luz.spotAngle = 18f; luz.innerSpotAngle = 12f; luz.intensity = 9f;
            luz.color = new Color(1f, 0.96f, 0.84f); luz.shadows = LightShadows.Hard;   // bug #138/#285: la luz no pasa a traves de obstaculos (sombra dura, resolucion alta, bias bajo)
            luz.shadowNearPlane = 0.5f; luz.shadowBias = 0.05f; luz.shadowNormalBias = 0.2f;
            SombraDeReflector(luz);
            lampara.AddComponent<Luminaria>().Health = 1;
            var r = raizR.AddComponent<ReflectorVigia>();
            float lado = x < 0f ? 4f : -4f;
            var op = TiradorEnSuelo(en, "Enemigo_Operador_" + nombre.Substring(nombre.Length - 2), x + lado, z, yaw);
            r.operador = op; r.cabeza = lampara.transform; r.yawBase = yaw;
            r.pies = new Vector3(x - d.x * 0.7f, h + 0.4f, z - d.z * 0.7f);
            arriba.Add(op);
            return r;
        }
    }
}
