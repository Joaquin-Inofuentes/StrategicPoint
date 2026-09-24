using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.Tutorial;
using SP.UI;
using SP.Vehicles;

namespace SP.Mision
{
    public enum FaseDeMision { Infiltrar, Resistir, Rescatar, Escapar, Victoria, Derrota }

    // MISION DE RESCATE (partida principal).
    //
    //   1. INFILTRAR   avanzar entre las lineas enemigas hasta el CENTRO (la plaza de la aldea).
    //   2. RESISTIR    aguantar 60 s en el centro: tres oleadas atacan la plaza.
    //   3. RESCATAR    un civil sale de su refugio: acercarse y sacarlo. Te sigue, tiene vida
    //                  (si muere se pierde la mision).
    //   4. ESCAPAR     volver al punto de origen atravesando lineas enemigas y refuerzos. Un
    //                  helicoptero espera en la base; al acercarse: helices a fondo, fuego de
    //                  cobertura con trazadoras y musica tensa. Al llegar con el civil: cinematica
    //                  de victoria con el helicoptero huyendo de una horda.
    //
    // La dificultad (Core/Dificultad) cambia vida y dano y el tamano de las oleadas.
    public class MisionDirector : MonoBehaviour
    {
        public static MisionDirector Instancia { get; private set; }
        public static bool Activo => Instancia != null && Instancia.isActiveAndEnabled;

        [SerializeField] GameObject enemigoPrefab;
        [SerializeField] GameObject civilPrefab;
        [SerializeField] GameObject heliPrefab;

        public Vector3 Plaza = new Vector3(4f, 0f, 119f);
        public Vector3 Helipuerto = new Vector3(-26f, 0f, -8f);
        public Vector3 RefugioDelCivil = new Vector3(-2f, 0f, 124f);
        public float RadioCentro = 14f;
        public float RadioResistencia = 32f;
        public float RadioExtraccion = 13f;
        public float RadioDeAlertaDelHeli = 85f;
        public float SegundosDeResistencia = 60f;

        public FaseDeMision Fase { get; private set; } = FaseDeMision.Infiltrar;
        public float Restante { get; private set; }
        public Soldier Civil { get; private set; }
        public bool CivilRescatado { get; private set; }
        public Helicoptero Heli { get; private set; }
        public int OleadasLanzadas { get; private set; }
        public int SoldadosGenerados { get; private set; }
        public bool TensionSonando => tension != null && tension.isPlaying && tension.volume > 0.05f;
        public event System.Action<FaseDeMision> CambioDeFase;

        PlayerInputDriver driver;
        GameOutcomeController outcome;
        MisionHud hud;
        SP.Presentation.ObjectiveArrowIndicator flechaDeObjetivo;
        TutorialBeacon baliza, balizaCivil;
        AudioSource tension;
        float tRescate, proximoSeguir, avisoAtras, tiempoFuera, proximoAvisoCentro;
        bool hordaLanzada, refuerzosLanzados;
        readonly List<bool> oleadaHecha = new List<bool> { false, false, false };
        Transform raizEnemigos;
        Transform raizDeRutas;
        ProjectilePool pool;
        int contadorNombres;

