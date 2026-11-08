using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Ajustes del nivel pedidos en los bugs #369, #375, #382, #387 y #392 (2026-11-08). Se aplican al cargar SC_Operacion* sobre lo que ya
    // esta en la escena (incluye objetos inactivos: los grupos de cada fase arrancan apagados):
    //  - #369: las hojas y jambas de las puertas de acceso del centro de datos no tenian collider -> se las atravesaba.
    //  - #392: se quita la bandera de gala (mastil, tela, franja y su luz) de la azotea de la mansion.
    //  - #375: se quitan las luminarias chicas (faroles de ruta, farolillos y luces de hada).
    //  - #382/#387: las luces decorativas de la mansion y los reflectores bajan de intensidad (x0,6).
    public static class AjustesDeNivelNov08
    {
        public static int ColliderAgregados { get; private set; }
        public static int Apagados { get; private set; }
        public static int LucesAtenuadas { get; private set; }
        public const float FactorDeLuces = 0.6f;

        static readonly HashSet<string> Quitar = new HashSet<string> { "Bandera_Gala", "Bandera_Gala_Franja", "Mastil_Gala", "Luz_Mastil", "Farol_Brazo", "Farol_Luz", "Farol_Poste", "Farolillo", "Luz_Hada" };
        static readonly HashSet<string> LucesBajar = new HashSet<string> { "Luz_Arana", "Luz_Lampara_Pie", "Luz_Aplique", "Reflector_Fachada" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { ColliderAgregados = Apagados = LucesAtenuadas = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribir()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargar;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode modo)
        {
            if (!s.name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            Aplicar();
            // Los grupos de cada fase (ruta, mansion, centro) se arman o se encienden despues: se reaplica cada pocos segundos mientras dure la escena.
            if (vigilante == null) { var go = new GameObject("VigilanteDeAjustesNov08") { hideFlags = HideFlags.DontSave }; vigilante = go.AddComponent<Vigilante>(); }
        }

        static Vigilante vigilante;
        sealed class Vigilante : MonoBehaviour
        {
            float proximo;
            void Update()
            {
                if (Time.unscaledTime < proximo) return;
                proximo = Time.unscaledTime + 3f;
                if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
                Aplicar(false);
            }
        }

        public static void Aplicar(bool registrar = true)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                string n = t.name;
                if (Quitar.Contains(n) && t.gameObject.activeSelf) { t.gameObject.SetActive(false); Apagados++; continue; }
                if ((n == "Fuente_Chorro" || n == "Fuente_Gota") && t.GetComponent<MarcaChorroAlineado>() == null) AlinearChorro(t);
                if (t.parent != null && t.parent.name == "PuertasDeAcceso" && (n.StartsWith("Hoja_") || n.StartsWith("Jamba_")) && !n.EndsWith("_Franja")
                    && t.GetComponent<Collider>() == null && t.GetComponent<MeshRenderer>() != null)
                {
                    t.gameObject.AddComponent<BoxCollider>(); ColliderAgregados++;
                }
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null || !LucesBajar.Contains(l.name) || l.GetComponent<MarcaLuzAtenuada>() != null) continue;
                l.gameObject.AddComponent<MarcaLuzAtenuada>();
                l.intensity *= FactorDeLuces; LucesAtenuadas++;
            }
            if (registrar) SP.Core.GameLog.Line($"AjustesDeNivelNov08: {ColliderAgregados} colliders, {Apagados} apagados, {LucesAtenuadas} luces atenuadas");
        }

        sealed class MarcaLuzAtenuada : MonoBehaviour { }
        sealed class MarcaChorroAlineado : MonoBehaviour { }

        // #383 ("falta alinear mejor las lineas de agua de la fuente"): los 8 chorros y sus gotas quedan sobre un mismo anillo (2,0 m del eje, entre la
        // columna y el borde del agua), a 45 grados exactos uno del otro y con cada gota centrada sobre su chorro.
        public const float RadioDeChorros = 2.0f;
        static readonly Vector2 EjeDeLaFuente = new Vector2(322f, 215.5f);
        public static int ChorrosAlineados { get; private set; }

        static void AlinearChorro(Transform t)
        {
            var p = t.position;
            var d = new Vector2(p.x - EjeDeLaFuente.x, p.z - EjeDeLaFuente.y);
            if (d.sqrMagnitude < 0.01f || d.magnitude > 4f) return;   // no es el de la fuente de la plaza
            float ang = Mathf.Round(Mathf.Atan2(d.y, d.x) / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
            t.position = new Vector3(EjeDeLaFuente.x + Mathf.Cos(ang) * RadioDeChorros, p.y, EjeDeLaFuente.y + Mathf.Sin(ang) * RadioDeChorros);
            t.rotation = Quaternion.identity;
            t.gameObject.AddComponent<MarcaChorroAlineado>(); ChorrosAlineados++;
        }
    }
}
