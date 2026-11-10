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

        class SpawnMansion { public Vector3 pos; public float radio; public float dispararEn; public int nivel; public bool desdeEscalera; public bool escalonado; }

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
            foreach (var e in enemigosMansion) if (e != null) Destroy(e.gameObject);   // una corrida anterior en el mismo Play no deja enemigos huerfanos fuera de la cuenta
            if (HeliDeLaAzotea != null) Destroy(HeliDeLaAzotea.gameObject);
            enemigosMansion.Clear(); pendientesMansion.Clear(); prisioneros.Clear();
            proximaSincronizacion = 0f; bajoElNivelDesde = 0f;
            relojMansion = 0f; proximoSpawnPlaza = 1.5f; plazaRestantes = EnemigosDeLaPlaza; ondasDeAzotea = 0; tAzotea = 0f;
            porton = false; azoteaIniciada = false; segundoIniciado = false; MilicianosLiberados = false; CineDeMansionEnCurso = false;
            MansionEnemigosCreados = 0; HeliDeLaAzotea = null;
            ReiniciarAzotea(); CerrarLaReja();
            QuitarPropsDelInterior();
            QuitarCompuertas();
            HelipadEnLaAzotea();   // el rescate y la extraccion son en el techo, no en la plaza
            if (GetComponent<PlantasDeLaMansion>() == null) gameObject.AddComponent<PlantasDeLaMansion>();   // #336: recorta las plantas de arriba en la vista tactica
            foreach (var h in hojasDelPorton) if (h != null) h.Cerrar();
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            PonerBaliza("MANSION", new Color(1f, 0.55f, 0.25f), new Vector3(G.PortonX, 1f, G.Z0 - 6f), null, 3.2f);
            Aviso("TANQUE DESTRUIDO\nLIMPIA LA PLAZA Y TOMA LA MANSION");
            SesionLog.Evento("MANSION: arranca el asalto (plaza)");
        }

        public int PropsQuitadosDelInterior { get; private set; }

        // Bug #010 (build, "hay un collider invisible aqui"): los props de la ciudad (P_Env_*: neumaticos, barriles, barricadas, carteles) quedaron
        // adentro de la huella de la mansion; sus mallas se fusionaron o quedan bajo el piso, pero el collider seguia ahi y frenaba al soldado en el
        // vestibulo. Al armarse la mansion se sacan los que caen adentro (con su marcador de cobertura, si lo tienen).
        void QuitarPropsDelInterior()
        {
            PropsQuitadosDelInterior = 0;
            var centro = new Vector3((G.X0 + G.X1) * 0.5f, 3f, (G.Z0 + G.Z1) * 0.5f);
            var mitad = new Vector3((G.X1 - G.X0) * 0.5f - 0.3f, 3.5f, (G.Z1 - G.Z0) * 0.5f - 0.3f);
            var vistos = new HashSet<GameObject>();
            foreach (var c in Physics.OverlapBox(centro, mitad, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
            {
                if (c == null) continue;
                var go = c.gameObject;
                while (go != null && !go.name.StartsWith("P_Env_", System.StringComparison.Ordinal)) go = go.transform.parent != null ? go.transform.parent.gameObject : null;
                if (go == null || !vistos.Add(go)) continue;
                Destroy(go);
                PropsQuitadosDelInterior++;
            }
            if (PropsQuitadosDelInterior > 0) GameLog.Line($"[Operacion] Mansion: se quitaron {PropsQuitadosDelInterior} props de la ciudad que quedaban adentro");
        }

        void SalirDeLaMansion()
        {
            if (!MansionActiva) return;
            MansionActiva = false;
            QuitarCompuertas();   // en Extraer la escuadra tiene que poder subir por las torres hasta el techo
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
        // Bug #349 ("faltan enemigos, deberian venir a mi y verse facilmente"): la plaza pasa de 8 a 16 enemigos, entran mas seguido, se ven todos
        // desde que entran y cada tanto se les vuelve a dar la posicion del jugador (ver ReorientarALosDeLaPlaza).
        public const int EnemigosDeLaPlaza = 16;
        float proximaReorientacion;
        void ReorientarALosDeLaPlaza(Soldier yo)
        {
            if (Time.time < proximaReorientacion) return;
            proximaReorientacion = Time.time + 2.5f;
            foreach (var e in enemigosMansion)
            {
                if (e == null || e.Brain == null || e.Health == null || !e.Health.IsAlive) continue;
                e.Brain.SetPatrolRoute(new[] { yo.transform.position, yo.transform.position });
            }
        }

        Soldier CrearEnemigoMansion(Vector3 pos, bool desdeEscalera = false)
        {
            if (plantillaDeRefuerzo == null) return null;
            // Red de seguridad (#337): fuera de las torres de escalera salvo que salga justo de una (los refuerzos de la azotea).
            if (!desdeEscalera)
                for (int k = 0; k < 8 && G.EnTorre(pos, out _); k++)
                {
                    var haciaCentro = Plano(G.Centro(0), pos);
                    pos += (haciaCentro.sqrMagnitude > 0.01f ? haciaCentro.normalized : Vector3.right) * 2.5f;
                }
            var yo = Poseido();
            var haciaYo = yo != null ? Plano(yo.transform.position, pos) : Vector3.forward;
            if (haciaYo.sqrMagnitude < 0.01f) haciaYo = Vector3.forward;
            var go = Instantiate(plantillaDeRefuerzo, new Vector3(pos.x, pos.y + 0.8f, pos.z), Quaternion.LookRotation(haciaYo.normalized, Vector3.up), plantillaDeRefuerzo.transform.parent);
            go.name = "Mansion_Enemigo_" + (++MansionEnemigosCreados);
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return null; }
            if (MansionEnemigosCreados % 8 == 0) EnemigoPesado.Aplicar(s);   // 2026-11-07: cada 8.o enemigo de la mansion es un PESADO
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

        // Bug #011 (build, "los enemigos deberian bajar de las escaleras, no aparecer magicamente frente a mi"): en el 1.er y 2.o piso la guardia
        // aparecia de golpe en su punto al acercarse el jugador. Ahora, al llegar al piso, cada uno sale del RELLANO de una escalera que
        // baja a ese piso (la que esta mas lejos del jugador), uno cada ~0,9 s, y llega caminando por el tramo hasta la puerta del salon.
        // Entre ellos y el jugador hay siempre una torre cerrada: nadie se materializa a la vista.
        bool RellanoParaBajar(int nivel, Soldier yo, out Vector3 rellano)
        {
            rellano = Vector3.zero;
            float mejor = -1f; int idx = -1;
            for (int i = 0; i < G.Torres.Length; i++)
            {
                if (G.Torres[i].nivelBase != nivel) continue;
                var r = G.RellanoDeTorre(i);
                float d = Plano(yo.transform.position, r).magnitude + UnityEngine.Random.value * 6f;   // algo de azar entre torres parecidas
                if (d > mejor) { mejor = d; idx = i; }
            }
            if (idx < 0) return false;
            rellano = G.RellanoDeTorre(idx);
            return true;
        }

        public int EnemigosBajadosPorEscalera { get; private set; }

        void TickSpawnsPendientes(Soldier yo)
        {
            int escalonados = 0;
            for (int i = pendientesMansion.Count - 1; i >= 0; i--)
            {
                var sp = pendientesMansion[i];
                bool porEscalera = !sp.desdeEscalera && sp.nivel >= 1 && sp.nivel <= 2;
                bool cerca = Plano(yo.transform.position, sp.pos).magnitude < sp.radio && MansionNivelDelJugador >= sp.nivel;
                if (porEscalera && !sp.escalonado)
                {
                    if (!cerca && relojMansion < sp.dispararEn) continue;
                    sp.escalonado = true; sp.dispararEn = relojMansion + 0.5f + 0.9f * escalonados++;
                    continue;
                }
                if (sp.escalonado ? relojMansion < sp.dispararEn : (!cerca && relojMansion < sp.dispararEn)) continue;
                pendientesMansion.RemoveAt(i);
                var pos = sp.pos; bool desdeEscalera = sp.desdeEscalera;
                if (sp.escalonado && RellanoParaBajar(sp.nivel, yo, out var rellano))
                {
                    pos = rellano + new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 0f, UnityEngine.Random.Range(-0.8f, 0.8f));
                    desdeEscalera = true; EnemigosBajadosPorEscalera++;
                }
                CrearEnemigoMansion(pos, desdeEscalera);
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
            RevelarEnemigosDeLaMansion(yo, faltan);
            RescatarEnemigosAtascados(yo, faltan);
            ActualizarCompuertas(faltan, yo);

            switch (EtapaMansion)
            {
                case EtapaDeLaMansion.Plaza:
                {
                    if (plazaRestantes > 0 && relojMansion >= proximoSpawnPlaza)
                    {
                        proximoSpawnPlaza = relojMansion + UnityEngine.Random.Range(0.8f, 1.6f);   // bug #349: llegan mas seguido
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
                    ReorientarALosDeLaPlaza(yo);
                    int restan = plazaRestantes + faltan;
                    if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), $"Plaza: elimina a los enemigos que entran por las calles · {restan} restantes", 0.05f, new Color(1f, 0.55f, 0.25f));
                    if (plazaRestantes == 0 && VivosDeLaMansion() + pendientesMansion.Count == 0) AbrirElPorton();   // 'faltan' se calculo antes de spawnear el ultimo
                    break;
                }
                case EtapaDeLaMansion.PlantaBaja:
                {
                    if (!porton) break;
                    if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), faltan > 0 ? $"Planta baja: limpia el salon · {faltan} enemigos · usa los pilares y el mostrador" : "Planta baja libre: sube por la escalera del noroeste", 0.25f, new Color(1f, 0.55f, 0.25f));
                    if (G.Dentro(yo.transform.position, 0.5f) && nivel == 0) BalizaDeLaMansion("ESCALERA", new Color(0.35f, 0.8f, 1f), G.EntradaDeTorre(0), 0);
                    if (nivel >= 1) EntrarPrimerPiso();
                    break;
                }
                case EtapaDeLaMansion.PrimerPiso:
                {
                    if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), faltan > 0 ? $"1.er piso: emboscada · {faltan} enemigos · cada cuarto esconde un grupo" : "1.er piso libre: cruza el pasillo hasta la escalera del sureste", 0.45f, new Color(1f, 0.55f, 0.25f));
                    if (nivel == 1) BalizaDeLaMansion("ESCALERA", new Color(0.35f, 0.8f, 1f), G.EntradaDeTorre(1), 1);
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
                        if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), faltan > 0 ? $"2.o piso: elimina a la guardia · {faltan} enemigos" : "Llega a la terminal junto a las celdas del norte y libera a los milicianos", 0.65f, new Color(1f, 0.55f, 0.25f));
                        // Bug #333 ("no puedo interactuar, deberia apretarlo y abrir las carceles"): se abren desde 9 m aunque queden guardias.
                        var posTerminal = PosTerminalDeCeldas; posTerminal.y = G.Nivel(2);
                        float dTerminal = Plano(yo.transform.position, posTerminal).magnitude;
                        if (nivel == 2 && dTerminal < AlcanceDeLaTerminalDeCeldas)
                        {
                            if (hud != null) hud.Prompt("[E] USAR LA TERMINAL · ABRIR LAS CELDAS");
                            if (OperacionTerminal.EToque()) LiberarMilicianos();
                        }
                        else if (nivel == 2) BalizaDeLaMansion("TERMINAL", new Color(1f, 0.95f, 0.2f), posTerminal + Vector3.up * 0.8f, 2);   // el rombo marca las celdas desde que se entra al piso (#330)
                    }
                    else
                    {
                        if (hud != null) hud.Objetivo(TituloObjetivo(5, "TOMAR LA MANSION"), "Milicianos liberados: sube a la azotea por la escalera del noreste", 0.78f, new Color(0.45f, 1f, 0.55f));
                        if (nivel == 2) BalizaDeLaMansion("AZOTEA", new Color(0.35f, 0.8f, 1f), G.EntradaDeTorre(2), 2);
                        if (nivel >= 3) EntrarAzotea();
                    }
                    break;
                }
                case EtapaDeLaMansion.Azotea:
                {
                    if (!azoteaIniciada) break;
                    TickAzotea(dt, faltan);   // OperacionDirector.Azotea.cs: resistir hasta que llegue el helicoptero de rescate
                    break;
                }
            }
        }

        // El rombo del objetivo sigue a la baliza del director: se reemplaza solo si cambia el objetivo y se apoya en el piso del nivel donde esta.
        string nombreDeLaBaliza;
        void BalizaDeLaMansion(string nombre, Color color, Vector3 pos, int nivel)
        {
            if (baliza != null && nombreDeLaBaliza == nombre) return;
            PonerBaliza(nombre, color, pos, null, 2.4f, G.Nivel(nivel) + 0.15f);
            nombreDeLaBaliza = nombre;
        }

        // Bug #334 ("falta señalizar mejor a los enemigos"): los enemigos de la mansion llevan su rombo rojo aunque haya paredes en el medio cuando
        // estan en el mismo piso que vos y a menos de 28 m, o cuando quedan pocos (<= 4) en cualquier lugar del edificio.
        float proximoRevelado;
        void RevelarEnemigosDeLaMansion(Soldier yo, int faltan)
        {
            if (Time.time < proximoRevelado) return;
            proximoRevelado = Time.time + 0.5f;
            for (int i = 0; i < enemigosMansion.Count; i++)
            {
                var s = enemigosMansion[i];
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                bool pocos = faltan <= 4;
                bool enLaPlaza = EtapaMansion == EtapaDeLaMansion.Plaza;   // bug #349: en la plaza se ven todos, vengan de donde vengan (a cielo abierto, sin paredes)
                bool cerca = Mathf.Abs(s.transform.position.y - yo.transform.position.y) < 3.2f && Plano(s.transform.position, yo.transform.position).magnitude < 28f;
                if (pocos || cerca || enLaPlaza) { SP.Core.InteligenciaDeEnemigos.Revelar(s.Id); WorldUiDirector.Revelar(s, 1.5f); }
            }
        }

        // Bug #364 (2026-11-06: "al finalizar queda pendiente 1 enemigo"): un enemigo que se queda trabado (sin camino, detras de una compuerta) deja la
        // mision sin terminar. Cuando quedan pocos y uno lleva 15 s casi sin moverse lejos del jugador, se lo acerca a un punto del piso del jugador.
        readonly Dictionary<int, (Vector3 pos, float desde)> vigiaDeAtascados = new Dictionary<int, (Vector3, float)>();
        float proximoVigiaDeAtascados;
        public int EnemigosRescatadosDeUnAtasco { get; private set; }
        public const float SegundosParaRescatarAtascado = 15f;

        void RescatarEnemigosAtascados(Soldier yo, int faltan)
        {
            if (Time.time < proximoVigiaDeAtascados) return;
            proximoVigiaDeAtascados = Time.time + 2f;
            if (faltan > 3 || pendientesMansion.Count > 0) { vigiaDeAtascados.Clear(); return; }
            for (int i = 0; i < enemigosMansion.Count; i++)
            {
                var s = enemigosMansion[i];
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                var p = s.transform.position;
                if (!vigiaDeAtascados.TryGetValue(s.Id, out var v) || (p - v.pos).sqrMagnitude > 2.25f || Plano(p, yo.transform.position).magnitude < 10f) { vigiaDeAtascados[s.Id] = (p, Time.time); continue; }
                if (Time.time - v.desde < SegundosParaRescatarAtascado) continue;
                var lado = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(8f, 11f);
                var destino = new Vector3(yo.transform.position.x + lado.x, yo.transform.position.y, yo.transform.position.z + lado.y);
                if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var h, 5f, UnityEngine.AI.NavMesh.AllAreas)) destino = h.position;
                ColocarEnElPiso(s.transform, destino);
                if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); }
                vigiaDeAtascados[s.Id] = (destino, Time.time);
                EnemigosRescatadosDeUnAtasco++;
                GameLog.Line($"[Operacion] Mansion: {s.DisplayName} llevaba {SegundosParaRescatarAtascado:0} s trabado; se lo acerca al jugador");
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
            CrearTerminalDeCeldas();
            var yo = Poseido();
            var p0 = yo != null ? yo.transform.position : G.Centro(2);
            StartCoroutine(CinematicaDeLasCeldas(p0));
        }

        // Atajo de pruebas: deja hecha la plaza y el 1.er piso y arranca el 2.o con la escuadra arriba.
        public void SaltarAlSegundoPisoPorPrueba()
        {
            SaltarA(FaseOperacion.Resistir, true);
            if (!MansionActiva) return;
            plazaRestantes = 0; porton = true;
            foreach (var h in hojasDelPorton) if (h != null) h.Abrir();
            PonerLaEscuadraEnElPiso(G.Centro(2) + new Vector3(0f, 0f, -6f));
            segundoIniciado = true;
            EntrarSegundoPiso();
        }

        void EntrarAzotea()
        {
            if (azoteaIniciada) return;
            ArrancarLaAzotea(true);
        }

        // Sin cinematica (F10): el Halcon ya esta orbitando el techo.
        void IniciarHalconDeLaAzotea()
        {
            if (anclaDelHeli == null) { var g = new GameObject("Mansion_AnclaHeli"); g.transform.position = G.CentroDeLaAzotea; anclaDelHeli = g.transform; }
            var h = HelicopteroEnemigo.IniciarSobre(halcon, anclaDelHeli, 25f, 120f);
            HeliDeLaAzotea = h;
            if (h == null) return;
            h.desnivel = G.Nivel(3); h.radioPropio = 34f;
            h.LiberarDeLaCinematica();
        }

        // ------------------------------------------------------------------ milicianos
        void PonerPrisioneros()
        {
            foreach (var s in prisioneros) if (s != null) Destroy(s.gameObject);
            prisioneros.Clear();
            if (prefabsDeMiliciano == null) return;
            string[] nombres = { "MILICIANO 1", "MILICIANO 2", "MILICIANO 3", "MILICIANO 4" };
            RoleType[] roles = { RoleType.Assault, RoleType.Medic, RoleType.Flanker, RoleType.Assault };
            for (int i = 0; i < prefabsDeMiliciano.Length && i < G.CeldasDeMilicianos.Length && i < MandoTactico.CantidadDeMilicianos; i++)   // #414: sólo 2 milicianos
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

        // La reja de las celdas (grupo P2_Reja, ver el constructor): al liberar a los milicianos se hunde en el piso y deja de ser solida.
        Transform rejaDeCeldas; Vector3[] rejaOrigen;
        public bool RejaAbierta { get; private set; }

        void BuscarLaReja()
        {
            if (rejaDeCeldas != null) return;
            var go = GameObject.Find("P2_Reja");
            if (go == null) return;
            rejaDeCeldas = go.transform;
            rejaOrigen = new Vector3[rejaDeCeldas.childCount];
            for (int i = 0; i < rejaOrigen.Length; i++) rejaOrigen[i] = rejaDeCeldas.GetChild(i).position;
        }

        void CerrarLaReja()
        {
            BuscarLaReja();
            RejaAbierta = false;
            if (rejaDeCeldas == null) return;
            for (int i = 0; i < rejaOrigen.Length; i++)
            {
                var b = rejaDeCeldas.GetChild(i);
                b.position = rejaOrigen[i]; b.gameObject.SetActive(true);
                var c = b.GetComponent<Collider>(); if (c != null) c.enabled = true;
            }
        }

        void AbrirLaReja()
        {
            BuscarLaReja();
            if (rejaDeCeldas == null || RejaAbierta) return;
            RejaAbierta = true;
            for (int i = 0; i < rejaDeCeldas.childCount; i++) { var c = rejaDeCeldas.GetChild(i).GetComponent<Collider>(); if (c != null) c.enabled = false; }
            StartCoroutine(HundirLaReja());
        }

        IEnumerator HundirLaReja()
        {
            const float duracion = 1.3f;
            for (float t = 0f; t < duracion; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duracion);
                for (int i = 0; i < rejaOrigen.Length; i++)
                    rejaDeCeldas.GetChild(i).position = rejaOrigen[i] + Vector3.down * (3.7f * k) + Vector3.right * (Mathf.Sin((t * 40f) + i) * 0.015f * (1f - k));
                yield return null;
            }
            for (int i = 0; i < rejaOrigen.Length; i++) rejaDeCeldas.GetChild(i).gameObject.SetActive(false);
        }

        void LiberarMilicianos()
        {
            MilicianosLiberados = true;
            AbrirLaReja();
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
            // Bug #338: liberados no seguian al jugador (quedaban parados en las celdas). Ahora salen siguiendolo, y suena un logro.
            var lider = Poseido();
            if (lider != null) foreach (var s in prisioneros) if (s != null && s.Brain != null) OrderService.IssueFollowOrder(s, lider, default, 0f, true);
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.Logro), 0.8f * AudioDirector.GainFor(SfxChannel.Sfx), 1.05f, "Logro");
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.ObjetivoCumplido), 0.7f * AudioDirector.GainFor(SfxChannel.Sfx), 1f, "ObjetivoCumplido");
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
            var cam = SP.Core.CamaraPrincipal.Actual;
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
