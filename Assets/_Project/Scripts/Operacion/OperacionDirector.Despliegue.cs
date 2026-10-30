using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Operacion
{
    // Bug #321: durante la fuga en el tanque llegan helicopteros enemigos que se paran sobre la ruta, bajan soldados por cuerdas y despues se
    // ponen a atacar al tanque (HelicopteroEnemigo.IniciarDespliegue). Tres desembarcos repartidos en la carrera, tres soldados cada uno.
    public partial class OperacionDirector
    {
        // Apagado a pedido: los helicopteros que bajaban soldados y perseguian al tanque ya no aparecen en la ruta (LanzarDespliegue sigue disponible).
        public static bool DesplieguesActivos = false;
        public static readonly float[] SegundosDeLosDespliegues = { 7f, 22f, 38f };
        public const int SoldadosPorDespliegue = 3;
        public const float MetrosPorDelanteDelDespliegue = 95f;

        public int DesplieguesLanzados { get; private set; }
        public IReadOnlyList<HelicopteroEnemigo> HelicopterosDeDespliegue => helisDeDespliegue;
        readonly List<HelicopteroEnemigo> helisDeDespliegue = new List<HelicopteroEnemigo>();
        int proximoDespliegue;

        void ReiniciarDespliegues()
        {
            foreach (var h in helisDeDespliegue) if (h != null) Destroy(h.gameObject);
            helisDeDespliegue.Clear();
            proximoDespliegue = 0; DesplieguesLanzados = 0;
        }

        void TickDespliegues()
        {
            if (!DesplieguesActivos || !EnTanque || tanque == null || halcon == null || plantillaDeRefuerzo == null) return;
            if (proximoDespliegue >= SegundosDeLosDespliegues.Length || Reloj < SegundosDeLosDespliegues[proximoDespliegue]) return;
            proximoDespliegue++;
            LanzarDespliegue(PuntoDeLaRutaPorDelante(MetrosPorDelanteDelDespliegue));
        }

        public HelicopteroEnemigo LanzarDespliegue(Vector3 punto, int soldados = SoldadosPorDespliegue)
        {
            var h = HelicopteroEnemigo.IniciarDespliegue(halcon, tanque, plantillaDeRefuerzo.gameObject, punto, soldados);
            if (h == null) return null;
            helisDeDespliegue.Add(h);
            DesplieguesLanzados++;
            HalconesCreados++;
            h.AlDerribarse += () => HalconesDerribados++;
            return h;
        }

        // Un punto de la ruta del tanque a "metros" por delante de el, corrido 4 m a un costado y apoyado en el piso.
        Vector3 PuntoDeLaRutaPorDelante(float metros)
        {
            var pos = tanque.transform.position;
            var fwd = tanque.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 punto = pos + fwd * metros;
            if (rutaDelTanque != null && indiceRuta < rutaDelTanque.Length)
            {
                float acumulado = 0f; var previo = pos;
                for (int i = indiceRuta; i < rutaDelTanque.Length; i++)
                {
                    if (rutaDelTanque[i] == null) continue;
                    var q = rutaDelTanque[i].position;
                    float tramo = Vector3.Distance(new Vector3(previo.x, 0f, previo.z), new Vector3(q.x, 0f, q.z));
                    if (acumulado + tramo >= metros)
                    {
                        float f = tramo > 0.01f ? (metros - acumulado) / tramo : 0f;
                        punto = Vector3.Lerp(previo, q, f);
                        fwd = (q - previo); fwd.y = 0f; if (fwd.sqrMagnitude > 0.01f) fwd.Normalize();
                        break;
                    }
                    acumulado += tramo; previo = q; punto = q;
                }
            }
            var lado = new Vector3(fwd.z, 0f, -fwd.x);
            punto += lado * 4f;
            if (Physics.Raycast(punto + Vector3.up * 40f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)) punto.y = hit.point.y;
            else punto.y = 0f;
            return punto;
        }
    }
}
