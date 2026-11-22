using System.Collections;
using System.Text;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 18: el arranque queda limpio de arboles, el muro sur no existe y detras hay un bosque poblado; el valle trae decoracion extra.
    public static partial class ChecksBugs065
    {
        static IEnumerator Bug992a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(2f)) yield return x;
            var centro = BosqueDeInicioR18.Arranque;
            int enClaro = 0, bosque = 0, murosSur = 0, deco = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                bool veg = (t.name.StartsWith("P_Env_Arbol") || t.name.StartsWith("P_Env_Arbusto")) && UnityEditor.PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject);
                if (veg && (new Vector2(t.position.x, t.position.z) - centro).magnitude < BosqueDeInicioR18.RadioLimpio && t.gameObject.activeInHierarchy) enClaro++;
                if (t.parent != null && t.parent.name == "R18_BosqueSur") bosque++;
                if (t.parent != null && t.parent.name == "R18_DecoradoDelValle") deco++;
                if ((t.name.StartsWith("P_Mod_Muro") || t.name.StartsWith("P_Mod_Losa") || t.name.StartsWith("P_Mod_Valla")) && t.position.z < -384f && t.position.z > -396f && Mathf.Abs(t.position.x) < 200f && t.gameObject.activeInHierarchy) murosSur++;   // las esquinas de los muros este y oeste no cuentan
            }
            W8Ok(ref ok, enClaro == 0, sb, $"claro de arranque: {enClaro} arboles/arbustos a menos de {BosqueDeInicioR18.RadioLimpio:0} m (pide 0)");
            W8Ok(ref ok, bosque >= 400, sb, $"bosque sur: {bosque} plantas (pide >= 400)");
            W8Ok(ref ok, murosSur == 0, sb, $"muro sur: {murosSur} modulos activos (pide 0)");
            W8Ok(ref ok, deco >= 150, sb, $"decoracion del valle: {deco} piezas (pide >= 150)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
