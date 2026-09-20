using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;

namespace SP.Ai
{
    // Ronda 11 (puntos 3, 12 y 13): como dispara la IA. Antes cada soldado gatillaba en el mismo tick en que
    // entraba en Attack y a cadencia plena hasta que la cobertura o el objetivo lo sacaban, con la dispersion del arma casi en 0
    // (medido en Docs/RONDA_11: 93-100 % de impactos entre 8 y 28 m) y sin salir jamas de 13 m. Ahora, SOLO en la partida
    // principal (Dificultad.Activa; el tutorial y la suite headless siguen deterministas):
    //   - tarda entre 0,25 y 0,6 s en reaccionar cuando ve al blanco;
    //   - dispara en rafagas de 3 a 5 tiros con pausas de 0,4 a 0,9 s;
    //   - tiene una dispersion minima (enemigo 3 grados, aliado 2,2: la esfera de impacto de un soldado mide 1 m de radio, con menos de ~2 grados a 28 m las balas caen todas dentro) aunque el arma este "fria" o el soldado agachado;
    //   - alcanza mas lejos (fusil 35 m, metralleta 25, escopeta 12, francotirador ~56 m).
    // Un soldado herido que corre a cubrirse hace pausas para devolver una rafaga corta al blanco.
    public partial class AiBrain
    {
        public const float DispersionMinEnemigo = 3.0f;
        public const float DispersionMinAliado = 2.2f;
        public const float ReaccionMin = 0.25f, ReaccionMax = 0.6f;
        public const float PausaMin = 0.4f, PausaMax = 0.9f;
        public const int RafagaMin = 3, RafagaMax = 5;
        public const float FraccionDeHerido = 0.5f;

        // Multiplicadores de alcance de la partida principal (ver EffectiveAttackRange / EffectiveVisionRange).
        // Los soldados del nivel traen 22/13 (enemigo) y 20/12 (aliado): con estos factores llegan a ~40/35 m.
        public const float AmpliacionDeVision = 1.8f;
        public const float AmpliacionDeAtaque = 2.7f;
        public const float FactorDeAlcanceEscopeta = 0.35f;

        static bool Humanizada => Dificultad.Activa;

        float reaccionRestante;
        bool reaccionPendiente;
        int rafagaRestante;
        float pausaRestante;
        float correrHerido;
        bool disparandoHerido;

        public bool Herido => self != null && self.Health != null && self.Health.IsAlive
                              && self.Health.Current < self.Health.MaxHealth * FraccionDeHerido;

        float DispersionMinima => !Humanizada ? 0f : (self.Team == TeamId.Enemy ? DispersionMinEnemigo : DispersionMinAliado);

        void ArmarReaccion()
        {
            reaccionPendiente = Humanizada;
            reaccionRestante = Humanizada ? Random.Range(ReaccionMin, ReaccionMax) : 0f;
            rafagaRestante = 0;
            pausaRestante = 0f;
        }

        // Un tick de gatillo. Devuelve true si salio un tiro.
        bool DispararConRafagas(Vector3 direccion, float dt)
        {
            if (!Humanizada) return self.Weapon.TryFire(self.transform.position, direccion);

            if (reaccionPendiente)
            {
                reaccionRestante -= dt;
                if (reaccionRestante > 0f) return false;
                reaccionPendiente = false;
            }
            if (pausaRestante > 0f) { pausaRestante -= dt; return false; }
            if (rafagaRestante <= 0) rafagaRestante = Random.Range(RafagaMin, RafagaMax + 1);

            if (!self.Weapon.TryFire(self.transform.position, direccion, DispersionMinima)) return false;
            rafagaRestante--;
            if (rafagaRestante <= 0) pausaRestante = Random.Range(PausaMin, PausaMax);
            return true;
        }

        // Herido y corriendo a cubrirse: 0,9 s corriendo, y despues se frena, gira hacia el blanco y le tira una rafaga corta.
        // Devuelve true si este tick lo uso para disparar (no camina).
        bool FuegoDeCoberturaHerido(float dt)
        {
            if (!Humanizada || !Herido || target == null || !target.Health.IsAlive) { disparandoHerido = false; correrHerido = 0f; return false; }
            if (!disparandoHerido)
            {
                correrHerido += dt;
                if (correrHerido < 0.9f || !TieneLineaDeTiro(target)) return false;
                disparandoHerido = true;
                rafagaRestante = Random.Range(2, 4);
                pausaRestante = 0f;
                reaccionPendiente = false;
            }
            self.Motor.LookTowards(target.transform.position, dt);
            self.Weapon.Tick(dt);
            var muzzle = self.Weapon.Muzzle;
            Vector3 origen = muzzle != null ? muzzle.position : self.transform.position;
            Vector3 dir = target.transform.position - origen;
            Vector3 plano = target.transform.position - self.transform.position; plano.y = 0f;
            bool apunta = plano.sqrMagnitude < 0.0001f || Vector3.Angle(self.transform.forward, plano) <= aimToleranceDeg;
            if (apunta && dir.sqrMagnitude > 0.0001f && self.Weapon.TryFire(self.transform.position, dir.normalized, DispersionMinima * 1.5f))
                rafagaRestante--;
            if (rafagaRestante <= 0) { disparandoHerido = false; correrHerido = 0f; }
            return true;
        }
    }
}
