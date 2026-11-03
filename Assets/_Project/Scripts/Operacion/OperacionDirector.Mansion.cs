using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.UI;
using G = SP.Operacion.MansionGeometria;

namespace SP.Operacion
{
    // Objetivo 5 rediseñado: LA MANSION. Reemplaza la defensa de los cuatro flancos (mando tactico + helicoptero que aterriza) por un asalto
    // piso por piso a la mansion de la ciudad (ver MansionGeometria):
    //
    //   PLAZA        enemigos que entran por las calles; hay que limpiarla. Despues se abre el porton.
    //   PLANTA BAJA  salon con pilares y mostrador: cobertura baja, pilares para flanquear, enemigos que cierran el salon desde los costados.
    //   1.er PISO    pasillo con seis cuartos: emboscada, cada grupo sale de su cuarto cuando la escuadra pasa por la puerta.
    //   2.o PISO     gran salon abierto. Cinematica de las celdas; hay que llegar y abrir las celdas para liberar a los milicianos.
    //   AZOTEA       cinematica del helicoptero enemigo que llega a toda velocidad; hay que derribarlo mientras salen soldados por las
    //                otras tres escaleras. Al derribarlo y limpiar, el helicoptero propio aterriza en la azotea (Extraer).
    public partial class OperacionDirector
    {
        public enum EtapaDeLaMansion { Plaza, PlantaBaja, PrimerPiso, SegundoPiso, Azotea, Cerrada }

        [Header("Mansion")]
        public bool usarMansion = true;
        // Lo prende el corredor de checks para los checks de la defensa de los flancos (no cruzan por la mansion). Se baja en cada Play.
        public static bool ApagarMansion;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ReiniciarMansion() { ApagarMansion = false; }
        public bool MansionActiva { get; private set; }
        public EtapaDeLaMansion EtapaMansion { get; private set; }
        public bool CineDeMansionEnCurso { get; private set; }
        public int MansionEnemigosCreados { get; private set; }
        public bool MilicianosLiberados { get; private set; }
        public HelicopteroEnemigo HeliDeLaAzotea { get; private set; }
        public int MansionNivelDelJugador { get; private set; }

        class SpawnMansion { public Vector3 pos; public float radio; public float dispararEn; public int nivel; public bool desdeEscalera; }

        PortonDeMansion[] hojasDelPorton;
        readonly List<Soldier> enemigosMansion = new List<Soldier>();
        readonly List<SpawnMansion> pendientesMansion = new List<SpawnMansion>();
        readonly List<Soldier> prisioneros = new List<Soldier>();
        float relojMansion, proximoSpawnPlaza, proximaSincronizacion, tAzotea, bajoElNivelDesde;
        int plazaRestantes, ondasDeAzotea;
        bool porton, azoteaIniciada, segundoIniciado, sincEscuadra;
        Transform anclaDelHeli;

        bool PuedeUsarLaMansion()
        {
            if (!usarMansion || ApagarMansion || !Application.isPlaying || plantillaDeRefuerzo == null || halcon == null) return false;
            return FindObjectsByType<PortonDeMansion>().Length > 0;
        }

        // Se llama desde EntrarFase(Resistir).
        void IniciarMansion()
        {
            MansionActiva = true;
            EtapaMansion = EtapaDeLaMansion.Plaza;
            hojasDelPorton = FindObjectsByType<PortonDeMansion>();
            enemigosMansion.Clear(); pendientesMansion.Clear(); prisioneros.Clear();
            relojMansion = 0f; proximoSpawnPlaza = 1.5f; plazaRestantes = 8; ondasDeAzotea = 0; tAzotea = 0f;
            porton = false; azoteaIniciada = false; segundoIniciado = false; MilicianosLiberados = false; CineDeMansionEnCurso = false;
            MansionEnemigosCreados = 0; HeliDeLaAzotea = null;
            foreach (var h in hojasDelPorton) if (h != null) h.Cerrar();
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            PonerBaliza("MANSION", new Color(1f, 0.55f, 0.25f), new Vector3(G.PortonX, 1f, G.Z0 - 6f), null, 3.2f);
            Aviso("TANQUE DESTRUIDO\nLIMPIA LA PLAZA Y TOMA LA MANSION");
            SesionLog.Evento("MANSION: arranca el asalto (plaza)");
        }

        void SalirDeLaMansion()
        {
            if (!MansionActiva) return;
            MansionActiva = false;
            if (anclaDelHeli != null) Destroy(anclaDelHeli.gameObject);
        }