        // Tamanos base de cada oleada (se multiplican por la dificultad).
        static readonly int[] TamanoOleada = { 4, 5, 6 };
        static readonly float[] SegundoOleada = { 2f, 22f, 42f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => Instancia = null;

        void Awake()
        {
            // SC_Tutorial quedo con un objeto "Mision" heredado: lanzaba las lineas enemigas y la derrota de la mision
            // sobre el tutorial (ronda 11: la corrida automatica perdia en el paso 12). El tutorial nunca corre la mision.
            var boot = GameplaySceneBootstrap.Activo;
            if (boot != null && boot.esTutorial) { Destroy(gameObject); return; }
            Instancia = this;
        }
        void OnDestroy() { if (Instancia == this) Instancia = null; MusicDirector.Atenuacion = 1f; }

        void Start()
        {
            driver = PlayerInputDriver.Activo;
            outcome = GameOutcomeController.Activo;
            // "Enemies" y "Waypoints" son raices de escena: se buscan entre las raices, no en toda la jerarquia, y una sola vez.
            var enemigos = SP.Core.RaicesDeEscena.Buscar("Enemies");
            if (enemigos == null) SP.Core.GameLog.Line("[MisionDirector] no hay una raiz 'Enemies' en la escena: los enemigos nacen bajo la mision");
            raizEnemigos = enemigos != null ? enemigos.transform : transform;
            raizDeRutas = SP.Core.RaicesDeEscena.Buscar("Waypoints")?.transform;

            if (heliPrefab != null)
            {
                var go = Instantiate(heliPrefab, Helipuerto, Quaternion.Euler(0f, 270f, 0f));
                go.name = "Helicoptero_Extraccion";
                Heli = go.AddComponent<Helicoptero>();
            }

            LanzarLineasEnemigas();
            hud = MisionHud.Crear(this);
            flechaDeObjetivo = SP.Presentation.ObjectiveArrowIndicator.Crear();
            baliza = TutorialBeacon.Crear("CENTRO", new Color(1f, 0.85f, 0.25f), Plaza, null, 3.2f, 24f);
            SP.Presentation.RutaAlObjetivo.Crear();
            SP.Presentation.ObjectiveDiamondMarker.Crear(PuntoObjetivoActual);
            GameLog.Line($"Mision iniciada (dificultad {Dificultad.PerfilActual.Nombre})");
            CambioDeFase?.Invoke(Fase);
        }

        // ---------------- utilidades ----------------
        public Vector3 PosicionDelJugador()
        {
            if (driver == null) driver = PlayerInputDriver.Activo;
            if (driver == null || driver.Brain == null || driver.Brain.Current == null) return Vector3.zero;
            if (driver.CurrentSeat.HasValue && driver.Vehicle != null) return driver.Vehicle.transform.position;
            return driver.Brain.Current.transform.position;
        }

        static float Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        Vector3 PosicionDelCivil()
        {
            if (Civil == null) return Vector3.zero;
            foreach (var v in WorldSystemsRegistry.Vehicles)
                if (v != null && v.RoleOf(Civil) != null) return v.transform.position;
            return Civil.transform.position;
        }

        // Punto de mundo del objetivo activo -- mismo criterio de DistanciaAlObjetivo (fase por
        // fase) pero devolviendo la posicion en vez de la distancia, para la flecha en el piso.
        public Vector3 PuntoObjetivoActual()
        {
            switch (Fase)
            {
                case FaseDeMision.Infiltrar: return Plaza;
                case FaseDeMision.Resistir: return Plaza;
                case FaseDeMision.Rescatar: return Civil != null ? Civil.transform.position : PosicionDelJugador();
                default: return Helipuerto;
            }
        }

        public float DistanciaAlObjetivo()
        {
            var p = PosicionDelJugador();
            switch (Fase)
            {
                case FaseDeMision.Infiltrar: return Plano(p, Plaza);
                case FaseDeMision.Resistir: return Plano(p, Plaza);
                case FaseDeMision.Rescatar: return Civil != null ? Plano(p, Civil.transform.position) : 0f;
                default: return Plano(p, Helipuerto);
            }
        }

        // ---------------- creacion de enemigos ----------------
        // BUG REAL reportado: "los enemigos que aparecen parecen aparecer desde adentro de las
        // casas" -- los puntos de aparicion (lineas, oleadas, refuerzos, horda) son coordenadas de
        // mundo fijas o dispersadas por angulo/radio sin conocer donde estan las casas del blockout
        // (LevelBlockoutBuilder), asi que a veces caen adentro de una. Se corrige en UN solo lugar
        // (todo enemigo nace via CrearEnemigo) empujando el punto fuera de la casa mas cercana antes
        // de instanciar, en vez de tener que auditar cada llamador.
        List<Bounds> casasCache;
        List<Bounds> Casas()
        {
            // No se memoriza un resultado vacio: si "Nivel_Blockout" todavia no estaba listo la
            // primera vez que se llamo (orden de carga de la escena), un cache vacio dejaria la
            // proteccion apagada para el resto de la partida. Recalcular unas pocas veces (una por
            // oleada de enemigos, no por enemigo) no cuesta nada.
            if (casasCache != null && casasCache.Count > 0) return casasCache;
            casasCache = new List<Bounds>();
            var raiz = GameObject.Find("Nivel_Blockout");
            if (raiz != null)
            {
                foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.name != "M_Blocking_Casa") continue;
                    casasCache.Add(r.bounds);
                }
            }
            return casasCache;
        }

