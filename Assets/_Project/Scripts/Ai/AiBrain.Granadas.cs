using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;
using SP.Presentation;
using SP.UI;

namespace SP.Ai
{
    // Granadas para la IA: (57) los aliados las lanzan contra enemigos agrupados, y los enemigos tambien en DIFICIL;
    // (58) cualquiera que este cerca de una granada viva sale corriendo; (60) los aliados avisan con voz de radio
    // ("GRANADA!", "NOS DISPARAN!"); (63) el fuego de supresion (balas que pasan cerca) los agacha.
    public partial class AiBrain
    {
        public const float DistanciaMinimaGranadaIA = 9f;
        public const float DistanciaMaximaGranadaIA = 24f;
        public const float SegundosEntreGranadasIA = 14f;
        public const float RadioDeHuidaGranada = Granada.RadioDeExplosion + 1.8f;

        float proximaGranadaIA;
        float huyendoHasta;
        float suprimidoHasta;
        bool estabaSuprimido;
        static float proximoAvisoRadio;
        static readonly List<Vector3> puntosGranada = new List<Vector3>();
        static readonly List<Soldier> cercanosGranada = new List<Soldier>();

        public bool Suprimido => Time.time < suprimidoHasta;
        public bool HuyendoDeGranada => Time.time < huyendoHasta;
        public static int GranadasLanzadasPorIA { get; private set; }
        public static int HuidasDeGranada { get; private set; }
        public static void ReiniciarContadoresGranadaIA() { GranadasLanzadasPorIA = 0; HuidasDeGranada = 0; }

        // Devuelve true si este tick lo consumio (huyendo de una granada).
        bool TickGranadas(float dt)
        {
            if (self == null || self.Weapon == null) return false;
            if (TickSupresion()) { /* agachado: puede seguir disparando */ }
            if (HuirDeGranada(dt)) return true;
            if (TickSuprimirOrdenado(dt)) return true;
            IntentarLanzarGranada();
            return false;
        }

        // ---- Ordenes del radial (item 61): suprimir un punto y lanzar una granada alli
        public const float SegundosDeSupresionOrdenada = 6f;
        float suprimirOrdenadoHasta;
        Vector3 suprimirOrdenadoPunto;
        public bool SuprimiendoPorOrden => Time.time < suprimirOrdenadoHasta;

        // Dispara rafagas hacia el punto (aunque no vea a nadie) durante unos segundos: mantiene cabezas abajo.
        public void OrdenSuprimir(Vector3 punto, float segundos = SegundosDeSupresionOrdenada)
        {
            if (self == null || IsPossessedByPlayer || !self.Health.IsAlive) return;
            suprimirOrdenadoPunto = punto;
            suprimirOrdenadoHasta = Time.time + segundos;
        }

        bool TickSuprimirOrdenado(float dt)
        {
            if (!SuprimiendoPorOrden) return false;
            var w = self.Weapon;
            if (w == null) return false;
            var origen = self.transform.position + Vector3.up * 1.4f;
            var hacia = suprimirOrdenadoPunto + Vector3.up * 1f - origen;
            self.Motor.LookTowards(suprimirOrdenadoPunto, 20f);
            self.Motor.SetCrouching(true);
            if (w.CurrentAmmo <= 0 && !w.IsReloading) w.Reload();
            var dir = (hacia.normalized + Random.insideUnitSphere * 0.035f).normalized;
            w.TryFire(self.transform.position, dir);
            return true;
        }

        // Lanza una granada al punto si el soldado tiene y la parabola llega. Devuelve si la lanzo.
        public bool OrdenLanzarGranada(Vector3 punto)
        {
            if (self == null || self.Weapon == null || IsPossessedByPlayer || !self.Health.IsAlive) return false;
            if (self.Weapon.Granadas <= 0) return false;
            var origen = self.transform.position + Vector3.up * 1.6f + self.transform.forward * 0.4f;
            var v = Granada.VelocidadHacia(origen, punto);
            Granada.Simular(origen, v, self.transform, puntosGranada, out var caida, out _);
            if ((caida - punto).magnitude > 4f) return false;
            self.Motor.LookTowards(punto, 20f);
            if (!self.Weapon.ConsumirGranada()) return false;
            Granada.Lanzar(origen, v, self);
            GranadasLanzadasPorIA++;
            proximaGranadaIA = Time.time + 3f;
            return true;
        }

