using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Tutorial
{
    // Modulo de tutorial. Recorre 26 pasos en orden; cada paso tiene sub-pasos y
    // cada sub-paso una BANDERA booleana (ver TutorialFlags). El cuadro de
    // dialogo (TutorialUI) muestra el mensaje del primer sub-paso pendiente, las
    // teclas que hay que apretar (se iluminan al apretarlas), una pista si el
    // jugador se traba y, al terminar todo, el efecto de victoria.
    //
    // El tutorial NO modifica el juego: solo MIRA lo que el jugador hace (teclas,
    // estado del soldado, ordenes, impactos) y reacciona. La unica ayuda es que
    // mantiene con vida al jugador y al tanque, para que nadie pierda por
    // estar aprendiendo.
    public class TutorialManager : MonoBehaviour
    {
        // ---- Escena (se buscan por nombre si no estan asignados) ----
        public GameObject prefabEnemigo;
        public float pausaEntrePasos = 1.7f;

        [Header("Banderas (solo lectura: mira como cambian en Play)")]
        public TutorialFlags Flags = new TutorialFlags();

        public static TutorialManager Instance { get; private set; }

        public int Indice { get; private set; } = -1;
        public int Total => pasos.Count;
        public bool Terminado { get; private set; }
        public bool EnPausa => pausaHasta > Time.time;
        public TutorialUI Ui => ui;
        public float TiempoTotal => Time.time - tInicio;
        public Paso PasoActual => Indice >= 0 && Indice < pasos.Count ? pasos[Indice] : null;
        public IReadOnlyList<float> DuracionPorPaso => duraciones;
        public int Bajas => bajas;

        // ---------------------------------------------------------------
        public class Sub
        {
            public string Texto;
            public Func<string> TextoVivo;   // si existe, el texto de la casilla se recalcula (contadores)
            public string Mensaje;           // que hacer AHORA si esta pendiente
            public string Pista;             // ayuda si el jugador se traba
            public Func<bool> Leer;
            public Action<bool> Poner;
            public bool Hecha;
            public string TextoActual => TextoVivo != null ? TextoVivo() : Texto;
        }

        public class Paso
        {
            public string Id, Titulo, Teclas;
            public Color Acento = new Color(0.30f, 0.85f, 1f);
            public bool OcultarSubs;
            public Func<string> MensajeVivo;      // si el paso arma el mensaje solo (WASD)
            public Sub[] Subs;
            public Action AlEntrar, Evaluar, AlSalir;
        }

        readonly List<Paso> pasos = new List<Paso>();
        readonly List<float> duraciones = new List<float>();
        TutorialUI ui;
        PlayerInputDriver driver;
        float tInicio, tPaso, sinProgreso, pausaHasta;
        bool pausaActiva;
        string pistaMostrada = "";
        int bajas;
        readonly List<IDisposable> suscripciones = new List<IDisposable>();

        // ---- Estado compartido entre pasos ----
        Soldier soldadoInicial;
        readonly List<Soldier> aliados = new List<Soldier>();
        float ultimoYaw, ultimoPitch, acumYaw, acumPitch;
        readonly HashSet<int> ordenadosARts = new HashSet<int>();
        int aliadoSeleccionadoId = -1;
        readonly HashSet<int> idsOlaA = new HashSet<int>(), idsOlaB = new HashSet<int>();
        readonly List<Soldier> olaViva = new List<Soldier>();
        int muertosOlaA, bajasTotales;
        float ultimoCd = 1f;
        const int BajasNecesarias = 3;
        float proximoRelleno;
        Vector3 tanquePosInicio;
        bool olaBLanzada, rtsAjustado;
        float proximoAcoso, proximaCura;
        Transform objDummy, objPared, objDestruible, zonaA, zonaB, meta;
        ObstacleMarker marcaPared, marcaDestruible;
        Soldier dummy;
        TutorialBeacon balizaDummy, balizaPared, balizaDestruible, balizaTanque, balizaZonaA, balizaZonaB, balizaMeta;
        readonly List<TutorialBeacon> balizasAliados = new List<TutorialBeacon>();
        VictoriaTutorial victoria;

        // ===============================================================
        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Desuscribir();
            TutorialBeacon.QuitarTodas();
        }

        void Start()
        {
            driver = FindFirstObjectByType<PlayerInputDriver>();
            var canvas = GameObject.Find("UI_Canvas/Canvas");
            var cv = canvas != null ? canvas.transform : FindFirstObjectByType<Canvas>().transform;
            ui = TutorialUI.Crear(cv);

            Flags.Reiniciar();
            ModoDios.Poner(false);
            SP.UI.MenuDeOrdenes.PonerPista(-1);
            TutorialBeacon.QuitarTodas();
            TutorialLog.Reiniciar();
            tInicio = Time.time;

            objDummy = Buscar("Tut_Enemigo_Estatico");
            objPared = Buscar("Tut_Pared");
            objDestruible = Buscar("Tut_Destruible");
            zonaA = Buscar("Tut_ZonaA");
            zonaB = Buscar("Tut_ZonaB");
            meta = Buscar("Tut_Meta");
            if (objDummy != null)
            {
                dummy = objDummy.GetComponent<Soldier>();
                // La postura no se serializa: se pone al empezar. Quieto, sin disparar.
                if (dummy != null && dummy.Brain != null) dummy.Brain.Stance = CombatStance.AltoElFuego;
            }
            if (objPared != null) marcaPared = objPared.GetComponent<ObstacleMarker>();
            if (objDestruible != null) marcaDestruible = objDestruible.GetComponent<ObstacleMarker>();
            soldadoInicial = driver != null && driver.Brain != null ? driver.Brain.Current : null;

            // Los aliados no le disparan al enemigo de practica (lo tiene que derribar el jugador).
            PostureDeAliados(CombatStance.AltoElFuego);

            suscripciones.Add(EventBus.Instance.Subscribe<EntityDiedEvent>(AlMorir));
            suscripciones.Add(EventBus.Instance.Subscribe<MoveOrderIssuedEvent>(AlOrdenDeMover));
            suscripciones.Add(EventBus.Instance.Subscribe<ShotFiredEvent>(AlDisparar));
            ObstacleMarker.Golpeado += AlGolpearObstaculo;
            ObstacleMarker.Derrumbado += AlDerrumbarObstaculo;
            if (driver != null) driver.OrdenRadialEjecutada += AlOrdenRadial;

            DefinirPasos();
            TutorialLog.Escribir("[INICIO]", $"Tutorial listo: {pasos.Count} pasos. Escena {SceneManager.GetActiveScene().name}");
            EmpezarPaso(0);
        }

        void PostureDeAliados(CombatStance postura)
        {
            if (driver == null || driver.Squad == null) return;
            foreach (var s in driver.Squad)
                if (s != null && s.Brain != null) s.Brain.Stance = postura;
        }

        void Desuscribir()
        {
            foreach (var s in suscripciones) s?.Dispose();
            suscripciones.Clear();
            ObstacleMarker.Golpeado -= AlGolpearObstaculo;
            ObstacleMarker.Derrumbado -= AlDerrumbarObstaculo;
            if (driver != null) driver.OrdenRadialEjecutada -= AlOrdenRadial;
        }

        // Cada orden del radial que sale bien avisa (categoria, opcion): marca las banderas de los pasos.
        void AlOrdenRadial(int cat, int sub)
        {
            var p = PasoActual; if (p == null) return;
            TutorialLog.Escribir("[EVENTO]", $"   radial: categoria {cat + 1} ({SP.UI.MenuDeOrdenes.Porciones[cat].Replace("\n", " ")}) opcion {sub + 1} ({SP.UI.MenuDeOrdenes.NombreDeOpcion(cat, sub)})");
            var f = Flags;
            switch (p.Id)
            {
                case "seguir":
                    if (cat == 3 && sub == 1) f.ordenDeSeguir = true;
                    if (cat == 3 && sub == 0) f.ordenDeQuietos = true;
                    break;
                case "cubrirse": if (cat == 1) f.ordenDeCubrirse = true; break;
                case "curar": if (cat == 4 && sub != 3) f.ordenDeCurar = true; break;
                case "reanimar": if (cat == 4 && sub == 3) f.ordenDeReanimar = true; break;
                case "aliados_tanque": if (cat == 5 && sub == 0) f.ordenDeSubir = true; break;
            }
        }

        static Transform Buscar(string nombre)
        {
            var g = GameObject.Find(nombre);
            return g != null ? g.transform : null;
        }

        // ===============================================================
        // Definicion de los 26 pasos
        // ===============================================================
        Sub S(string texto, string mensaje, string pista, Func<bool> leer, Action<bool> poner, Func<string> vivo = null)
            => new Sub { Texto = texto, Mensaje = mensaje, Pista = pista, Leer = leer, Poner = poner, TextoVivo = vivo };

        void DefinirPasos()
        {
            var f = Flags;
            var cian = new Color(0.30f, 0.85f, 1f);
            var verde = new Color(0.40f, 0.92f, 0.50f);
            var naranja = new Color(1f, 0.66f, 0.25f);
            var dorado = new Color(1f, 0.86f, 0.30f);

            // 1 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "camara", Titulo = "MOVER LA CÁMARA", Teclas = "MOUSE", Acento = cian,
                Subs = new[]
                {
                    S("Mira a los costados (izquierda y derecha)", "Mueve el MOUSE hacia los costados para girar la cámara.", "Si la cámara no gira, haz clic dentro de la ventana del juego.", () => f.camaraHorizontal, v => f.camaraHorizontal = v),
                    S("Mira arriba y abajo", "Ahora mueve el MOUSE hacia arriba y hacia abajo.", "Sube y baja el mouse: la mira sigue tu mano.", () => f.camaraVertical, v => f.camaraVertical = v),
                },
                AlEntrar = () => { ultimoYaw = Yaw(); ultimoPitch = driver.Rig.Pitch; acumYaw = acumPitch = 0f; },
                Evaluar = () =>
                {
                    float y = Yaw(), p = driver.Rig.Pitch;
                    acumYaw += Mathf.Abs(Mathf.DeltaAngle(ultimoYaw, y));
                    acumPitch += Mathf.Abs(p - ultimoPitch);
                    ultimoYaw = y; ultimoPitch = p;
                    if (acumYaw >= 50f) f.camaraHorizontal = true;
                    if (acumPitch >= 18f) f.camaraVertical = true;
                },
            });

            // 2 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "wasd", Titulo = "MOVERSE CON W A S D", Teclas = "W A S D", Acento = cian, OcultarSubs = true,
                Subs = new[]
                {
                    S("W · adelante", null, null, () => f.teclaW, v => f.teclaW = v),
                    S("A · izquierda", null, null, () => f.teclaA, v => f.teclaA = v),
                    S("S · atrás", null, null, () => f.teclaS, v => f.teclaS = v),
                    S("D · derecha", null, null, () => f.teclaD, v => f.teclaD = v),
                },
                MensajeVivo = () =>
                {
                    var faltan = new List<string>();
                    if (!f.teclaW) faltan.Add("W"); if (!f.teclaA) faltan.Add("A"); if (!f.teclaS) faltan.Add("S"); if (!f.teclaD) faltan.Add("D");
                    return "Aprieta cada tecla y camina. Cada una se ilumina al apretarla.\nFaltan: " + string.Join("  ", faltan);
                },
                Evaluar = () =>
                {
                    var kb = Keyboard.current; if (kb == null) return;
                    if (kb.wKey.isPressed) f.teclaW = true;
                    if (kb.aKey.isPressed) f.teclaA = true;
                    if (kb.sKey.isPressed) f.teclaS = true;
                    if (kb.dKey.isPressed) f.teclaD = true;
                },
            });

            // 2b ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "correr", Titulo = "CORRER CON SHIFT", Teclas = "Shift W", Acento = verde,
                Subs = new[]
                {
                    S("Mantén SHIFT y camina adelante (W)", "Mantén SHIFT mientras caminas hacia adelante con W: corres, las piernas se aceleran y tus aliados corren contigo.", "SHIFT izquierdo y W a la vez. No sirve agachado ni apuntando.", () => f.corre, v => f.corre = v),
                    S("Suelta SHIFT: vuelves a caminar", "Suelta SHIFT: vuelves al paso normal.", "Deja de apretar SHIFT sin dejar de caminar.", () => f.caminaDeNuevo, v => f.caminaDeNuevo = v),
                },
                AlEntrar = () => { tCorriendo = 0f; },
                Evaluar = () =>
                {
                    var c = driver.Brain.Current; if (c == null) return;
                    if (c.Motor.Corriendo) { tCorriendo += Time.deltaTime; if (tCorriendo >= 1.2f) f.corre = true; }
                    else if (f.corre) f.caminaDeNuevo = true;
                },
            });

            // 3 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "disparar", Titulo = "DISPARAR", Teclas = "LMB R", Acento = naranja,
                Subs = new[]
                {
                    S("Elimina al enemigo quieto (rojo)", "Apunta la mira al ENEMIGO ROJO y dispara con el CLIC IZQUIERDO (mantenlo para ráfaga).", "Lleva el punto de la mira sobre el soldado. Si no hay balas, recarga con R.", () => f.disparoEnemigo, v => f.disparoEnemigo = v),
                    S("Dispara a la pared gris", "Ahora dispara a la PARED GRIS: la bala levanta cubitos amarillos y no la rompe.", "La pared gris es indestructible: solo comprueba el impacto.", () => f.disparoPared, v => f.disparoPared = v),
                    S("Destruye la caja amarilla", "Dispara a la CAJA AMARILLA hasta destruirla: cada impacto la daña.", "Sigue disparando a la caja hasta que se derrumbe.", () => f.disparoDestruible, v => f.disparoDestruible = v),
                },
                AlEntrar = () =>
                {
                    if (objDummy != null) balizaDummy = TutorialBeacon.Crear("ENEMIGO QUIETO", new Color(1f, 0.3f, 0.25f), objDummy.position, objDummy, 1.2f, 10f);
                    if (objPared != null) balizaPared = TutorialBeacon.Crear("PARED (no se rompe)", new Color(0.75f, 0.78f, 0.85f), objPared.position, null, 1.4f, 8f);
                    if (objDestruible != null) balizaDestruible = TutorialBeacon.Crear("CAJA DESTRUCTIBLE", new Color(1f, 0.86f, 0.15f), objDestruible.position, null, 1.4f, 8f);
                },
                Evaluar = () =>
                {
                    if (f.disparoEnemigo && balizaDummy != null) { balizaDummy.Quitar(); balizaDummy = null; }
                    if (f.disparoPared && balizaPared != null) { balizaPared.Quitar(); balizaPared = null; }
                    if (f.disparoDestruible && balizaDestruible != null) { balizaDestruible.Quitar(); balizaDestruible = null; }
                    if (dummy != null && !dummy.Health.IsAlive) f.disparoEnemigo = true;
                },
                AlSalir = QuitarBalizasDeDisparo,
            });
            // 4 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "cambiar", Titulo = "CAMBIAR DE SOLDADO (RADIAL)", Teclas = "Q", Acento = verde,
                Subs = new[]
                {
                    S("Mantén Q: se abre el radial de órdenes", "Mantén apretada la tecla [Q]: se abre el RADIAL. Siempre ofrece IR ALLÍ, CUBRIRSE y POSICIÓN; lo demás aparece en DORADO solo si apuntas a algo con lo que se puede interactuar.", "Mantén Q sin soltarla (un toque corto solo cicla de soldado).", () => f.radialAbierto, v => f.radialAbierto = v),
                    S("Apunta a un aliado → Q → POSEER (dorado)", "MIRA a un aliado (columna celeste) y mantén [Q]: arriba aparece POSEER en dorado. Mueve el mouse hacia ARRIBA (POSEER), sigue hacia AFUERA hasta POSEER A ESTE y suelta Q: tomas su control.", "Primero deja al aliado en el centro de la mira; recién después mantén Q (la mira se congela al abrirse).", () => f.cambioDeSoldado, v => f.cambioDeSoldado = v),
                },
                AlEntrar = () =>
                {
                    soldadoInicial = driver.Brain.Current;
                    foreach (var s in driver.Squad)
                        if (s != null && s != soldadoInicial)
                            balizasAliados.Add(TutorialBeacon.Crear(s.DisplayName.ToUpperInvariant() + " · aliado", cian, s.transform.position, s.transform, 1f, 12f));
                },
                Evaluar = () =>
                {
                    if (driver.RadialAbierto) f.radialAbierto = true;
                    if (driver.Brain.Current != soldadoInicial) { f.cambioDeSoldado = true; f.radialAbierto = true; }
                },
                AlSalir = QuitarBalizasDeAliados,
            });


            // 5 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "agacharse", Titulo = "AGACHARSE", Teclas = "Ctrl", Acento = verde,
                Subs = new[]
                {
                    S("Mantén CTRL: te agachas", "Mantén apretada la tecla CTRL: tu soldado se agacha (menos visible y más preciso).", "Es la tecla Ctrl de la izquierda del teclado.", () => f.agachado, v => f.agachado = v),
                    S("Suelta CTRL: te levantas", "Suelta CTRL para volver a ponerte de pie.", "Basta con dejar de apretar Ctrl.", () => f.levantado, v => f.levantado = v),
                },
                Evaluar = () =>
                {
                    var c = driver.Brain.Current;
                    if (c == null) return;
                    if (c.Motor.IsCrouching) f.agachado = true;
                    else if (f.agachado) f.levantado = true;
                },
            });

            // 6 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "mira", Titulo = "APUNTAR: MIRA EN PRIMERA PERSONA", Teclas = "RMB LMB", Acento = naranja,
                Subs = new[]
                {
                    S("Mantén el CLIC DERECHO: apuntas", "Mantén apretado el CLIC DERECHO: apuntas con la mira del arma y la cámara hace zoom.", "Mantén el botón derecho del mouse apretado (no lo sueltes todavía).", () => f.apuntaConZoom, v => f.apuntaConZoom = v),
                    S("La cámara pasa a los OJOS del soldado", "Sin soltar: la cámara viaja de encima del hombro a los ojos del soldado (primera persona). Fíjate cómo la mira respira.", "Sigue manteniendo el clic derecho un segundo más.", () => f.apuntaEnPrimeraPersona, v => f.apuntaEnPrimeraPersona = v),
                    S("Dispara mientras apuntas", "Sin soltar el clic derecho, dispara con el CLIC IZQUIERDO.", "Con el derecho apretado, clic izquierdo para disparar.", () => f.disparaConZoom, v => f.disparaConZoom = v),
                    S("Suelta: vuelve el encuadre normal", "Suelta el clic derecho: la cámara vuelve a verte por encima del hombro.", "Basta con soltar el botón derecho.", () => f.sueltaLaMira, v => f.sueltaLaMira = v),
                },
                Evaluar = () =>
                {
                    if (driver.Rig.EstaConZoom) f.apuntaConZoom = true;
                    if (driver.Rig.AdsBlendSuave > 0.9f) f.apuntaEnPrimeraPersona = true;
                    if (f.disparaConZoom && !driver.Rig.EstaConZoom && driver.Rig.AdsBlend < 0.1f) f.sueltaLaMira = true;
                },
            });

            // 6c ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "vista_tactica", Titulo = "VER COBERTURAS Y RUTAS", Teclas = "C", Acento = cian,
                Subs = new[]
                {
                    S("Mantén C: aparecen las coberturas", "Mantén apretada la tecla [C]: los puntos de cobertura del piso (discos celestes) y las rutas de patrulla enemigas se hacen visibles. Soltando la tecla se esconden.", "La tecla C está entre la X y la V.", () => f.verCoberturas, v => f.verCoberturas = v),
                    S("Suelta C: se esconden", "Suelta la tecla [C]: el mapa vuelve a quedar limpio. (Con el radial sobre CUBRIRSE también se ven.)", "Deja de apretar C.", () => f.ocultaCoberturas, v => f.ocultaCoberturas = v),
                },
                Evaluar = () =>
                {
                    if (Coberturas.MarcasVisibles) f.verCoberturas = true;
                    else if (f.verCoberturas) f.ocultaCoberturas = true;
                },
            });

            // 7 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "rts", Titulo = "RTS: ORDEN A LOS 2", Teclas = "Tab LMB RMB", Acento = dorado,
                Subs = new[]
                {
                    S("TAB: pasa a la vista táctica (RTS)", "Presiona [TAB]: la cámara sube y ves el mapa desde arriba.", "TAB está encima de Bloq Mayús.", () => f.rtsActivo, v => f.rtsActivo = v),
                    S("Selecciona a los 2 aliados", "Arrastra un recuadro con el CLIC IZQUIERDO alrededor de tus 2 aliados (columnas celestes), o Ctrl+A. Rueda del mouse: zoom hacia donde apuntas con el cursor.", "Mantén el clic izquierdo y arrastra un recuadro que los incluya a los dos.", () => f.aliadosSeleccionados, v => f.aliadosSeleccionados = v,
                      () => $"Selecciona a los 2 aliados ({Seleccionados()}/{aliados.Count})"),
                    S("CLIC DERECHO en el círculo verde: van ahí", "Clic DERECHO sobre el CÍRCULO VERDE: los 2 aliados caminan hasta allí. (Rueda del mouse: zoom hacia el cursor.)", "Apunta al círculo verde y clic derecho. Cerca del círculo alcanza.", () => f.ordenDeMoverARts, v => f.ordenDeMoverARts = v,
                      () => $"Clic derecho en el círculo verde ({ordenadosARts.Count}/{aliados.Count})"),
                },
                AlEntrar = () =>
                {
                    ArmarAliados();
                    ordenadosARts.Clear();
                    rtsAjustado = false;
                    foreach (var a in aliados)
                        balizasAliados.Add(TutorialBeacon.Crear(a.DisplayName.ToUpperInvariant(), cian, a.transform.position, a.transform, 1.6f, 14f));
                    if (zonaA != null) balizaZonaA = TutorialBeacon.Crear("ZONA A · clic derecho", verde, zonaA.position, null, 2.6f, 10f);
                },
                Evaluar = () =>
                {
                    if (driver.Rig.Mode == ControlMode.Rts) f.rtsActivo = true;
                    if (f.rtsActivo && !rtsAjustado)
                    {
                        // La vista tactica arranca cerrada: se aleja y se centra en la escuadra para que se vean los 2 aliados.
                        rtsAjustado = true;
                        var centro = Vector3.zero;
                        foreach (var a in aliados) centro += a.transform.position;
                        if (aliados.Count > 0) centro /= aliados.Count;
                        driver.Rig.Zoom(-60f);
                        driver.Rig.RecenterOn(centro);
                    }
                    if (f.rtsActivo && Seleccionados() >= aliados.Count && aliados.Count > 0) f.aliadosSeleccionados = true;
                    if (aliados.Count > 0 && ordenadosARts.Count >= aliados.Count) f.ordenDeMoverARts = true;
                },
                AlSalir = () => { if (balizaZonaA != null) { balizaZonaA.Quitar(); balizaZonaA = null; } QuitarBalizasDeAliados(); },
            });

            // 8 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "fps", Titulo = "VOLVER A PRIMERA PERSONA", Teclas = "Tab", Acento = dorado,
                Subs = new[]
                {
                    S("TAB: vuelves a primera persona (FPS)", "Presiona [TAB] otra vez para volver a mirar por los ojos de tu soldado.", "TAB alterna entre la vista táctica y la primera persona.", () => f.vueltaAFps, v => f.vueltaAFps = v),
                    S("Clic izquierdo: el mouse vuelve a la cámara", "Haz un CLIC IZQUIERDO en la pantalla: el mouse vuelve a controlar la cámara.", "Hace falta un clic para capturar el mouse otra vez.", () => f.mouseCapturado, v => f.mouseCapturado = v),
                },
                Evaluar = () =>
                {
                    if (driver.Rig.Mode == ControlMode.Fps && !driver.Rig.IsTransitioning) f.vueltaAFps = true;
                    if (f.vueltaAFps && Cursor.lockState == CursorLockMode.Locked) f.mouseCapturado = true;
                },
            });
            // 9 -------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "seguir", Titulo = "SÍGANME Y QUIETOS (RADIAL)", Teclas = "Q", Acento = verde,
                Subs = new[]
                {
                    S("Q → POSICIÓN → SÍGANME", "Mantén [Q], elige POSICIÓN (siempre está) y sigue hacia AFUERA hasta SÍGANME (la pista celeste late). Suelta Q: los 2 aliados vienen hacia ti.", "POSICIÓN; SÍGANME es la segunda opción del anillo de afuera.", () => f.ordenDeSeguir, v => f.ordenDeSeguir = v),
                    S("Los 2 aliados te siguen", "Mira cómo vienen: cada aliado que te sigue lo marca el ícono verde.", "Espera unos segundos: caminan hacia ti.", () => f.aliadosSiguen, v => f.aliadosSiguen = v,
                      () => $"Los 2 aliados te siguen ({Siguiendo()}/{aliados.Count})"),
                    S("Q → POSICIÓN → TODOS QUIETOS", "Ahora que se queden: [Q], POSICIÓN y TODOS QUIETOS (la primera opción). Se plantan donde están y ya no te siguen.", "Es la primera opción de POSICIÓN.", () => f.ordenDeQuietos, v => f.ordenDeQuietos = v,
                      () => $"Todos quietos ({QuietosContados()}/{aliados.Count})"),
                },
                Evaluar = () =>
                {
                    if (Siguiendo() > 0) f.ordenDeSeguir = true;
                    if (aliados.Count > 0 && Siguiendo() >= aliados.Count) f.aliadosSiguen = true;
                    if (f.aliadosSiguen && aliados.Count > 0 && QuietosContados() >= aliados.Count) f.ordenDeQuietos = true;
                },
            });


            // 10 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "seleccionar", Titulo = "SELECCIONAR EN FPS", Teclas = "Shift + RMB", Acento = dorado,
                Subs = new[]
                {
                    S("SHIFT + CLIC DERECHO sobre un aliado", "Tus aliados se adelantaron. Mira a uno, mantén SHIFT y haz CLIC DERECHO sobre él: queda seleccionado.", "Gira el mouse hasta tener a un aliado en la mira (están unos 9 metros delante).", () => f.aliadoSeleccionadoEnFps, v => f.aliadoSeleccionadoEnFps = v),
                },
                AlEntrar = () =>
                {
                    if (driver.Selection != null) driver.Selection.Clear();
                    aliadoSeleccionadoId = -1;
                    // Los aliados que te siguen quedan pegados a tu espalda y con la camara sobre el hombro la
                    // mira no llega a apuntarlos: se adelantan 9 m para poder mirarlos.
                    var yo = driver.Brain.Current;
                    if (yo != null && driver.Rig.Cam != null)
                    {
                        var frente = Vector3.ProjectOnPlane(driver.Rig.Cam.transform.forward, Vector3.up).normalized;
                        var lado = Vector3.Cross(Vector3.up, frente);
                        int k = 0;
                        foreach (var a in aliados)
                            if (a != null && a.Health.IsAlive)
                                OrderService.IssueMoveOrder(a, yo.transform.position + frente * 9f + lado * (k++ % 2 == 0 ? -3f : 3f));
                    }
                },
                Evaluar = () =>
                {
                    if (driver.Selection == null) return;
                    foreach (var s in driver.Selection.Selected)
                        if (s != null && aliados.Contains(s)) { f.aliadoSeleccionadoEnFps = true; aliadoSeleccionadoId = s.Id; }
                },
            });

            // 11 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "mover_fps", Titulo = "ORDEN DE MOVER EN FPS", Teclas = "RMB", Acento = dorado,
                Subs = new[]
                {
                    S("CLIC DERECHO en el suelo: el aliado va", "Con el aliado seleccionado, haz CLIC DERECHO en el SUELO: caminará hasta allí.", "Apunta al piso (no a una pared) y clic derecho.", () => f.ordenDeMoverEnFps, v => f.ordenDeMoverEnFps = v),
                },
                AlEntrar = () =>
                {
                    if (zonaB != null) balizaZonaB = TutorialBeacon.Crear("ORDEN AQUÍ", new Color(0.4f, 0.92f, 0.5f), zonaB.position, null, 1.6f, 8f);
                },
                Evaluar = () => { },   // lo marca AlOrdenDeMover
                AlSalir = () => { if (balizaZonaB != null) { balizaZonaB.Quitar(); balizaZonaB = null; } },
            });
            // 12 (nuevo) -----------------------------------------------
            pasos.Add(new Paso
            {
                Id = "cubrirse", Titulo = "CUBRIRSE HACIA DONDE MIRO (RADIAL)", Teclas = "Q", Acento = naranja,
                Subs = new[]
                {
                    S("Q → CUBRIRSE → TODOS", "Mira hacia las 2 coberturas de sacos (columnas naranjas). Mantén [Q], elige CUBRIRSE (los discos celestes de cobertura se ven mientras el radial está ahí) y sigue hasta TODOS: se ponen del lado protegido, lejos de donde miras.", "CUBRIRSE; TODOS es la primera opción. Mira antes hacia los sacos.", () => f.ordenDeCubrirse, v => f.ordenDeCubrirse = v),
                    S("Los 2 aliados quedan en cobertura", "Espera: caminan, se agachan detrás de los sacos y quedan a cubierto.", "Tardan unos segundos.", () => f.aliadosEnCobertura, v => f.aliadosEnCobertura = v,
                      () => $"Los 2 aliados en cobertura ({EnCobertura()}/{aliados.Count})"),
                },
                AlEntrar = () =>
                {
                    ArmarAliados();
                    CrearCoberturasDePractica();
                },
                Evaluar = () =>
                {
                    if (EnCobertura() > 0 || CubriendoseEnCamino() > 0) f.ordenDeCubrirse = true;
                    if (aliados.Count > 0 && EnCobertura() >= aliados.Count) f.aliadosEnCobertura = true;
                },
                AlSalir = QuitarCoberturasDePractica,
            });

            // 13 (nuevo) -----------------------------------------------
            pasos.Add(new Paso
            {
                Id = "curar", Titulo = "CURAR A UN ALIADO (RADIAL)", Teclas = "Q", Acento = verde,
                Subs = new[]
                {
                    S("Apunta al herido → Q → CURAR (dorado)", "Un aliado está herido (columna roja, mira su barra abajo a la izquierda). MÍRALO y mantén [Q]: CURAR aparece en dorado arriba. Sigue hasta CURAR A ESTE: el médico va y lo atiende. (Solo aparece si apuntas a un herido y el médico vive.)", "Deja al herido en la mira antes de mantener Q.", () => f.ordenDeCurar, v => f.ordenDeCurar = v),
                    S("El médico cura al herido", "Espera: el médico llega junto al herido y le devuelve la vida.", "El médico tiene que llegar a menos de 2,5 m.", () => f.aliadoCurado, v => f.aliadoCurado = v,
                      () => $"El herido recupera vida ({VidaDelHerido()}%)"),
                },
                AlEntrar = () =>
                {
                    AsegurarComoAsalto();
                    ArmarAliados();
                    PedidoDeCuracion.AtencionAutomatica = false;   // que lo pida el jugador
                    herido = null;
                    foreach (var a in aliados) if (a != null && a.Role != RoleType.Medic) { herido = a; break; }
                    if (herido == null && aliados.Count > 0) herido = aliados[0];
                    if (herido != null) herido.Health.TakeDamage(Mathf.RoundToInt(herido.Health.MaxHealth * 0.78f), -1);
                    if (herido != null) balizasAliados.Add(TutorialBeacon.Crear(herido.DisplayName.ToUpperInvariant() + " · HERIDO", new Color(1f, 0.4f, 0.4f), herido.transform.position, herido.transform, 1f, 10f));
                },
                Evaluar = () =>
                {
                    if (PedidoDeCuracion.Activo) f.ordenDeCurar = true;
                    if (herido != null && herido.Health.Current >= herido.Health.MaxHealth * 0.9f) { f.aliadoCurado = true; f.ordenDeCurar = true; }
                },
                AlSalir = () => { PedidoDeCuracion.AtencionAutomatica = true; QuitarBalizasDeAliados(); },
            });

            // 13b (nuevo) ----------------------------------------------
            pasos.Add(new Paso
            {
                Id = "reanimar", Titulo = "REANIMAR A UN CAÍDO (RADIAL)", Teclas = "Q", Acento = verde,
                Subs = new[]
                {
                    S("Apunta al caído → Q → REANIMAR (dorado)", "Un aliado CAYÓ (columna roja). MÍRALO y mantén [Q]: REANIMAR aparece en dorado (solo si queda un médico vivo). Elige REANIMAR A ESTE.", "Deja al caído en la mira antes de mantener Q.", () => f.ordenDeReanimar, v => f.ordenDeReanimar = v),
                    S("El médico lo levanta (4 s junto a él)", "Espera: el médico corre hasta el caído, se queda a su lado 4 segundos y lo reanima.", "El médico tiene que llegar a menos de 2,5 m.", () => f.aliadoReanimado, v => f.aliadoReanimado = v),
                },
                AlEntrar = () =>
                {
                    ArmarAliados();
                    PedidoDeCuracion.AtencionAutomatica = false;
                    PedidoDeCuracion.Cancelar();
                    if (herido == null || herido.Role == RoleType.Medic)
                    {
                        herido = null;
                        foreach (var a in aliados) if (a != null && a.Role != RoleType.Medic) { herido = a; break; }
                    }
                    if (herido != null)
                    {
                        if (herido.Brain != null) herido.Brain.CancelOrder();
                        herido.Health.TakeDamage(herido.Health.MaxHealth * 3, -1);
                        balizasAliados.Add(TutorialBeacon.Crear(herido.DisplayName.ToUpperInvariant() + " · CAIDO", new Color(1f, 0.4f, 0.4f), herido.transform.position, herido.transform, 1f, 10f));
                    }
                },
                Evaluar = () =>
                {
                    if (PedidoDeCuracion.Reanimando) f.ordenDeReanimar = true;
                    if (herido != null && herido.Health.IsAlive) { f.aliadoReanimado = true; f.ordenDeReanimar = true; }
                },
                AlSalir = () => { PedidoDeCuracion.AtencionAutomatica = true; QuitarBalizasDeAliados(); },
            });

            // 14 (nuevo) -----------------------------------------------
            pasos.Add(new Paso
            {
                Id = "demoler", Titulo = "DEMOLER UN MURO (ASALTO)", Teclas = "Ctrl", Acento = naranja,
                Subs = new[]
                {
                    S("Apunta al muro de práctica (columna naranja)", "Ahora eres el soldado de ASALTO: apunta al muro de práctica (a unos 7 m). Al apuntarlo, el radial [Q] ofrece DEMOLER en dorado. Solo el asalto puede demoler.", "Mira al bloque marcado con la columna naranja.", () => f.apuntaAlMuro, v => f.apuntaAlMuro = v),
                    S("CTRL agachado y quieto: carga 4 s", "Mantén CTRL (agachado) y NO te muevas: aparece un anillo que se llena en 4 segundos.", "Si te levantas o te mueves, la carga se cancela.", () => f.cargandoDemolicion, v => f.cargandoDemolicion = v),
                    S("El muro vuela en pedazos", "Sigue quieto hasta que el anillo se llene: la carga explota y el muro desaparece.", "Tarda 4 segundos seguidos, quieto y agachado.", () => f.muroDemolido, v => f.muroDemolido = v),
                },
                AlEntrar = () =>
                {
                    AsegurarComoAsalto();
                    CrearMuroDePractica();
                },
                Evaluar = () =>
                {
                    var a = driver.UltimaMira;
                    if (muroPractica != null && a.Type == AimTargetType.Obstacle && Demolicion.MarcadorApuntado(a.HitTransform, a.Point) == muroPractica) f.apuntaAlMuro = true;
                    var dem = driver.Brain.Current != null ? driver.Brain.Current.GetComponent<DemoledorAsalto>() : null;
                    if (dem != null && dem.Progreso > 0.05f) { f.cargandoDemolicion = true; f.apuntaAlMuro = true; }
                    if (muroPractica == null || muroPractica.IsCollapsed) { f.muroDemolido = true; f.cargandoDemolicion = true; f.apuntaAlMuro = true; }
                },
                AlSalir = () => { if (balizaMuro != null) { balizaMuro.Quitar(); balizaMuro = null; } },
            });


            // 14b (nuevo) ----------------------------------------------
            pasos.Add(new Paso
            {
                Id = "torreta_fija", Titulo = "AMETRALLADORA FIJA", Teclas = "E", Acento = naranja,
                Subs = new[]
                {
                    S("Acércate a la torreta amarilla (4 m)", "Camina hasta la AMETRALLADORA FIJA marcada con la columna naranja. Es fija: no se lleva.", "Está unos 9 m delante de ti.", () => f.cercaDeLaTorreta, v => f.cercaDeLaTorreta = v,
                      () => $"Acércate a la torreta ({DistanciaATorreta():0} m)"),
                    S("E: la ocupas (o Q → TORRETA → USAR)", "Mírala y aprieta [E] (o mantén [Q] y elige TORRETA FIJA en dorado): te plantas detrás del arma. Solo giras dentro de un arco y no puedes caminar.", "Apunta a la torreta: el cartel dorado dice [E] USAR LA AMETRALLADORA FIJA.", () => f.enTorretaFija, v => f.enTorretaFija = v),
                    S("Dispara una ráfaga", "Dispara con el CLIC IZQUIERDO: la cinta tiene 100 balas. Con el CLIC DERECHO mantenido miras por la mira.", "Clic izquierdo mantenido.", () => f.disparoTorretaFija, v => f.disparoTorretaFija = v),
                    S("E: sales de la torreta", "Aprieta [E] otra vez para dejar la torreta (vuelves a tu arma y a tu lugar).", "Tecla E.", () => f.salioDeLaTorreta, v => f.salioDeLaTorreta = v),
                },
                AlEntrar = () =>
                {
                    AsegurarComoAsalto();
                    CrearTorretaDePractica();
                },
                Evaluar = () =>
                {
                    if (torretaPractica != null)
                    {
                        if (DistanciaATorreta() <= TorretaFija.AlcanceDeUso) f.cercaDeLaTorreta = true;
                        if (torretaPractica.Ocupante == driver.Brain.Current && driver.Brain.Current != null)
                        {
                            f.enTorretaFija = true; f.cercaDeLaTorreta = true;
                            // Ya adentro, el cartel 3D de la baliza tapa la pantalla: se quita.
                            if (balizaTorreta != null) { balizaTorreta.Quitar(); balizaTorreta = null; }
                        }
                        if (f.enTorretaFija && torretaPractica.Ocupante == null && !driver.EnTorretaFija) f.salioDeLaTorreta = true;
                    }
                },
                AlSalir = () => { if (driver.EnTorretaFija) driver.SalirDeTorreta(); if (balizaTorreta != null) { balizaTorreta.Quitar(); balizaTorreta = null; } },
            });

            // 14c (nuevo) ----------------------------------------------
            pasos.Add(new Paso
            {
                Id = "modo_dios", Titulo = "MODO DIOS", Teclas = "F4", Acento = dorado,
                Subs = new[]
                {
                    S("F4: activa el modo dios", "Aprieta [F4]: NADIE de tu bando recibe daño (ni tú, ni tus aliados, ni tu tanque). Abajo aparece el cartel dorado. Sirve para probar y para practicar.", "Tecla F4, arriba del teclado.", () => f.modoDiosOn, v => f.modoDiosOn = v),
                    S("F4 otra vez: lo apagas", "Aprieta [F4] de nuevo para apagarlo y volver al daño normal.", "Tecla F4.", () => f.modoDiosOff, v => f.modoDiosOff = v),
                },
                Evaluar = () =>
                {
                    if (ModoDios.Activo) f.modoDiosOn = true;
                    else if (f.modoDiosOn) f.modoDiosOff = true;
                },
                AlSalir = () => ModoDios.Poner(false),
            });

            // 12 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "entrar_tanque", Titulo = "ENTRAR AL TANQUE (RADIAL)", Teclas = "Q", Acento = naranja,
                Subs = new[]
                {
                    S("Camina hasta el tanque (columna naranja)", "Camina hasta el TANQUE marcado con la columna naranja (W A S D).", "Está al norte, al final del camino de tierra.", () => f.cercaDelTanque, v => f.cercaDelTanque = v,
                      () => $"Camina hasta el tanque ({DistanciaAlTanque():0} m)"),
                    S("Q → TANQUE → SUBIRME YO", "Estás cerca. MIRA al tanque: la mira se pone dorada y [E] lo sube. También puedes mantener [Q]: TANQUE aparece en dorado; elige SUBIRME YO.", "Apunta al tanque antes de mantener Q. Hay que estar a menos de 7 m.", () => f.dentroDelTanque, v => f.dentroDelTanque = v),
                },
                AlEntrar = () =>
                {
                    if (driver.Vehicle != null)
                        balizaTanque = TutorialBeacon.Crear("TANQUE", naranja, driver.Vehicle.transform.position, driver.Vehicle.transform, 2.2f, 14f);
                },
                Evaluar = () =>
                {
                    if (driver.Vehicle == null) return;
                    if (DistanciaAlTanque() <= 6.5f) f.cercaDelTanque = true;
                    if (driver.Vehicle.PlayerAboard) { f.dentroDelTanque = true; f.cercaDelTanque = true; }
                },
                AlSalir = () => { if (balizaTanque != null) { balizaTanque.Quitar(); balizaTanque = null; } },
            });

            // 13 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "aliados_tanque", Titulo = "ALIADOS AL TANQUE (RADIAL)", Teclas = "Q", Acento = naranja,
                Subs = new[]
                {
                    S("Q → TANQUE → SUBIR TODOS", "Mantén [Q]: estando en el tanque, TANQUE aparece en dorado. Elige SUBIR TODOS: tus aliados corren al tanque.", "TANQUE (dorado); SUBIR TODOS es la primera opción.", () => f.ordenDeSubir, v => f.ordenDeSubir = v),
                    S("Espera a que suban los 2", "Tus aliados corren al tanque y se sientan. Espera a que estén adentro.", "Si uno está lejos tarda un poco: caminan hasta el tanque.", () => f.aliadosABordo, v => f.aliadosABordo = v,
                      () => $"Los 2 aliados a bordo ({ABordo()}/{aliados.Count})"),
                },
                Evaluar = () =>
                {
                    var v = driver.Vehicle; if (v == null) return;
                    if (aliados.Count == 0) return;
                    bool algunoEnCamino = false;
                    foreach (var a in aliados) if (a != null && (a.Brain.MountTargetVehicle == v || v.RoleOf(a) != null)) algunoEnCamino = true;
                    if (algunoEnCamino) f.ordenDeSubir = true;
                    if (ABordo() >= aliados.Count) f.aliadosABordo = true;
                },
            });

            // 14 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "torreta", Titulo = "CAMBIAR A LA TORRETA", Teclas = "2", Acento = naranja,
                Subs = new[]
                {
                    S("Presiona 2: pasas al cañón", "Presiona [2]: pasas al CAÑÓN. Si el puesto está ocupado, INTERCAMBIAS con quien lo ocupa.", "1 conductor · 2 cañón · 3 metralleta · 4 pasajero.", () => f.enLaTorreta, v => f.enLaTorreta = v),
                },
                Evaluar = () => { if (driver.CurrentSeat == VehicleSeatRole.Gunner) f.enLaTorreta = true; },
            });

            // 14d (nuevo) ----------------------------------------------
            pasos.Add(new Paso
            {
                Id = "mira_tanque", Titulo = "MIRA DEL CAÑÓN Y LA METRALLETA", Teclas = "RMB", Acento = naranja,
                Subs = new[]
                {
                    S("Clic derecho: miras por la mira del cañón", "Con el CLIC DERECHO mantenido la cámara pasa a la MIRA DEL CAÑÓN (primera persona con zoom y retícula de óptica). El cañón gira lento hacia donde apuntas.", "Mantén el botón derecho estando en el asiento del cañón [2].", () => f.miraDelCanon, v => f.miraDelCanon = v),
                    S("[3] la metralleta: clic derecho", "Suelta, aprieta [3] para pasar a la METRALLETA y mantén el CLIC DERECHO: la mira de la metralleta.", "El [3] cambia de asiento; después clic derecho mantenido.", () => f.miraDeMetralleta, v => f.miraDeMetralleta = v),
                    S("Suelta y vuelve al cañón [2]", "Suelta el clic derecho y aprieta [2]: la cámara vuelve a la tercera persona.", "Suelta el botón derecho y aprieta 2.", () => f.sueltaMiraTanque, v => f.sueltaMiraTanque = v),
                },
                Evaluar = () =>
                {
                    bool enCanon = driver.CurrentSeat == VehicleSeatRole.Gunner;
                    bool enMetra = driver.CurrentSeat == VehicleSeatRole.Passenger1;
                    if (enCanon && driver.Rig.EstaConZoom && driver.Rig.AdsBlend > 0.9f) f.miraDelCanon = true;
                    if (f.miraDelCanon && enMetra && driver.Rig.EstaConZoom && driver.Rig.AdsBlend > 0.9f) f.miraDeMetralleta = true;
                    if (f.miraDeMetralleta && enCanon && !driver.Rig.EstaConZoom) f.sueltaMiraTanque = true;
                },
            });

            // 15 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "avanzar_disparar", Titulo = "AVANZAR Y DISPARAR", Teclas = "Q LMB", Acento = new Color(1f, 0.45f, 0.35f),
                Subs = new[]
                {
                    S("Q → TANQUE → TANQUE ALLÍ: avanza", "Mira al SUELO, adelante en el camino. Mantén [Q], elige TANQUE y sigue hasta TANQUE ALLÍ: un aliado conduce hasta ahí.", "Baja la mira hasta ver el suelo delante del tanque. TANQUE ALLÍ es la tercera opción.", () => f.ordenDeAvanzar, v => f.ordenDeAvanzar = v),
                    S("Dispara el cañón (clic izquierdo)", "¡Vienen enemigos! Apunta el cañón con el mouse y dispara con el CLIC IZQUIERDO.", "Mueve el mouse para apuntar el cañón hacia los soldados rojos y haz clic izquierdo.", () => f.disparoCanon, v => f.disparoCanon = v),
                    S("Derriba a los enemigos que se acercan", "Sigue disparando: hay que derribar a 3 enemigos. (La metralleta de un aliado también ayuda.)", "Apunta el cañón a los soldados rojos que se acercan y dispara.", () => f.enemigosEliminados, v => f.enemigosEliminados = v,
                      () => $"Derriba 3 enemigos que se acercan ({Mathf.Min(bajasTotales, BajasNecesarias)}/{BajasNecesarias})"),
                },
                AlEntrar = () =>
                {
                    tanquePosInicio = driver.Vehicle != null ? driver.Vehicle.transform.position : Vector3.zero;
                    // Los aliados a bordo no le roban las bajas al jugador.
                    PostureDeAliados(CombatStance.AltoElFuego);
                    LanzarOlaA();
                    if (driver.Vehicle != null)
                        AlertQueueSafe("¡ENEMIGOS AL FRENTE!");
                },
                Evaluar = () =>
                {
                    var v = driver.Vehicle; if (v == null) return;
                    if ((v.transform.position - tanquePosInicio).sqrMagnitude > 36f) f.ordenDeAvanzar = true;
                    // El disparo del cañon se detecta por su enfriamiento (una vez que sale el proyectil, baja de 1).
                    var canon = Canon();
                    if (canon != null)
                    {
                        float cd = canon.CooldownFraction01;
                        if (driver.CurrentSeat == VehicleSeatRole.Gunner && ultimoCd >= 0.99f && cd < 0.95f) f.disparoCanon = true;
                        ultimoCd = cd;
                    }
                    if (bajasTotales >= BajasNecesarias) f.enemigosEliminados = true;
                    else RellenarOlaA();
                },
            });

            // 16 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "final", Titulo = "LLEGAR AL FINAL", Teclas = "Q", Acento = dorado,
                Subs = new[]
                {
                    S("Llega a la meta dorada", "Sigue mandando el tanque con [Q] → TANQUE → TANQUE ALLÍ hasta la META DORADA, al final del camino.", "Apunta al suelo, cerca de la baliza dorada, y usa el radial.", () => f.llegoAlFinal, v => f.llegoAlFinal = v,
                      () => $"Llega a la meta dorada ({DistanciaAMeta():0} m)"),
                },
                AlEntrar = () =>
                {
                    if (meta != null) balizaMeta = TutorialBeacon.Crear("META", dorado, meta.position, null, 4f, 22f);
                },
                Evaluar = () =>
                {
                    var v = driver.Vehicle; if (v == null || meta == null) return;
                    if (!olaBLanzada && v.transform.position.z > meta.position.z - 34f) LanzarOlaB();
                    if (DistanciaAMeta() <= 9f) f.llegoAlFinal = true;
                },
                AlSalir = () => { if (balizaMeta != null) { balizaMeta.Quitar(); balizaMeta = null; } },
            });

            // 17 ------------------------------------------------------
            pasos.Add(new Paso
            {
                Id = "victoria", Titulo = "¡TUTORIAL COMPLETADO!", Teclas = "", Acento = dorado, OcultarSubs = true,
                Subs = new Sub[0],
                MensajeVivo = () => "Aprendiste a moverte y correr, apuntar en primera persona, dar órdenes con el radial contextual, curar y reanimar, demoler muros, usar la ametralladora fija, el modo dios y el tanque. ¡Buena suerte, comandante!",
                AlEntrar = () =>
                {
                    f.victoria = true;
                    victoria = VictoriaTutorial.Iniciar(this, meta != null ? meta.position : driver.Vehicle.transform.position);
                },
            });
        }

        // ===============================================================
        // Helpers de estado
        // ===============================================================
        float Yaw() => driver.Rig.Cam != null ? driver.Rig.Cam.transform.eulerAngles.y : 0f;

        void ArmarAliados()
        {
            aliados.Clear();
            foreach (var s in driver.Squad)
                if (s != null && s != driver.Brain.Current && s.Health.IsAlive) aliados.Add(s);
        }

        int Seleccionados()
        {
            if (driver.Selection == null) return 0;
            int n = 0;
            foreach (var a in aliados)
                foreach (var s in driver.Selection.Selected) if (s == a) { n++; break; }
            return n;
        }

        int Siguiendo()
        {
            int n = 0;
            foreach (var a in aliados)
                if (a != null && a.Health.IsAlive && a.Brain.State == AiState.Follow) n++;
            return n;
        }

        int ABordo()
        {
            var v = driver.Vehicle; if (v == null) return 0;
            int n = 0;
            foreach (var a in aliados) if (a != null && v.RoleOf(a) != null && !v.IsMountAnimating(a)) n++;
            return n;
        }

        float DistanciaAlTanque()
        {
            if (driver.Vehicle == null || driver.Brain.Current == null) return 999f;
            return Vector3.Distance(driver.Brain.Current.transform.position, driver.Vehicle.transform.position);
        }

        float DistanciaAMeta()
        {
            if (driver.Vehicle == null || meta == null) return 999f;
            var a = driver.Vehicle.transform.position; var b = meta.position;
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        TurretWeapon Canon()
        {
            if (driver == null || driver.Vehicle == null) return null;
            foreach (var t in driver.Vehicle.GetComponentsInChildren<TurretWeapon>(true))
                if (t.name == "TurretPivot") return t;
            return null;
        }

        void QuitarBalizasDeDisparo()
        {
            foreach (var b in new[] { balizaDummy, balizaPared, balizaDestruible }) if (b != null) b.Quitar();
            balizaDummy = balizaPared = balizaDestruible = null;
        }

        void QuitarBalizasDeAliados()
        {
            foreach (var b in balizasAliados) if (b != null) b.Quitar();
            balizasAliados.Clear();
        }

        int QuietosContados()
        {
            int n = 0;
            foreach (var a in aliados) if (a != null && a.Health.IsAlive && a.Brain.Quieto) n++;
            return n;
        }

        int EnCobertura()
        {
            int n = 0;
            foreach (var a in aliados) if (a != null && a.Health.IsAlive && a.Brain.EnCobertura) n++;
            return n;
        }

        int CubriendoseEnCamino()
        {
            int n = 0;
            foreach (var a in aliados) if (a != null && a.Health.IsAlive && a.Brain.YendoACobertura) n++;
            return n;
        }

        int VidaDelHerido() => herido != null ? Mathf.RoundToInt(100f * herido.Health.Current / Mathf.Max(1, herido.Health.MaxHealth)) : 0;

        // El paso de curar y el de demoler los hace el soldado de ASALTO (el jugador vuelve a ser el).
        void AsegurarComoAsalto()
        {
            if (driver.Brain.Current != null && driver.Brain.Current.Role == RoleType.Assault) return;
            foreach (var s in driver.Squad)
                if (s != null && s.Role == RoleType.Assault && s.Health.IsAlive) { driver.TryPossess(s); break; }
        }

        // Dos coberturas de sacos a 14 m adelante (para el paso de cubrirse).
        readonly List<GameObject> coberturasPractica = new List<GameObject>();
        readonly List<TutorialBeacon> balizasCobertura = new List<TutorialBeacon>();
        void CrearCoberturasDePractica()
        {
            QuitarCoberturasDePractica();
            var yo = driver.Brain.Current; if (yo == null || driver.Rig.Cam == null) return;
            var frente = Vector3.ProjectOnPlane(driver.Rig.Cam.transform.forward, Vector3.up).normalized;
            var lado = Vector3.Cross(Vector3.up, frente);
            for (int i = 0; i < 2; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Tut_Cobertura_" + (i + 1);
                c.transform.position = yo.transform.position + frente * 14f + lado * (i == 0 ? -4f : 4f);
                c.transform.position = new Vector3(c.transform.position.x, 0.6f, c.transform.position.z);
                c.transform.localScale = new Vector3(3f, 1.2f, 1f);
                c.transform.rotation = Quaternion.LookRotation(frente);
                c.GetComponent<MeshRenderer>().sharedMaterial = SP.Presentation.SafeMaterial.Create(new Color(0.78f, 0.66f, 0.42f));
                c.AddComponent<ObstacleMarker>();
                coberturasPractica.Add(c);
                balizasCobertura.Add(TutorialBeacon.Crear("COBERTURA", naranja_, c.transform.position, null, 1.2f, 6f));
            }
            SP.Core.Coberturas.Registrar();
        }

        void QuitarCoberturasDePractica()
        {
            foreach (var c in coberturasPractica) if (c != null) Destroy(c);
            coberturasPractica.Clear();
            foreach (var b in balizasCobertura) if (b != null) b.Quitar();
            balizasCobertura.Clear();
            if (Application.isPlaying) SP.Core.Coberturas.Registrar();
        }

        static readonly Color naranja_ = new Color(1f, 0.66f, 0.25f);
        float tCorriendo;

        // Una ametralladora fija de practica 9 m adelante: base baja, tripode y canon.
        TorretaFija torretaPractica;
        TutorialBeacon balizaTorreta;
        float DistanciaATorreta()
        {
            if (torretaPractica == null || driver.Brain.Current == null) return 999f;
            var v = torretaPractica.transform.position - driver.Brain.Current.transform.position; v.y = 0f;
            return v.magnitude;
        }

        void CrearTorretaDePractica()
        {
            if (torretaPractica != null) return;
            var yo = driver.Brain.Current; if (yo == null || driver.Rig.Cam == null) return;
            var frente = Vector3.ProjectOnPlane(driver.Rig.Cam.transform.forward, Vector3.up).normalized;
            var raiz = new GameObject("Tut_TorretaFija");
            var pos = yo.transform.position + frente * 9f;
            raiz.transform.position = new Vector3(pos.x, 0.5f, pos.z);
            raiz.transform.rotation = Quaternion.LookRotation(frente);
            var gris = SP.Presentation.SafeMaterial.Create(new Color(0.34f, 0.36f, 0.34f));
            var negro = SP.Presentation.SafeMaterial.Create(new Color(0.08f, 0.09f, 0.1f));

            var baseGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGO.name = "Base"; Destroy(baseGO.GetComponent<Collider>());
            baseGO.transform.SetParent(raiz.transform, false);
            baseGO.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            baseGO.transform.localScale = new Vector3(2.6f, 0.5f, 2.6f);
            baseGO.GetComponent<MeshRenderer>().sharedMaterial = gris;

            var tripode = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tripode.name = "Tripode"; Destroy(tripode.GetComponent<Collider>());
            tripode.transform.SetParent(raiz.transform, false);
            tripode.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            tripode.transform.localScale = new Vector3(0.18f, 0.35f, 0.18f);
            tripode.GetComponent<MeshRenderer>().sharedMaterial = negro;

            var canon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            canon.name = "Canon"; Destroy(canon.GetComponent<Collider>());
            canon.transform.SetParent(raiz.transform, false);
            canon.transform.localPosition = new Vector3(0f, 0.75f, 0.55f);
            canon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            canon.transform.localScale = new Vector3(0.12f, 0.6f, 0.12f);
            canon.GetComponent<MeshRenderer>().sharedMaterial = negro;

            torretaPractica = TorretaFija.Instalar(raiz);
            balizaTorreta = TutorialBeacon.Crear("AMETRALLADORA FIJA", naranja_, raiz.transform.position, null, 1.6f, 10f);
        }

        // Categoria/opcion del radial que el paso actual pide elegir: se dibuja latiendo en celeste.
        void AplicarPistaDeRadial(Paso p)
        {
            int cat = -1, opc = -1;
            var f = Flags;
            switch (p.Id)
            {
                case "cambiar": cat = SP.UI.MenuDeOrdenes.Poseer; opc = 4; break;
                case "seguir": cat = SP.UI.MenuDeOrdenes.Posicion; opc = f.ordenDeSeguir ? 0 : 1; break;
                case "cubrirse": cat = SP.UI.MenuDeOrdenes.Cubrirse; opc = 0; break;
                case "curar": cat = SP.UI.MenuDeOrdenes.Curar; opc = 2; break;
                case "reanimar": cat = SP.UI.MenuDeOrdenes.Curar; opc = 3; break;
                case "demoler": cat = SP.UI.MenuDeOrdenes.Demoler; opc = 1; break;
                case "torreta_fija": cat = SP.UI.MenuDeOrdenes.Torreta; opc = 0; break;
                case "entrar_tanque": cat = SP.UI.MenuDeOrdenes.Tanque; opc = 3; break;
                case "aliados_tanque": cat = SP.UI.MenuDeOrdenes.Tanque; opc = 0; break;
                case "avanzar_disparar":
                case "final": cat = SP.UI.MenuDeOrdenes.Tanque; opc = 2; break;
            }
            SP.UI.MenuDeOrdenes.PonerPista(cat, opc);
        }
        Soldier herido;
        ObstacleMarker muroPractica;
        TutorialBeacon balizaMuro;

        // Un bloque demolible a 7 m adelante (para el paso de demoler).
        void CrearMuroDePractica()
        {
            if (muroPractica != null) return;
            var yo = driver.Brain.Current; if (yo == null || driver.Rig.Cam == null) return;
            var frente = Vector3.ProjectOnPlane(driver.Rig.Cam.transform.forward, Vector3.up).normalized;
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = "Tut_MuroDemolible";
            c.transform.position = yo.transform.position + frente * 7f;
            c.transform.position = new Vector3(c.transform.position.x, 1.25f, c.transform.position.z);
            c.transform.localScale = new Vector3(5f, 2.5f, 1.2f);
            c.transform.rotation = Quaternion.LookRotation(frente);
            c.GetComponent<MeshRenderer>().sharedMaterial = SP.Presentation.SafeMaterial.Create(new Color(0.62f, 0.62f, 0.66f));
            muroPractica = c.AddComponent<ObstacleMarker>();
            balizaMuro = TutorialBeacon.Crear("MURO · DEMOLER", naranja_, c.transform.position, null, 1.4f, 8f);
        }

        static void AlertQueueSafe(string texto)
            => SP.UI.AlertQueue.Push(texto, SP.UI.AlertPriority.Alta, 2.2f);

        // ===============================================================
        // Eventos del juego
        // ===============================================================
        void AlMorir(EntityDiedEvent e)
        {
            if (dummy != null && e.ActorId == dummy.Id) Flags.disparoEnemigo = true;
            if (idsOlaA.Contains(e.ActorId))
            {
                muertosOlaA++; bajas++; bajasTotales++;
                var muerto = ActorRegistry.FindById(e.ActorId);
                string nombreMuerto = muerto != null ? muerto.name : "?";
                TutorialLog.Escribir("[EVENTO]", $"   enemigo derribado {bajasTotales}/{BajasNecesarias}: {nombreMuerto}");
            }
            else if (idsOlaB.Contains(e.ActorId)) bajas++;
        }

        void AlOrdenDeMover(MoveOrderIssuedEvent e)
        {
            var p = PasoActual; if (p == null) return;
            if (p.Id == "rts" && driver.Rig.Mode == ControlMode.Rts && zonaA != null)
            {
                foreach (var a in aliados)
                    if (a != null && a.Id == e.ActorId)
                    {
                        var d = e.Destination - zonaA.position; d.y = 0f;
                        if (d.magnitude <= 10f) ordenadosARts.Add(a.Id);
                    }
            }
            else if (p.Id == "mover_fps" && driver.Rig.Mode == ControlMode.Fps && e.ActorId == aliadoSeleccionadoId)
                Flags.ordenDeMoverEnFps = true;
        }

        void AlDisparar(ShotFiredEvent e)
        {
            var p = PasoActual; if (p == null || driver.Brain.Current == null) return;
            if (p.Id == "mira" && e.ShooterId == driver.Brain.Current.Id && driver.Rig.EstaConZoom && Flags.apuntaConZoom)
                Flags.disparaConZoom = true;
            if (p.Id == "torreta_fija" && e.ShooterId == driver.Brain.Current.Id && driver.EnTorretaFija)
                Flags.disparoTorretaFija = true;
        }

        void AlGolpearObstaculo(ObstacleMarker m, int dano)
        {
            if (m == marcaPared && PasoActual != null && PasoActual.Id == "disparar") Flags.disparoPared = true;
        }

        void AlDerrumbarObstaculo(ObstacleMarker m)
        {
            if (m == marcaDestruible) Flags.disparoDestruible = true;
        }

        // ===============================================================
        // Olas de enemigos (pasos 15 y 16)
        // ===============================================================
        Soldier NuevoEnemigo(string nombre, Vector3 pos)
        {
            if (prefabEnemigo == null) return null;
            var go = Instantiate(prefabEnemigo, pos, Quaternion.Euler(0f, 180f, 0f));
            go.name = nombre;
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            var ai = go.GetComponent<AiBrain>();
            if (ai != null) ai.SetPatrolWaypoints(null);
            olaViva.Add(s);
            return s;
        }

        void LanzarOlaA()
        {
            var v = driver.Vehicle; if (v == null) return;
            var basePos = v.transform.position + v.transform.forward * 36f;
            for (int i = 0; i < 4; i++)
            {
                var pos = basePos + new Vector3((i - 1.5f) * 4f, 0f, (i % 2) * 5f);
                pos.y = 0.8f;
                var s = NuevoEnemigo("Tut_Enemigo_OlaA_" + (i + 1), pos);
                if (s != null) idsOlaA.Add(s.Id);
            }
            TutorialLog.Escribir("[EVENTO]", $"Ola A: {idsOlaA.Count} enemigos aparecen al frente y avanzan hacia el tanque");
        }

        // Si algo mato a los enemigos y al jugador todavia le faltan bajas, aparecen mas.
        void RellenarOlaA()
        {
            if (Time.time < proximoRelleno) return;
            proximoRelleno = Time.time + 3f;
            int vivos = 0;
            foreach (var s in olaViva) if (s != null && s.Health.IsAlive && idsOlaA.Contains(s.Id)) vivos++;
            int faltan = BajasNecesarias - bajasTotales;
            if (vivos >= faltan) return;
            var v = driver.Vehicle; if (v == null) return;
            var basePos = v.transform.position + v.transform.forward * 30f;
            for (int i = 0; i < faltan - vivos; i++)
            {
                var pos = basePos + new Vector3((i - 0.5f) * 5f, 0f, 0f);
                pos.y = 0.8f;
                var s = NuevoEnemigo("Tut_Enemigo_OlaA_extra" + (idsOlaA.Count + 1), pos);
                if (s != null) idsOlaA.Add(s.Id);
            }
            TutorialLog.Escribir("[EVENTO]", $"Llegan {faltan - vivos} enemigos mas (faltan {faltan} bajas y hay {vivos} vivos)");
        }

        void LanzarOlaB()
        {
            olaBLanzada = true;
            var v = driver.Vehicle; if (v == null) return;
            var basePos = v.transform.position + v.transform.forward * 26f;
            for (int i = 0; i < 3; i++)
            {
                var pos = basePos + new Vector3((i - 1f) * 5f, 0f, (i % 2) * 4f);
                pos.y = 0.8f;
                var s = NuevoEnemigo("Tut_Enemigo_OlaB_" + (i + 1), pos);
                if (s != null) idsOlaB.Add(s.Id);
            }
            AlertQueueSafe("REFUERZOS ENEMIGOS");
            Feedback.Accion(SfxKind.EnemySpotted, "¡REFUERZOS!", v.transform.position, Feedback.Bad, aviso: false, pulso: false, volumen: 0.6f);
            TutorialLog.Escribir("[EVENTO]", $"Ola B: {idsOlaB.Count} enemigos de refuerzo (opcionales)");
        }

        // Los enemigos de las olas siguen al tanque mientras esten vivos.
        void AcosarAlTanque()
        {
            var v = driver.Vehicle; if (v == null) return;
            for (int i = olaViva.Count - 1; i >= 0; i--)
            {
                var s = olaViva[i];
                if (s == null || !s.Health.IsAlive) { olaViva.RemoveAt(i); continue; }
                if (Vector3.Distance(s.transform.position, v.transform.position) > 9f)
                {
                    var ai = s.Brain;
                    if (ai != null && ai.State != AiState.Attack && ai.State != AiState.Chase)
                        ai.IssueMoveOrder(v.transform.position + (s.transform.position - v.transform.position).normalized * 7f);
                }
            }
        }

        // Ayuda: nadie pierde por aprender. El tanque y los soldados se curan.
        void Protecciones()
        {
            var v = driver.Vehicle;
            if (v != null && v.Health != null && v.Health.IsAlive && v.Health.Current < v.Health.MaxHealth * 0.6f)
                v.Health.Heal(v.Health.MaxHealth / 4);
            foreach (var s in driver.Squad)
                if (s != null && s.Health.IsAlive && s.Health.Current < s.Health.MaxHealth * 0.5f)
                    s.Health.Heal(s.Health.MaxHealth / 3);
        }

        // ===============================================================
        // Maquina de pasos
        // ===============================================================
        void EmpezarPaso(int i)
        {
            Indice = i;
            var p = pasos[i];
            tPaso = Time.time;
            sinProgreso = 0f;
            pistaMostrada = "";
            pausaActiva = false;
            foreach (var s in p.Subs) s.Hecha = false;
            p.AlEntrar?.Invoke();

            var textos = new string[p.OcultarSubs ? 0 : p.Subs.Length];
            for (int k = 0; k < textos.Length; k++) textos[k] = p.Subs[k].TextoActual;
            ui.MostrarPaso(i, pasos.Count, p.Titulo, textos, p.Teclas, p.Acento);

            TutorialLog.Paso(i, pasos.Count, p.Titulo, $"INICIO · {p.Subs.Length} sub-pasos · teclas: {(string.IsNullOrEmpty(p.Teclas) ? "-" : p.Teclas)}");
            for (int k = 0; k < p.Subs.Length; k++)
                TutorialLog.Escribir($"[PASO {i + 1:00}/{pasos.Count:00} {p.Titulo}]", $"   sub-paso {k + 1}/{p.Subs.Length} PENDIENTE: {p.Subs[k].TextoActual}");

            Feedback.Accion(SfxKind.TutStep, null, null, null, aviso: false, pulso: false, volumen: 0.35f);
        }

        void Update()
        {
            if (driver == null || ui == null || Indice < 0 || Terminado) return;
            var p = pasos[Indice];

            if (Time.time >= proximaCura) { proximaCura = Time.time + 1f; if (p.Id != "curar") Protecciones(); }
            if (Time.time >= proximoAcoso) { proximoAcoso = Time.time + 2.2f; AcosarAlTanque(); }

            if (pausaActiva)
            {
                if (Time.time >= pausaHasta) AvanzarDePaso();
                ui.Refrescar(p.MensajeVivo != null && p.Id != "wasd" ? p.MensajeVivo() : "Muy bien. Sigamos...", HechasDe(p), TextosDe(p), "", ProgresoTotal());
                return;
            }

            p.Evaluar?.Invoke();
            AplicarPistaDeRadial(p);

            // Sub-pasos: se notifica cada uno cuando pasa de false a true.
            int hechas = 0;
            for (int k = 0; k < p.Subs.Length; k++)
            {
                var s = p.Subs[k];
                bool v = s.Leer();
                if (v && !s.Hecha)
                {
                    s.Hecha = true;
                    sinProgreso = 0f;
                    pistaMostrada = "";
                    TutorialLog.Sub(Indice, pasos.Count, p.Titulo, k + 1, p.Subs.Length, s.TextoActual);
                    var pos = driver.Brain.Current != null ? driver.Brain.Current.transform.position : Vector3.zero;
                    Feedback.Accion(SfxKind.TutSub, "OK", pos, Feedback.Ok, aviso: false, pulso: false, volumen: 0.45f);
                }
                if (s.Hecha) hechas++;
            }

            sinProgreso += Time.deltaTime;
            string pista = "";
            Sub pendiente = null;
            foreach (var s in p.Subs) if (!s.Hecha) { pendiente = s; break; }
            if (pendiente != null && sinProgreso > 16f && !string.IsNullOrEmpty(pendiente.Pista))
            {
                pista = "PISTA: " + pendiente.Pista;
                if (pistaMostrada != pista)
                {
                    pistaMostrada = pista;
                    TutorialLog.Escribir($"[PASO {Indice + 1:00}/{pasos.Count:00} {p.Titulo}]", "   " + pista);
                    Feedback.Accion(SfxKind.Select, null, null, null, aviso: false, pulso: false, volumen: 0.3f);
                }
            }

            string mensaje = p.MensajeVivo != null ? p.MensajeVivo()
                : pendiente != null ? pendiente.Mensaje : "Paso completo";
            ui.Refrescar(mensaje, HechasDe(p), TextosDe(p), pista, ProgresoTotal());

            if (p.Subs.Length > 0 && hechas == p.Subs.Length) CompletarPaso();
        }

        bool[] HechasDe(Paso p)
        {
            var r = new bool[p.Subs.Length];
            for (int i = 0; i < r.Length; i++) r[i] = p.Subs[i].Hecha;
            return r;
        }

        string[] TextosDe(Paso p)
        {
            if (p.OcultarSubs) return new string[0];
            var r = new string[p.Subs.Length];
            for (int i = 0; i < r.Length; i++) r[i] = p.Subs[i].TextoActual;
            return r;
        }

        float ProgresoTotal()
        {
            if (Indice < 0) return 0f;
            var p = pasos[Indice];
            int h = 0; foreach (var s in p.Subs) if (s.Hecha) h++;
            float frac = p.Subs.Length > 0 ? (float)h / p.Subs.Length : 1f;
            return Mathf.Clamp01((Indice + (pausaActiva ? 1f : frac * 0.9f)) / pasos.Count);
        }

        void CompletarPaso()
        {
            var p = pasos[Indice];
            float dur = Time.time - tPaso;
            duraciones.Add(dur);
            TutorialLog.Paso(Indice, pasos.Count, p.Titulo, $"COMPLETADO en {dur:0.0} s");
            p.AlSalir?.Invoke();
            ui.DestelloDePaso($"PASO {Indice + 1} COMPLETADO", new Color(0.40f, 0.95f, 0.55f));
            Feedback.Accion(SfxKind.TutStep, null, null, null, aviso: false, pulso: false, volumen: 0.7f);
            pausaActiva = true;
            pausaHasta = Time.time + pausaEntrePasos;
        }

        void AvanzarDePaso()
        {
            if (Indice + 1 < pasos.Count) EmpezarPaso(Indice + 1);
        }

        // Solo para pruebas / editor: da el paso por cumplido.
        public void SaltarPaso()
        {
            if (Indice < 0 || Indice >= pasos.Count || pausaActiva) return;
            foreach (var s in pasos[Indice].Subs) s.Poner?.Invoke(true);
            TutorialLog.Escribir("[SALTO]", $"Paso {Indice + 1} marcado como cumplido a mano");
        }

        public void Finalizar()
        {
            Terminado = true;
            SP.UI.MenuDeOrdenes.PonerPista(-1);
            TutorialLog.Escribir("[FIN]", $"Tutorial finalizado en {TiempoTotal:0.0} s · {bajas} bajas");
        }

        public void Reintentar()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void VolverAlMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("SC_MainMenu");
        }
    }
}
