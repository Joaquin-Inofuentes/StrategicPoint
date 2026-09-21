using UnityEngine;
using SP.Core;
using SP.Tutorial;

namespace SP.EditorTools
{
    // FASE 22: eliminacion de los Find de runtime. Los servicios unicos exponen "Activo", el tutorial lee un catalogo
    // serializado, y cada estatico nuevo se limpia con ReinicioDeEstaticos.
    public static partial class HeadlessTestRunner
    {
        static void RunPhase22()
        {
            TestLog.Phase("FASE 22 - Sin Find de runtime: catalogo del tutorial, servicios Activo, reinicio de estaticos");

            // El catalogo de SC_Tutorial tiene todas sus referencias (un renombre ya no rompe el tutorial en silencio).
            const string rutaTutorial = "Assets/_Project/Scenes/SC_Tutorial.unity";
            var escena = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(rutaTutorial, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            CatalogoDelTutorial catalogo = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                catalogo = raiz.GetComponentInChildren<CatalogoDelTutorial>(true);
                if (catalogo != null) break;
            }
            Check("SC_Tutorial trae el CatalogoDelTutorial", catalogo != null);
            if (catalogo != null)
            {
                var vacios = catalogo.CamposVacios();
                Check("El catalogo del tutorial no tiene referencias vacias: " + string.Join(", ", vacios), vacios.Count == 0);
                Check("El catalogo apunta a objetos de su propia escena", catalogo.EnemigoEstatico != null && catalogo.EnemigoEstatico.scene == escena && catalogo.Meta.scene == escena);
                Check("Los nombres del catalogo siguen siendo Tut_*", catalogo.Pared != null && catalogo.Pared.name == "Tut_Pared" && catalogo.Destruible.name == "Tut_Destruible");
            }
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(escena, true);

            // Los servicios de la escena armada por la suite estan registrados como Activo (Fase 18 ya verifico que el reinicio los limpia).
            Check("PlayerInputDriver.Activo apunta al driver de la escena", SP.Player.PlayerInputDriver.Activo != null);
            Check("PlayerBrain.Activo apunta al cerebro de la escena", SP.Player.PlayerBrain.Activo != null);
            Check("ProjectilePool.Activo apunta al pool de la escena", SP.Combat.ProjectilePool.Activo != null);

            // El reinicio de estaticos limpia el catalogo aunque el componente siga vivo.
            var probe = new GameObject("CatalogoPrueba").AddComponent<CatalogoDelTutorial>();
            probe.RegistrarActivo();
            Check("El catalogo se registra como Activo", CatalogoDelTutorial.Activo == probe);
            ReinicioDeEstaticos.Restablecer();
            Check("Reiniciar estaticos limpia el catalogo Activo", CatalogoDelTutorial.Activo == null);
            Object.DestroyImmediate(probe.gameObject);
            Check("Sin catalogo, la lista de faltantes de uno vacio los enumera todos", new GameObject("CatalogoVacio").AddComponent<CatalogoDelTutorial>().CamposVacios().Count == 6);
            foreach (var o in Object.FindObjectsByType<CatalogoDelTutorial>(FindObjectsInactive.Include)) Object.DestroyImmediate(o.gameObject);
        }
    }
}
