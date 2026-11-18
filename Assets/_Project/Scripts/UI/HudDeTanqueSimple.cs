using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using SP.Core;
using SP.Vehicles;

namespace SP.UI
{
    // Bug #374 (2026-11-08): "la barra de vida que aparezca en el centro abajo y oculta lo de los aliados temporalmente mientras estoy en el tanque.
    // Cuando estoy en el tanque unicamente deberia ver la barra de vida y una barra de recarga delgada simple".
    // Overlay propio (no depende del layout del HUD de a pie): una barra de vida del tanque y, debajo, una barra de recarga del canon de 6 px.
    // CapasDeHud.ModoTanque lo prende al subir y lo apaga al bajar; la lista blanca del modo tanque ya no deja pasar a la escuadra (Roster).
    public sealed class HudDeTanqueSimple : MonoBehaviour
    {
        public static HudDeTanqueSimple Instancia { get; private set; }
        public static bool Visible => Instancia != null && Instancia.raiz != null && Instancia.raiz.activeSelf;
        public float FraccionDeVida { get; private set; }
        public float FraccionDeRecarga { get; private set; }

        public const float AnchoDeBarra = 760f;
        GameObject raiz; Image vida, recarga; Text texto, textoRecarga; Vehicle vehiculo; TurretWeapon torreta;
        GameObject insigniaDios;
        public bool InsigniaDeDiosVisible => insigniaDios != null && insigniaDios.activeInHierarchy;
        public float AnchoDeLaBarraDeVida => vida != null ? ((RectTransform)vida.transform.parent).rect.width : 0f;
        public static Rect RectEnPantalla(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
        }
        public Rect RectDeVida => RectEnPantalla((RectTransform)vida.transform.parent);
        public Rect RectDeRecarga => RectEnPantalla((RectTransform)recarga.transform.parent);
        public Rect RectDeInsignia => RectEnPantalla((RectTransform)insigniaDios.transform);
        // #396: el HUD duplicado (VehicleStatusView, abajo a la derecha) y el cartel de modo dios (ModoDiosView, pisaba las barras) se esconden mientras
        // este HUD esta visible; el modo dios lo muestra una insignia propia a la izquierda de la barra de vida.
        readonly System.Collections.Generic.List<CanvasGroup> escondidos = new System.Collections.Generic.List<CanvasGroup>();
        float proximoBarrido;

        public static void Mostrar(Vehicle v)
        {
            if (Instancia == null) Crear();
            Instancia.vehiculo = v;
            // #399: la barra de recarga tiene que leer el CANON (la ametralladora tambien es TurretWeapon y su cooldown no baja al disparar el canon).
            Instancia.torreta = v != null ? (v.TorretaCanon != null ? v.TorretaCanon : v.GetComponentInChildren<TurretWeapon>(true)) : null;
            Instancia.raiz.SetActive(v != null);
            if (Instancia.anillo != null) Instancia.anillo.SetActive(v != null);
            Instancia.LimpiarMarcas();
        }

        static float destelloHasta;
        public static void Destellar() { destelloHasta = Time.unscaledTime + 0.25f; }   // #398: la barra de vida destella cuando le pegan al tanque
        public static void Ocultar() { if (Instancia != null && Instancia.raiz != null) { Instancia.raiz.SetActive(false); if (Instancia.anillo != null) Instancia.anillo.SetActive(false); Instancia.Restaurar(); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; }

        static void Crear()
        {
            var go = new GameObject("HudDeTanqueSimple"); DontDestroyOnLoad(go);
            var h = go.AddComponent<HudDeTanqueSimple>(); Instancia = h;
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            h.raiz = new GameObject("Raiz", typeof(RectTransform)); h.raiz.transform.SetParent(go.transform, false);
            var rt = (RectTransform)h.raiz.transform; rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 26f); rt.sizeDelta = new Vector2(AnchoDeBarra, 64f);
            h.vida = Barra(rt, "Vida", new Vector2(0f, 26f), new Vector2(AnchoDeBarra, 28f), new Color(0.35f, 0.9f, 0.4f), new Color(0.08f, 0.08f, 0.08f, 0.65f));   // Ronda 13 (T13): el fondo gris claro (0,88) se leia como un 'cuadro gris'; ahora oscuro y translucido, con borde
            h.recarga = Barra(rt, "Recarga", new Vector2(0f, 2f), new Vector2(AnchoDeBarra, 18f), new Color(1f, 0.8f, 0.3f));
            h.textoRecarga = Etiqueta(h.recarga.transform.parent, "TextoRecarga", 13, new Vector2(0f, 0f), new Vector2(0f, 18f));
            h.insigniaDios = Insignia(rt);
            var tgo = new GameObject("Texto", typeof(RectTransform), typeof(Text)); tgo.transform.SetParent(h.vida.transform.parent, false);
            h.texto = tgo.GetComponent<Text>(); h.texto.font = SP.Presentation.FuentesBelicas.Para(20); h.texto.fontSize = 20; h.texto.raycastTarget = false; h.texto.alignment = TextAnchor.MiddleCenter; h.texto.color = Color.white; h.texto.fontStyle = FontStyle.Bold;   // Ronda 13 (T13): texto blanco con sombra (el fondo ya es oscuro)
            var sombra = tgo.AddComponent<Shadow>(); sombra.effectColor = new Color(0f, 0f, 0f, 0.9f); sombra.effectDistance = new Vector2(1.5f, -1.5f);
            var borde = h.vida.transform.parent.gameObject.AddComponent<Outline>(); borde.effectColor = new Color(0f, 0f, 0f, 0.85f); borde.effectDistance = new Vector2(1.5f, -1.5f);
            var trt = (RectTransform)tgo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f); trt.offsetMin = trt.offsetMax = Vector2.zero;   // centrado DENTRO de la barra
            h.CrearAnillo(go.transform);
            h.raiz.SetActive(false);
        }

