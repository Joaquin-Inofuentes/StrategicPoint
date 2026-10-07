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
    // Zona 3: centro de datos, computadora, oleadas y torretas de la salida norte (OperacionDirector.Centro).
    public static partial class OperacionBuilder
    {
        // ---------------------------------------------------------------
        // 3. Centro de datos
        // ---------------------------------------------------------------
        static OperacionTerminal CentroDeDatos(Transform padre, out OleadaDeReserva[] oleadas)
        {
            var g = Grupo(padre, "3_CentroDeDatos");
            var est = Grupo(g, "Estructuras");
            // Patio sur y patios laterales (adentro del valle).
            Pintura(est, "Patio_Sur", 0f, -97f, 120f, 36f, CementoOsc, 0f, 0f, 0.06f);
            Pintura(est, "Patio_Oeste", -44f, -60f, 34f, 42f, CementoOsc, 0f, 0f, 0.06f);
            Pintura(est, "Patio_Este", 44f, -60f, 34f, 42f, CementoOsc, 0f, 0f, 0.06f);
            // Cierre del valle a los costados del edificio (z=-40) y paredes del edificio (sin techo: vista libre).
            MuroX(est, "Cierre_Norte_O", -60f, -26f, -40f, 5f, 2f, CementoOsc);
            MuroX(est, "Cierre_Norte_E", 26f, 60f, -40f, 5f, 2f, CementoOsc);
            MuroX(est, "CD_Sur_O", -26f, -4f, -80f, 5f, 1.5f, Datos);
            MuroX(est, "CD_Sur_E", 4f, 26f, -80f, 5f, 1.5f, Datos);
            MuroX(est, "CD_Norte_O", -26f, -4f, -40f, 5f, 1.5f, Datos);
            MuroX(est, "CD_Norte_E", 4f, 26f, -40f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Oeste_S", -26f, -80f, -62f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Oeste_N", -26f, -56f, -40f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Este_S", 26f, -80f, -62f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Este_N", 26f, -56f, -40f, 5f, 1.5f, Datos);
            Pintura(est, "CD_Piso", 0f, -60f, 52f, 40f, Mat("PisoDatos", new Color(0.52f, 0.58f, 0.66f)), 0f, 0.0f, 0.1f);
            Rotulo(est, "CENTRO DE DATOS", 0f, 8.2f, -80.9f, 0f, 2f, Color.white, AzulSenal, 22f, 2.6f);
            Rotulo(est, "ENTRADA", 0f, 5.9f, -80.9f, 0f, 1f, Color.white, VerdeSenal, 6f, 1.2f);
            Rotulo(est, "SALIDA", 0f, 5.9f, -39.1f, 180f, 1f, Color.white, VerdeSenal, 6f, 1.2f);
            Rotulo(est, "SALIDA ^", 0f, 5.9f, -40.9f, 0f, 1f, Color.white, VerdeSenal, 6f, 1.2f);
            // Racks de servidores (cobertura) con luces de estado.
            float[][] racks = { new[]{-9f,-71f,1.4f,7f}, new[]{9f,-71f,1.4f,7f}, new[]{-15f,-58f,7f,1.4f}, new[]{15f,-58f,7f,1.4f}, new[]{-19f,-46f,1.4f,5f}, new[]{19f,-46f,1.4f,5f} };
            for (int i = 0; i < racks.Length; i++)
            {
                var r = Cubo(est, "Rack_" + (i + 1), racks[i][0], racks[i][1], racks[i][2], 2.8f, racks[i][3], Rack, 0f, 0f, true, 600);
                Decor(est, "Rack_Luz_" + (i + 1), racks[i][0], racks[i][1], racks[i][2] + 0.04f, 0.18f, racks[i][3] + 0.04f, RackLuz, 0f, 2.1f);
            }
            LuzPuntual(est, "Luz_CD_1", -10f, 5.5f, -56f, new Color(0.7f, 0.9f, 1f), 3.5f, 22f);
            LuzPuntual(est, "Luz_CD_2", 10f, 5.5f, -56f, new Color(0.7f, 0.9f, 1f), 3.5f, 22f);
            LuzPuntual(est, "Luz_CD_Sur", 0f, 6f, -90f, new Color(1f, 0.93f, 0.8f), 3f, 24f);

            // La computadora: mesa + monitor emisivo + barra de progreso.
            var pc = Grupo(g, "Computadora");
            var mesa = Solido(pc, "Mesa", 0f, -45.2f, 4.4f, 1.1f, 1.6f, CementoOsc);
            Solido(pc, "Torre_Servidor", -3.4f, -45.2f, 0.9f, 1.6f, 0.9f, Rack);
            var monitor = Decor(pc, "Monitor", 0f, -44.6f, 3.2f, 1.5f, 0.15f, Pantalla, 0f, 1.1f);
            var barra = Decor(pc, "BarraProgreso", 0f, -45.9f, 3.2f, 0.12f, 0.2f, Mat("Progreso", new Color(0.35f, 1f, 0.5f), 1.4f), 0f, 1.1f);
            Rotulo(pc, "COMPUTADORA\n[E] INTERACTUAR 30 s", 0f, 7.2f, -45f, 0f, 0.8f, Color.white, Negro, 6.6f, 1.6f);
            LuzPuntual(pc, "Luz_PC", 0f, 3.2f, -50f, new Color(0.4f, 0.8f, 1f), 3.5f, 14f);
            var term = mesa.AddComponent<OperacionTerminal>();
            term.titulo = "COMPUTADORA";
            term.duracion = 30f;
            term.soloUnRol = false;
            term.radio = 3.6f;
            term.modoSostener = false;
            term.decae = false;
            term.indicador = barra.transform;
            term.luz = monitor.GetComponent<MeshRenderer>();

            // Oleadas de reserva (apagadas hasta que empieza el hackeo).
            var res = Grupo(rReservas, "CentroDeDatos");
            var w1 = new List<Soldier>(); var w2 = new List<Soldier>(); var w3 = new List<Soldier>();
            for (int i = 0; i < 5; i++) w1.Add(Enemigo("Reserva_CD1_" + (i + 1), -16f + i * 8f, -104f, 0f, res, false, 3f, true));
            for (int i = 0; i < 5; i++) w2.Add(Enemigo("Reserva_CD2_" + (i + 1), -52f + (i % 2) * 6f, -50f - i * 5f, 90f, res, false, 3f, true));
            for (int i = 0; i < 6; i++) w3.Add(Enemigo("Reserva_CD3_" + (i + 1), 52f - (i % 2) * 6f, -50f - i * 4.5f, 270f, res, false, 3f, true));
            oleadas = new[]
            {
                new OleadaDeReserva { segundo = 0f, aviso = "OLEADA 1/3 · POR LA ENTRADA SUR", soldados = w1.ToArray(), destino = DestinoPc(pc) },
                new OleadaDeReserva { segundo = 10f, aviso = "OLEADA 2/3 · POR EL PATIO OESTE", soldados = w2.ToArray(), destino = DestinoPc(pc) },
                new OleadaDeReserva { segundo = 20f, aviso = "OLEADA 3/3 · POR EL PATIO ESTE", soldados = w3.ToArray(), destino = DestinoPc(pc) },
            };
            TorretasDelCentro(g, term.transform.position.x, term.transform.position.z);
            return term;
        }

        // Bug #043: dos ametralladoras fijas (emplazamiento del arte) a los costados de la salida norte del centro de datos, mirando
        // a la ruta, para parar las camionetas que bajan mientras se hackea. Antes las ponia a mano un script suelto y un rebuild las
        // perdia. Posicion medida de la escena: x = +-8,5 y 10,5 m al norte de la computadora, giradas 8 grados hacia la ruta.
        // OperacionDirector.Start les instala TorretaFija; el director las lleva en torretasDelCentro.
        const string PrefabEmplazamientoMg = "Assets/_Project/Prefabs/ArteMundo/P_Env_Emplazamiento_MG.prefab";

        static void TorretasDelCentro(Transform padre, float x0, float zComputadora)
        {
            torretasDelCentro = new GameObject[0];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEmplazamientoMg);
            if (prefab == null) { Debug.LogWarning("[Operacion] Falta " + PrefabEmplazamientoMg + ": el centro de datos queda sin torretas."); return; }
            var cont = Grupo(padre, "TorretasDelCentro");
            var lista = new List<GameObject>();
            foreach (var lado in new[] { -1f, 1f })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cont);
                go.name = "Torreta_Centro_" + (lado < 0f ? "Oeste" : "Este");
                go.transform.position = new Vector3(x0 + lado * 8.5f, 0f, zComputadora + 10.5f);
                go.transform.rotation = Quaternion.Euler(0f, -lado * 8f, 0f);
                lista.Add(go);
            }
            torretasDelCentro = lista.ToArray();
        }

        static Transform DestinoPc(Transform pc)
        {
            var t = new GameObject("Destino_Oleadas").transform;
            t.SetParent(pc, false);
            t.position = new Vector3(0f, 0f, -54f);
            return t;
        }
    }
}
