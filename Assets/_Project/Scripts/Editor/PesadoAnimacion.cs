using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SP.EditorTools
{
    // Animacion del enemigo PESADO (pedido 2026-11-07: "su animacion de mixamo tambien sea de alguien pesado con esa metralleta"; "otro clip para recarga y que
    // sea lento"). Se bajaron de Mixamo "Gunplay" (disparo sostenido) y "Reloading" (recarga de pie): quedan como "gunplay heavy.fbx" y "reloading heavy.fbx"
    // junto al pack. Este menu los importa como humanoides y arma AC_Pesado: una COPIA de AC_Soldado donde el clip de disparo se reemplaza por Gunplay y se agrega
    // la capa "Recarga" (mascara del tren superior, peso 0) con el clip de recarga estirado a la duracion de la recarga de la minigun (4,2 s).
    // EnemigoPesado maneja el peso de esa capa segun WeaponHolder.IsReloading.
    public static class PesadoAnimacion
    {
        const string FbxFuego = "Assets/ARTS/Slim Shooter Pack/gunplay heavy.fbx";
        const string FbxRecarga = "Assets/ARTS/Slim Shooter Pack/reloading heavy.fbx";
        const string Base = "Assets/_Project/Animation/AC_Soldado.controller";
        const string Mascara = "Assets/_Project/Animation/Mask_TrenSuperior.mask";
        const string Salida = "Assets/_Project/Resources/Soldados/AC_Pesado.controller";
        const string SalidaVieja = "Assets/_Project/Resources/Soldados/AC_Pesado.overrideController";
        public const string CapaRecarga = "Recarga", EstadoRecarga = "Recarga";
        public const float SegundosDeRecarga = 4.2f;

        static AnimationClip Importar(string fbx, string nombre, bool bucle)
        {
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null) { Debug.LogError("[PesadoAnimacion] Falta " + fbx); return null; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            var todos = imp.defaultClipAnimations;
            var utiles = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (var c in todos) if (c.takeName == "mixamo.com") utiles.Add(c);   // los FBX de Mixamo traen ademas una "Take 001" vacia
            if (utiles.Count == 0) utiles.AddRange(todos);
            foreach (var c in utiles) { c.name = nombre; c.loopTime = bucle; c.loopPose = bucle; }
            imp.clipAnimations = utiles.ToArray();
            imp.SaveAndReimport();
            AnimationClip clip = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx)) if (o is AnimationClip ac && !ac.name.StartsWith("__preview") && (clip == null || ac.length > clip.length)) clip = ac;
            return clip;
        }

        [MenuItem("Strategic Point/Arte/10. Animacion del enemigo pesado (Mixamo)")]
        public static void Generar()
        {
            var fuego = Importar(FbxFuego, "gunplay heavy", true);
            var recarga = Importar(FbxRecarga, "reloading heavy", false);
            if (fuego == null || recarga == null) { Debug.LogError("[PesadoAnimacion] Faltan clips"); return; }
            Directory.CreateDirectory("Assets/_Project/Resources/Soldados");
            if (AssetDatabase.LoadAssetAtPath<Object>(SalidaVieja) != null) AssetDatabase.DeleteAsset(SalidaVieja);
            if (AssetDatabase.LoadAssetAtPath<Object>(Salida) != null) AssetDatabase.DeleteAsset(Salida);
            if (!AssetDatabase.CopyAsset(Base, Salida)) { Debug.LogError("[PesadoAnimacion] No se pudo copiar " + Base); return; }
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Salida);

            int cambiados = 0;
            foreach (var capa in ctrl.layers) cambiados += Reemplazar(capa.stateMachine, fuego);

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(Mascara);
            var sm = new AnimatorStateMachine { name = CapaRecarga, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, ctrl);
            var estado = sm.AddState(EstadoRecarga);
            estado.motion = recarga; estado.speed = recarga.length / SegundosDeRecarga; estado.writeDefaultValues = true;
            sm.defaultState = estado;
            ctrl.AddLayer(new AnimatorControllerLayer { name = CapaRecarga, stateMachine = sm, avatarMask = mask, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override });
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PesadoAnimacion] AC_Pesado listo: fuego {fuego.length:0.00} s ({cambiados} reemplazo/s), recarga {recarga.length:0.00} s estirada a {SegundosDeRecarga} s");
        }

        static int Reemplazar(AnimatorStateMachine sm, AnimationClip nuevo)
        {
            int n = 0;
            foreach (var cs in sm.states)
            {
                var m = cs.state.motion;
                if (m is AnimationClip ac && ac.name == "firing rifle") { cs.state.motion = nuevo; n++; }
                else if (m is BlendTree bt) n += ReemplazarEnArbol(bt, nuevo);
            }
            foreach (var sub in sm.stateMachines) n += Reemplazar(sub.stateMachine, nuevo);
            return n;
        }

        static int ReemplazarEnArbol(BlendTree bt, AnimationClip nuevo)
        {
            int n = 0; var hijos = bt.children;
            for (int i = 0; i < hijos.Length; i++)
            {
                if (hijos[i].motion is AnimationClip ac && ac.name == "firing rifle") { hijos[i].motion = nuevo; n++; }
                else if (hijos[i].motion is BlendTree sub) n += ReemplazarEnArbol(sub, nuevo);
            }
            bt.children = hijos; return n;
        }
    }
}
