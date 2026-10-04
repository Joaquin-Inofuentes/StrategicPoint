using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Vehicles;

namespace SP.UI
{
    // La mira del ARTILLERO del tanque / metralleta (pedido: "se vea la mira, la torreta no se ve bien donde apunta;
    // el radial de mira igual que con los soldados: radial de vida del enemigo y de recarga del cañon").
    //
    // Antes, sin zoom, el artillero solo tenia un cuadradito verde de 14 px y una barra: no se sabia hacia donde apuntaba el tubo.
    // Ahora, desde que se sienta (con o sin zoom) se ve:
    //   - una CRUCETA clara (anillo + 4 marcas + punto) en el centro de la vista = hacia donde mira el tubo; verde si el tubo ya
    //     llego al angulo pedido por el mouse, ambar mientras todavia gira
    //   - un ANILLO de vida del enemigo (igual que el de los soldados) cuando el tubo apunta a un soldado o vehiculo enemigo
    //   - un ANILLO de recarga del cañon (se llena hasta quedar listo)
    //   - un ROMBO de IMPACTO: donde va a caer el proyectil (el obus cae en arco, no donde apunta el tubo)
    // Todo se genera por codigo (sin assets) y vive bajo el Canvas del HUD.
    public static class MiraDeTorreta
    {
        static GameObject raiz;
        static Image cruceta, rombo;
        static CirculoDeProgreso anilloVida, anilloRecarga;
        static Text textoBlanco;
        static Sprite spriteCruceta;
        static readonly RaycastHit[] golpes = new RaycastHit[24];

        static readonly Color Listo = new Color(0.45f, 1f, 0.55f);
        static readonly Color Girando = new Color(1f, 0.85f, 0.35f);
        static readonly Color Enemigo = new Color(1f, 0.25f, 0.2f);
        static readonly Color VidaFondo = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color VidaRelleno = new Color(0.95f, 0.25f, 0.2f);
        static readonly Color RecargaFondo = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color RecargaRelleno = new Color(1f, 0.62f, 0.2f);

        public static bool Visible => raiz != null && raiz.activeSelf;
        public static string TextoDelBlanco => textoBlanco != null && textoBlanco.gameObject.activeSelf ? textoBlanco.text : "";
        public static bool ApuntaAEnemigo { get; private set; }
        public static float VidaDelBlanco01 { get; private set; }
        public static float Recarga01 { get; private set; } = 1f;
        public static Vector2 PosicionRomboPx { get; private set; }

        public static void Ocultar()
        {
            if (raiz != null && raiz.activeSelf) raiz.SetActive(false);
            ApuntaAEnemigo = false;
        }

