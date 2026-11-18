using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;

namespace SP.EditorTools
{
    // Verificacion visual de lo que quedaba sin mirar: (a) [quitado en R13 T12 con el generador], (b) la trazadora de primera persona no se dibuja pegada al lente y si mas alla de 2,2 m.
    public static partial class ChecksBugs065
    {
        static IEnumerator Bug298v()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            Time.timeScale = 1f;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault);
            if (d == null || vega == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            try
            {
                Poseer(vega); yield return null;
                // (a) el cartel de demolicion del generador se quito con el generador (R13 T12); queda la trazadora.

                // (b) trazadora: se dispara de frente y se mira, cuadro a cuadro, cada proyectil contra la camara.
                var drv = PlayerInputDriver.Activo;
                                var cam = CamaraPrincipal.Actual;
                if (drv == null || cam == null) { ok = false; sb.Append("FALLO sin driver o camara; "); }
                else
                {
                    ModoDios.Poner(true);
                    Projectile bala = null;
                    for (int i = 0; i < 30 && bala == null; i++)
                    {
                        drv.Brain.Fire(cam.transform.position + cam.transform.forward * 40f);
                        yield return null;
                        foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude)) { bala = p; break; }
                    }
                    if (bala == null) { ok = false; sb.Append("MAL no salio ninguna bala; "); }
                    else
                    {
                        var r = bala.GetComponent<Renderer>();
                        // se la lleva a 1 m del lente (quieta: sin velocidad) y despues a 6 m, y se mira si se dibuja
                        var rb = bala.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
                        bala.enabled = false;   // quieta: se evalua la regla a mano en cada posicion (a 200 m/s se iria en un cuadro)
                        var regla = typeof(Projectile).GetMethod("OcultarSiPegadaALaCamara", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        bala.transform.position = cam.transform.position + cam.transform.forward * 1f;
                        regla.Invoke(bala, null);
                        bool cercaVisible = r != null && r.enabled;
                        if (bala != null) bala.transform.position = cam.transform.position + cam.transform.forward * 6f;
                        regla.Invoke(bala, null);
                        bool lejosVisible = bala != null && r != null && r.enabled;
                        W8Ok(ref ok, !cercaVisible, sb, $"trazadora a 1 m del lente: dibujada={cercaVisible} (pide no)");
                        W8Ok(ref ok, lejosVisible, sb, $"trazadora a 6 m: dibujada={lejosVisible} (pide si)");
                    }
                }
            }
            finally { Poseer(vega); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }
    }
}