        static Text Etiqueta(Transform padre, string nombre, int tam, Vector2 pos, Vector2 delta)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text)); go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>(); t.font = SP.Presentation.FuentesBelicas.Para(tam); t.fontSize = tam; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return t;
        }

        // Insignia de modo dios (variante pedida en el #396): escudo dorado a la izquierda de la barra de vida, fuera de ella.
        static GameObject Insignia(RectTransform padre)
        {
            var go = new GameObject("InsigniaDios", typeof(RectTransform), typeof(Image)); go.transform.SetParent(padre, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(1f, 0f);
            r.anchoredPosition = new Vector2(-AnchoDeBarra * 0.5f - 8f, 26f); r.sizeDelta = new Vector2(96f, 28f);
            go.GetComponent<Image>().color = new Color(0.25f, 0.19f, 0.02f, 0.9f);
            var t = Etiqueta(go.transform, "Texto", 15, Vector2.zero, Vector2.zero); t.text = "DIOS · F4"; t.color = new Color(1f, 0.85f, 0.3f); t.fontStyle = FontStyle.Bold;
            go.SetActive(false);
            return go;
        }

        void Esconder()
        {
            if (Time.unscaledTime < proximoBarrido) return;
            proximoBarrido = Time.unscaledTime + 0.5f;
            foreach (var v in FindObjectsByType<VehicleStatusView>(FindObjectsInactive.Include)) Atenuar(v.gameObject);
            foreach (var m in FindObjectsByType<ModoDiosView>(FindObjectsInactive.Include)) Atenuar(m.gameObject);
        }

        void Atenuar(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>(); if (g == null) g = go.AddComponent<CanvasGroup>();
            if (!escondidos.Contains(g)) escondidos.Add(g);
        }

        void Restaurar() { foreach (var g in escondidos) if (g != null) g.alpha = 1f; escondidos.Clear(); }

        void LateUpdate()
        {
            if (raiz == null || !raiz.activeSelf) return;
            foreach (var g in escondidos) if (g != null) g.alpha = 0f;
        }

        static Image Barra(RectTransform padre, string nombre, Vector2 pos, Vector2 tam, Color color, Color? fondoColor = null)
        {
            var fondo = new GameObject(nombre + "_Fondo", typeof(RectTransform), typeof(Image)); fondo.transform.SetParent(padre, false);
            var frt = (RectTransform)fondo.transform; frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0f); frt.pivot = new Vector2(0.5f, 0f); frt.anchoredPosition = pos; frt.sizeDelta = tam;
            fondo.GetComponent<Image>().color = fondoColor ?? new Color(0f, 0f, 0f, 0.6f);
            var relleno = new GameObject(nombre, typeof(RectTransform), typeof(Image)); relleno.transform.SetParent(fondo.transform, false);
            var rrt = (RectTransform)relleno.transform; rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = new Vector2(1f, 1f); rrt.offsetMax = new Vector2(-1f, -1f); rrt.pivot = new Vector2(0f, 0.5f);
            var img = relleno.GetComponent<Image>(); img.color = color; img.type = Image.Type.Simple;   // 2026-11-13: Filled sin sprite NO recorta (la barra no bajaba); el relleno se acota con el ancla derecha (ver Rellenar)
            return img;
        }

        // Acota el relleno por el ancla derecha: la fraccion del ancho de la barra (el Image Filled no recorta si no tiene sprite).
        public static void Rellenar(Image img, float f) { var r = (RectTransform)img.transform; r.anchorMax = new Vector2(Mathf.Clamp01(f), 1f); }
        public float AnchoRellenoDeVida => vida != null ? ((RectTransform)vida.transform).anchorMax.x : 0f;

        // ---- Ronda 13 (T4): anillo de impactos ------------------------------------------------------------------------------
        // Hermano de 'raiz' en el mismo Canvas overlay: un aro tenue de 320 px (no pisa los anillos de MiraDeTorreta, 124 y 92 px) con hasta 8
        // marcas rojas de 22 grados. Cada golpe con origen conocido prende una marca hacia donde esta el atacante, medida contra la CAMARA
        // (la torreta gira, el casco no es la referencia); se apaga en 2 s con un pulso de escala de 0,15 s.
        public const float DiametroDelAnillo = 320f, DuracionDeMarca = 2f, DuracionDelPulso = 0.15f, ArcoDeMarcaGrados = 22f;
        public const int MaxMarcas = 8;
        GameObject anillo; RectTransform anilloRt;
        readonly List<Marca> marcas = new List<Marca>();
        IDisposable subDano;
        sealed class Marca { public RectTransform rt; public Image img; public Vector3 origen; public float t0; public bool libre = true; }
        static Sprite spriteDeAro, spriteDeArco;
        public static Color ColorDeMarca => new Color(1f, 0.15f, 0.1f, 1f);

        // Angulos (grados, -180..180, + = a la derecha de la camara) de las marcas encendidas. Los checks lo leen.
        public IReadOnlyList<float> AngulosActivos
        {
            get
            {
                var l = new List<float>();
                foreach (var m in marcas) if (!m.libre) l.Add(AnguloDe(m.origen));
                return l;
            }
        }
        public int CantidadDeMarcas { get { int n = 0; foreach (var m in marcas) if (!m.libre) n++; return n; } }
        public RectTransform RectDelAnillo => anilloRt;
        public bool AnilloActivo => anillo != null && anillo.activeInHierarchy;

        static Camera CamaraDeRef()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            var c = d != null && d.Rig != null ? d.Rig.Cam : null;
            return c != null ? c : CamaraPrincipal.Actual;
        }

        float AnguloDe(Vector3 origen)
        {
            var cam = CamaraDeRef();
            Vector3 centro = vehiculo != null ? vehiculo.transform.position : (cam != null ? cam.transform.position : Vector3.zero);
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            if (fwd.sqrMagnitude < 1e-4f) fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.up, Vector3.up) : Vector3.forward;
            Vector3 dir = Vector3.ProjectOnPlane(origen - centro, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) return 0f;
            return Vector3.SignedAngle(fwd.normalized, dir.normalized, Vector3.up);
        }

        static Sprite SpriteRadial(bool arco)
        {
            if (arco ? spriteDeArco != null : spriteDeAro != null) return arco ? spriteDeArco : spriteDeAro;
            const int n = 256; float r = n * 0.5f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = arco ? "R13_ArcoImpacto" : "R13_AroImpacto" };
            var px = new Color32[n * n];
            float grosor = arco ? 0.11f : 0.012f, mitad = ArcoDeMarcaGrados * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (d <= 1f && d >= 1f - grosor) a = Mathf.Clamp01((d - (1f - grosor)) * r + 0.5f) * Mathf.Clamp01((1f - d) * r + 0.5f);
                    if (arco)
                    {
                        float ang = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);   // 0 = arriba
                        if (ang > mitad) a = 0f; else a *= Mathf.Clamp01((mitad - ang) * 1.5f);
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            if (arco) spriteDeArco = sp; else spriteDeAro = sp;
            return sp;
        }

        void CrearAnillo(Transform padreCanvas)
        {
            anillo = new GameObject("AnilloDeImpactos", typeof(RectTransform)); anillo.transform.SetParent(padreCanvas, false);
            anilloRt = (RectTransform)anillo.transform; anilloRt.anchorMin = anilloRt.anchorMax = anilloRt.pivot = new Vector2(0.5f, 0.5f);
            anilloRt.anchoredPosition = Vector2.zero; anilloRt.sizeDelta = new Vector2(DiametroDelAnillo, DiametroDelAnillo);
            var aro = new GameObject("Aro", typeof(RectTransform), typeof(Image)); aro.transform.SetParent(anillo.transform, false);
            var art = (RectTransform)aro.transform; art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = art.offsetMax = Vector2.zero;
            var ai = aro.GetComponent<Image>(); ai.sprite = SpriteRadial(false); ai.color = new Color(1f, 1f, 1f, 0.18f); ai.raycastTarget = false;
            for (int i = 0; i < MaxMarcas; i++)
            {
                var go = new GameObject("MarcaDeImpacto_" + i, typeof(RectTransform), typeof(Image)); go.transform.SetParent(anillo.transform, false);
                var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
                var im = go.GetComponent<Image>(); im.sprite = SpriteRadial(true); im.color = ColorDeMarca; im.raycastTarget = false;
                go.SetActive(false);
                marcas.Add(new Marca { rt = rt, img = im });
            }
            anillo.SetActive(false);
            subDano?.Dispose();
            subDano = EventBus.Instance.Subscribe<VehicleDamagedEvent>(AlGolpe);
        }

        void OnDestroy() { subDano?.Dispose(); subDano = null; }

        void LimpiarMarcas() { foreach (var m in marcas) { m.libre = true; if (m.rt != null) m.rt.gameObject.SetActive(false); } }

        void AlGolpe(VehicleDamagedEvent e)
        {
            if (vehiculo == null || e.Vehicle != vehiculo || e.Amount <= 0) return;
            if (e.TieneOrigen) RegistrarImpacto(e.Origen);
        }

        // Prende una marca hacia 'origen' (punto de mundo). Publico: los checks y los emisores que ya sepan de donde viene el golpe lo usan.
        public static void RegistrarImpactoEn(Vehicle v, Vector3 origen)
        {
            if (Instancia == null || Instancia.vehiculo != v || v == null) return;
            Instancia.RegistrarImpacto(origen);
        }

        void RegistrarImpacto(Vector3 origen)
        {
            if (anillo == null || !anillo.activeSelf) return;
            Marca elegida = null;
            foreach (var m in marcas) if (m.libre) { elegida = m; break; }
            if (elegida == null) { elegida = marcas[0]; foreach (var m in marcas) if (m.t0 < elegida.t0) elegida = m; }   // todas ocupadas: se recicla la mas vieja
            elegida.libre = false; elegida.origen = origen; elegida.t0 = Time.unscaledTime;
            elegida.rt.gameObject.SetActive(true);
            PosarMarca(elegida, 0f);
        }

        void PosarMarca(Marca m, float edad)
        {
            float ang = AnguloDe(m.origen);
            m.rt.localRotation = Quaternion.Euler(0f, 0f, -ang);
            float pulso = edad < DuracionDelPulso ? Mathf.Lerp(1.15f, 1f, edad / DuracionDelPulso) : 1f;
            m.rt.localScale = new Vector3(pulso, pulso, 1f);
            var c = ColorDeMarca; c.a = Mathf.Clamp01(1f - edad / DuracionDeMarca); m.img.color = c;
        }

        void ActualizarMarcas()
        {
            foreach (var m in marcas)
            {
                if (m.libre) continue;
                float edad = Time.unscaledTime - m.t0;
                if (edad >= DuracionDeMarca) { m.libre = true; m.rt.gameObject.SetActive(false); continue; }
                PosarMarca(m, edad);
            }
        }

        void Update()
        {
            if (raiz == null || !raiz.activeSelf) return;
            if (vehiculo == null || vehiculo.IsDestroyed || !CapasDeHud.EnModoTanque) { raiz.SetActive(false); if (anillo != null) anillo.SetActive(false); Restaurar(); return; }
            Esconder();
            ActualizarMarcas();
            if (insigniaDios != null) insigniaDios.SetActive(SP.Core.ModoDios.Activo);
            var hp = vehiculo.Health;
            FraccionDeVida = hp != null && hp.MaxHealth > 0 ? Mathf.Clamp01(hp.Current / (float)hp.MaxHealth) : 0f;
            Rellenar(vida, FraccionDeVida);
            vida.color = Time.unscaledTime < destelloHasta ? Color.white : Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.35f, 0.9f, 0.4f), FraccionDeVida);
            texto.text = hp != null ? $"TANQUE  {hp.Current} / {hp.MaxHealth}" : "TANQUE";
            FraccionDeRecarga = torreta != null ? torreta.CooldownFraction01 : 1f;
            Rellenar(recarga, FraccionDeRecarga);
            recarga.color = FraccionDeRecarga >= 0.999f ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.8f, 0.3f);
            if (textoRecarga != null) textoRecarga.text = FraccionDeRecarga >= 0.999f ? "CAÑÓN · LISTO" : $"CAÑÓN · RECARGANDO {Mathf.RoundToInt(FraccionDeRecarga * 100f)} %";
        }
    }
}
