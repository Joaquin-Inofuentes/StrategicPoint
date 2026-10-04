using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Pedido explicito: reemplazar los cilindros localizadores por un gizmo
    // de ROMBO (dos capas: un rombo blanco de fondo, mas grande, que hace de
    // contorno, y uno interior mas chico que varia de color -- rojo enemigo,
    // azul aliado) que SIEMPRE mira de frente al jugador principal
    // (billboard) y hace mucho contraste contra el ambiente. Reemplaza al
    // cilindro columna que habia antes; se mantiene el mismo nombre de clase
    // para no romper la referencia serializada en los prefabs de soldado.
    //
    // Se sigue apagando solo en cuanto el jugador lo tiene encima de la mira
    // (adentro del cono de AnguloDeMira grados): ahi ya lo esta viendo/
    // apuntando, y el rombo solo taparia la vista.
    //
    // Pedido explicito: "en rojo los enemigos solamente los que los aliados
    // hayan visto o vos hayas visto o te hayan disparado". El rombo de
    // ALIADO sigue mostrandose siempre (no hay "fog of war" entre
    // companeros); el de ENEMIGO ahora exige ademas que
    // InteligenciaDeEnemigos.EstaRevelado(soldier.Id) sea verdadero, que se
    // marca desde tres lugares: un aliado lo senso (AiBrain.Sentidos.cs),
    // le disparo al jugador (InteligenciaDeEnemigos.AlRecibirDano), o el
    // propio jugador lo tuvo delante con linea de tiro libre (mas abajo).
    public class UnitLocatorCylinder : MonoBehaviour
    {
        Soldier soldier;
        GameObject marcador;   // raiz billboardeada (rombo blanco + rombo de color)
        Material materialInterior;
        Material materialIcono;
        TeamId equipoPintado;

        const string MarkerName = "LocatorRombo";
        public const float AnguloDeMira = 5f;
        // Pedido explicito: "en rojo los enemigos solamente los que los
        // aliados hayan visto o vos hayas visto o te hayan disparado" -- el
        // cono, mas ancho que AnguloDeMira (que es "lo tengo en la mira,
        // apagar el rombo"), representa "lo tengo mas o menos delante,
        // pude haberlo notado".
        const float ConoDeVisionJugador = 45f;
        // BUG REAL: con 90 m, los enemigos casi nunca mostraban el rombo en
        // combate real -- las lineas iniciales y las oleadas de esta mision
        // se enfrentan habitualmente entre 90 y 180 m (medido en vivo). El
        // jugador reportaba "no veo los rombos de enemigo" porque el rombo
        // se apagaba antes de que el enemigo entrara en rango util de mira.
        const float DistanciaVisible = 160f;
        const float Altura = 2.4f;          // flota sobre la cabeza, no a 14 m como la columna vieja
        const float TamanoBorde = 0.62f;
        const float TamanoInterior = 0.40f;

        // Pedido explicito: "los rombos aliados si estan muy cerca mio se
        // achicaran al 5% y mientras mas lejos estan mas grandes seran los
        // rombos. Osea es proporcional su tamano a mi distancia con ellos" --
        // solo aplica a ALIADOS (el enemigo no cambia de tamano, para no
        // confundir "mas grande = mas peligroso"). Cerca el aliado ya se ve
        // solo, el rombo solo estorbaria; lejos es donde mas hace falta
        // destacarlo.
        const float EscalaAliadoCerca = 0.05f;
        const float DistanciaEscalaMin = 2f;   // a esta distancia o menos, 5%
        const float DistanciaEscalaMax = 45f;  // a esta distancia o mas, tamano normal (100%)

        // Pedido explicito: "y si estan siguiendome y estan muy cerca
        // directamente no tendran rombo por que se que son aliados" -- un
        // aliado en formacion (Follow) pegado al jugador no necesita marca
        // ninguna, ya se lo ve caminando al lado.
        const float DistanciaOcultarSiguiendo = 4f;

        // Colores solidos y saturados a proposito -- pedido explicito de
        // "mucho contraste": el cilindro viejo era 16% opaco y se perdia
        // contra el pasto/tierra. Rojo/azul puros + emision (ver
        // DiamondGizmo.NuevoMaterial) se leen igual de bien de dia, de
        // noche o contra niebla. Compartidos con DiamondGizmo para que el
        // minimapa use exactamente el mismo par.
        static readonly Color ColorEnemigo = DiamondGizmo.ColorEnemigo;
        static readonly Color ColorAliado = DiamondGizmo.ColorAliado;
        static readonly Color ColorBorde = Color.white;

        // El rombo blanco de fondo es identico para todos los enemigos Y
        // todos los aliados (mismo color, mismo tamano): un solo material
        // compartido en vez de uno por soldado.
        static Material materialBordeCompartido;

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload
        // (mismo patron que el resto del proyecto): sin este reset, el
        // material compartido de una sesion de Play anterior podria quedar
        // referenciado como "fake null" si algo lo destruyo entre medio.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetearMaterialCompartido() { materialBordeCompartido = null; materialSombraCompartido = null; materialesPorColor.Clear(); materialesPorIcono.Clear(); }

        // Llamado desde ReinicioDeEstaticos (mismo patron que el resto de
        // los sistemas del proyecto): idempotente con ResetearMaterialCompartido.
        public static void ReiniciarActivo() => materialBordeCompartido = null;

        static Material materialSombraCompartido;
        static Material MaterialSombra()
        {
            if (materialSombraCompartido == null) materialSombraCompartido = DiamondGizmo.NuevoMaterial(new Color(0.02f, 0.02f, 0.03f, 1f));
            return materialSombraCompartido;
        }

        // Bug #049: en RTS "rombos mas grandes para aliados, yo y seleccionado; cuando me acerco se achican y si me alejo se
        // agrandan". El tamaño crece con la distancia a la camara (ocupa mas o menos lo mismo en pantalla a cualquier zoom),
        // el tuyo es amarillo y los seleccionados son mas grandes y laten.
        public const float EscalaRtsPorMetro = 1f / 26f, EscalaRtsMax = 6f, ExtraYo = 1.3f, ExtraSeleccionado = 1.3f;
        bool yoPintado;
        public float EscalaActual => marcador != null ? marcador.transform.localScale.x : 0f;
        public bool MarcadorVisible => marcador != null && marcador.activeSelf;

        static bool EnRts() => SP.CameraSystem.CameraRig.Instance != null && SP.CameraSystem.CameraRig.Instance.Mode == SP.CameraSystem.ControlMode.Rts;

        bool EstaSeleccionado()
        {
            var sc = SP.Player.SelectionController.Instance;
            if (sc == null || sc.Selected == null) return false;
            for (int i = 0; i < sc.Selected.Count; i++) if (sc.Selected[i] == soldier) return true;
            return false;
        }

        // Bug #052: cada soldado creaba 2 materiales propios (interior + icono): 12 ms por refuerzo que aparecia. Ahora se
        // comparten por color y por textura de icono, y "pintar" es cambiar el material del renderer.
        static readonly Dictionary<Color, Material> materialesPorColor = new Dictionary<Color, Material>();
        static readonly Dictionary<Texture, Material> materialesPorIcono = new Dictionary<Texture, Material>();
        Renderer rendInterior;

        static Material MaterialDeColor(Color c)
        {
            if (!materialesPorColor.TryGetValue(c, out var m) || m == null) { m = DiamondGizmo.NuevoMaterial(c); materialesPorColor[c] = m; }
            return m;
        }

        static Material MaterialDeIcono(Texture tex)
        {
            if (tex == null) return MaterialDeColor(Color.white);
            if (!materialesPorIcono.TryGetValue(tex, out var m) || m == null)
            {
                m = DiamondGizmo.NuevoMaterial(Color.white);
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                materialesPorIcono[tex] = m;
            }
            return m;
        }

        void PintarInterior(Color c)
        {
            materialInterior = MaterialDeColor(c);
            if (rendInterior != null) rendInterior.sharedMaterial = materialInterior;
        }

        static float EscalaPara(float distancia, bool esEnemigo, bool soyYo)
        {
            if (EnRts())
            {
                // Proporcional a la distancia: el mismo tamaño en pantalla a cualquier altura de camara. Los enemigos un poco
                // mas chicos que los propios (no "crecen por ser peligrosos": crecen por estar lejos, igual que todos).
                float e = Mathf.Clamp(distancia * EscalaRtsPorMetro, 0.6f, EscalaRtsMax);
                if (esEnemigo) e *= 0.8f;
                if (soyYo) e *= ExtraYo;
                return e;
            }
            if (esEnemigo) return 1f;
            // FPS: aliados casi invisibles pegados a vos y mas grandes cuanto mas lejos (antes topaban en 1 a los 45 m).
            float t = Mathf.InverseLerp(DistanciaEscalaMin, DistanciaEscalaMax, distancia);
            float escala = Mathf.Lerp(EscalaAliadoCerca, 1f, t);
            if (distancia > DistanciaEscalaMax) escala = Mathf.Min(2.5f, distancia / DistanciaEscalaMax);
            return escala;
        }

        static Material MaterialBorde()
        {
            if (materialBordeCompartido == null) materialBordeCompartido = DiamondGizmo.NuevoMaterial(ColorBorde);
            return materialBordeCompartido;
        }

        // Throttle igual que antes: con muchos soldados en pantalla, el
        // angulo/distancia contra camara no necesita mirarse cada frame.
        const float LodCheckInterval = 0.15f;
        float lodTimer;
        bool dead;
        float vibrateTime;
        Vector3 baseLocalPosition = new Vector3(0f, Altura, 0f);
        IDisposable shotSub;

        void OnEnable()
        {
            dead = false;
            if (soldier == null) soldier = GetComponent<Soldier>();
            if (soldier == null) { enabled = false; return; }
            if (marcador == null) Construir();
            shotSub = EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFired);
        }

        void OnShotFired(ShotFiredEvent evt)
        {
            if (evt.ShooterId == soldier.Id && marcador != null && marcador.activeInHierarchy)
            {
                vibrateTime = 0.2f; // 200ms of vibration
            }
        }

        void OnDisable()
        {
            if (marcador != null) marcador.SetActive(false);
            shotSub?.Dispose();
            shotSub = null;
        }

        // Los materiales son compartidos (ver MaterialDeColor): no se destruyen con el soldado.
        void OnDestroy() { materialInterior = null; materialIcono = null; }

        void Construir()
        {
            marcador = new GameObject(MarkerName);
            marcador.transform.SetParent(transform, false);
            marcador.transform.localPosition = new Vector3(0f, Altura, 0f);

            // Bug #049 ("que resalten y haya mas contraste real"): contorno OSCURO detras del blanco. El blanco solo se perdia
            // contra el asfalto claro y el cielo; negro + blanco + color se lee sobre cualquier fondo.
            var sombra = DiamondGizmo.CrearCara("Sombra", marcador.transform, TamanoBorde * 1.28f, MaterialSombra());
            sombra.transform.localPosition = new Vector3(0f, 0f, 0.02f);

            // Fondo blanco primero (mas grande, sin offset): al ser mas
            // grande que el interior, siempre asoma como un contorno parejo
            // alrededor del rombo de color.
            DiamondGizmo.CrearCara("Borde", marcador.transform, TamanoBorde, MaterialBorde());

            equipoPintado = soldier.Team;
            materialInterior = MaterialDeColor(equipoPintado == TeamId.Enemy ? ColorEnemigo : ColorAliado);
            var interior = DiamondGizmo.CrearCara("Interior", marcador.transform, TamanoInterior, materialInterior);
            rendInterior = interior.GetComponent<Renderer>();
            // Un pelo hacia la camara para que nunca compita en Z con el
            // borde (evita z-fighting entre los dos rombos coplanares).
            interior.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            // Pedido explicito: "los rombos dentro de los aliados tengan
            // iconos segun su especialidad, y lo mismo para enemigos" -- un
            // tercer rombo, mas chico y blanco, encima del relleno de color,
            // con la silueta de la especialidad (medico/francotirador/
            // asalto/civil para aliados; el arma que lleva para enemigos,
            // que ya tienen variedad real via SoldierClasses.Enemigos).
            var iconoTex = equipoPintado == TeamId.Enemy
                ? RoleIconFactory.WorldIconTextureForWeapon(SP.Actors.SoldierClasses.Para(soldier).Loadout is { Length: > 0 } lo ? lo[0] : WeaponKind.Rifle)
                : RoleIconFactory.WorldIconTexture(soldier.Role);
            materialIcono = MaterialDeIcono(iconoTex);
            var icono = DiamondGizmo.CrearCara("Icono", marcador.transform, TamanoInterior * 0.72f, materialIcono);
            icono.transform.localPosition = new Vector3(0f, 0f, -0.03f);

            marcador.SetActive(false);

            // BUG REAL reportado jugando: "el minimapa no se ven los
            // triangulos de los enemigos, aliados". Ningun soldado (ni la
            // escuadra inicial ni los que aparecen durante la mision via
            // MisionDirector.CrearEnemigo) tenia jamas un MinimapIcon --
            // solo los obstaculos lo tenian (GameplaySceneBootstrap llama
            // MinimapIcon.RegistrarObstaculos, pero nada equivalente existia
            // para soldados). Este componente SI esta garantizado en cada
            // soldado (viene en el prefab, por eso el rombo de mundo
            // siempre aparece), asi que es el enganche natural: en vez de
            // agregar un hook nuevo en cada lugar que crea un soldado, el
            // icono de minimapa se arma aca, una sola vez, junto con el
            // resto de la señalizacion de esta unidad. MinimapIcon.Spawn ya
            // detecta el Soldier del Target solo y se pinta/convierte a
            // doble triangulo por su cuenta (ver MinimapIcon.DetectarSoldado).
            // MinimapIconBakePipeline (editor, menu "Strategic Point/UI/4. ...")
            // ya deja este icono horneado en la escena para que el minimapa
            // se vea en el editor igual que en partida -- sin el chequeo de
            // abajo, entrar en Play volvia a llamar Spawn() y duplicaba el
            // icono de cada soldado ya horneado.
            if (Application.isPlaying && !MinimapIcon.ExisteIconoPara(soldier.transform))
            {
                int layerMinimapa = LayerMask.NameToLayer("Minimap");
                if (layerMinimapa < 0) layerMinimapa = 8;
                // Pedido explicito: "los q me importan son muy pequeños" --
                // el radio subio de 1.6 a 3.0 y despues a 4.0 (al agrandar
                // el radio visible del minimapa de 26 a 110 m, ver
                // MinimapFollow.OrthoSizeMinimo, el mismo icono ocupa menos
                // de un tercio de los pixeles de antes) para que un soldado
                // se lea a simple vista contra los rectangulos grises de
                // obstaculos (esos quedan en su propio tamaño, ver
                // RegistrarObstaculos).
                MinimapIcon.Spawn(soldier.transform, equipoPintado == TeamId.Enemy ? ColorEnemigo : ColorAliado, layerMinimapa, 4.0f);
            }
        }

        void Update()
        {
            if (marcador == null || soldier == null || soldier.Health == null) return;
            // Al revivir, el rombo vuelve (antes quedaba apagado para siempre despues de la primera muerte).
            if (dead) { if (!soldier.Health.IsAlive) return; dead = false; lodTimer = 0f; }

            if (RomboVisibilidad.Suprimidos) { marcador.SetActive(false); return; }

            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam == null) { marcador.SetActive(false); return; }

            lodTimer -= Time.deltaTime;
            if (lodTimer <= 0f)
            {
                lodTimer = LodCheckInterval;

                if (!soldier.Health.IsAlive) { dead = true; marcador.SetActive(false); return; }
                // El propio poseido no necesita ubicarse a si mismo en primera persona; en RTS si (bug #049: "rombos para
                // aliados y YO"), en amarillo.
                bool soyYo = soldier.Brain != null && soldier.Brain.IsPossessedByPlayer;
                if (soyYo && !EnRts()) { marcador.SetActive(false); return; }
                if (soyYo != yoPintado)
                {
                    yoPintado = soyYo;
                    PintarInterior(soyYo ? DiamondGizmo.ColorObjetivo : (soldier.Team == TeamId.Enemy ? ColorEnemigo : ColorAliado));
                }

                // Pedido explicito: "una vez que me subo al tanque el rombo de
                // arriba no se va". Para los asientos ocultos (conductor,
                // cañon, pasajero de atras) esto ya pasaba solo -- Vehicle.
                // Mount desactiva el GameObject entero y Update() ni corre --
                // pero el artillero de la metralleta queda de pie, VISIBLE y
                // con su GameObject activo (a proposito: se lo ve asomando
                // por la escotilla), asi que sin este chequeo seguia flotando
                // un rombo sobre su cabeza mientras viaja pegado al chasis.
                foreach (var v in SP.Vehicles.Vehicle.Todos)
                    if (v != null && v.RoleOf(soldier) != null) { marcador.SetActive(false); return; }

                // BUG REAL encontrado jugando: el civil (y cualquier otro
                // soldado cuyo equipo se fija DESPUES de Instantiate, via
                // Configure) ya tenia este componente corriendo su OnEnable
                // -- que pasa DURANTE Instantiate, antes de esa linea -- asi
                // que el rombo quedaba pintado para siempre con el equipo
                // que traiga el prefab de base (a veces el de Enemigo), sin
                // importar el equipo real. Se revisa en cada tick de LOD
                // (barato, ya corre cada 0.15 s) y se repinta si cambio.
                if (soldier.Team != equipoPintado)
                {
                    equipoPintado = soldier.Team;
                    PintarInterior(yoPintado ? DiamondGizmo.ColorObjetivo : equipoPintado == TeamId.Enemy ? ColorEnemigo : ColorAliado);
                }

                var haciaSoldado = transform.position - cam.transform.position;
                float distancia = haciaSoldado.magnitude;
                if (distancia > DistanciaVisible) { marcador.SetActive(false); return; }

                float angulo = Vector3.Angle(cam.transform.forward, haciaSoldado);
                bool esEnemigo = soldier.Team == TeamId.Enemy;

                // Tercera fuente de revelado (las otras dos son
                // AiBrain.Sentidos.cs para los aliados e
                // InteligenciaDeEnemigos.AlRecibirDano para "te disparo"):
                // el propio jugador lo tiene mas o menos delante Y con
                // linea de tiro libre -- no a traves de una pared.
                if (esEnemigo && angulo <= ConoDeVisionJugador &&
                    SP.Core.NavService.HayLineaDeTiro(cam.transform.position, transform.position, null, soldier.transform))
                    SP.Core.InteligenciaDeEnemigos.Revelar(soldier.Id);

                // En RTS no hay "mira": el rombo no se apaga por quedar en el centro de la pantalla.
                bool anguloOk = angulo > AnguloDeMira || EnRts();
                bool inteligenciaOk = !esEnemigo || SP.Core.InteligenciaDeEnemigos.EstaRevelado(soldier.Id);

                // Aliado en formacion (siguiendome) y pegado a mi: sin rombo,
                // ya se lo ve al lado.
                bool ocultoPorSeguirCerca = !esEnemigo && soldier.Brain != null
                    && soldier.Brain.State == SP.Ai.AiState.Follow
                    && distancia <= DistanciaOcultarSiguiendo;

                marcador.SetActive(anguloOk && inteligenciaOk && !ocultoPorSeguirCerca);

            }

            // La escala se recalcula todos los frames que se ve: el zoom de RTS cambia la distancia de forma continua y a
            // saltos de 0,15 s se notaba.
            if (marcador.activeSelf)
            {
                float dist = (transform.position - cam.transform.position).magnitude;
                bool yo = soldier.Brain != null && soldier.Brain.IsPossessedByPlayer;
                float e = EscalaPara(dist, soldier.Team == TeamId.Enemy, yo);
                if (EnRts() && soldier.Team != TeamId.Enemy && EstaSeleccionado()) e *= ExtraSeleccionado * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 7f));
                marcador.transform.localScale = Vector3.one * e;
            }

            // Billboard: hay que rehacerlo TODOS los frames que este
            // visible (no solo en el tick de LOD de arriba), si no el giro
            // se nota a los tirones cada 0.15 s en vez de verse fijo mirando
            // a camara mientras el jugador se mueve alrededor.
            if (marcador.activeSelf) 
            {
                marcador.transform.rotation = cam.transform.rotation;
                
                if (vibrateTime > 0f)
                {
                    vibrateTime -= Time.deltaTime;
                    marcador.transform.localPosition = baseLocalPosition + new Vector3(
                        UnityEngine.Random.Range(-0.1f, 0.1f),
                        UnityEngine.Random.Range(-0.1f, 0.1f),
                        0f);
                }
                else
                {
                    marcador.transform.localPosition = baseLocalPosition;
                }
            }
        }
    }
}

