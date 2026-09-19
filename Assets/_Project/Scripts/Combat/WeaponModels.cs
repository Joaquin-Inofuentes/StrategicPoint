using System.Collections.Generic;
using UnityEngine;

namespace SP.Combat
{
    // Carga (y cachea) los prefabs de arma real armados por
    // SP.EditorTools.WeaponPrefabBuilder a partir de los FBX de
    // Assets/ARTS/SP_Arte/_FBX_Export/05_Armas. Resources.Load en vez de
    // AssetDatabase: esto lo llaman WeaponHolder/WeaponBackRack en RUNTIME
    // (Play mode y un build final), no solo el editor.
    public static class WeaponModels
    {
        static readonly Dictionary<WeaponKind, GameObject> cache = new Dictionary<WeaponKind, GameObject>();

        public static GameObject Get(WeaponKind kind)
        {
            if (cache.TryGetValue(kind, out var cached)) return cached;

            string name = kind switch
            {
                WeaponKind.Rifle => "P_Wpn_Fusil",
                WeaponKind.Pistol => "P_Wpn_Pistola",
                WeaponKind.Heavy => "P_Wpn_Heavy",
                WeaponKind.Smg => "P_Wpn_Metralleta",
                WeaponKind.Rocket => "P_Wpn_Lanzacohetes",
                WeaponKind.Shotgun => "P_Wpn_Escopeta",
                WeaponKind.Sniper => "P_Wpn_Sniper",
                _ => null,
            };

            // Cachea null tambien a proposito: si el prefab falta, que no
            // reintente Resources.Load (relativamente caro) en cada arma
            // que se equipa el resto de la partida -- se queda con el
            // cubo de siempre como respaldo, silencioso.
            var prefab = name != null ? SP.Core.RecursosCache.Cargar<GameObject>("Weapons/" + name) : null;
            cache[kind] = prefab;
            return prefab;
        }

        // Largo natural del modelo real (eje mas largo de sus bounds, a
        // escala 1) medido una vez sobre los prefabs de WeaponPrefabBuilder.
        // BUG REAL que esto corrige: WeaponBackRack aplicaba la MISMA escala
        // plana (0,85) a los tres, pensada para el cubo abstracto de antes
        // (mismo tamaño base para cualquier arma). Contra el modelo real
        // -- que va de 0,21 m (pistola) a 1,12 m (pesada) -- esa escala
        // plana dejaba la ametralladora casi tan larga como el soldado,
        // envolviendole el torso. Con esto cada arma se reescala a un largo
        // de espalda fijo (ver WeaponBackRack.TargetBackLength), sin
        // importar cuanto mida su modelo real.
        public static float NaturalLength(WeaponKind kind) => kind switch
        {
            WeaponKind.Rifle => 0.761f,
            WeaponKind.Pistol => 0.209f,
            WeaponKind.Heavy => 1.121f,
            WeaponKind.Smg => 0.5f,
            WeaponKind.Rocket => 0.92f,
            WeaponKind.Shotgun => 0.9f,
            WeaponKind.Sniper => 1.06f,
            _ => 0.5f,
        };

        // Prisma (ancho x alto x largo) que cada arma NUNCA debe superar,
        // a escala 1. Se deriva de dos numeros ya calibrados en vez de
        // inventar medidas nuevas: el largo real medido de arriba
        // (NaturalLength) y la PROPORCION ancho/alto/largo del cubo de
        // WeaponCatalog.VisualScale -- ese cubo ya distingue a ojo el
        // fusil flaco y largo de la pesada gruesa y corta, asi que estirar
        // esa misma proporcion hasta el largo real da una caja razonable
        // por arma sin medir cada FBX a mano.
        public static Vector3 Prisma(WeaponKind kind)
        {
            var cubo = WeaponCatalog.Get(kind).VisualScale;
            float largoCubo = Mathf.Max(0.0001f, cubo.z);
            float largoReal = NaturalLength(kind);
            float factor = largoReal / largoCubo;
            var base_ = new Vector3(cubo.x * factor, cubo.y * factor, largoReal);

            // MEDIDO: contra el modelo real, esta proporcion (heredada del
            // cubo viejo, nunca calibrada contra la malla de verdad) se
            // queda corta en altura para el fusil (0.237 real contra 0.208
            // de caja) y sobre todo la pistola (0.144 contra 0.097) -- el
            // grip/mira de la malla real sobresale mas de lo que el cubo
            // planito asumia. El prisma NUNCA puede ser mas chico que el
            // arma ya autorizada: encogerla para que "entre" en una
            // proporcion vieja rompería el arte de verdad, y el pedido es
            // que el arma entre en el prisma, no que el prisma mande sobre
            // el arte. Se toma el mayor de los dos por eje, asi el prisma
            // sigue siendo un limite real (frena una malla rota o mal
            // escalada a futuro) sin recortar la actual.
            var medido = MeasuredNaturalSize(kind);
            return new Vector3(
                Mathf.Max(base_.x, medido.x),
                Mathf.Max(base_.y, medido.y),
                Mathf.Max(base_.z, medido.z));
        }

        static readonly Dictionary<WeaponKind, Vector3> tamañoMedidoCache = new Dictionary<WeaponKind, Vector3>();

        // Tamaño real (ancho x alto x largo) del modelo real de esta arma,
        // medido sobre el propio prefab-asset (sin instanciar en escena:
        // sus renderers ya tienen mesh y transform propios, alcanza para
        // encapsular sus bounds). Se cachea por arma -- es geometria fija,
        // no cambia entre soldados ni entre partidas.
        public static Vector3 MeasuredNaturalSize(WeaponKind kind)
        {
            if (tamañoMedidoCache.TryGetValue(kind, out var cached)) return cached;

            Vector3 tamaño = Vector3.zero;
            var prefab = Get(kind);
            if (prefab != null)
            {
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var caja = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) caja.Encapsulate(renderers[i].bounds);
                    tamaño = caja.size;
                }
            }
            tamañoMedidoCache[kind] = tamaño;
            return tamaño;
        }

        // La metralleta del vehiculo no es un WeaponKind del loadout de un
        // soldado -- es el arma montada de un asiento del tanque -- asi que
        // no entra en el switch de arriba. Nombre propio, mismo cache.
        static GameObject metralletaVehiculo;
        static bool metralletaVehiculoCargada;
        public static GameObject GetMetralletaVehiculo()
        {
            if (!metralletaVehiculoCargada)
            {
                metralletaVehiculoCargada = true;
                metralletaVehiculo = SP.Core.RecursosCache.Cargar<GameObject>("Weapons/P_Wpn_Metralleta");
            }
            return metralletaVehiculo;
        }
    }
}
