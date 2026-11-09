using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SP.CameraSystem;
using SP.Combat;
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

        public static PlantasDeLaMansion Activa { get; private set; }
        void OnEnable() { Activa = this; }
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

        readonly int[] porNivel = new int[4];
        int ultimoNivel = -1;

        // Piso (0..3) donde esta la mayoria de la escuadra viva dentro de la mansion; -1 si no hay nadie adentro. Empate: el piso del que manejas,
        // y si tampoco decide, el que ya se estaba mostrando (sin parpadeo cuando dos grupos iguales cruzan un rellano).
        int NivelDeLaEscuadra(SP.Actors.Soldier yo)
        {
            for (int i = 0; i < porNivel.Length; i++) porNivel[i] = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian) continue;
                var h = s.GetComponent<SP.Combat.Health>();
                if (h == null || !h.IsAlive) continue;
                int n = G.NivelDe(s.transform.position);
                if (n >= 0) porNivel[n]++;
            }
            int mejor = -1, cuenta = 0; bool empate = false;
            for (int n = 0; n < porNivel.Length; n++)
            {
                if (porNivel[n] > cuenta) { mejor = n; cuenta = porNivel[n]; empate = false; }
                else if (porNivel[n] == cuenta && cuenta > 0) empate = true;
            }
            if (mejor < 0) return yo != null ? G.NivelDe(yo.transform.position) : -1;
            if (empate)
            {
                int propio = yo != null ? G.NivelDe(yo.transform.position) : -1;
                if (propio >= 0 && porNivel[propio] == cuenta) mejor = propio;
                else if (ultimoNivel >= 0 && porNivel[ultimoNivel] == cuenta) mejor = ultimoNivel;
            }
            ultimoNivel = mejor;
            return mejor;
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

        void OnDisable() { if (Activa == this) Activa = null; Restaurar(); }

        void Update()
        {
            if (!Application.isPlaying || Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.25f;
            var d = OperacionDirector.Instancia;
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            var cam = CamaraPrincipal.Actual;
            // Bug #014 (build, fase Extraer): "falta recorte de planta en donde estan la mayoria de soldados". Al pasar a Extraer el director da la
            // mansion por terminada (MansionActiva=false) y el recorte se apagaba justo cuando la escuadra sube piso por piso. El componente solo
            // existe si hubo mansion, asi que ya no depende de ese flag; y el piso que se muestra es donde esta la MAYORIA de la escuadra (el soldado
            // que manejas desempata), no solo donde esta el que manejas.
            if (d == null || cam == null) { Restaurar(); return; }
            var rig = CameraRig.Instance;
            bool rts = rig != null && rig.Mode == ControlMode.Rts;
            // Con la vista tactica manda la mayoria; en primera/tercera persona, el piso del soldado que manejas (la camara esta con el).
            int nivel = rts ? NivelDeLaEscuadra(yo) : (yo != null ? G.NivelDe(yo.transform.position) : -1);
            if (nivel >= 3) { Restaurar(); return; }   // en la azotea no hay nada por encima que recortar
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
