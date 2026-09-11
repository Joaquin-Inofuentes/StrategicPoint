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
                _ => null,
            };

            // Cachea null tambien a proposito: si el prefab falta, que no
            // reintente Resources.Load (relativamente caro) en cada arma
            // que se equipa el resto de la partida -- se queda con el
            // cubo de siempre como respaldo, silencioso.
            var prefab = name != null ? Resources.Load<GameObject>("Weapons/" + name) : null;
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
            _ => 0.5f,
        };

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
                metralletaVehiculo = Resources.Load<GameObject>("Weapons/P_Wpn_Metralleta");
            }
            return metralletaVehiculo;
        }
    }
}