        Vector3 EmpujarFueraDeCasas(Vector3 pos)
        {
            const float margen = 2f; // asi el soldado no aparece pegado a la pared, tiene donde pararse
            for (int pasada = 0; pasada < 2; pasada++)
            {
                foreach (var b in Casas())
                {
                    float minX = b.min.x - margen, maxX = b.max.x + margen;
                    float minZ = b.min.z - margen, maxZ = b.max.z + margen;
                    if (pos.x < minX || pos.x > maxX || pos.z < minZ || pos.z > maxZ) continue;

                    var centro = b.center;
                    var fuera = new Vector2(pos.x - centro.x, pos.z - centro.z);
                    if (fuera.sqrMagnitude < 0.0001f) fuera = new Vector2(0f, 1f); // exacto en el centro: empuja al norte
                    fuera.Normalize();
                    float mitadX = (maxX - minX) * 0.5f, mitadZ = (maxZ - minZ) * 0.5f;
                    // Distancia al borde de la caja expandida en la direccion "fuera" (caja, no circulo).
                    float t = Mathf.Min(
                        Mathf.Abs(fuera.x) > 0.0001f ? mitadX / Mathf.Abs(fuera.x) : float.MaxValue,
                        Mathf.Abs(fuera.y) > 0.0001f ? mitadZ / Mathf.Abs(fuera.y) : float.MaxValue);
                    pos = new Vector3(centro.x + fuera.x * t, pos.y, centro.z + fuera.y * t);
                }
            }
            return pos;
        }

