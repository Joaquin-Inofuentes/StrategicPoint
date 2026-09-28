using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Hornea en la escena el icono de minimapa de cada soldado/vehiculo YA
    // colocado a mano (por el level builder), para que el minimapa se vea
    // en el editor (sin Play) igual que en partida real -- mismo criterio
    // que WeaponStatusUiPipeline/RosterUiPipeline (items 1-3 de este menu).
    //
    // Antes de esto, MinimapIcon.Spawn() solo se llamaba desde
    // UnitLocatorCylinder cuando Application.isPlaying (ver el bloque
    // "BUG REAL" ahi mismo) -- en el editor el minimapa quedaba
    // completamente vacio hasta apretar Play.
    public static class MinimapIconBakePipeline
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Gameplay.unity";
        const string ContainerName = "UI_World";
        const float RadioSoldado = 3.0f;
        const float RadioVehiculo = 2.4f;

        [MenuItem("Strategic Point/UI/4. Hornear iconos de minimapa (escuadra + enemigos + vehiculos)")]
        public static void Hornear()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.Contains("SC_Gameplay"))
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            int layerMinimapa = LayerMask.NameToLayer("Minimap");
            if (layerMinimapa < 0) layerMinimapa = 8;

            var contenedor = GameObject.Find(ContainerName);
            if (contenedor == null)
            {
                Debug.LogError($"[MinimapIconBakePipeline] No se encontro '{ContainerName}' en la escena.");
                return;
            }

            // Rehorneado completo y no incremental: mas simple y sin riesgo
            // de dejar iconos viejos/rotos a medio camino si esto corre mas
            // de una vez (por ejemplo, tras agregar un enemigo nuevo). Barre
            // TODA la escena, no solo UI_World, porque los de vehiculo
            // quedan colgados del vehiculo mismo (ver mas abajo).
            int limpiados = LimpiarTodosLosIconos();
            int soldadosCreados = 0, vehiculosCreados = 0;

            foreach (var soldier in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var color = soldier.Team == TeamId.Enemy ? DiamondGizmo.ColorEnemigo : DiamondGizmo.ColorAliado;
                var icon = MinimapIcon.Spawn(soldier.transform, color, layerMinimapa, RadioSoldado);
                icon.transform.SetParent(contenedor.transform, true);
                RehacerDeteccion(icon);
                soldadosCreados++;
            }

            foreach (var vehicle in Object.FindObjectsByType<Vehicle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var icon = MinimapIcon.Spawn(vehicle.transform, Color.gray, layerMinimapa, RadioVehiculo);
                // NO como hijo del vehiculo: Vehicle.RefreshOccupancyColor
                // usa GetComponentsInChildren<Renderer>() para re-tintar el
                // chasis y agarraria tambien este renderer (BUG REAL
                // encontrado probando esto -- NullReferenceException en
                // Vehicle.cs:250 al subir/bajar de un vehiculo). Bajo
                // UI_World como los demas; Vehicle.cs encuentra "su" icono
                // por Target, no por jerarquia (MinimapIcon.BuscarPorTarget).
                icon.transform.SetParent(contenedor.transform, true);
                RehacerDeteccion(icon);
                vehiculosCreados++;
            }

            EditorUtility.SetDirty(contenedor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MinimapIconBakePipeline] {soldadosCreados} iconos de soldado, {vehiculosCreados} de vehiculo ({limpiados} viejos reemplazados). Escena guardada.");
        }

        // Spawn() hace AddComponent<MinimapIcon>() y RECIEN DESPUES asigna
        // Target (ver el comentario en MinimapIcon.Target) -- para cuando
        // el setter llega, OnEnable ya corrio con Target en null y
        // DetectarTarget() no encontro nada. Apagar/prender el icono
        // vuelve a disparar OnEnable, esta vez con Target ya puesto, y
        // arma color/forma/cono correctamente sin duplicar codigo.
        static void RehacerDeteccion(MinimapIcon icon)
        {
            icon.gameObject.SetActive(false);
            icon.gameObject.SetActive(true);
        }

        static int LimpiarTodosLosIconos()
        {
            var iconos = Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var icon in iconos) Object.DestroyImmediate(icon.gameObject);
            return iconos.Length;
        }
    }
}
