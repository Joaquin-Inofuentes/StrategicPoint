using System.Collections;
using UnityEngine;
using SP.Core;
using SP.UI;

namespace SP.Presentation
{
    // Marca en el log de flujo que la escena de gameplay terminó de
    // cargar (Start corre después de que todo el resto ya se construyó),
    // y muestra el objetivo de la mision al arrancar -- antes la partida
    // empezaba sin decir que hacer, y el jugador deducia el objetivo
    // matando cosas hasta que aparecia la pantalla de victoria.
    [DefaultExecutionOrder(-200)]
    public class GameplaySceneBootstrap : MonoBehaviour
    {
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static GameplaySceneBootstrap Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
        void OnDestroy() { if (Activo == this) Activo = null; }
        void Awake() => RegistrarActivo();
        public PhaseBannerView ObjectiveBanner;
        public ModeToastView ModeToast;

        // Escena de tutorial: el modulo de tutorial da sus propios carteles, asi
        // que se omiten el objetivo de mision, el aviso de TAB, el consejo de la
        // primera accion y el aviso de bloques.
        public bool esTutorial;

        const string PrefUsedTab = "sp_used_tab";

        void Start()
        {
            // Prellenado de los escombros en runtime (no al construir la
            // escena): asi el primer estallido real no paga la creacion
            // de 64 objetos, y nada de esto termina guardado en la escena.
            DebrisPool.Prewarm();

            // TODAS las barras del juego estaban rotas: una Image con
            // type = Filled solo respeta fillAmount si tiene sprite, y no
            // habia una sola que lo tuviera. Ver SP.UI.SpriteBlanco.
            // Un barrido al arrancar, no por frame.
            int reparadas = 0;
            foreach (var raiz in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                reparadas += SP.UI.SpriteBlanco.RepararTodo(raiz);
            if (reparadas > 0) GameLog.Line($"Se repararon {reparadas} barras de la interfaz (Filled sin sprite)");

            // Los soldados estaban a cinco alturas distintas respecto del
            // piso -- dos enemigos flotando 1,60 m, otros dos a 0,80, y la
            // escuadra hundida 20 cm. Ver SP.Core.ApoyoEnElPiso: se apoya
            // cada uno con su propio collider, una vez, al arrancar.
            int apoyados = SP.Core.ApoyoEnElPiso.ApoyarATodos();
            if (apoyados > 0) GameLog.Line($"Se apoyaron {apoyados} soldados que estaban flotando o hundidos");

            // El menu de ordenes ([Q] sostenido) no esta serializado en
            // SC_Gameplay -- esa escena no la construye HeadlessTestRunner.
            // Se arma aca para que la funcionalidad exista en el juego y no
            // solo en la escena de pruebas.
            if (SP.UI.MenuDeOrdenes.AsegurarEnEscena() != null)
                GameLog.Line("Menu de ordenes listo ([Q] sostenido)");

            // Los emplazamientos de ametralladora de las torres y bunkers pasan a ser torretas fijas
            // utilizables, y el cartel de modo dios ([F4]) queda listo.
            int torretas = SP.Vehicles.TorretaFija.InstalarEnEscena();
            if (torretas > 0) GameLog.Line($"{torretas} ametralladoras fijas listas ([E] para usarlas)");
            var driverBoot = SP.Player.PlayerInputDriver.Activo;
            if (driverBoot != null && driverBoot.AimUiRef != null) SP.UI.ModoDiosView.Asegurar(driverBoot.AimUiRef.transform.parent);

            // Dificultad elegida en el menu: potenciadores de vida/dano (solo partida principal).
            SP.Core.Dificultad.Activa = !esTutorial;
            if (!esTutorial)
            {
                int ajustados = SP.Core.Dificultad.AplicarVida();
                GameLog.Line($"Dificultad {SP.Core.Dificultad.PerfilActual.Nombre}: vida ajustada en {ajustados} soldados");
            }

            // El soldado de asalto puede demoler muros (agachado y quieto 4 s).
            int asaltos = SP.Player.DemoledorAsalto.AsegurarEnAsaltos();
            if (asaltos > 0) GameLog.Line($"Demolicion lista en {asaltos} soldado(s) de asalto");

            // Los puntos de cobertura del mapa (F1): los cuatro costados
            // de cada obstaculo solido. Se calculan una vez al arrancar y
            // se rehacen si un obstaculo se derrumba (ver ObstacleMarker).
            int coberturas = SP.Core.Coberturas.Registrar();
            if (coberturas > 0) GameLog.Line($"Se registraron {coberturas} puntos de cobertura");

            // El minimapa mostraba a la escuadra y a los vehiculos (ya
            // puestos a mano en la escena) pero nunca a los obstaculos: el
            // mapa no decia nada del terreno hasta acercarse a mirarlo (D1).
            int obstaculosEnMinimapa = MinimapIcon.RegistrarObstaculos(MinimapIcon.ObstacleMinimapColor);
            if (obstaculosEnMinimapa > 0) GameLog.Line($"Se agregaron {obstaculosEnMinimapa} obstaculos al minimapa");

            int vehiculosLibres = MinimapIcon.RegistrarVehiculosLibres();
            if (vehiculosLibres > 0) GameLog.Line($"Se agregaron {vehiculosLibres} vehiculos libres al minimapa");

            // El minimapa siempre arranca en MINI (MinimapFollow.tamanoMini),
            // desde el primer frame.
            var minimapFollow = SP.UI.MinimapFollow.Activo;
            if (minimapFollow != null) minimapFollow.AplicarTamanoInicial();

            // Aviso de bloque del nivel (el mapa mide 320 m de largo) y ajustes
            // de la escuadra (distancia a la que los aliados te siguen).
            if (!esTutorial) AnuncioDeZonas.Asegurar();
            SP.Ai.AjustesDeEscuadra.AsegurarEnEscena();

            // C1: etiqueta al pie de cada unidad (vida, tipo, ocupantes),
            // solo visible en RTS. Ninguna escena la trae puesta a mano.
            int etiquetas = UnitLabelView.RegistrarTodas();
            if (etiquetas > 0) GameLog.Line($"Se armaron {etiquetas} etiquetas de unidad");

            // Cartel "[Q] REVIVIR" sobre cada aliado: arranca oculto (Tick
            // recien lo prende cuando ESE soldado cae), mismo patron que
            // las etiquetas de arriba.
            int cartelesRevivir = RevivePromptView.RegistrarTodas();
            if (cartelesRevivir > 0) GameLog.Line($"Se armaron {cartelesRevivir} carteles de revivir");

            // BUG REPORTADO: recargar la escena o volver de SC_Tutorial a
            // SC_Gameplay (SceneLoader.Cargar) dejaba a algun aliado de la
            // escuadra teletransportado a cientos de metros del nivel real
            // (fuera del NavMesh) apenas arranca la partida -- se lo ve
            // "corriendo desde la nada" un par de segundos hasta que la IA
            // de seguimiento lo trae de vuelta. No se identifico el punto
            // exacto donde algo escribe esa posicion (corre en el primer
            // Update de otro sistema, despues de este Start en orden -200),
            // asi que esto es la misma red de seguridad que ApoyoEnElPiso:
            // un frame despues de que todo termino de arrancar, se verifica
            // que cada soldado de la escuadra este de verdad DENTRO del
            // nivel, y si no, se lo reubica al lado del lider.
            StartCoroutine(ValidarPosicionDeEscuadraUnFrameDespues());

            GameLog.Line("Inicio partida");
            GameLog.Line("Cargo la escena");
            if (ObjectiveBanner != null && !esTutorial)
                {
                if (SP.Mision.MisionDirector.Instancia != null)
                    ObjectiveBanner.Show("MISION: RESCATE\nCentro · Resiste 60 s · Rescate · Helicoptero", 5f);
                else
                    ObjectiveBanner.Show("Elimina a todos los enemigos\nmanteniendo viva a tu escuadra", 3f);
                // La dificultad se dice al arrancar y queda fija en el cartel de la mision.
                var pf = SP.Core.Dificultad.PerfilActual;
                SP.UI.AlertQueue.Push($"DIFICULTAD {pf.Nombre}",
                                      SP.UI.AlertPriority.Alta, 6f);
            }

            // El cambio de vista FPS/RTS es la mecanica central del juego
            // y nada la explicaba: se podia jugar la partida entera sin
            // descubrirla. Se avisa una vez, despues del cartel de
            // objetivo, y nunca mas una vez que el jugador la usa (el
            // propio TAB marca el PlayerPref, ver PlayerInputDriver).
            if (esTutorial) return;
            if (ModeToast != null && PlayerPrefs.GetInt(PrefUsedTab, 0) == 0)
                StartCoroutine(ShowTabHintDelayed());

            // 52: guiar la PRIMERA accion. El cartel de objetivo dice QUE
            // hay que lograr, pero no que hacer en el primer segundo: un
            // jugador nuevo se quedaba parado sin saber por donde empezar.
            // Va por la cola de alertas (216) para no pisarse con el aviso
            // de TAB, que se dispara en la misma ventana de tiempo.
            if (PlayerPrefs.GetInt(PrefFirstActionShown, 0) == 0)
            {
                SP.UI.AlertQueue.Push("Avanza con [WASD] y dispara con clic izquierdo",
                                      SP.UI.AlertPriority.Baja, 3.5f);
                PlayerPrefs.SetInt(PrefFirstActionShown, 1);
                PlayerPrefs.Save();
            }
        }

        // BUG REAL encontrado en vivo: con el umbral original (60 m) un
        // aliado que aparecio a 512 m se corrigio bien, pero otro que
        // aparecio a ~59 m (fuera del rango normal de la escuadra, que
        // arranca agrupada a metros del lider) se colaba sin corregir. El
        // seguimiento automatico recien dispara a los 12,5 m
        // (AjustesDeEscuadra.DistanciaParaSeguir); no hay ningun escenario
        // normal de arranque de mision donde alguien de la escuadra este a
        // mas de 20 m del lider.
        const float DistanciaMaximaRazonable = 20f;

        System.Collections.IEnumerator ValidarPosicionDeEscuadraUnFrameDespues()
        {
            // Un solo chequeo a destiempo cero no alcanza: lo que sea que
            // provoca el salto no se vio en el primer frame en las pruebas
            // en vivo, sino recien despues de un par de segundos de
            // simulacion real. Se repite unas cuantas veces al arrancar en
            // vez de una unica vez, y se corta apenas la escuadra esta bien.
            for (int intento = 0; intento < 10; intento++)
            {
                yield return new WaitForSeconds(0.5f);

                var lider = SP.Ai.AjustesDeEscuadra.Lider;
                var raizEscuadra = SP.Core.RaicesDeEscena.Buscar("PlayerSquad");
                if (lider == null || raizEscuadra == null) continue;

                bool huboProblema = false;
                foreach (var soldier in raizEscuadra.GetComponentsInChildren<SP.Actors.Soldier>(true))
                {
                    if (soldier == null || soldier == lider) continue;
                    float dist = Vector3.Distance(soldier.transform.position, lider.transform.position);
                    if (dist <= DistanciaMaximaRazonable) continue;

                    huboProblema = true;
                    var destino = lider.transform.position - lider.transform.forward * 2f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                        destino = hit.position;
                    soldier.transform.position = destino;
                    SP.Core.ApoyoEnElPiso.Apoyar(soldier.transform);
                    if (soldier.Brain != null) soldier.Brain.ReactivarNavegacion();
                    GameLog.Line($"{soldier.DisplayName} aparecio a {dist:0} m del resto de la escuadra: reubicado junto al lider");
                }
                if (!huboProblema) yield break;
            }
        }

        static string Pct(float f)
        {
            int p = Mathf.RoundToInt((f - 1f) * 100f);
            return p == 0 ? "x1" : (p > 0 ? "+" : "") + p + "%";
        }

        // Una sola vez en la vida del jugador, igual que el aviso de TAB:
        // repetirlo en cada partida seria ruido para quien ya sabe jugar.
        const string PrefFirstActionShown = "sp_first_action_shown";

        IEnumerator ShowTabHintDelayed()
        {
            yield return new WaitForSeconds(3.5f);
            if (PlayerPrefs.GetInt(PrefUsedTab, 0) == 0)
                ModeToast.Show("[TAB] cambia entre vista en primera persona y vista tactica", 3f);
        }
    }
}
