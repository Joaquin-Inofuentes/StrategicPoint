using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Player;
using SP.UI;

namespace SP.EditorTools
{
    // Deja SC_Gameplay, SIN dar Play, como se ve el primer frame del juego
    // real: camara detras del primer soldado, minimapa mini centrado en el,
    // HUD con sus textos de arranque, roster armado y el cartel de mision.
    //
    // Nada de esto cambia el juego: en Play cada componente vuelve a poner
    // sus propios valores (RosterView.Rebuild destruye y rearma las filas,
    // la camara sigue al soldado, etc.). Es solo el estado guardado de la
    // escena, para que la vista Game del Editor muestre lo mismo que se va
    // a ver al arrancar.
    //
    // Menu: Strategic Point > Vista previa > Preparar primer frame
    public static class PrimerFramePreview
    {
        // Camara por encima del hombro medida en Play: relativa al soldado
        // (x derecha, y arriba, z atras) en su espacio local.
        static readonly Vector3 OffsetDeCamara = new Vector3(0.55f, 1.53f, -4.0f);

        [MenuItem("Strategic Point/Vista previa/Preparar primer frame (sin dar Play)")]
        public static void Aplicar()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SC_Gameplay") { Debug.LogWarning("[PrimerFrame] Abri SC_Gameplay primero."); return; }

            var driver = Object.FindFirstObjectByType<PlayerInputDriver>(FindObjectsInactive.Include);
            Soldier primero = driver != null && driver.Squad != null && driver.Squad.Count > 0 ? driver.Squad[0] : null;
            if (primero == null) { Debug.LogWarning("[PrimerFrame] No hay escuadra."); return; }