        // ApoyoEnElPiso tira el rayo desde 4 m arriba: adentro de la mansion pega contra el piso de arriba y deja al soldado un piso mas alto.
        // Aca se apoya sobre el NavMesh del piso pedido (la altura del pivote es PivoteSobrePiso sobre la superficie).
        static void ColocarEnElPiso(Transform t, Vector3 piso)
        {
            var p = piso;
            if (UnityEngine.AI.NavMesh.SamplePosition(piso + Vector3.up * 0.3f, out var h, 1.4f, UnityEngine.AI.NavMesh.AllAreas)) p = h.position;
            t.position = new Vector3(p.x, p.y + SP.Actors.SoldierMotor.PivoteSobrePiso, p.z);
        }

        // ------------------------------------------------------------------ enemigos
        Soldier CrearEnemigoMansion(Vector3 pos, bool desdeEscalera = false)
        {
            if (plantillaDeRefuerzo == null) return null;
            var yo = Poseido();
            var haciaYo = yo != null ? Plano(yo.transform.position, pos) : Vector3.forward;
            if (haciaYo.sqrMagnitude < 0.01f) haciaYo = Vector3.forward;
            var go = Instantiate(plantillaDeRefuerzo, new Vector3(pos.x, pos.y + 0.8f, pos.z), Quaternion.LookRotation(haciaYo.normalized, Vector3.up), plantillaDeRefuerzo.transform.parent);
            go.name = "Mansion_Enemigo_" + (++MansionEnemigosCreados);
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return null; }
            if (G.Dentro(pos, 1f) || pos.y > 1f) ColocarEnElPiso(s.transform, pos); else ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null)
            {
                s.Brain.CancelOrder();
                s.Brain.ReactivarNavegacion();
                var meta = yo != null ? yo.transform.position : pos;
                s.Brain.SetPatrolRoute(new[] { meta, meta });
            }
            CazaDeEnemigos.Marcar(new[] { s });
            enemigosMansion.Add(s);
            return s;
        }

        int VivosDeLaMansion()
        {
            int n = 0;
            for (int i = enemigosMansion.Count - 1; i >= 0; i--)
            {
                var s = enemigosMansion[i];
                if (s == null) { enemigosMansion.RemoveAt(i); continue; }
                if (s.Health != null && s.Health.IsAlive) n++;
            }
            return n;
        }

        void ProgramarSpawns(Vector3[] puntos, int nivel, float radio, float fallbackSegundos)
        {
            foreach (var p in puntos)
                pendientesMansion.Add(new SpawnMansion { pos = p, radio = radio, nivel = nivel, dispararEn = relojMansion + fallbackSegundos });
        }

        void TickSpawnsPendientes(Soldier yo)
        {
            for (int i = pendientesMansion.Count - 1; i >= 0; i--)
            {
                var sp = pendientesMansion[i];
                bool cerca = Plano(yo.transform.position, sp.pos).magnitude < sp.radio && MansionNivelDelJugador >= sp.nivel;
                if (!cerca && relojMansion < sp.dispararEn) continue;
                pendientesMansion.RemoveAt(i);
                CrearEnemigoMansion(sp.pos, sp.desdeEscalera);
            }
        }

        // ------------------------------------------------------------------ el tick
        void TickMansion(float dt)
        {
            var yo = Poseido();
            if (yo == null || yo.Health == null || !yo.Health.IsAlive) return;
            relojMansion += dt;
            if (CineDeMansionEnCurso) return;
            MansionNivelDelJugador = G.NivelDe(yo.transform.position);
            int nivel = MansionNivelDelJugador;
            int vivos = VivosDeLaMansion();
            int faltan = vivos + pendientesMansion.Count;
            TickSpawnsPendientes(yo);
            SincronizarEscuadraEnLaMansion(yo, nivel);

            switch (EtapaMansion)
            {
                case EtapaDeLaMansion.Plaza:
                {
                    if (plazaRestantes > 0 && relojMansion >= proximoSpawnPlaza)
                    {
                        proximoSpawnPlaza = relojMansion + UnityEngine.Random.Range(1.2f, 2.4f);
                        var bocas = BocasEnElNavMesh();
                        if (bocas.Count > 0 && plaza != null)
                        {
                            var orden = new List<Vector3>(bocas);
                            orden.Sort((a, b) => Plano(a, plaza.position).sqrMagnitude.CompareTo(Plano(b, plaza.position).sqrMagnitude));
                            var b0 = orden[UnityEngine.Random.Range(0, Mathf.Min(6, orden.Count))];
                            EntradaDeReserva.Marcar(b0);
                            CrearEnemigoMansion(b0);
                        }
                        else CrearEnemigoMansion(plaza != null ? plaza.position + new Vector3(18f, 0f, 0f) : yo.transform.position);
                        plazaRestantes--;
                    }
                    int restan = plazaRestantes + faltan;
                    if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), $"Plaza: elimina a los enemigos que entran por las calles · {restan} restantes", 0.05f, new Color(1f, 0.55f, 0.25f));
                    if (plazaRestantes == 0 && faltan == 0) AbrirElPorton();
                    break;
                }
                case EtapaDeLaMansion.PlantaBaja:
                {
                    if (!porton) break;
                    if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), faltan > 0 ? $"Planta baja: limpia el salon · {faltan} enemigos · usa los pilares y el mostrador" : "Planta baja libre: sube por la escalera del noroeste", 0.25f, new Color(1f, 0.55f, 0.25f));
                    if (faltan == 0 && nivel == 0 && baliza == null) PonerBaliza("ESCALERA", new Color(0.35f, 0.8f, 1f), G.EntradaDeTorre(0) + Vector3.up, null, 2.4f);
                    if (nivel >= 1) EntrarPrimerPiso();
                    break;
                }
                case EtapaDeLaMansion.PrimerPiso:
                {
                    if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), faltan > 0 ? $"1.er piso: emboscada · {faltan} enemigos · cada cuarto esconde un grupo" : "1.er piso libre: cruza el pasillo hasta la escalera del sureste", 0.45f, new Color(1f, 0.55f, 0.25f));
                    if (faltan == 0 && nivel == 1 && baliza == null) PonerBaliza("ESCALERA", new Color(0.35f, 0.8f, 1f), G.EntradaDeTorre(1) + Vector3.up, null, 2.4f);
                    if (nivel >= 2) EntrarSegundoPiso();
                    break;
                }
                case EtapaDeLaMansion.SegundoPiso:
                {
                    if (!segundoIniciado) break;
                    var puertaCeldas = G.PuertaDeCeldas + Vector3.up * 0.8f;
                    float dCeldas = Plano(yo.transform.position, puertaCeldas).magnitude;
                    if (!MilicianosLiberados)
                    {
                        if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), faltan > 0 ? $"2.o piso: elimina a la guardia · {faltan} enemigos" : "Llega a las celdas del norte y libera a los milicianos", 0.65f, new Color(1f, 0.55f, 0.25f));
                        if (faltan == 0 && nivel == 2 && dCeldas < 4f)
                        {
                            if (hud != null) hud.Prompt("[E] LIBERAR A LOS MILICIANOS");
                            if (OperacionTerminal.EToque()) LiberarMilicianos();
                        }
                        else if (faltan == 0 && baliza == null) PonerBaliza("CELDAS", new Color(1f, 0.82f, 0.25f), puertaCeldas, null, 2.4f);
                    }
                    else
                    {
                        if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), "Milicianos liberados: sube a la azotea por la escalera del noreste", 0.78f, new Color(0.45f, 1f, 0.55f));
                        if (baliza == null) PonerBaliza("AZOTEA", new Color(0.35f, 0.8f, 1f), G.EntradaDeTorre(2) + Vector3.up, null, 2.4f);
                        if (nivel >= 3) EntrarAzotea();
                    }
                    break;
                }
                case EtapaDeLaMansion.Azotea:
                {
                    if (!azoteaIniciada) break;
                    tAzotea += dt;
                    // Tres oleadas salen por las otras escaleras; cada una asoma desde el rellano y sube por el carril de bajada.
                    float[] cuando = { 4f, 16f, 30f };
                    int[] torre = { 3, 4, 2 };
                    while (ondasDeAzotea < cuando.Length && tAzotea >= cuando[ondasDeAzotea])
                    {
                        int i = torre[ondasDeAzotea++];
                        var t = G.Torres[i];
                        var p = G.RellanoDeTorre(i);
                        for (int k = 0; k < 4; k++) pendientesMansion.Add(new SpawnMansion { pos = p, radio = 0f, dispararEn = relojMansion + k * 1.1f, nivel = 3, desdeEscalera = true });
                        if (hud != null) hud.Aviso($"REFUERZOS POR LA ESCALERA {t.nombre}", 2.6f);
                    }
                    bool heliVivo = HeliDeLaAzotea != null && !HeliDeLaAzotea.Derribado;
                    if (hud != null)
                    {
                        float f = HeliDeLaAzotea != null ? HeliDeLaAzotea.Fraccion01 : 0f;
                        hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), heliVivo ? $"Azotea: derriba al helicoptero ({Mathf.RoundToInt(f * 100f)} %) · {faltan} soldados" : $"Azotea: helicoptero derribado · quedan {faltan} soldados", 0.85f + 0.12f * (heliVivo ? 1f - f : 1f), new Color(1f, 0.4f, 0.3f));
                        if (heliVivo) hud.BarraDeJefe(HelicopteroEnemigo.Nombre, f);
                    }
                    if (!heliVivo && ondasDeAzotea >= cuando.Length && faltan == 0) TerminarLaMansion();
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ etapas
        void AbrirElPorton()
        {
            porton = true;
            EtapaMansion = EtapaDeLaMansion.PlantaBaja;
            foreach (var h in hojasDelPorton) if (h != null) h.Abrir();
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            PonerBaliza("PORTON", new Color(0.35f, 0.8f, 1f), new Vector3(G.PortonX, 1f, G.Z0), null, 3f);
            Aviso("PLAZA LIMPIA\nSE ABRE EL PORTON DE LA MANSION");
            ProgramarSpawns(G.EnemigosPB, 0, 15f, 22f);
            SesionLog.Evento("MANSION: porton abierto");
        }

        void EntrarPrimerPiso()
        {
            EtapaMansion = EtapaDeLaMansion.PrimerPiso;
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            Aviso("1.ER PISO\nCADA CUARTO ESCONDE UN GRUPO");
            ProgramarSpawns(G.EnemigosPrimero, 1, 11f, 30f);
            SesionLog.Evento("MANSION: 1.er piso");
        }

        void EntrarSegundoPiso()
        {
            EtapaMansion = EtapaDeLaMansion.SegundoPiso;
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            SesionLog.Evento("MANSION: 2.o piso");
            PonerPrisioneros();
            var yo = Poseido();
            var p0 = yo != null ? yo.transform.position : G.Centro(2);
            StartCoroutine(CinematicaDeLasCeldas(p0));
        }

        void EntrarAzotea()
        {
            if (azoteaIniciada) return;
            azoteaIniciada = true;
            EtapaMansion = EtapaDeLaMansion.Azotea;
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            SesionLog.Evento("MANSION: azotea");
            StartCoroutine(CinematicaDelHelicoptero());
        }

        void TerminarLaMansion()
        {
            EtapaMansion = EtapaDeLaMansion.Cerrada;
            if (heliAterrizaje != null) heliAterrizaje.position = G.CentroDeLaAzotea;
            Aviso("AZOTEA ASEGURADA\nEL HELICOPTERO ATERRIZA PARA EXTRAERTE");
            SesionLog.Evento("MANSION: terminada");
            ArrancarHeli();
            if (hud != null) { hud.OcultarTimer(); hud.OcultarBarraDeJefe(); }
            EntrarFase(FaseOperacion.Extraer);
        }

        // ------------------------------------------------------------------ milicianos
        void PonerPrisioneros()
        {
            foreach (var s in prisioneros) if (s != null) Destroy(s.gameObject);
            prisioneros.Clear();
            if (prefabsDeMiliciano == null) return;
            string[] nombres = { "MILICIANO 1", "MILICIANO 2", "MILICIANO 3", "MILICIANO 4" };
            RoleType[] roles = { RoleType.Assault, RoleType.Medic, RoleType.Flanker, RoleType.Assault };
            for (int i = 0; i < prefabsDeMiliciano.Length && i < G.CeldasDeMilicianos.Length; i++)
            {
                if (prefabsDeMiliciano[i] == null) continue;
                var p = G.CeldasDeMilicianos[i];
                var go = Instantiate(prefabsDeMiliciano[i], new Vector3(p.x, p.y + 0.8f, p.z), Quaternion.LookRotation(Vector3.back, Vector3.up));
                go.name = "Prisionero_" + (i + 1);
                go.SetActive(true);
                var s = go.GetComponent<Soldier>();
                if (s == null) { Destroy(go); continue; }
                s.Configure(nombres[i], TeamId.Player, roles[i], 160);
                ColocarEnElPiso(s.transform, p);
                if (s.Brain != null) { s.Brain.IsPossessedByPlayer = false; s.Brain.Quieto = true; s.Brain.Pasivo = true; s.Brain.CancelOrder(); }
                if (s.Health != null) s.Health.Invulnerable = true;
                prisioneros.Add(s);
            }
            ActorRegistry.Invalidate();
        }

        void LiberarMilicianos()
        {
            MilicianosLiberados = true;
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            foreach (var s in prisioneros)
            {
                if (s == null) continue;
                if (s.Health != null) s.Health.Invulnerable = false;
                if (s.Brain != null) { s.Brain.Quieto = false; s.Brain.Pasivo = false; s.Brain.ReactivarNavegacion(); }
                var w = s.Weapon;
                if (w != null && w.Loadout.Count > 0) { w.Loadout[0] = WeaponKind.Rifle; w.EquipFromLoadout(0); w.ReponerMunicion(); }
                if (s.Brain != null) s.Brain.FactorDeRafaga = AiBrain.FactorDeRafagaMiliciano;
                MandoTactico.Milicianos.Add(s);
                milicianosActivos.Add(s);
                WorldUiDirector.Revelar(s, 5f);
            }
            ActorRegistry.Invalidate();
            if (RosterView.Activo != null) RosterView.Activo.Rebuild();
            AlertQueue.Push($"{prisioneros.Count} MILICIANOS LIBERADOS", AlertPriority.Alta, 3.2f);
            Aviso("MILICIANOS LIBERADOS\nSUBE A LA AZOTEA POR LA ESCALERA NORESTE");
            prisioneros.Clear();
            SesionLog.Evento("MANSION: milicianos liberados");
        }

        // La escuadra sube con el jugador: si un aliado quedo en otro piso mucho rato, se lo acerca (las escaleras no se pueden esperar eternamente).
        void SincronizarEscuadraEnLaMansion(Soldier yo, int nivel)
        {
            if (relojMansion < proximaSincronizacion) return;
            proximaSincronizacion = relojMansion + 1.5f;
            var todos = ActorRegistry.All;
            int lejos = 0;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a == yo || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                if (a.Brain == null || a.Brain.IsPossessedByPlayer || a.Brain.Pasivo) continue;
                float dy = Mathf.Abs(a.transform.position.y - yo.transform.position.y);
                float d = Plano(a.transform.position, yo.transform.position).magnitude;
                if (dy > 3.2f && nivel >= 1 && G.Dentro(yo.transform.position, 1f)) lejos++;
                else continue;
                if (relojMansion - bajoElNivelDesde < 8f) continue;
                if (UnityEngine.AI.NavMesh.SamplePosition(yo.transform.position + new Vector3(UnityEngine.Random.Range(-2f, 2f), -0.8f, UnityEngine.Random.Range(-2f, 2f)), out var h, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    ColocarEnElPiso(a.transform, h.position);
                    if (a.Brain != null) { a.Brain.CancelOrder(); a.Brain.ReactivarNavegacion(); }
                }
            }
            if (lejos == 0) bajoElNivelDesde = relojMansion;
        }

        // ------------------------------------------------------------------ cinematicas
        IEnumerator CineCorta(float duracion, Func<float, Vector3> posicion, Func<float, Vector3> mira, Action<float> cadaCuadro, VozDeCinematica.Linea? linea, float fov = 55f)
        {
            CineDeMansionEnCurso = true;
            var cam = Camera.main;
            var driver = PlayerInputDriver.Activo;
            AiBrain.IAPausada = true;
            RomboVisibilidad.Suprimidos = true;
            if (driver != null) { driver.enabled = false; if (driver.Rig != null) driver.Rig.enabled = false; }
            var apagados = new List<Canvas>();
            foreach (var c in FindObjectsByType<Canvas>())
            {
                if (c == null || !c.enabled || c.renderMode == RenderMode.WorldSpace) continue;
                if (c.name == "SesionLog_Indicador" || c.name == "CanvasPausa" || c.name == "AvisoUnico") continue;
                c.enabled = false; apagados.Add(c);
            }
            AlertQueue.Clear();
            float fov0 = cam != null ? cam.fieldOfView : 60f;
            if (cam != null) cam.fieldOfView = fov;
            if (linea.HasValue) VozDeCinematica.Decir(linea.Value);
            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duracion);
                cadaCuadro?.Invoke(u);
                if (cam != null)
                {
                    var p = posicion(u);
                    cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation((mira(u) - p).normalized, Vector3.up));
                }
                yield return null;
            }
            VozDeCinematica.Callar();
            if (cam != null) cam.fieldOfView = fov0;
            AiBrain.IAPausada = false;
            RomboVisibilidad.Suprimidos = false;
            if (driver != null) { driver.enabled = true; if (driver.Rig != null) driver.Rig.enabled = true; }
            foreach (var c in apagados) if (c != null) c.enabled = true;
            CineDeMansionEnCurso = false;
        }

        static readonly VozDeCinematica.Linea LineaCeldas = new VozDeCinematica.Linea("cmd_mansion_celdas", 6.5f,
            "Inteligencia confirma que los milicianos estan presos en las celdas del ala norte. Barre el salon y liberalos: los vamos a necesitar arriba.");
        static readonly VozDeCinematica.Linea LineaHelicoptero = new VozDeCinematica.Linea("cmd_mansion_heli", 6.0f,
            "¡Helicoptero enemigo por el este, viene a toda velocidad! Derribalo antes de que barra la azotea. Y cuidado: sube gente por las otras escaleras.");

        IEnumerator CinematicaDeLasCeldas(Vector3 desde)
        {
            var llegada = new Vector3(desde.x, desde.y + 2.4f, desde.z);
            var alto = new Vector3(334f, G.Nivel(2) + 5.5f, 256f);
            var cerca = new Vector3(318f, G.Nivel(2) + 3.0f, 268f);
            var mirada = new Vector3(306f, G.Nivel(2) + 1.3f, 283f);
            yield return CineCorta(6.5f,
                u => u < 0.45f ? Vector3.Lerp(llegada, alto, Mathf.SmoothStep(0f, 1f, u / 0.45f)) : Vector3.Lerp(alto, cerca, Mathf.SmoothStep(0f, 1f, (u - 0.45f) / 0.55f)),
                u => Vector3.Lerp(new Vector3(322f, G.Nivel(2) + 1.2f, 265f), mirada, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 1.6f))),
                null, LineaCeldas, 60f);
            foreach (var s in prisioneros) if (s != null) WorldUiDirector.Revelar(s, 6f);
            segundoIniciado = true;
            ProgramarSpawns(G.EnemigosSegundo, 2, 13f, 25f);
            Aviso("2.O PISO\nLLEGA A LAS CELDAS DEL NORTE");
        }

        IEnumerator CinematicaDelHelicoptero()
        {
            var yo = Poseido();
            var centro = G.CentroDeLaAzotea;
            if (anclaDelHeli == null) { var g = new GameObject("Mansion_AnclaHeli"); g.transform.position = centro; anclaDelHeli = g.transform; }
            float ang = 25f * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            var h = HelicopteroEnemigo.IniciarSobre(halcon, anclaDelHeli, 25f, 220f);
            HeliDeLaAzotea = h;
            if (h == null) { azoteaIniciada = true; yield break; }
            h.desnivel = G.Nivel(3); h.radioPropio = 34f;
            var inicio = centro + dir * 230f + Vector3.up * 46f;
            var sobre = centro + dir * 26f + Vector3.up * 20f;
            h.transform.position = inicio;
            var camPos = new Vector3(centro.x - 12f, centro.y + 1.7f, centro.z - 4f);
            if (yo != null) camPos = new Vector3(yo.transform.position.x, centro.y + 1.9f, yo.transform.position.z);
            Vector3 PosHeli(float u)
            {
                // A toda velocidad (60 m/s) y frena sobre el techo: la distancia recorrida sigue una curva de frenado fuerte al final.
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(u / 0.8f), 2.2f);
                return Vector3.Lerp(inicio, sobre, k);
            }
            yield return CineCorta(6.0f,
                u => camPos,
                u => PosHeli(u) + Vector3.up * 0.5f,
                u =>
                {
                    var p = PosHeli(u);
                    var rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(centro - p, Vector3.up).normalized, Vector3.up) * Quaternion.Euler(Mathf.Lerp(12f, -4f, Mathf.Clamp01(u / 0.8f)), 0f, 0f);
                    h.FijarParaLaCinematica(p, rot);
                    h.transform.SetPositionAndRotation(p, rot);
                },
                LineaHelicoptero, 48f);
            h.LiberarDeLaCinematica();
            tAzotea = 0f;
            Aviso("DERRIBA AL HELICOPTERO ENEMIGO");
        }
    }
}
