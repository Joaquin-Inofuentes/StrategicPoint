using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Mision;

namespace SP.EditorTools
{
    // Arma el camino de camara de la cinematica de apertura en SC_Gameplay: un GameObject raiz
    // "CinematicaDeIntro" (mismo nivel que "Mision", "Enemies", "Waypoints" -- ver
    // SP.Core.RaicesDeEscena) con un CinematicaIntroPath y 4 waypoints por defecto (aparicion,
    // objetivo/centro, rehen, helicoptero). A DIFERENCIA de MisionBuilder.Construir(), esto es
    // idempotente sin destruir nada existente: si "CinematicaDeIntro" ya esta armado, no lo toca --
    // las posiciones de los waypoints se retocan a mano en el Editor despues (por eso el pedido
    // explicito de "decime el nombre del gameobject padre para reubicarlos mejor luego": es este,
    // "CinematicaDeIntro").
    public static class CinematicaIntroBuilder
    {
        [MenuItem("Strategic Point/Mision/Construir cinematica de apertura (SC_Gameplay)")]
        public static void Construir()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SC_Gameplay") { Debug.LogWarning("[CinematicaIntro] Abri SC_Gameplay antes (escena activa: " + scene.name + ")."); return; }

            var raiz = GameObject.Find("CinematicaDeIntro");
            if (raiz != null)
            {
                var pathExistente = raiz.GetComponent<CinematicaIntroPath>();
                if (pathExistente != null) pathExistente.Reconectar();
                var mdExistente = GameObject.Find("Mision")?.GetComponent<MisionDirector>();
                if (mdExistente != null) AsignarRuta(mdExistente, pathExistente);
                Debug.Log("[CinematicaIntro] 'CinematicaDeIntro' ya existe: se reconecto sin tocar posiciones.");
                return;
            }

            raiz = new GameObject("CinematicaDeIntro");
            var path = raiz.AddComponent<CinematicaIntroPath>();

            var squad = GameObject.Find("PlayerSquad");
            Vector3 origenJugador = squad != null ? squad.transform.position : new Vector3(0f, 1.6f, -20f);
            // Mismos valores por defecto que MisionDirector (Plaza/RefugioDelCivil/Helipuerto): si
            // se cambian ahi, conviene reubicar estos puntos a mano tambien (no se leen en vivo del
            // MisionDirector porque este builder puede correr ANTES de que exista "Mision").
            Vector3 plaza = new Vector3(4f, 0f, 119f);
            Vector3 refugioCivil = new Vector3(-2f, 0f, 124f);
            Vector3 helipuerto = new Vector3(-26f, 0f, -8f);

            var w1 = CrearWaypoint(raiz.transform, "1_Aparicion",
                origenJugador + new Vector3(-6f, 3.2f, -8f), origenJugador,
                0.1f, 2.5f, "");
            var w2 = CrearWaypoint(raiz.transform, "2_ObjetivoCentro",
                plaza + new Vector3(-14f, 9f, -18f), plaza,
                3f, 3.5f, "Debes rescatar al rehen y luego volver al helicoptero");
            var w3 = CrearWaypoint(raiz.transform, "3_Rehen",
                refugioCivil + new Vector3(4f, 2.2f, 4f), refugioCivil,
                2.5f, 2.2f, "");
            // Offset alto y alejado del cluster de arboles del perimetro (pedido explicito: "en la
            // toma final esta obstruido por arboles"): el offset anterior (10,5,-12) quedaba a
            // 7-9 m de varios SM_Env_ArbolA/B, casi a la altura de su copa (~y=3.2-4.0) -- la
            // camara terminaba metida entre las copas. Este offset verificado en vivo no tiene
            // ningun arbol a menos de 9 m de la camara.
            var w4 = CrearWaypoint(raiz.transform, "4_Helicoptero",
                helipuerto + new Vector3(12f, 14f, 4f), helipuerto,
                3f, 2f, "");

            path.Waypoints = new[] { w1, w2, w3, w4 };
            EditorUtility.SetDirty(path);

            var md = GameObject.Find("Mision")?.GetComponent<MisionDirector>();
            if (md != null) AsignarRuta(md, path);
            else Debug.LogWarning("[CinematicaIntro] No encontre 'Mision' en la escena: arma primero MisionDirector (Strategic Point/Mision/Construir mision de rescate) y volve a correr esto para conectarla.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CinematicaIntro] 'CinematicaDeIntro' armado con 4 waypoints por defecto. Reubicalos a mano en la Scene view y despues usa 'Reconectar waypoints' (menu de contexto del componente) si agregas/borras/reordenas hijos.");
        }

        static CinematicaWaypoint CrearWaypoint(Transform padre, string nombre, Vector3 posicion, Vector3 mirarPunto, float segundosParaLlegar, float segundosDeEspera, string subtitulo)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, true);
            go.transform.position = posicion;
            var w = go.AddComponent<CinematicaWaypoint>();
            w.mirarPunto = mirarPunto;
            w.segundosParaLlegar = segundosParaLlegar;
            w.segundosDeEspera = segundosDeEspera;
            w.subtitulo = subtitulo;
            return w;
        }

        static void AsignarRuta(MisionDirector md, CinematicaIntroPath path)
        {
            var so = new SerializedObject(md);
            so.FindProperty("rutaDeIntro").objectReferenceValue = path;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
