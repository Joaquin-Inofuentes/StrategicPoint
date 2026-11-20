using System.Collections;
using SP.Operacion;
using UnityEngine;

namespace SP.Core
{
    // Ronda 15: capturas del atlas (ANTES con la build del 18/11 14:44, DESPUES con la actual) de lo que se ve en pantalla de cada pedido.
    public sealed partial class CapturasDeBuild
    {
        public static readonly string[] IdsR15 =
        {
            "r15_minimapa", "r15_calle_despues", "r15_calle_antes", "r15_calle_tanque_despues", "r15_calle_tanque_antes", "r15_solapado_1_despues", "r15_solapado_1_antes", "r15_solapado_2_despues", "r15_solapado_2_antes", "r15_arbustos",
            "r15_spawn_cuartel_este", "r15_spawn_cuartel_oeste", "r15_spawn_trinchera", "r15_terminal_progreso",
            "r15_ui_normal", "r15_ui_interaccion",
        };

        IEnumerator R15()
        {
            Saltar(4); yield return Espera(3f);
            // calle: DESPUES (asfalto desde el tanque) y ANTES (se reactivan los tramos apagados: calle de salida, T, veredas, rayas)
            var apagados = new System.Collections.Generic.List<GameObject>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.gameObject.activeSelf || !t.gameObject.scene.IsValid()) continue;
                var n = t.name; var p = t.position;
                bool calle = n == "R13_T" || n == "Calle_Salida" || n == "Amarillo_x-40_z-40" || n == "Amarillo_x0_z-40" || n == "Amarillo_x0_z0";
                bool solapado = (n == "P_Env_Barricada_Sacos" && Mathf.Abs(p.x - 36f) < 0.6f && Mathf.Abs(p.z - 75f) < 0.6f) || (n == "P_Env_Caja_Madera" && Mathf.Abs(p.x - 177f) < 0.6f && Mathf.Abs(p.z - 281f) < 0.6f);
                if (calle || solapado) apagados.Add(t.gameObject);
            }
            Dato("apagados encontrados para el ANTES: " + apagados.Count);
            var vC = new Vector3(-14.5f, 3.8f, -31.3f); var mC = new Vector3(10f, 0f, -20f);
            var vT = new Vector3(0f, 3.5f, 14f); var mT = new Vector3(0f, 0f, 60f);
            var vS1 = new Vector3(30f, 3f, 68f); var mS1 = new Vector3(36f, 1f, 75.5f);
            var vS2 = new Vector3(172f, 3f, 275f); var mS2 = new Vector3(178f, 0.8f, 280.5f);
            yield return Foto("r15_calle_despues", vC, mC, 60f, false);
            yield return Foto("r15_calle_tanque_despues", vT, mT, 60f, false);
            yield return Foto("r15_solapado_1_despues", vS1, mS1, 55f, false);
            yield return Foto("r15_solapado_2_despues", vS2, mS2, 55f, false);
            foreach (var g in apagados) g.SetActive(true);
            yield return null;
            yield return Foto("r15_calle_antes", vC, mC, 60f, false);
            yield return Foto("r15_calle_tanque_antes", vT, mT, 60f, false);
            yield return Foto("r15_solapado_1_antes", vS1, mS1, 55f, false);
            yield return Foto("r15_solapado_2_antes", vS2, mS2, 55f, false);
            foreach (var g in apagados) g.SetActive(false);
            // arbustos sin collider: vista rasante donde se agrupan
            yield return Foto("r15_arbustos", new Vector3(83f, 1.2f, 196f), new Vector3(83f, 0.6f, 203f), 60f, false);
            // spawns del cuartel y la trinchera (zonas donde nacen los enemigos)
            yield return Foto("r15_spawn_cuartel_este", new Vector3(30f, 28f, -150f), new Vector3(44f, 0f, -180f), 60f, false);
            yield return Foto("r15_spawn_cuartel_oeste", new Vector3(-30f, 28f, -150f), new Vector3(-46f, 0f, -178f), 60f, false);
            yield return Foto("r15_spawn_trinchera", new Vector3(0f, 28f, -165f), new Vector3(0f, 0f, -197f), 60f, false);
            // minimapa y HUD de la partida (captura real de pantalla)
            yield return FotoJuego("r15_minimapa");
            yield return FotoJuego("r15_ui_normal");
            for (int k = 0; k < 20; k++) { UiDeInteraccion.Marcar(); yield return Espera(0.05f); }
            yield return FotoJuego("r15_ui_interaccion");
            // computadora con hackeo a medias: la luz del panel late y el indicador avanza
            var d = OperacionDirector.Instancia; var jug = Yo();
            if (jug != null) { jug.transform.position = new Vector3(308.7f, 13.9f, 270.2f); Physics.SyncTransforms(); }
            yield return Espera(2f);
            if (d != null && d.TerminalDeCeldas == null) typeof(OperacionDirector).GetMethod("CrearTerminalDeCeldas", Priv)?.Invoke(d, null);
            Physics.SyncTransforms(); yield return Espera(0.5f);
            var tt = d != null ? d.TerminalDeCeldas : null;
            if (tt != null)
            {
                var term = tt.GetComponentInParent<OperacionTerminal>() ?? tt.GetComponent<OperacionTerminal>();
                if (term != null) { term.Avanzar(0.5f * term.DuracionEfectiva); }
                yield return Foto("r15_terminal_progreso", tt.position + tt.forward * 3.2f + Vector3.up * 1.7f, tt.position + Vector3.up * 1.0f, 60f, false);
            }
        }
    }
}
