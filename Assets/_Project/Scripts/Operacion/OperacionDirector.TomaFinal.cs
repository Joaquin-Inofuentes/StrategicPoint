using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Presentation;
using SP.UI;

namespace SP.Operacion
{
    // Bug #077: toma final de la extraccion. En vez de planos aereos y un fundido a negro, la camara salta al piso (1,2 m), detras
    // y entre tres enemigos de la plaza que giran hacia el helicoptero y le disparan mientras se achica (de ~60 a 250 m). La pantalla
    // de resultado entra translucida sobre esta misma toma, que sigue en camara lenta.
    public partial class OperacionDirector
    {
        public const float AlfaDeFondoDeVictoria = 0.55f;
        public const float AlturaDeCamaraFinal = 1.2f;

        readonly List<Soldier> tiradoresFinales = new List<Soldier>();
        readonly Dictionary<Soldier, float> ultimoTiro = new Dictionary<Soldier, float>();
        float[] proximoTiro, pausaTiro; int[] restantesTiro;
        Vector3 posCamaraFinal;
        Camera camaraFinal;
        static readonly RaycastHit[] bufferPiso = new RaycastHit[8];

        public bool TomaFinalActiva { get; private set; }
        public Camera CamaraFinal => camaraFinal;
        public IReadOnlyList<Soldier> TiradoresFinales => tiradoresFinales;
        public int DisparosDeLaTomaFinal { get; private set; }
        public int FigurantesDeLaTomaFinal { get; private set; }
        public float DistanciaInicialCamaraHeli { get; private set; }
        public float DistanciaMaximaCamaraHeli { get; private set; }
        public float DistanciaCamaraHeli { get; private set; }
        public float AlturaDeCamaraSobrePiso { get; private set; }

        // Cuantos tiradores dispararon en los ultimos `ventana` segundos y estan dentro del frustum de la camara final.
        public int TiradoresDisparandoEnPantalla(float ventana = 1f)
        {
            if (camaraFinal == null) return 0;
            var planos = GeometryUtility.CalculateFrustumPlanes(camaraFinal);
            int n = 0;
            foreach (var s in tiradoresFinales)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                if (!ultimoTiro.TryGetValue(s, out float t) || Time.unscaledTime - t > ventana) continue;
                var b = new Bounds(s.transform.position, new Vector3(0.8f, 1.8f, 0.8f));
                if (GeometryUtility.TestPlanesAABB(planos, b)) n++;
            }
            return n;
        }