        public Soldier CrearEnemigo(string nombre, Vector3 pos, float yaw = 180f)
        {
            if (enemigoPrefab == null) return null;
            pos = EmpujarFueraDeCasas(pos);
            var go = Instantiate(enemigoPrefab, new Vector3(pos.x, 0.8f, pos.z), Quaternion.Euler(0f, yaw, 0f), raizEnemigos);
            go.name = nombre;
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return null; }
            // Mismos numeros que los enemigos del nivel: 180 de vida, ven a 22 m y disparan a 13 m.
            s.Configure(nombre, TeamId.Enemy, RoleType.Enemy, 180);
            if (s.Brain != null) s.Brain.ConfigurarAlcances(22f, 13f);
            // BUG REAL: el prefab no trae el ProjectilePool de la escena (una referencia a un objeto de
            // escena no se guarda en un prefab), y sin pool WeaponHolder.TryFire no dispara nunca:
            // los enemigos que aparecen en juego eran decorativos. Se conecta aca.
            if (pool == null) pool = ProjectilePool.Activo;
            if (s.Weapon != null && pool != null) s.Weapon.SetPool(pool);
            Dificultad.AjustarVida(s);
            SoldadosGenerados++;
            return s;
        }

        void Patrullar(Soldier s, float mediaX, float mediaZ)
        {
            var brain = s.Brain;
            if (brain == null) return;
            var c = s.transform.position;
            var linea = PatrolRouteLine.Spawn(new[]
            {
                new Vector3(c.x - mediaX, 0f, c.z - mediaZ), new Vector3(c.x + mediaX, 0f, c.z - mediaZ),
                new Vector3(c.x + mediaX, 0f, c.z + mediaZ), new Vector3(c.x - mediaX, 0f, c.z + mediaZ),
            }, new Color(0.95f, 0.6f, 0.2f));
            linea.gameObject.name = "PatrolRoute_" + s.name;
            if (raizDeRutas != null) linea.transform.SetParent(raizDeRutas, true);
            brain.SetPatrolWaypoints(linea.Markers);
        }

        int Escalar(int baseN) => Mathf.Max(2, Mathf.RoundToInt(baseN * Dificultad.PerfilActual.CantidadDeOleadas));

        // Dispersa N puntos alrededor de un centro repartiendo angulos de forma pareja (en vez de
        // agruparlos con Random.Range en un rango angular chico), con jitter y radio variable, y
        // descarta puntos que caigan demasiado cerca de uno ya elegido (separacion minima). No cambia
        // la CANTIDAD de enemigos, solo donde aparecen: la dificultad (Dificultad.CantidadDeOleadas)
        // sigue decidiendo cuantos son.
        static List<Vector3> PosicionesDispersas(Vector3 centro, int n, float anguloInicioDeg, float anguloFinDeg, float radioMin, float radioMax, float separacionMinima = 5f)
        {
            var puntos = new List<Vector3>(n);
            if (n <= 0) return puntos;
            float arco = anguloFinDeg - anguloInicioDeg;
            float paso = n > 1 ? arco / n : 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 candidato = Vector3.zero;
                bool ok = false;
                for (int intento = 0; intento < 6 && !ok; intento++)
                {
                    // Un sector distinto por enemigo (arco / n) + jitter propio, asi no se amontonan
                    // todos en el mismo angulo; el radio tambien varia enemigo a enemigo.
                    float centroSector = anguloInicioDeg + paso * i + paso * 0.5f;
                    float angulo = centroSector + Random.Range(-paso * 0.5f, paso * 0.5f) * (intento == 0 ? 1f : 1.6f);
                    float radio = Random.Range(radioMin, radioMax);
                    var dir = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
                    candidato = centro + dir * radio;
                    ok = true;
                    foreach (var p in puntos)
                        if (Plano(p, candidato) < separacionMinima) { ok = false; break; }
                }
                puntos.Add(candidato);
            }
            return puntos;
        }

        // Posiciones al azar dentro de una franja rectangular (X entre xMin/xMax, Z entre
        // zMin/zMax), con separacion minima entre ellas -- mismo criterio de rechazo por
        // intentos que PosicionesDispersas, pero para una franja en vez de un arco: la forma
        // que hace falta para una "linea" que cruza el corredor de aproximacion.
        static List<Vector3> PosicionesEnFranja(float xMin, float xMax, float zMin, float zMax, int n, float separacionMinima)
        {
            var puntos = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 candidato = Vector3.zero;
                bool ok = false;
                for (int intento = 0; intento < 8 && !ok; intento++)
                {
                    candidato = new Vector3(Random.Range(xMin, xMax), 0f, Random.Range(zMin, zMax));
                    ok = true;
                    foreach (var p in puntos)
                        if (Plano(p, candidato) < separacionMinima) { ok = false; break; }
                }
                puntos.Add(candidato);
            }
            return puntos;
        }

        // Dos lineas enemigas entre la base y el centro: campo de tiro (z 52-66) y paso del canon (z 84-92).
        void LanzarLineasEnemigas()
        {
            // El campo de tiro arranca a 46 m de la base: mas cerca, los aliados (vision 20 m) los ven
            // y salen a pelear solos contra todo el grupo antes de que el jugador decida nada.
            //
            // Pedido explicito: "reubicar mejor la aparicion de enemigos asi se ve mejor". Antes
            // estas eran 8+6 coordenadas fijas a mano: la MISMA formacion, en el MISMO lugar
            // exacto, en cada partida -- se leia artificial/"en fila" apenas se jugaba dos veces.
            // Se mantienen las mismas franjas tacticas (mismo rango de X/Z que las coordenadas
            // viejas cubrian) pero dispersas al azar con separacion minima, mismo criterio que ya
            // usa PosicionesDispersas para las oleadas (que tuvo este mismo problema y se arreglo
            // asi -- ver su comentario mas abajo).
            int nc = Escalar(5), np = Escalar(4);
            var campo = PosicionesEnFranja(-34f, 46f, 52f, 66f, nc, 7f);
            var paso = PosicionesEnFranja(-32f, 34f, 84f, 92f, np, 7f);
            for (int i = 0; i < nc; i++)
            {
                var s = CrearEnemigo($"Enemigo_LineaA_{i + 1}", campo[i]);
                if (s != null) Patrullar(s, 5f, 3f);
            }
            for (int i = 0; i < np; i++)
            {
                var s = CrearEnemigo($"Enemigo_LineaB_{i + 1}", paso[i]);
                if (s != null) Patrullar(s, 5f, 3f);
            }
            GameLog.Line($"Mision: lineas enemigas listas ({nc} + {np})");
        }

        // Una oleada: soldados que aparecen en el borde norte o sur de la aldea y avanzan a la plaza.
        void LanzarOleada(int indice)
        {
            int n = Escalar(TamanoOleada[indice]);
            bool desdeElNorte = indice != 1;
            // Antes: todos alineados en un x angosto (-12..16) a una z fija -> se sentian "en fila".
            // Ahora: arco de ~130 grados centrado en la direccion de ataque, con radio (distancia al
            // borde) variable y separacion minima entre ellos, asi atacan la plaza desde angulos y
            // distancias distintas en vez de todos juntos en el mismo punto.
            int nNorte = indice == 2 ? (n + 1) / 2 : (desdeElNorte ? n : 0);
            int nSur = n - nNorte;
            var puntosNorte = PosicionesDispersas(Plaza, nNorte, 180f - 65f, 180f + 65f, 30f, 42f, 6f);
            var puntosSur = PosicionesDispersas(Plaza, nSur, 0f - 65f, 0f + 65f, 24f, 34f, 6f);
            for (int i = 0; i < n; i++)
            {
                bool norte = i < nNorte;
                var pos = norte ? puntosNorte[i] : puntosSur[i - nNorte];
                var s = CrearEnemigo($"Enemigo_Oleada{indice + 1}_{i + 1}", pos, norte ? 180f : 0f);
                if (s == null) continue;
                s.Brain.IssueMoveOrder(Plaza + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)));
            }
            OleadasLanzadas++;
            AlertQueue.Push($"OLEADA {indice + 1}/3: {n} ENEMIGOS AVANZAN A LA PLAZA", AlertPriority.Alta, 3f);
            GameLog.Line($"Mision: oleada {indice + 1} ({n} enemigos)");
        }

        // Refuerzos al salir con el civil: dos grupos entre el centro y la base.
        void LanzarRefuerzos()
        {
            refuerzosLanzados = true;
            int n = Escalar(5);
            // Centro entre la plaza y la base: antes dos franjas de Z angostas con X libre (columnas
            // paralelas). Ahora un arco amplio con radio variable para que corten el camino de vuelta
            // desde angulos distintos, no todos en la misma franja.
            var centro = new Vector3(5f, 0f, 67f);
            var puntos = PosicionesDispersas(centro, n, 20f, 340f, 18f, 42f, 6f);
            for (int i = 0; i < n; i++)
            {
                var s = CrearEnemigo($"Enemigo_Refuerzo_{i + 1}", puntos[i]);
                if (s != null) Patrullar(s, 6f, 4f);
            }
            // Pedido explicito: quitar el aviso "REFUERZOS ENEMIGOS EN EL CAMINO DE VUELTA" -- molesta.
            GameLog.Line($"Mision: refuerzos ({n})");
        }

        // La horda que persigue al jugador al acercarse al helicoptero.
        void LanzarHorda()
        {
            hordaLanzada = true;
            int n = Escalar(6);
            // Antes: un unico rectangulo angosto al norte del heli -> la horda llegaba en bloque desde
            // un solo lado. Ahora: arco de 220 grados alrededor del helipuerto (evitando el sur, donde
            // esta el camino de escape ya recorrido) con radio variable, asi rodean desde varios
            // angulos y distancias -- mas sensacion de horda envolvente / adrenalina.
            var puntos = PosicionesDispersas(Helipuerto, n, -20f, 200f, 22f, 45f, 6f);
            for (int i = 0; i < n; i++)
            {
                var s = CrearEnemigo($"Enemigo_Horda_{i + 1}", puntos[i], 180f);
                if (s != null) s.Brain.IssueMoveOrder(Helipuerto + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(6f, 14f)));
            }
            AlertQueue.Push("¡UNA HORDA TE PERSIGUE! ¡AL HELICOPTERO!", AlertPriority.Alta, 3f);
            GameLog.Line($"Mision: horda ({n})");
        }

        // ---------------- civil ----------------
        void AparecerCivil()
        {
            if (civilPrefab == null || Civil != null) return;
            var go = Instantiate(civilPrefab, new Vector3(RefugioDelCivil.x, 0.8f, RefugioDelCivil.z), Quaternion.Euler(0f, 180f, 0f));
            go.name = "Civil";
            Civil = go.GetComponent<Soldier>();
            Civil.Configure("Civil", TeamId.Player, RoleType.Civilian, 150);
            if (Civil.Brain != null) Civil.Brain.Pasivo = true;
            Civil.gameObject.AddComponent<Rehen>();   // tinte propio, marcador flotante y aviso sonoro al acercarse
            // Pedido explicito: "el cartel de civil rescatado mas delgado...
            // y sea en la base de abajo" -- columna mas fina (0.35 en vez de
            // 0.9), texto mas chico (escalaTexto 0.6) y pegado al piso
            // (0.7 m) en vez de flotando a la altura de la cabeza (3.4 m).
            balizaCivil = TutorialBeacon.Crear("CIVIL", new Color(0.4f, 0.9f, 1f), RefugioDelCivil, Civil.transform, 0.35f, 10f, alturaEtiqueta: 0.7f, escalaTexto: 0.6f);
            GameLog.Line("Mision: el civil sale de su refugio");
        }

        void SeguirAlJugador()
        {
            if (Civil == null || Civil.Health == null || !Civil.Health.IsAlive || driver == null || driver.Brain == null || driver.Brain.Current == null) return;
            if (Time.time < proximoSeguir) return;
            proximoSeguir = Time.time + 1.5f;
            var brain = Civil.Brain;
            if (brain == null || !Civil.gameObject.activeInHierarchy) return;
            var lider = driver.Brain.Current;
            bool yaSigue = brain.State == AiState.Follow && brain.FollowTarget == lider;
            if (!yaSigue && Plano(Civil.transform.position, lider.transform.position) > 5f)
                OrderService.IssueFollowOrder(Civil, lider, new Vector3(0.8f, 0f, -1.6f));
        }

        // ---------------- maquina de estados ----------------
        void Update()
        {
            if (Fase == FaseDeMision.Victoria || Fase == FaseDeMision.Derrota)
            {
                if (flechaDeObjetivo != null) flechaDeObjetivo.Ocultar();
                return;
            }
            if (driver == null) driver = PlayerInputDriver.Activo;
            float dt = Time.deltaTime;

            switch (Fase)
            {
                case FaseDeMision.Infiltrar: TickInfiltrar(); break;
                case FaseDeMision.Resistir: TickResistir(dt); break;
                case FaseDeMision.Rescatar: TickRescatar(dt); break;
                case FaseDeMision.Escapar: TickEscapar(dt); break;
            }
            if (hud != null) hud.Refrescar();
            if (flechaDeObjetivo != null)
            {
                var p = PosicionDelJugador();
                if (p != Vector3.zero) flechaDeObjetivo.Actualizar(p, PuntoObjetivoActual());
                else flechaDeObjetivo.Ocultar();
            }
        }

        void CambiarFase(FaseDeMision f)
        {
            Fase = f;
            GameLog.Line("Mision: fase " + f);
            CambioDeFase?.Invoke(f);
        }

        void TickInfiltrar()
        {
            if (Plano(PosicionDelJugador(), Plaza) > RadioCentro) return;
            Restante = SegundosDeResistencia;
            CambiarFase(FaseDeMision.Resistir);

            // Pedido explicito: "feedback visual de cuando llegue al centro,
            // particulas simples" -- un estallido chico de escombros dorados
            // (mismo color que el rombo de objetivo) mas el flash de
            // ImpactFx, en la posicion del jugador. Reusa DebrisPool/ImpactFx
            // en vez de un ParticleSystem nuevo: mismo criterio "fisica
            // simple" que ya usa el resto del juego para este tipo de aviso.
            var puntoDeLlegada = PosicionDelJugador();
            ImpactFx.Spawn(puntoDeLlegada + Vector3.up, DiamondGizmo.ColorObjetivo, 1.1f, 0.4f);
            for (int i = 0; i < 10; i++)
            {
                var dir = (Random.insideUnitSphere + Vector3.up * 1.6f).normalized;
                DebrisPool.Spawn(puntoDeLlegada + Vector3.up * 0.4f, dir * Random.Range(3f, 7f), DiamondGizmo.ColorObjetivo, Random.Range(0.08f, 0.15f), 0.9f);
            }
            // La columna es guia de LARGA distancia; con el jugador ya
            // parado adentro (justo donde va a pelear los proximos
            // SegundosDeResistencia) queda atravesandole la vista. El
            // fundido por distancia de camara no alcanza a apagarla sola
            // porque RadioCentro es mas grande que el radio del fundido.
            if (baliza != null) baliza.OcultarColumna();
            AlertQueue.Push($"LLEGASTE AL CENTRO: RESISTI {Mathf.RoundToInt(SegundosDeResistencia)} SEGUNDOS", AlertPriority.Alta, 3.5f);
        }

        void TickResistir(float dt)
        {
            bool dentro = Plano(PosicionDelJugador(), Plaza) <= RadioResistencia;
            if (dentro)
            {
                float antes = SegundosDeResistencia - Restante;
                Restante -= dt;
                for (int i = 0; i < oleadaHecha.Count; i++)
                    if (!oleadaHecha[i] && SegundosDeResistencia - Restante >= SegundoOleada[i]) { oleadaHecha[i] = true; LanzarOleada(i); }
                tiempoFuera = 0f;
                if (Restante <= 0f)
                {
                    Restante = 0f;
                    if (baliza != null) { baliza.Quitar(); baliza = null; }
                    AparecerCivil();
                    CambiarFase(FaseDeMision.Rescatar);
                    AlertQueue.Push("¡AGUANTASTE! UN CIVIL PIDE AYUDA: ROMPE EL CERCO Y SACALO", AlertPriority.Alta, 3.5f);
                }
            }
            else
            {
                tiempoFuera += dt;
                if (Time.time >= proximoAvisoCentro)
                {
                    proximoAvisoCentro = Time.time + 4f;
                    AlertQueue.Push("VOLVE AL CENTRO: EL TEMPORIZADOR ESTA DETENIDO", AlertPriority.Media, 2.5f);
                }
            }
        }

        void TickRescatar(float dt)
        {
            if (Civil == null || !Civil.Health.IsAlive) { Perder("EL CIVIL MURIO"); return; }
            float d = Plano(PosicionDelJugador(), Civil.transform.position);
            if (d <= 4.5f) tRescate += dt; else tRescate = Mathf.Max(0f, tRescate - dt);
            if (tRescate < 1.2f) return;

            CivilRescatado = true;
            if (balizaCivil != null) { balizaCivil.Quitar(); balizaCivil = null; }
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            baliza = TutorialBeacon.Crear("HELICOPTERO", new Color(0.35f, 1f, 0.5f), Helipuerto + Vector3.right * 6f, null, 3.2f, 26f);
            // Pedido explicito (ronda nueva): "al llegar al objetivo [del]
            // civil q tambien tenga su sistema de particulas" -- antes este
            // momento no tenia ningun efecto visual, solo el texto de
            // AlertQueue de abajo. Estallido blanco-verdoso ("a salvo") en
            // el punto del civil.
            SparkleBurstFx.Spawn(Civil.transform.position + Vector3.up * 1f, new Color(0.55f, 1f, 0.65f), 1f, 2.6f, 30, 3f);
            LanzarRefuerzos();
            CambiarFase(FaseDeMision.Escapar);
            AlertQueue.Push("¡CIVIL RESCATADO! LLEVALO AL HELICOPTERO (PUNTO DE ORIGEN)", AlertPriority.Alta, 4f);
            if (Heli != null) Heli.Alerta(false);
            proximoSeguir = 0f;
        }

        void TickEscapar(float dt)
        {
            if (Civil == null || !Civil.Health.IsAlive) { Perder("EL CIVIL MURIO"); return; }
            SeguirAlJugador();

            var pj = PosicionDelJugador();
            float dHeli = Plano(pj, Helipuerto);
            float dCivil = Plano(PosicionDelCivil(), Helipuerto);
            float entreEllos = Plano(pj, PosicionDelCivil());

            // El civil se queda muy atras: aviso, y a los 25 s se pierde.
            if (entreEllos > 40f)
            {
                avisoAtras += dt;
                if (avisoAtras > 4f && Time.time >= proximoAvisoCentro)
                {
                    proximoAvisoCentro = Time.time + 4f;
                    AlertQueue.Push("EL CIVIL SE QUEDO ATRAS: VOLVE POR EL", AlertPriority.Media, 2.5f);
                }
                if (avisoAtras > 25f) { Perder("PERDISTE AL CIVIL"); return; }
            }
            else avisoAtras = 0f;

            // Llegando al helicoptero: helices a fondo, fuego de cobertura, musica tensa y horda.
            bool cerca = dHeli <= RadioDeAlertaDelHeli;
            if (Heli != null)
            {
                Heli.Alerta(cerca);
                Heli.DisparaCobertura = cerca;
            }
            SonarTension(cerca);
            if (cerca && !hordaLanzada) LanzarHorda();

            if (dHeli <= RadioExtraccion && dCivil <= RadioExtraccion + 3f) Ganar();
        }

        // ---------------- musica tensa ----------------
        void SonarTension(bool activa)
        {
            if (tension == null)
            {
                var clip = SP.Core.RecursosCache.Cargar<AudioClip>("Audio/Music/Tension");
                if (clip == null) return;
                var go = new GameObject("MusicaTension");
                go.transform.SetParent(transform, false);
                tension = go.AddComponent<AudioSource>();
                tension.clip = clip; tension.loop = true; tension.spatialBlend = 0f; tension.volume = 0f;
            }
            float objetivo = activa ? 0.75f : 0f;
            tension.volume = Mathf.MoveTowards(tension.volume, objetivo, Time.deltaTime * 0.5f);
            if (activa && !tension.isPlaying) tension.Play();
            if (!activa && tension.volume <= 0.001f && tension.isPlaying) tension.Pause();
            MusicDirector.Atenuacion = Mathf.MoveTowards(MusicDirector.Atenuacion, activa ? 0.12f : 1f, Time.deltaTime * 0.6f);
        }

        // ---------------- final ----------------
        void Ganar()
        {
            CambiarFase(FaseDeMision.Victoria);
            GameLog.Line("Mision: llegaron al helicoptero con el civil");
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            var cine = gameObject.AddComponent<CinematicaDeVictoria>();
            cine.Iniciar(this, driver, outcome, Heli, enemigoPrefab, tension);
        }

        public void Perder(string motivo)
        {
            if (Fase == FaseDeMision.Derrota || Fase == FaseDeMision.Victoria) return;
            CambiarFase(FaseDeMision.Derrota);
            GameLog.Line("Mision fallida: " + motivo);
            AlertQueue.Push(motivo, AlertPriority.Alta, 3f);
            if (Heli != null) { Heli.Alerta(false); Heli.DisparaCobertura = false; }
            if (outcome != null) outcome.ShowDefeat();
        }

        // Para las pruebas: salta a una fase (reposiciona lo necesario).
        public void SaltarAFase(FaseDeMision f)
        {
            switch (f)
            {
                case FaseDeMision.Resistir: Restante = SegundosDeResistencia; CambiarFase(f); break;
                case FaseDeMision.Rescatar: AparecerCivil(); Restante = 0f; CambiarFase(f); break;
                case FaseDeMision.Escapar:
                    AparecerCivil(); CivilRescatado = true; if (!refuerzosLanzados) LanzarRefuerzos();
                    if (baliza != null) baliza.Quitar();
                    baliza = TutorialBeacon.Crear("HELICOPTERO", new Color(0.35f, 1f, 0.5f), Helipuerto + Vector3.right * 6f, null, 3.2f, 26f);
                    CambiarFase(f); break;
                default: CambiarFase(f); break;
            }
        }
    }
}