        // arma: el cañon o la metralleta; vehiculo: el propio (para que el rayo no choque con el casco); conImpacto: el rombo de caida.
        public static void Actualizar(Transform canvasRoot, Camera cam, TurretWeapon arma, Transform vehiculo, bool conImpacto, bool periscopioActivo = false)
        {
            if (canvasRoot == null || cam == null || arma == null) { Ocultar(); return; }
            Asegurar(canvasRoot);
            if (!raiz.activeSelf) raiz.SetActive(true);

            // Cruceta: verde si el tubo ya llego, ambar mientras gira.
            cruceta.color = arma.IsOnTarget() ? Listo : Girando;
            // Con el periscopio (zoom) manda su propia reticula: la cruceta se esconde, los anillos y el rombo quedan.
            if (cruceta.enabled == periscopioActivo) cruceta.enabled = !periscopioActivo;

            // Que hay en el centro de la vista (el tubo mira donde mira la camara).
            Soldier soldado = null; Vehicle auto = null;
            var origen = cam.transform.position;
            var dir = cam.transform.forward;
            int n = Physics.RaycastNonAlloc(origen, dir, golpes, 400f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = golpes[i];
                if (h.distance >= mejor || h.distance < 0.5f) continue;
                if (vehiculo != null && h.collider.transform.IsChildOf(vehiculo)) continue;
                var s = h.collider.GetComponentInParent<Soldier>();
                var v = h.collider.GetComponentInParent<Vehicle>();
                mejor = h.distance;
                soldado = s; auto = v;
            }
            float vida = 0f; string nombre = null; bool enemigo = false;
            if (soldado != null && soldado.Team == TeamId.Enemy && soldado.Health != null && soldado.Health.IsAlive)
            {
                enemigo = true; vida = (float)soldado.Health.Current / Mathf.Max(1, soldado.Health.MaxHealth); nombre = soldado.DisplayName;
            }
            else if (auto != null && auto.Bando == TeamId.Enemy && !auto.IsDestroyed && auto.Health != null)
            {
                enemigo = true; vida = (float)auto.Health.Current / Mathf.Max(1, auto.Health.MaxHealth); nombre = auto.name.Replace("(Clone)", "");
            }
            ApuntaAEnemigo = enemigo;
            VidaDelBlanco01 = vida;
            // Bug #047: "quiero que cambie el cursor cuando apunto a un enemigo": la cruceta se pone roja y un poco mas grande.
            if (enemigo) cruceta.color = Enemigo;
            ((RectTransform)cruceta.transform).sizeDelta = Vector2.one * (enemigo ? 84f : 72f);
            anilloVida.SetVisible(enemigo);
            if (enemigo) anilloVida.SetProgreso(vida);
            textoBlanco.gameObject.SetActive(enemigo);
            if (enemigo) textoBlanco.text = $"{nombre.ToUpperInvariant()} · {Mathf.RoundToInt(vida * 100f)} %";

            // Recarga del cañon: el anillo se llena y desaparece al quedar listo.
            Recarga01 = arma.CooldownFraction01;
            bool recargando = Recarga01 < 0.999f;
            anilloRecarga.SetVisible(recargando);
            if (recargando) anilloRecarga.SetProgreso(Recarga01);

            // Rombo del punto de impacto balistico.
            bool verRombo = false;
            if (conImpacto)
            {
                var punto = TurretAimView.PredictedImpactPoint(arma);
                var sp = cam.WorldToScreenPoint(punto);
                if (sp.z > 1f)
                {
                    var rc = (RectTransform)raiz.transform;
                    var pantalla = new Vector2(sp.x, sp.y);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rc, pantalla, null, out var local) ||
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(rc, pantalla, cam, out local))
                    {
                        var medio = rc.rect.size * 0.5f;
                        local.x = Mathf.Clamp(local.x, -medio.x + 20f, medio.x - 20f);
                        local.y = Mathf.Clamp(local.y, -medio.y + 20f, medio.y - 20f);
                        ((RectTransform)rombo.transform).anchoredPosition = local;
                        PosicionRomboPx = local;
                        verRombo = true;
                    }
                }
            }
            if (rombo.gameObject.activeSelf != verRombo) rombo.gameObject.SetActive(verRombo);
        }

        static void Asegurar(Transform canvasRoot)
        {
            if (raiz != null) return;
            var existente = canvasRoot.Find("MiraDeTorreta");
            if (existente != null) Object.Destroy(existente.gameObject);

            raiz = new GameObject("MiraDeTorreta", typeof(RectTransform));
            raiz.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)raiz.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Anillos de vida (grande) y recarga (mediano), concentricos con la cruceta.
            anilloVida = CirculoDeProgreso.Construir(raiz.transform, 124f, VidaFondo, VidaRelleno);
            anilloVida.gameObject.name = "AnilloVidaEnemigo";
            Centrar((RectTransform)anilloVida.transform);
            anilloVida.SetVisible(false);
            anilloRecarga = CirculoDeProgreso.Construir(raiz.transform, 92f, RecargaFondo, RecargaRelleno);
            anilloRecarga.gameObject.name = "AnilloRecargaCanon";
            Centrar((RectTransform)anilloRecarga.transform);
            anilloRecarga.SetVisible(false);

            var cr = new GameObject("Cruceta", typeof(RectTransform), typeof(Image));
            cr.transform.SetParent(raiz.transform, false);
            cruceta = cr.GetComponent<Image>();
            cruceta.sprite = Cruceta();
            cruceta.raycastTarget = false;
            var borde = cr.AddComponent<Outline>();
            borde.effectColor = new Color(0f, 0f, 0f, 0.85f);
            borde.effectDistance = new Vector2(1.5f, -1.5f);
            var crt = (RectTransform)cr.transform;
            Centrar(crt);
            crt.sizeDelta = new Vector2(72f, 72f);

            var rm = new GameObject("RomboImpacto", typeof(RectTransform), typeof(Image));
            rm.transform.SetParent(raiz.transform, false);
            rombo = rm.GetComponent<Image>();
            rombo.sprite = SpriteBlanco.Obtener();
            rombo.color = new Color(0.4f, 0.9f, 1f, 0.95f);
            rombo.raycastTarget = false;
            var rrt = (RectTransform)rm.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(16f, 16f);
            rrt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var bordeR = rm.AddComponent<Outline>();
            bordeR.effectColor = new Color(0f, 0f, 0f, 0.9f);
            bordeR.effectDistance = new Vector2(1.5f, -1.5f);
            rm.SetActive(false);

            var tx = new GameObject("TextoBlanco", typeof(RectTransform), typeof(Text));
            tx.transform.SetParent(raiz.transform, false);
            textoBlanco = tx.GetComponent<Text>();
            textoBlanco.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoBlanco.fontSize = 16;
            textoBlanco.fontStyle = FontStyle.Bold;
            textoBlanco.alignment = TextAnchor.MiddleCenter;
            textoBlanco.color = new Color(1f, 0.55f, 0.45f);
            textoBlanco.raycastTarget = false;
            var tob = tx.AddComponent<Outline>();
            tob.effectColor = new Color(0f, 0f, 0f, 0.9f);
            tob.effectDistance = new Vector2(1.2f, -1.2f);
            var trt = (RectTransform)tx.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(320f, 24f);
            trt.anchoredPosition = new Vector2(0f, -82f);
            tx.SetActive(false);

            raiz.SetActive(false);
        }

        static void Centrar(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        // Anillo fino + 4 marcas con hueco central + punto: se lee sobre cielo, pasto y asfalto (con el borde oscuro de arriba).
        static Sprite Cruceta()
        {
            if (spriteCruceta != null) return spriteCruceta;
            const int lado = 128;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            var px = new Color32[lado * lado];
            float c = lado * 0.5f;
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                // anillo (radio 0.40 del lado, grosor ~3 px)
                a = Mathf.Max(a, Mathf.Clamp01(1.6f - Mathf.Abs(d - lado * 0.40f) * 0.9f));
                // marcas desde 0.10 hasta 0.30 del lado, grosor ~3 px, en los cuatro ejes
                bool eje = (Mathf.Abs(dx) < 1.6f && Mathf.Abs(dy) > lado * 0.10f && Mathf.Abs(dy) < lado * 0.30f)
                        || (Mathf.Abs(dy) < 1.6f && Mathf.Abs(dx) > lado * 0.10f && Mathf.Abs(dx) < lado * 0.30f);
                if (eje) a = 1f;
                // punto central
                a = Mathf.Max(a, Mathf.Clamp01(2.4f - d));
                px[y * lado + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;
            spriteCruceta = Sprite.Create(tex, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            spriteCruceta.name = "CrucetaDeTorreta";
            spriteCruceta.hideFlags = HideFlags.HideAndDontSave;
            return spriteCruceta;
        }
    }
}
