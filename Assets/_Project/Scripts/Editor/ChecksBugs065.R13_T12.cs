using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T12: sin camion, tambores ni generador junto al inicio; la 1.a puerta se abre solo con la carga del porton norte, habilitada desde t=0.
    public static partial class ChecksBugs065
    {
        static readonly Regex reCamionTamborGenerador = new Regex(@"^(Camion|Tambor|Generador)", RegexOptions.Compiled);

        // Renderers activos y encendidos cuyo nombre (o el de algun ancestro) empieza con Camion/Tambor/Generador, en z < -200 o a menos de 60 m del punto dado.
        static int ContarCamionTamborGenerador(Vector3 spawn, StringBuilder lista)
        {
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var p = r.bounds.center;
                bool cerca = p.z < -200f || Vector2.Distance(new Vector2(p.x, p.z), new Vector2(spawn.x, spawn.z)) < 60f;
                if (!cerca) continue;
                string nombre = null;
                for (var t = r.transform; t != null; t = t.parent) if (reCamionTamborGenerador.IsMatch(t.name)) { nombre = t.name; break; }
                if (nombre == null) continue;
                n++; if (lista != null && lista.Length < 300) lista.Append(nombre).Append(' ');
            }
            return n;
        }

        // ---------------------------------------------------------------- 981a inicio sin camion ni gasolina; la carga abre el porton
        static IEnumerator Bug981a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = Poseido();
            if (d == null || yo == null || d.cargaPorton == null) { Fin("FALLO faltan director/jugador/carga del porton"); yield break; }
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                var lista = new StringBuilder();
                int n = ContarCamionTamborGenerador(yo.transform.position, lista);
                W8Ok(ref ok, n == 0, sb, $"{n} renderers Camion/Tambor/Generador en z < -200 o a menos de 60 m del inicio ({lista})");
                W8Ok(ref ok, d.cargaPorton.Habilitado, sb, "la carga del porton esta habilitada desde t=0");
                W8Ok(ref ok, d.Fase == FaseOperacion.Infiltrar && d.portonCuartel != null && d.portonCuartel.activeSelf, sb, $"fase Infiltrar con el porton cerrado ({d.Fase})");
                // plantar con el Asalto (mantener [E]) y esperar la mecha
                var vega = Escuadrista(RoleType.Assault);
                if (vega == null) { W8Ok(ref ok, false, sb, "hay un soldado de Asalto"); Fin("FALLO " + sb); yield break; }
                Poseer(vega); yield return null;
                Junto(vega, d.cargaPorton.transform.position, 2f);
                // flujo de Bug082c: plantar sin los 3,5 s de [E] (el mantener [E] real se cubre en Wp5a/Wp5b) y acortar la mecha
                d.cargaPorton.PlantarPorPrueba(vega);
                d.cargaPorton.AcortarMecha(3f);
                float t0 = Time.realtimeSinceStartup;
                W8Ok(ref ok, d.cargaPorton.Situacion != CargaExplosiva.Estado.Intacta, sb, $"el Asalto planta la carga ({d.cargaPorton.Situacion})");
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < d.cargaPorton.segundosDeMecha + 12f && d.Fase == FaseOperacion.Infiltrar) yield return null;
                foreach (var x in Esperar(1.5f)) yield return x;
                W8Ok(ref ok, d.cargaPorton.EstaVolada && d.Fase == FaseOperacion.Puestos, sb, $"al terminar la mecha la fase es Puestos (fase {d.Fase}, volada {d.cargaPorton.EstaVolada})");
                W8Ok(ref ok, d.portonCuartel == null || !d.portonCuartel.activeSelf, sb, "Porton_Cuartel inactivo");
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
