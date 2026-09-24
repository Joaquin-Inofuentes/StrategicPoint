using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.Mision
{
    // Cinematica de victoria: la escuadra y el civil suben al helicoptero, este despega en una
    // nube de polvo y huye hacia el oeste mientras una horda de enemigos llega corriendo al
    // helipuerto y le dispara desde el piso. Barras de cine, camara propia, fundido a negro y
    // cartel "MISION CUMPLIDA"; al terminar aparece la pantalla de victoria de siempre.
    public class CinematicaDeVictoria : MonoBehaviour
    {
        public bool EnCurso { get; private set; }
        public bool Terminada { get; private set; }
        public int SoldadosDeLaHorda { get; private set; }
        public int TracerasDisparadas { get; private set; }
        public float Tiempo { get; private set; }
        public string Plano { get; private set; } = "";

        public const float Duracion = 15.5f;

        readonly List<Soldier> horda = new List<Soldier>();
        readonly List<Vector3> hordaDestino = new List<Vector3>();
        readonly List<Soldier> pasajeros = new List<Soldier>();
        GameObject lienzo;
        Image negro, barraArriba, barraAbajo;
        Text titulo;

        public void Iniciar(MisionDirector m, PlayerInputDriver driver, GameOutcomeController outcome, Helicoptero heli, GameObject enemigoPrefab, AudioSource tension)
        {
            if (EnCurso) return;
            StartCoroutine(Rutina(m, driver, outcome, heli, enemigoPrefab, tension));
        }

        // ---------------- capa de cine ----------------
        void ArmarLienzo()
        {
            lienzo = new GameObject("CinematicaLienzo", typeof(Canvas), typeof(CanvasScaler));
            var c = lienzo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 900;
            var sc = lienzo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            negro = Rect(lienzo.transform, "Negro", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            barraArriba = Rect(lienzo.transform, "BarraArriba", new Vector2(0f, 1f), new Vector2(1f, 1f), Color.black);
            barraAbajo = Rect(lienzo.transform, "BarraAbajo", new Vector2(0f, 0f), new Vector2(1f, 0f), Color.black);
            barraArriba.rectTransform.pivot = new Vector2(0.5f, 1f); barraAbajo.rectTransform.pivot = new Vector2(0.5f, 0f);
            barraArriba.rectTransform.sizeDelta = new Vector2(0f, 0f); barraAbajo.rectTransform.sizeDelta = new Vector2(0f, 0f);
            negro.transform.SetAsFirstSibling();

            var go = new GameObject("Titulo", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(lienzo.transform, false);
            titulo = go.GetComponent<Text>();
            titulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titulo.fontSize = 84; titulo.fontStyle = FontStyle.Bold; titulo.alignment = TextAnchor.MiddleCenter;
            titulo.color = new Color(1f, 0.9f, 0.4f, 0f); titulo.raycastTarget = false;
            var rt = titulo.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(1500f, 200f);
            titulo.text = "MISION CUMPLIDA";
        }

        static Image Rect(Transform padre, string nombre, Vector2 aMin, Vector2 aMax, Color color)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(padre, false);
            var img = g.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            var r = img.rectTransform; r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = r.offsetMax = Vector2.zero;
            return img;
        }

        // ---------------- secuencia ----------------
        IEnumerator Rutina(MisionDirector m, PlayerInputDriver driver, GameOutcomeController outcome, Helicoptero heli, GameObject enemigoPrefab, AudioSource tension)
        {
            EnCurso = true;
            // Pedido explicito: "en la cinematica se desactivan todos los
            // rombos, todos" -- el jugador ya no controla la camara ni
            // apunta nada durante la secuencia, asi que los rombos de
            // equipo/objetivo/interaccion solo estorbarian el plano.
            RomboVisibilidad.Suprimidos = true;
            ArmarLienzo();

            // 1. el jugador pierde el control y se apaga el HUD del juego
            if (driver != null)
            {
                OrderService.ManejadoAMano = null;
                driver.enabled = false;
                if (driver.Brain != null && driver.Brain.Current != null && driver.Brain.Current.Brain != null)
                    driver.Brain.Current.Brain.IsPossessedByPlayer = false;
            }
            foreach (var cv in FindObjectsByType<Canvas>())
                if (cv != null && cv.gameObject != lienzo && cv.renderMode != RenderMode.WorldSpace) cv.enabled = false;
            AlertQueue.Clear();

            var cam = SP.Core.CamaraPrincipal.Actual != null ? SP.Core.CamaraPrincipal.Actual : (driver != null && driver.Rig != null ? driver.Rig.Cam : null);
            if (driver != null && driver.Rig != null) driver.Rig.enabled = false;
            if (cam != null) cam.fieldOfView = 46f;

            if (heli == null)
            {
                yield return EsperarYCerrar(outcome, 2f);
                yield break;
            }
            heli.Volando = false;
            heli.Alerta(true);
            heli.DisparaCobertura = true;
            var pad = heli.transform.position;

            // 2. todos al helicoptero
            var puertaLocal = pad + heli.transform.right * 1.6f;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                pasajeros.Add(s);
                if (s.Brain != null) { s.Brain.IsPossessedByPlayer = false; s.Brain.Pasivo = true; s.Brain.IssueMoveOrder(puertaLocal + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f))); }
            }

            // 3. la horda aparece en el horizonte y corre al helipuerto
            int n = Mathf.Max(8, Mathf.RoundToInt(14f * Dificultad.PerfilActual.CantidadDeOleadas));
            if (m != null)
                for (int i = 0; i < n; i++)
                {
                    var s = m.CrearEnemigo("Enemigo_HordaFinal_" + (i + 1), new Vector3(Random.Range(-34f, 14f), 0f, Random.Range(14f, 36f)), 180f);
                    if (s == null) continue;
                    s.Brain.Pasivo = true;   // solo corren hacia el helicoptero
                    var destino = pad + new Vector3(Random.Range(-8f, 10f), 0f, Random.Range(7f, 14f));
                    s.Brain.IssueMoveOrder(destino);
                    horda.Add(s);
                    hordaDestino.Add(destino);
                }
            SoldadosDeLaHorda = horda.Count;

            float t = 0f;
            var pool = ProjectilePool.Activo;
            float proximaTraza = 0f, proximoPolvo = 0f;
            bool despego = false;
            Vector3 heliInicio = heli.transform.position;
            float yawHeli = heli.transform.eulerAngles.y;
            var adelanteHeli = heli.transform.forward;

            while (t < Duracion)
            {
                float dt = Time.deltaTime;
                t += dt; Tiempo = t;

                // barras de cine
                float b = Mathf.Clamp01(t / 1f) * 110f;
                barraArriba.rectTransform.sizeDelta = new Vector2(0f, b);
                barraAbajo.rectTransform.sizeDelta = new Vector2(0f, b);

                // embarque: al llegar a la puerta (o a los 4 s) se apagan
                if (t > 2.2f)
                    for (int i = pasajeros.Count - 1; i >= 0; i--)
                    {
                        var p = pasajeros[i];
                        if (p == null) { pasajeros.RemoveAt(i); continue; }
                        var d = puertaLocal - p.transform.position; d.y = 0f;
                        if (d.magnitude < 1.8f || t > 4.6f) { p.gameObject.SetActive(false); pasajeros.RemoveAt(i); }
                    }

                // despegue a los 4,8 s: sube, cabecea y sale hacia el oeste
                if (t >= 4.8f)
                {
                    despego = true;
                    heli.Volando = true;
                    float k = t - 4.8f;
                    float alto = Mathf.Min(38f, 1.5f * k * k);                 // aceleracion vertical
                    float avance = Mathf.Max(0f, 0.9f * k * k * k * 0.35f);       // sale despues de ganar altura
                    heli.transform.position = heliInicio + Vector3.up * alto + adelanteHeli * avance;
                    heli.transform.rotation = Quaternion.Euler(Mathf.Min(14f, k * 4f), yawHeli, Mathf.Sin(k * 1.3f) * 3f);
                }

                // polvo en el helipuerto mientras las helices ruegan
                if (t >= proximoPolvo && t < 9f)
                {
                    proximoPolvo = t + 0.07f;
                    float ang = Random.value * Mathf.PI * 2f;
                    ImpactFx.Spawn(pad + new Vector3(Mathf.Cos(ang), 0.3f, Mathf.Sin(ang)) * Random.Range(2f, 9f), new Color(0.65f, 0.52f, 0.38f), Random.Range(1.8f, 3.2f), 0.9f);
                }

                // la horda dispara al helicoptero desde el piso (trazadoras, sin dano). Solo
                // dispara el que ya llego a su puesto (no en plena carrera): se para, gira para
                // encarar el helicoptero y recien ahi tira -- y se publica ShotFiredEvent para que
                // el Animator levante la pose de disparo real en vez de verse correr con balas
                // saliendole del pecho, que era la queja original.
                if (t >= 5.2f && pool != null && t >= proximaTraza && horda.Count > 0)
                {
                    proximaTraza = t + 0.05f;
                    for (int i = 0; i < 2; i++)
                    {
                        int idx = Random.Range(0, horda.Count);
                        var s = horda[idx];
                        if (s == null || !s.gameObject.activeInHierarchy) continue;
                        var faltante = hordaDestino[idx] - s.transform.position; faltante.y = 0f;
                        if (faltante.sqrMagnitude > 6.25f) continue; // todavia corriendo: no dispara en el aire

                        var mira = heli.transform.position + Vector3.up * 1.4f;
                        var haciaHeli = mira - s.transform.position; haciaHeli.y = 0f;
                        if (haciaHeli.sqrMagnitude > 0.01f) s.transform.rotation = Quaternion.LookRotation(haciaHeli.normalized);

                        var boca = s.Weapon != null && s.Weapon.Muzzle != null
                            ? s.Weapon.Muzzle.position
                            : s.transform.position + Vector3.up * 1.4f + s.transform.forward * 0.5f;
                        var dir = (mira - boca).normalized;
                        dir = (dir + Random.insideUnitSphere * 0.03f).normalized;
                        pool.Spawn(boca, dir, -1, TeamId.Enemy, 0, new Color(1f, 0.45f, 0.25f));
                        MuzzleLightPool.Flash(boca, new Color(1f, 0.6f, 0.3f), 4f, 7f);
                        EventBus.Instance.Publish(new ShotFiredEvent(s.Id));
                        TracerasDisparadas++;
                    }
                }

                // camara: plano 1 (0-4,8 s) bajo y lateral hacia el helicoptero con la horda al fondo;
                // plano 2 (4,8-10,5 s) lo sigue mientras sube; plano 3 (10,5 s..) abierto sobre la horda.
                if (cam != null)
                {
                    Vector3 mira, pos;
                    if (t < 4.8f)
                    {
                        Plano = "1 · helipuerto";
                        float u = t / 4.8f;
                        pos = pad + new Vector3(14f - 3f * u, 2.4f + 0.8f * u, -13f + 2f * u);
                        mira = pad + new Vector3(-2f, 3.2f, 6f);
                    }
                    else if (t < 10.5f)
                    {
                        Plano = "2 · despegue";
                        var hp = heli.transform.position;
                        pos = hp + new Vector3(12f, 3f, -16f) + Vector3.up * Mathf.Min(6f, (t - 4.8f) * 1.2f);
                        mira = Vector3.Lerp(hp, pad + new Vector3(0f, 1f, 9f), 0.25f);
                    }
                    else
                    {
                        // Camara baja, a la altura de la horda (no un plano cenital generico):
                        // queda entre los soldados que disparan, con el fogonazo de las trazadoras
                        // en primer plano y el helicoptero huyendo al fondo. Un vaiveen suave
                        // (mano en cámara) le da energia en vez de un travelling perfecto.
                        Plano = "3 · la horda dispara";
                        float u = Mathf.Clamp01((t - 10.5f) / 5f);
                        float vaivenX = Mathf.Sin(t * 11f) * 0.05f;
                        float vaivenY = Mathf.Sin(t * 17f + 1.3f) * 0.035f;
                        pos = pad + new Vector3(-6f + u * 10f, 2.1f + vaivenY, 5f - u * 2f);
                        mira = Vector3.Lerp(heli.transform.position, pad + new Vector3(2f, 3f, 9f), 0.3f) + new Vector3(vaivenX, 0f, 0f);
                    }
                    cam.transform.position = Vector3.Lerp(cam.transform.position, pos, t < 0.05f ? 1f : Mathf.Clamp01(dt * 6f));
                    var rotDeseada = Quaternion.LookRotation((mira - cam.transform.position).normalized);
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rotDeseada, t < 0.05f ? 1f : Mathf.Clamp01(dt * 5f));
                }

                // musica: la tension sigue hasta el despegue y baja despues
                if (tension != null && despego) tension.volume = Mathf.MoveTowards(tension.volume, 0.25f, dt * 0.25f);

                // fundido final y titulo
                if (t > Duracion - 3f)
                {
                    float f = Mathf.Clamp01((t - (Duracion - 3f)) / 1.6f);
                    negro.color = new Color(0f, 0f, 0f, f * 0.85f);
                    titulo.color = new Color(1f, 0.9f, 0.4f, f);
                }
                yield return null;
            }

            Plano = "fin";
            yield return EsperarYCerrar(outcome, 0.4f);
        }

        IEnumerator EsperarYCerrar(GameOutcomeController outcome, float espera)
        {
            yield return new WaitForSeconds(espera);
            RomboVisibilidad.Suprimidos = false;
            foreach (var s in horda) if (s != null) Destroy(s.gameObject);
            horda.Clear();
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (cv != null && cv.gameObject != lienzo && cv.renderMode != RenderMode.WorldSpace) cv.enabled = true;
            if (lienzo != null) lienzo.SetActive(false);
            Terminada = true;
            EnCurso = false;
            if (outcome != null) outcome.ShowVictory();
        }
    }
}
