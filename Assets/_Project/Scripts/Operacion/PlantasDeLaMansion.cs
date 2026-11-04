using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SP.CameraSystem;
using SP.Core;
using SP.Player;
using G = SP.Operacion.MansionGeometria;

namespace SP.Operacion
{
    // Bug #336 ("aqui deberia recortarse y ver la planta en la q estan mis soldados principales"): con la camara por encima de la mansion (vista tactica RTS
    // o una camara alta) el techo y los pisos de arriba tapaban la planta donde esta el soldado. Mientras el que manejas esta dentro de la mansion y la camara
    // esta arriba, se dejan de dibujar (siguen proyectando sombra, con su collider) las piezas que quedan por encima del cielo raso de ese nivel. Con la
    // camara en primera/tercera persona dentro del edificio no se toca nada. Lo agrega OperacionDirector.IniciarMansion.
    public sealed class PlantasDeLaMansion : MonoBehaviour
    {
        const float Holgura = 0.4f;
        MeshRenderer[] renders;
        float[] bajoY;
        ShadowCastingMode[] sombras;
        float proximo;
        int nivelRecortado = int.MinValue;   // nivel cuyo cielo raso se esta recortando (int.MinValue = nada oculto)

        public int NivelMostrado => nivelRecortado == int.MinValue ? -1 : nivelRecortado;
        public int Ocultos { get; private set; }
        public int Candidatos => renders != null ? renders.Length : 0;

        static bool EsDelEdificio(MeshRenderer r)
        {
            if (r == null || !r.gameObject.activeInHierarchy) return false;
            var b = r.bounds;
            if (b.size.y > 30f || b.size.x > 60f || b.size.z > 60f) return false;          // halo, haces de luz, montañas
            if (b.min.y < 3f) return false;                                                // calle, planta baja y pies de los muros
            if (b.center.x < G.X0 - 1f || b.center.x > G.X1 + 1f || b.center.z < G.Z0 - 1f || b.center.z > G.Z1 + 1f) return false;
            for (var t = r.transform; t != null; t = t.parent) if (t.name == "Casco") return false;   // los muros exteriores quedan siempre
            // Bug #006 (build, "no se ve el helicoptero"): el helicoptero de rescate espera a 32 m, por encima del techo, y se ocultaba con la mansion.
            // Los vehiculos y los helicopteros nunca son parte del edificio.
            if (r.GetComponentInParent<SP.Vehicles.Vehicle>() != null || r.GetComponentInParent<HelicopteroEnemigo>() != null) return false;
            return true;
        }

        void Construir()
        {
            var lista = new List<MeshRenderer>();
            foreach (var r in FindObjectsByType<MeshRenderer>()) if (EsDelEdificio(r)) lista.Add(r);
            renders = lista.ToArray();
            bajoY = new float[renders.Length]; sombras = new ShadowCastingMode[renders.Length];
            for (int i = 0; i < renders.Length; i++) { bajoY[i] = renders[i].bounds.min.y; sombras[i] = renders[i].shadowCastingMode; }
        }

        void Restaurar()
        {
            if (renders == null || nivelRecortado == int.MinValue) return;
            for (int i = 0; i < renders.Length; i++) if (renders[i] != null) renders[i].shadowCastingMode = sombras[i];
            nivelRecortado = int.MinValue; Ocultos = 0;
        }

        void OnDisable() { Restaurar(); }

        void Update()
        {
            if (!Application.isPlaying || Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.25f;
            var d = OperacionDirector.Instancia;
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            var cam = CamaraPrincipal.Actual;
            if (d == null || !d.MansionActiva || yo == null || cam == null) { Restaurar(); return; }
            int nivel = G.NivelDe(yo.transform.position);
            if (nivel >= 3) { Restaurar(); return; }   // en la azotea no hay nada por encima que recortar
            var rig = CameraRig.Instance;
            bool rts = rig != null && rig.Mode == ControlMode.Rts;
            float techo = nivel >= 0 ? G.Nivel(nivel + 1) : 0f;
            bool camaraArriba = nivel >= 0 && (rts || cam.transform.position.y > techo + 1f);
            if (!camaraArriba) { Restaurar(); return; }
            if (renders == null) Construir();
            if (nivel == nivelRecortado) return;
            nivelRecortado = nivel;
            int n = 0;
            for (int i = 0; i < renders.Length; i++)
            {
                if (renders[i] == null) continue;
                bool ocultar = bajoY[i] >= techo - Holgura;
                renders[i].shadowCastingMode = ocultar ? ShadowCastingMode.ShadowsOnly : sombras[i];
                if (ocultar) n++;
            }
            Ocultos = n;
        }
    }
}
