using UnityEngine;
using SP.Combat;

namespace SP.Operacion
{
    // Bug #378 (2026-11-08: "quiero ver los disparos de los vehiculos enemigos y quiero q haya tanques enemigos tambien con otro color y mas vida y mas
    // daño"). Algunas camionetas perseguidoras salen como TANQUE ENEMIGO: mas grande, rojo oscuro, 3 veces la vida, rafaga que pega el doble y,
    // ademas, un obus lento y bien visible (estela naranja, fogonazo grande) cada pocos segundos.
    public partial class OperacionAuto
    {
        public const float FactorDeVidaDelTanque = 3f, FactorDeDanoDelTanque = 2f, EscalaDelTanque = 1.25f, SegundosEntreObuses = 3.4f;
        public const int DanoDelObus = 45;
        public static readonly Color ColorDelTanqueEnemigo = new Color(0.55f, 0.06f, 0.08f);

        public bool EsTanque { get; private set; }
        public int Obuses { get; private set; }
        public static int TanquesCreados { get; private set; }
        float proximoObus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarTanques() { TanquesCreados = 0; }

        public void ConvertirEnTanque()
        {
            if (EsTanque || Vehiculo == null) return;
            EsTanque = true; TanquesCreados++;
            name = name.Replace("Auto_Perseguidor_", "Tanque_Enemigo_");
            transform.localScale *= EscalaDelTanque;
            danoPorBala = Mathf.RoundToInt(danoPorBala * FactorDeDanoDelTanque);
            alcance *= 1.15f;
            var h = Vehiculo.Health;
            if (h != null) h.Initialize(-1, Mathf.RoundToInt(h.MaxHealth * FactorDeVidaDelTanque));
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.name == "RomboTanque") continue;   // #417: el rombo marca el bando (rojo vivo), no se tine como la carroceria
                var mats = r.materials;   // instancia propia: no tine a las camionetas comunes
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), ColorDelTanqueEnemigo, 0.85f));
                    else if (m.HasProperty("_Color")) m.SetColor("_Color", Color.Lerp(m.GetColor("_Color"), ColorDelTanqueEnemigo, 0.85f));
                }
                r.materials = mats;
            }
            proximoObus = Time.time + Random.Range(1.5f, SegundosEntreObuses);
        }

        // Se llama desde Disparar(): un obus lento con explosion, apuntado con la misma mira adelantada.
        void DispararObus(Vector3 hacia, float distancia)
        {
            if (!EsTanque || Time.time < proximoObus || distancia > alcance * 1.2f) return;
            if (pool == null) pool = ProjectilePool.Activo;
            if (pool == null) return;
            proximoObus = Time.time + SegundosEntreObuses * Random.Range(0.85f, 1.2f);
            var origen = boca != null ? boca.position : transform.position + Vector3.up * 2.1f;
            var dir = (hacia - origen).normalized;
            dir = (dir + Random.insideUnitSphere * 0.02f).normalized;
            pool.Spawn(origen, dir, -2, TeamId.Enemy, DanoDelObus, new Color(1f, 0.55f, 0.1f), 3.2f, 0f, null, 0.55f);
            SP.Presentation.MuzzleLightPool.Flash(origen, new Color(1f, 0.6f, 0.2f), 10f, 16f);
            EfectoDeDisparo(origen, dir);
            Obuses++;
        }
    }
}