            Camara(primero);
            Minimapa(primero);
            Hud(primero);
            Roster(driver);
            Cartel();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PrimerFrame] Escena lista: camara, minimapa mini, HUD, roster y cartel de mision como al iniciar.");
        }

        static void Camara(Soldier primero)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rot = Quaternion.Euler(0f, primero.transform.eulerAngles.y, 0f);
            cam.transform.SetPositionAndRotation(primero.transform.position + rot * OffsetDeCamara, rot);
            cam.fieldOfView = 60f;
            EditorUtility.SetDirty(cam.transform);
        }

        static void Minimapa(Soldier primero)
        {
            var mf = Object.FindFirstObjectByType<MinimapFollow>(FindObjectsInactive.Include);
            if (mf == null) return;
            mf.Target = primero.transform;
            mf.AplicarTamanoInicial();
            var p = primero.transform.position;
            mf.transform.position = new Vector3(p.x, mf.transform.position.y, p.z);
            EditorUtility.SetDirty(mf);
        }

        static void Hud(Soldier primero)
        {
            var salud = Object.FindFirstObjectByType<PlayerHealthView>(FindObjectsInactive.Include);
            if (salud != null)
            {
                var t = salud.GetComponentInChildren<Text>(true);
                if (t != null) t.text = $"VIDA   {primero.Health.Current}/{primero.Health.MaxHealth}";
                var fill = salud.transform.Find("BarBG/BarFill");
                if (fill != null && fill.GetComponent<Image>() != null) fill.GetComponent<Image>().fillAmount = 1f;
            }

            var arma = Object.FindFirstObjectByType<WeaponStatusView>(FindObjectsInactive.Include);
            if (arma != null)
            {
                WeaponStatusUiPipeline.ConstruirLayoutCompleto(arma.gameObject);
                var w = primero.Weapon;
                var t = arma.GetComponentInChildren<Text>(true);
                if (t != null && w != null)
                {
                    string ammoStr = w.CurrentAmmo.ToString();
                    if (w.UsaReservas) ammoStr += $" / {w.ReservaActual}";
                    else ammoStr += $" / {w.MagazineSize}";
                    t.text = ammoStr;
                }
                if (arma.Icon != null && w != null) arma.Icon.sprite = WeaponStatusView.IconFor(w.CurrentWeaponKind);
                arma.EnsureExtras();
                var fill = arma.transform.Find("BarBG/BarFill");
                if (fill != null && fill.GetComponent<Image>() != null) fill.GetComponent<Image>().fillAmount = 1f;
            }

            var mision = Object.FindFirstObjectByType<MissionStatusView>(FindObjectsInactive.Include);
            if (mision != null)
            {
                int enemigos = 0, escuadra = 0;
                foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (s.Health == null || !s.Health.IsAlive) continue;
                    if (s.Team == SP.Combat.TeamId.Enemy) enemigos++; else escuadra++;
                }
                var t = mision.GetComponentInChildren<Text>(true);
                if (t != null) t.text = $"ENEMIGOS {enemigos}   ·   ESCUADRA {escuadra}";
            }
        }

        // Solo la esquina inferior izquierda (roster) y la derecha (arma y municion), listas en modo edicion.
        [MenuItem("Strategic Point/Vista previa/Inicializar UI inferior (roster + arma)")]
        public static void InicializarUiInferior()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SC_Gameplay") { Debug.LogWarning("[PrimerFrame] Abri SC_Gameplay primero."); return; }
            var driver = Object.FindFirstObjectByType<PlayerInputDriver>(FindObjectsInactive.Include);
            Soldier primero = driver != null && driver.Squad != null && driver.Squad.Count > 0 ? driver.Squad[0] : null;
            if (primero == null) { Debug.LogWarning("[PrimerFrame] No hay escuadra."); return; }
            Roster(driver);
            var arma = Object.FindFirstObjectByType<WeaponStatusView>(FindObjectsInactive.Include);
            if (arma != null)
            {
                arma.EnsureExtras();
                CorregirIconosDeArma(arma);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PrimerFrame] UI inferior inicializada: roster con " + driver.Squad.Count + " filas y panel de arma.");
        }

        // Las Image del panel de arma se guardaron una vez con un Sprite generado por codigo (sin textura al reabrir la escena,
        // se dibujaban como ovalos punteados): se reapuntan a los PNG de Resources/UI/HudIcons.
        static void CorregirIconosDeArma(WeaponStatusView arma)
        {
            void Poner(string nombre, Sprite sprite)
            {
                var t = arma.transform.Find(nombre);
                var im = t != null ? t.GetComponent<Image>() : null;
                if (im == null || sprite == null) return;
                im.sprite = sprite; EditorUtility.SetDirty(im);
            }
            Poner("ExtrasCuchillo", SP.Presentation.HudIconFactory.Cuchillo());
            Poner("ExtrasGranada", SP.Presentation.HudIconFactory.Granada());
            Poner("Reloj", SP.Presentation.HudIconFactory.Reloj());
        }

        // Icono del rol como PNG real (Resources/UI/RoleIcons): asi el Image de la fila lo puede guardar la escena.
        static Sprite IconoDeRol(SP.Combat.RoleType rol)
        {
            const string dir = "Assets/_Project/Resources/UI/RoleIcons";
            string path = dir + "/RoleIcon_" + rol + ".png";
            var existente = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existente != null) return existente;
            System.IO.Directory.CreateDirectory(dir);
            var origen = SP.Presentation.RoleIconFactory.For(rol).texture;
            int w = origen.width, h = origen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, origen.GetPixel(x, y).a));
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void Roster(PlayerInputDriver driver)
        {
            var roster = Object.FindFirstObjectByType<RosterView>(FindObjectsInactive.Include);
            if (roster == null || driver.Squad == null) return;

            var prefab = new SerializedObject(roster).FindProperty("rowPrefab").objectReferenceValue as RosterRowView;
            if (prefab == null) return;

            for (int i = roster.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(roster.transform.GetChild(i).gameObject);

            int indice = 1;
            foreach (var s in driver.Squad)
            {
                if (s == null) continue;
                var fila = (RosterRowView)PrefabUtility.InstantiatePrefab(prefab, roster.transform);
                fila.gameObject.name = "Row_" + s.DisplayName;
                bool poseido = indice == 1;

                // Mismo aspecto que arma RosterRowView.Bind en partida: icono del rol, solo el numero de vida y la barra llena.
                var so = new SerializedObject(fila);
                var fondo = so.FindProperty("background").objectReferenceValue as Image;
                var label = so.FindProperty("label").objectReferenceValue as Text;
                var relleno = so.FindProperty("healthFill").objectReferenceValue as Image;
                var icono = so.FindProperty("icon").objectReferenceValue as Image;
                if (fondo != null) { fondo.color = poseido ? new Color(0.15f, 0.55f, 0.85f, 0.9f) : new Color(0f, 0f, 0f, 0.85f); EditorUtility.SetDirty(fondo); }
                if (label != null) { label.text = "<size=12>" + s.Health.Current + "</size>"; label.color = Color.white; EditorUtility.SetDirty(label); }
                if (relleno != null) { relleno.fillAmount = 1f; EditorUtility.SetDirty(relleno); }
                if (icono != null) { icono.sprite = IconoDeRol(s.Role); EditorUtility.SetDirty(icono); }
                indice++;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)roster.transform);
        }

        static void Cartel()
        {
            var fase = Object.FindFirstObjectByType<PhaseBannerView>(FindObjectsInactive.Include);
            if (fase == null) return;
            var t = fase.GetComponentInChildren<Text>(true);
            if (t == null) return;
            t.text = "Elimina a todos los enemigos\nmanteniendo viva a tu escuadra";
            t.gameObject.SetActive(true);
            FondoOpaco.Poner(t);
        }
    }
}
