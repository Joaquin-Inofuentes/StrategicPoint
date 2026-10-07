using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // WP2 (#065/#095): un clip ESTEREO en una AudioSource 3D no se posiciona bien (los dos canales quedan abiertos a izquierda y
    // derecha, "suena al lado"). Fuerza forceToMono en las voces (Death, Wounded) y en los sonidos del mundo que van por el pool 3D.
    // Se apaga "normalize" para que el mixdown no cambie el volumen que ya tenia cada clip. Idempotente.
    // Los sonidos de interfaz (UiClick, UiHover, OrderBark...) y la musica no se tocan.
    public static class AudioImportFix
    {
        public const string Raiz = "Assets/_Project/Resources/Audio/Sfx/";

        // Primero las voces (lo que pide el plan); despues los sonidos del mundo que se reproducen 3D.
        public static readonly string[] Voces = { "Death", "Wounded" };
        public static readonly string[] Mundo =
        {
            "Shoot", "Shot_Rifle", "Shot_Pistol", "Shot_Smg", "Shot_Heavy", "Shot_Shotgun", "Shot_Sniper", "Shot_Rocket",
            "Explosion", "CannonBody", "Hit", "ImpactDirt", "ImpactMetal", "VehicleHit", "FootstepGrass", "FootstepConcrete",
            "GrenadeBounce", "EmptyClick", "Orugas", "OrugasChirrido",
        };

        [MenuItem("Strategic Point/Audio/Forzar mono en voces")]
        public static void Menu() { Debug.Log("[AudioImportFix] " + Aplicar()); }

        public static IEnumerable<string> Rutas(IEnumerable<string> carpetas)
        {
            foreach (var c in carpetas)
            {
                string dir = Raiz + c;
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(g);
                    if (ruta.EndsWith(".meta")) continue;
                    yield return ruta;
                }
            }
        }

        public static bool EsMono(string ruta) => AssetImporter.GetAtPath(ruta) is AudioImporter imp && imp.forceToMono;

        // Devuelve "cambiados=N ya=M (normalize apagado en K)".
        public static string Aplicar(bool tambienMundo = true)
        {
            var carpetas = new List<string>(Voces);
            if (tambienMundo) carpetas.AddRange(Mundo);
            int cambiados = 0, ya = 0, sinNormalize = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var ruta in Rutas(carpetas))
                {
                    var imp = AssetImporter.GetAtPath(ruta) as AudioImporter;
                    if (imp == null) continue;
                    bool toca = false;
                    if (!imp.forceToMono) { imp.forceToMono = true; toca = true; }
                    // "normalize" no tiene propiedad publica: se apaga por la serializacion del importador.
                    var so = new SerializedObject(imp);
                    var pn = so.FindProperty("m_Normalize");
                    if (pn != null && pn.boolValue) { pn.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); toca = true; sinNormalize++; }
                    if (toca) { imp.SaveAndReimport(); cambiados++; } else ya++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            return $"cambiados={cambiados} ya={ya} (normalize apagado en {sinNormalize})";
        }
    }
}
