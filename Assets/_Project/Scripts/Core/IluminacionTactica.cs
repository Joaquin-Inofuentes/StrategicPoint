using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // LA LUZ COMO RIESGO (diseño del nivel).
    //
    // De noche, un enemigo solo te ve a distancia completa si estas ILUMINADO: bajo una farola, un reflector, una fogata
    // o el haz barredor de una torre. En la sombra te ve recien a FactorEnSombra de su vision. Asi cada camino tiene su
    // propio juego: la carretera esta llena de luz (rapida pero expuesta), el sendero del bosque casi no tiene (sigiloso
    // pero cerrado) y el camino de servicio esta bañado de reflectores... que el francotirador puede APAGAR a tiros desde
    // lejos para cruzar en la oscuridad.
    //
    // Los emisores (Luminaria, LuzTitilante, FocoBarredor) se registran solos al activarse; no hay que arrastrar nada.
    // Solo esta activo en la mision (MisionDirector lo enciende): la suite headless y el tutorial no lo notan.
    public static class IluminacionTactica
    {
        // Fraccion de su vision a la que un enemigo detecta a alguien que esta en la sombra.
        public const float FactorEnSombra = 0.6f;
        // Fraccion del rango de una luz puntual que cuenta como "iluminado" (el borde ya casi no alumbra).
        const float FraccionDeRango = 0.72f;
        const float AlcanceMaximoDeHaz = 46f;

        public static bool Activa;

        static readonly List<Light> luces = new List<Light>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { luces.Clear(); Activa = false; }

        public static void Registrar(Light l)
        {
            if (l != null && !luces.Contains(l)) luces.Add(l);
        }

        public static void Quitar(Light l) => luces.Remove(l);

        public static int CantidadDeLuces => luces.Count;

        // Esta el punto bajo alguna luz encendida?
        public static bool EstaIluminado(Vector3 p)
        {
            for (int i = luces.Count - 1; i >= 0; i--)
            {
                var l = luces[i];
                if (l == null) { luces.RemoveAt(i); continue; }
                if (!l.enabled || l.intensity < 0.4f || !l.gameObject.activeInHierarchy) continue;
                var lp = l.transform.position;
                if (l.type == LightType.Point)
                {
                    float r = l.range * FraccionDeRango;
                    var d = p - lp; d.y = 0f;
                    if (d.sqrMagnitude <= r * r) return true;
                }
                else if (l.type == LightType.Spot)
                {
                    var d = p - lp;
                    float dist = d.magnitude;
                    if (dist > Mathf.Min(l.range, AlcanceMaximoDeHaz) || dist < 0.5f) continue;
                    if (Vector3.Angle(l.transform.forward, d) <= l.spotAngle * 0.5f) return true;
                }
            }
            return false;
        }

        // Cuanto ve un enemigo de 'vision' metros a un objetivo en 'p'.
        public static float VisionContra(Vector3 p, float vision)
        {
            if (!Activa || luces.Count == 0) return vision;
            return EstaIluminado(p) ? vision : vision * FactorEnSombra;
        }
    }
}