        static bool PisoBajo(Vector3 desde, out float y)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(desde.x, desde.y + 4f, desde.z), Vector3.down, bufferPiso, 14f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue; y = 0f; bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var c = bufferPiso[i].collider;
                if (c == null || c.GetComponentInParent<Soldier>() != null) continue;
                if (bufferPiso[i].distance < mejor) { mejor = bufferPiso[i].distance; y = bufferPiso[i].point.y; hay = true; }
            }
            return hay;
        }

        // Elige a los tres tiradores (enemigos reales de la plaza; si faltan, figurantes tomados de la reserva), fija la camara y
        // esconde el HUD. heliBase = donde estaba posado; rumbo = hacia donde se va.
        void PrepararTomaFinal(Camera cam, Vector3 heliBase, Vector3 rumbo)
        {
            TomaFinalActiva = true;
            camaraFinal = cam;
            tiradoresFinales.Clear(); ultimoTiro.Clear();
            DisparosDeLaTomaFinal = 0; FigurantesDeLaTomaFinal = 0;
            DistanciaMaximaCamaraHeli = 0f; DistanciaInicialCamaraHeli = 0f;

            var atras = -Plano(rumbo, Vector3.zero); atras = atras.sqrMagnitude > 0.01f ? atras.normalized : Vector3.back;
            var lateral = Vector3.Cross(Vector3.up, atras);
            var punto0 = heliBase + atras * 30f;

            // 1) enemigos reales, vivos y activos cerca de donde tiene que quedar la toma.
            var cand = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && Plano(s.transform.position, punto0).magnitude < 55f)
                    cand.Add(s);
            cand.Sort((a, b) => Plano(a.transform.position, punto0).sqrMagnitude.CompareTo(Plano(b.transform.position, punto0).sqrMagnitude));
            for (int i = 0; i < cand.Count && tiradoresFinales.Count < 3; i++) tiradoresFinales.Add(cand[i]);

            // 2) figurantes: enemigos vivos de la reserva (apagados) puestos en fila detras del helipuerto.
            if (tiradoresFinales.Count < 3)
            {
                int k = tiradoresFinales.Count;
                foreach (var s in ActorRegistry.All)
                {
                    if (tiradoresFinales.Count >= 3) break;
                    if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.gameObject.activeInHierarchy || tiradoresFinales.Contains(s)) continue;
                    var deseada = punto0 + lateral * ((k - 1) * 3.4f + Random.Range(-0.6f, 0.6f)) + atras * Random.Range(-1.5f, 1.5f);
                    if (UnityEngine.AI.NavMesh.SamplePosition(deseada, out var h, 8f, UnityEngine.AI.NavMesh.AllAreas)) deseada = h.position;
                    s.transform.position = new Vector3(deseada.x, s.transform.position.y, deseada.z);
                    s.gameObject.SetActive(true);
                    ApoyoEnElPiso.Apoyar(s.transform);
                    tiradoresFinales.Add(s);
                    FigurantesDeLaTomaFinal++;
                    k++;
                }
            }
            int n = tiradoresFinales.Count;
            proximoTiro = new float[n]; pausaTiro = new float[n]; restantesTiro = new int[n];
            var centro = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var s = tiradoresFinales[i];
                if (s.Brain != null) { s.Brain.Pasivo = true; s.Brain.Quieto = true; }
                if (s.Motor != null) s.Motor.SetCrouching(false);
                proximoTiro[i] = Time.time + 0.1f + i * 0.17f;
                centro += s.transform.position;
            }
            if (n == 0) centro = punto0; else centro /= n;

            // Camara: detras del grupo (del lado opuesto al helicoptero), a 1,2 m del piso, con un leve descentrado.
            var haciaGrupo = Plano(centro - heliBase, Vector3.zero);
            haciaGrupo = haciaGrupo.sqrMagnitude > 0.01f ? haciaGrupo.normalized : atras;
            var pos = centro + haciaGrupo * 3.8f + Vector3.Cross(Vector3.up, haciaGrupo) * 0.7f;
            pos.y = centro.y;
            if (PisoBajo(pos, out float piso)) pos.y = piso + AlturaDeCamaraFinal; else pos.y = centro.y - 0.8f + AlturaDeCamaraFinal;
            pos = CinematicaDeVictoria.EvitarObstruccion(centro + Vector3.up * 1.2f, pos);
            if (PisoBajo(pos, out piso)) pos.y = Mathf.Max(pos.y, piso + AlturaDeCamaraFinal) ;
            // Validacion #077: en la partida real los enemigos que quedan estan pegados a casas o en callejones y la camara caia a menos de 1 m de una
            // pared o sin ver al helicoptero (cuadro tapado, tiradores fuera de plano). Se prueban posiciones alrededor del grupo y se queda con la
            // que mejor ve; si ninguna sirve, los tiradores pasan a una fila despejada detras del helipuerto (el corte de camara oculta el salto).
            int minTiradores = Mathf.Min(2, tiradoresFinales.Count);
            bool buena = CamaraFinalLibre(pos, heliBase, rumbo, tiradoresFinales, out int vis0, out int heliVis0, out _) && heliVis0 >= 4 && vis0 >= minTiradores;
            if (!buena)
            {
                if (ElegirCamaraFinal(centro, haciaGrupo, heliBase, rumbo, out var mejor, out int visM, out int heliM)) { pos = mejor; buena = heliM >= 4 && visM >= minTiradores; }
                if (!buena)
                {
                    var enFila = ReubicarTiradoresEnFila(punto0, lateral, atras);
                    if (enFila != Vector3.zero)
                    {
                        centro = enFila;
                        haciaGrupo = Plano(centro - heliBase, Vector3.zero); haciaGrupo = haciaGrupo.sqrMagnitude > 0.01f ? haciaGrupo.normalized : atras;
                        if (ElegirCamaraFinal(centro, haciaGrupo, heliBase, rumbo, out mejor, out visM, out heliM)) pos = mejor;
                    }
                }
            }
            posCamaraFinal = pos;
            cam.transform.position = pos;
            cam.fieldOfView = 58f;
            var mira0 = heli.position;
            cam.transform.rotation = RotacionHaciaElHeli(pos, mira0);

            OcultarHudDeLaToma();
            if (baliza != null) { baliza.Quitar(); baliza = null; }   // el cartel "HELICOPTERO" del piso no entra en la toma
            DistanciaInicialCamaraHeli = (heli.position - pos).magnitude;
            GameLog.Line($"[Operacion] Toma final: {n} tiradores ({FigurantesDeLaTomaFinal} figurantes), camara a {AlturaDeCamaraFinal:0.0} m, heli a {DistanciaInicialCamaraHeli:0} m");
        }

        static readonly RaycastHit[] bufferVista = new RaycastHit[16];

        // Distancia al primer solido de verdad (pared, casa, auto; ni el piso ni actores) entre a y b, o -1 si el camino esta libre.
        static float SolidoEntre(Vector3 a, Vector3 b)
        {
            var d = b - a; float dist = d.magnitude;
            if (dist < 0.01f) return -1f;
            int n = Physics.RaycastNonAlloc(a, d / dist, bufferVista, dist, ~0, QueryTriggerInteraction.Ignore);
            float mejor = -1f;
            for (int i = 0; i < n; i++)
            {
                var h = bufferVista[i];
                if (h.normal.y > 0.7f && h.point.y < a.y - 0.3f) continue;   // el piso no tapa el cuadro
                if (!NavService.BlocksMovement(h.collider)) continue;
                if (mejor < 0f || h.distance < mejor) mejor = h.distance;
            }
            return mejor;
        }

        // Una camara es libre si no esta metida en un solido (< 0,9 m) y tiene el campo de vision libre 7 m al frente (13 de 15 rayos).
        // Ademas cuenta cuantos tiradores se ven sin nada en el medio y en cuantos de 5 instantes de la salida del helicoptero (los 4 s de toma) se lo ve.
        static bool CamaraFinalLibre(Vector3 pos, Vector3 heliBase, Vector3 rumbo, List<Soldier> tiradores, out int visibles, out int heliVisto, out int rayosLibres)
        {
            visibles = 0; heliVisto = 0; rayosLibres = 0;
            foreach (var c in Physics.OverlapSphere(pos, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                if (NavService.BlocksMovement(c)) return false;
            // instantes de la toma: el helicoptero lleva ~2 s de avance al corte y se achica hasta ~6 s
            var puntosHeli = new Vector3[5];
            for (int i = 0; i < 5; i++) puntosHeli[i] = PosicionDeSalida(heliBase, rumbo, 2.2f + 2f + i, 2.2f, out _);
            var rot = RotacionHaciaElHeli(pos, puntosHeli[1]);
            int bloqueados = 0;
            for (int gx = -2; gx <= 2; gx++)
                for (int gy = -1; gy <= 1; gy++)
                {
                    var dir = rot * Quaternion.Euler(gy * 12f, gx * 13f, 0f) * Vector3.forward;
                    if (SolidoEntre(pos, pos + dir * 7f) >= 0f) bloqueados++;
                }
            rayosLibres = 15 - bloqueados;
            for (int i = 0; i < 5; i++) if (SolidoEntre(pos, puntosHeli[i]) < 0f) heliVisto++;
            var fwd = rot * Vector3.forward;
            if (tiradores != null)
                foreach (var s in tiradores)
                {
                    if (s == null || !s.gameObject.activeInHierarchy) continue;
                    var pecho = s.transform.position + Vector3.up * 1.2f;
                    if (Vector3.Angle(fwd, pecho - pos) > 30f) continue;
                    float tapa = SolidoEntre(pos, pecho);
                    if (tapa < 0f || tapa > (pecho - pos).magnitude - 0.6f) visibles++;
                }
            return rayosLibres >= 13;
        }

        // Prueba posiciones a 1,2 m del piso alrededor del grupo (4 distancias x 11 giros), sobre terreno caminable, y devuelve la de mejor cuadro:
        // helicoptero visto durante la toma, mas tiradores a la vista, cerca del grupo.
        bool ElegirCamaraFinal(Vector3 centro, Vector3 haciaGrupo, Vector3 heliBase, Vector3 rumbo, out Vector3 mejor, out int mejorVis, out int mejorHeli)
        {
            mejor = default; mejorVis = 0; mejorHeli = 0; bool hay = false; float mejorPuntaje = float.MinValue;
            float[] distancias = { 3.8f, 6f, 9f, 13f };
            float[] giros = { 0f, 20f, -20f, 40f, -40f, 65f, -65f, 90f, -90f, 125f, -125f };
            foreach (float dist in distancias)
                foreach (float giro in giros)
                {
                    var p = centro + Quaternion.Euler(0f, giro, 0f) * haciaGrupo * dist;
                    p.y = centro.y;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var nh, 1.5f, UnityEngine.AI.NavMesh.AllAreas) || Plano(nh.position, p).magnitude > 1.5f) continue;   // no sobre techos ni dentro de casas
                    if (PisoBajo(p, out float piso)) p.y = piso + AlturaDeCamaraFinal; else p.y = centro.y - 0.8f + AlturaDeCamaraFinal;
                    if (!CamaraFinalLibre(p, heliBase, rumbo, tiradoresFinales, out int vis, out int heliVis, out int libres)) continue;
                    float puntaje = heliVis * 4f + vis * 5f + libres * 0.1f - dist * 0.25f - Mathf.Abs(giro) * 0.04f;
                    if (vis == 0 && tiradoresFinales.Count > 0) puntaje -= 6f;
                    if (puntaje > mejorPuntaje) { mejorPuntaje = puntaje; mejor = p; mejorVis = vis; mejorHeli = heliVis; hay = true; }
                }
            return hay;
        }

        // Ultimo recurso: los tiradores reales quedaron entre casas sin lugar para la camara; se los pone en fila sobre el NavMesh detras del helipuerto.
        Vector3 ReubicarTiradoresEnFila(Vector3 punto0, Vector3 lateral, Vector3 atras)
        {
            var suma = Vector3.zero; int n = 0;
            for (int i = 0; i < tiradoresFinales.Count; i++)
            {
                var s = tiradoresFinales[i]; if (s == null) continue;
                var deseada = punto0 + lateral * ((i - 1) * 3.4f);
                if (!UnityEngine.AI.NavMesh.SamplePosition(deseada, out var h, 8f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                s.transform.position = new Vector3(h.position.x, h.position.y, h.position.z);
                ApoyoEnElPiso.Apoyar(s.transform);
                suma += s.transform.position; n++;
            }
            return n > 0 ? suma / n : Vector3.zero;
        }

        // Mira al helicoptero pero sin levantar tanto la camara: queda abajo del encuadre y los tiradores entran en cuadro.
        static Quaternion RotacionHaciaElHeli(Vector3 pos, Vector3 heliPos)
        {
            var d = heliPos - pos;
            var plano = new Vector3(d.x, 0f, d.z);
            float elev = Mathf.Atan2(d.y, Mathf.Max(0.1f, plano.magnitude)) * Mathf.Rad2Deg;
            float cabeceo = Mathf.Clamp(elev * 0.6f, 0f, 22f);
            float yaw = plano.sqrMagnitude > 0.01f ? Mathf.Atan2(plano.x, plano.z) * Mathf.Rad2Deg : 0f;
            return Quaternion.Euler(-cabeceo, yaw, 0f);
        }

        void TickTomaFinal(Camera cam, float dt)
        {
            if (cam == null || heli == null) return;
            float udt = Time.unscaledDeltaTime;
            cam.transform.position = posCamaraFinal + new Vector3(Mathf.Sin(Time.unscaledTime * 1.3f), Mathf.Sin(Time.unscaledTime * 1.9f) * 0.5f, 0f) * 0.02f;
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, RotacionHaciaElHeli(posCamaraFinal, heli.position), 1f - Mathf.Exp(-5f * udt));
            DistanciaCamaraHeli = (heli.position - cam.transform.position).magnitude;
            DistanciaMaximaCamaraHeli = Mathf.Max(DistanciaMaximaCamaraHeli, DistanciaCamaraHeli);
            if (PisoBajo(cam.transform.position, out float piso)) AlturaDeCamaraSobrePiso = cam.transform.position.y - piso;
            if (DistanciaCamaraHeli > 330f && heli.gameObject.activeSelf) heli.gameObject.SetActive(false);

            // Tiradores: giran hacia el helicoptero y disparan rafagas de 8 con pausas (trazadoras sin dano, con fogonazo).
            var pool = ProjectilePool.Activo;
            for (int i = 0; i < tiradoresFinales.Count; i++)
            {
                var s = tiradoresFinales[i];
                if (s == null || !s.gameObject.activeInHierarchy || s.Health == null || !s.Health.IsAlive) continue;
                var miraHeli = heli.position + Vector3.up * 1.2f;
                var haciaHeli = miraHeli - s.transform.position; haciaHeli.y = 0f;
                if (haciaHeli.sqrMagnitude > 0.01f)
                    s.transform.rotation = Quaternion.Slerp(s.transform.rotation, Quaternion.LookRotation(haciaHeli.normalized), 1f - Mathf.Exp(-8f * dt));
                if (pool == null || !heli.gameObject.activeSelf || Time.time < proximoTiro[i]) continue;
                if (restantesTiro[i] <= 0)
                {
                    if (Time.time < pausaTiro[i]) continue;
                    restantesTiro[i] = Random.Range(7, 12);
                }
                restantesTiro[i]--;
                proximoTiro[i] = Time.time + (restantesTiro[i] > 0 ? 0.11f : 0f);
                if (restantesTiro[i] <= 0) pausaTiro[i] = Time.time + Random.Range(0.5f, 1.0f);
                var boca = s.Weapon != null && s.Weapon.Muzzle != null ? s.Weapon.Muzzle.position : s.transform.position + Vector3.up * 1.4f + s.transform.forward * 0.5f;
                var dir = (miraHeli - boca).normalized;
                dir = (dir + Random.insideUnitSphere * 0.03f).normalized;
                pool.Spawn(boca, dir, -1, TeamId.Enemy, 0, new Color(1f, 0.45f, 0.25f));
                MuzzleLightPool.Flash(boca, new Color(1f, 0.6f, 0.3f), 4f, 7f);
                SpriteFx.Lanzar("muzzle_01", boca + dir * 0.2f, new Color(1f, 0.78f, 0.4f, 1f), 0.4f, 0.65f, 0.05f, Random.Range(0f, 360f), default, 0f, 6);
                EventBus.Instance.Publish(new ShotFiredEvent(s.Id));
                if ((restantesTiro[i] & 1) == 0) AudioDirector.PlayAt(SfxKind.Shoot, boca, 0.3f);
                ultimoTiro[s] = Time.unscaledTime;
                DisparosDeLaTomaFinal++;
            }
        }

        void TerminarTomaFinal()
        {
            // La escena sigue viva bajo la pantalla de resultado; no hay nada que restaurar (Reintentar / Salir recargan la escena).
            tiradoresFinales.Clear();
        }

        // Sin HUD durante la toma final: se apagan los canvas de pantalla (la pantalla de resultado trae su propio canvas anidado,
        // que se dibuja igual) y los rombos de la escuadra/objetivo.
        void OcultarHudDeLaToma()
        {
            foreach (var c in FindObjectsByType<Canvas>())
            {
                if (c == null || c.renderMode == RenderMode.WorldSpace || c.name == "SesionLog_Indicador") continue;
                c.enabled = false;
            }
            RomboVisibilidad.Suprimidos = true;
            AlertQueue.Clear();
        }
    }
}
