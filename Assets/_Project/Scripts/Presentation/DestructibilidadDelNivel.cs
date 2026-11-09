using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Vehicles;

namespace SP.Presentation
{
    // #402 ("todos los objetos se tienen que poder destruir"): el nivel se arma por codigo y cada builder decidia por su cuenta si una pieza llevaba ObstacleMarker.
    // Resultado: la mitad de los props (palets, postes, setos, jardineras, puestos) eran inmortales. Esta pasada, unica y compartida, reparte tres categorias:
    //   A. destructible: todo collider con malla a menos de 60 m de la plaza (o dentro de la mansion) que no sea estructura y no tenga logica propia -> ObstacleMarker
    //      con vida proporcional a su volumen (se parte en trozos con el Fragmentador, que ya tiene tope de cupo).
    //   C. indestructible por diseno: suelo, calles, edificios, losas, muros portantes, torres y pasarelas (la lista de nombres de abajo y el tamano).
    //   D. con logica propia (terminales, compuertas, heli...): no se toca.
    // Se corre desde AjustesDeNivelNov08.Aplicar (al cargar y cada 3 s), una sola vez por collider.
    public static class DestructibilidadDelNivel
    {
        public static readonly Vector3 Plaza = new Vector3(322f, 0f, 226f);
        public const float Alcance = 60f;
        public static int Convertidos { get; private set; }
        static readonly HashSet<int> vistos = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { vistos.Clear(); Convertidos = 0; }

        static readonly string[] Estructura =
        {
            "Ground", "Suelo", "Losa", "Muro", "Torre_", "Mansion_", "Edificio", "Calle", "Acera", "Cordon", "Vereda", "Pared", "Techo", "Piso", "Escalon", "Rampa",
            "Fusionados", "Zocalo", "Cornisa", "Medianera", "Tabique", "Pilastra", "Portico", "Azotea", "Jamba", "Hoja_", "Reja", "Baranda", "Parapeto", "Claraboya",
            "Terreno", "Cielo", "Limite", "Borde", "P1_", "P2_", "PB_", "P3_", "Entrada", "Vidriera", "Escalera", "Cuerpo", "Pasarela", "Soportal", "Patio_Muro",
            "Cimientos", "Fundacion", "Viaducto", "Puente", "Tunel", "Avenida", "Plataforma", "Cerco_Ciudad", "Barrera_Mapa", "Limite_Mapa",
        };

        public static bool EnAlcance(Bounds b) =>
            new Vector2(b.center.x - Plaza.x, b.center.z - Plaza.z).magnitude < Alcance || SP.Operacion.MansionGeometria.Dentro(b.center, 2f);

        // Estructura por nombre (en el objeto o en cualquier padre) o por tamano (algo muy grande no es un prop).
        public static bool EsEstructural(Collider c)
        {
            var b = c.bounds;
            if (Mathf.Max(b.size.x, b.size.z) > 14f || b.size.y > 6f) return true;
            for (var t = c.transform; t != null; t = t.parent)
            {
                string n = t.name;
                foreach (var pre in Estructura) if (n.StartsWith(pre, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // Algo con comportamiento propio de gameplay (terminal, compuerta, heli, baliza, escudo...): su ciclo de vida lo maneja otro sistema.
        public static bool TieneLogicaPropia(Collider c)
        {
            foreach (var mb in c.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var tipo = mb.GetType();
                if (tipo == typeof(ObstacleMarker)) continue;
                string ns = tipo.Namespace ?? "";
                if (ns.StartsWith("SP.", System.StringComparison.Ordinal) && tipo.Name != "MarcaEquipoMovido") return true;
            }
            return c.attachedRigidbody != null;
        }

        public static bool YaEsDestructible(Collider c) =>
            c.GetComponentInParent<ObstacleMarker>() != null || c.GetComponentInParent<SP.Operacion.TorreDestruible>() != null
            || c.GetComponentInParent<Vehicle>() != null || c.GetComponentInParent<Soldier>() != null;

        public static void Aplicar()
        {
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c == null || c.isTrigger || !c.enabled) continue;
                if (!vistos.Add(c.GetHashCode())) continue;
                if (c.GetComponent<MeshRenderer>() == null || !EnAlcance(c.bounds)) continue;
                if (YaEsDestructible(c) || EsEstructural(c) || TieneLogicaPropia(c)) continue;
                var b = c.bounds.size;
                var m = c.gameObject.AddComponent<ObstacleMarker>();
                m.ConfigurarVida(Mathf.Clamp(Mathf.RoundToInt(b.x * b.y * b.z * 40f), 120, 500));
                Convertidos++;
            }
        }
    }
}