        bool HuirDeGranada(float dt)
        {
            Granada peor = null; float mejor = float.MaxValue;
            var yo = self.transform.position;
            for (int i = 0; i < Granada.Activas.Count; i++)
            {
                var g = Granada.Activas[i];
                if (g == null) continue;
                var d = g.transform.position - yo; d.y = 0f;
                float dist = d.magnitude;
                if (dist < RadioDeHuidaGranada && dist < mejor) { mejor = dist; peor = g; }
            }
            if (peor == null) return false;
            var lejos = yo - peor.transform.position; lejos.y = 0f;
            if (lejos.sqrMagnitude < 0.01f) lejos = -self.transform.forward;
            if (!HuyendoDeGranada) { HuidasDeGranada++; AvisoDeRadio("GRANADA! ALEJENSE!"); }
            huyendoHasta = Time.time + 0.6f;
            self.Motor.SetCrouching(false);
            self.Motor.SetRunning(true);
            self.Motor.Move(lejos.normalized, dt);
            return true;
        }

        void IntentarLanzarGranada()
        {
            if (Time.time < proximaGranadaIA) return;
            if (target == null || !target.Health.IsAlive || self.Weapon.Granadas <= 0) return;
            bool aliado = self.Team == TeamId.Player;
            if (!aliado && !(Dificultad.Activa && Dificultad.Actual == NivelDificultad.Dificil)) return;   // los enemigos solo en DIFICIL
            if (State != AiState.Attack && State != AiState.Chase) return;

            var origen = self.transform.position + Vector3.up * 1.6f + self.transform.forward * 0.4f;
            var haciaBlanco = target.transform.position - self.transform.position;
            float dist = new Vector3(haciaBlanco.x, 0f, haciaBlanco.z).magnitude;
            if (dist < DistanciaMinimaGranadaIA || dist > DistanciaMaximaGranadaIA) return;

            proximaGranadaIA = Time.time + 1f;   // reintenta seguido, pero sin recalcular la parabola en cada frame
            var v = Granada.VelocidadHacia(origen, target.transform.position);
            Granada.Simular(origen, v, self.transform, puntosGranada, out var caida, out _);
            if ((caida - target.transform.position).magnitude > 3.2f) return;   // la parabola no llega (pared, techo)

            // No tirar si la explosion alcanza a alguien del propio bando.
            cercanosGranada.Clear();
            SpatialGrid.QueryInRange(caida, Granada.RadioDeExplosion + 1f, cercanosGranada, s => s != null && s.Team == self.Team && s.Health.IsAlive && s != self);
            if (cercanosGranada.Count > 0) return;

            self.Motor.LookTowards(target.transform.position, 20f);
            if (!self.Weapon.ConsumirGranada()) return;
            Granada.Lanzar(origen, v, self);
            GranadasLanzadasPorIA++;
            proximaGranadaIA = Time.time + SegundosEntreGranadasIA * Random.Range(0.8f, 1.3f);
            if (aliado) AvisoDeRadio($"{self.DisplayName.ToUpperInvariant()}: GRANADA!");
        }

        // ---- Supresion
        public void RecibirSupresion(float segundos)
        {
            if (self == null || IsPossessedByPlayer || !self.Health.IsAlive) return;
            bool era = Suprimido;
            suprimidoHasta = Mathf.Max(suprimidoHasta, Time.time + segundos);
            if (!era && self.Team == TeamId.Player) AvisoDeRadio($"{self.DisplayName.ToUpperInvariant()}: NOS DISPARAN! A CUBIERTO!");
        }

        bool TickSupresion()
        {
            bool ahora = Suprimido;
            if (ahora) self.Motor.SetCrouching(true);
            else if (estabaSuprimido && !enCobertura) self.Motor.SetCrouching(false);
            estabaSuprimido = ahora;
            return ahora;
        }

        // Voces de radio de los aliados: texto en el aviso (no hay locuciones grabadas); una a la vez, con enfriamiento.
        static void AvisoDeRadio(string texto)
        {
            if (Time.time < proximoAvisoRadio) return;
            proximoAvisoRadio = Time.time + 3.5f;
            AlertQueue.Push(texto, AlertPriority.Media, 2f);
        }
    }
}
