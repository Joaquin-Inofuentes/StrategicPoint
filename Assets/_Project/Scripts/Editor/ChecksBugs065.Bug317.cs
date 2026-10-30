using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Bug #317 ("ese no me dispara, quiero que me dispare a mi o a mis aliados"): en la captura la escuadra esta a ~70 m del francotirador de la torre
    // TF1 y a ~82 m del artillero TV1 (este no tiene linea de tiro: paredes). Guardia: el francotirador de la torre SI responde a la escuadra a 70 m.
    public static partial class ChecksBugs065
    {
        static IEnumerator Bug317u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            Time.timeScale = 1f;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var kes = Escuadrista(RoleType.Flanker);
            FrancotiradorEnTorre francotirador = null; ArtilleroDeTorreta artillero = null;   // solo informativo: desde la posicion del reporte no tiene linea de tiro (paredes)
            foreach (var t in Object.FindObjectsByType<TiradorDeTorre>(FindObjectsInactive.Exclude))
            {
                if (t.soldado == null) continue;
                if (t.soldado.name == "Enemigo_Francotirador_TF1") francotirador = t as FrancotiradorEnTorre;
                if (t.soldado.name == "Enemigo_Artillero_TV1") artillero = t as ArtilleroDeTorreta;
            }
            if (kes == null || francotirador == null) { ModoDios.Poner(diosAntes); Fin($"FALLO faltan piezas (kes={kes != null}, TF1={francotirador != null}, TV1={artillero != null})"); yield break; }
            try
            {
                foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s.gameObject.activeInHierarchy) { s.transform.position = new Vector3(-3f, s.transform.position.y, -341.6f); ApoyoEnElPiso.Apoyar(s.transform); }
                Poseer(kes); yield return null;
                float dF = Vector2.Distance(new Vector2(kes.transform.position.x, kes.transform.position.z), new Vector2(francotirador.soldado.transform.position.x, francotirador.soldado.transform.position.z));
                float dA = artillero != null ? Vector2.Distance(new Vector2(kes.transform.position.x, kes.transform.position.z), new Vector2(artillero.soldado.transform.position.x, artillero.soldado.transform.position.z)) : -1f;
                int antesF = francotirador.Disparos, antesA = artillero != null ? artillero.Disparos : 0;
                float t0 = Time.realtimeSinceStartup;
                while (francotirador.Disparos == antesF && Time.realtimeSinceStartup - t0 < 20f) yield return null;
                sb.Append($"distancias: francotirador {dF:0} m (alcance {FrancotiradorEnTorre.AlcanceDelTiro:0}), artillero {dA:0} m (alcance {ArtilleroDeTorreta.AlcanceDeLaTorreta:0}); ");
                W8Ok(ref ok, francotirador.Disparos > antesF, sb, $"el francotirador de la torre TF1 dispara a la escuadra (disparos {francotirador.Disparos - antesF}, objetivo={(francotirador.Objetivo != null ? francotirador.Objetivo.name : "ninguno")})");
                foreach (var x in CapturarPantalla("v2_317_despues.png")) yield return x;
            }
            finally { Poseer(kes); ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }
    }
}
