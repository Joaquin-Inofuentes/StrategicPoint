using UnityEngine;
using SP.Actors;
using SP.Combat;

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

        // Colores solidos y saturados a proposito -- pedido explicito de
        // "mucho contraste": el cilindro viejo era 16% opaco y se perdia
        // contra el pasto/tierra. Rojo/azul puros + emision (ver
        // DiamondGizmo.NuevoMaterial) se leen igual de bien de dia, de
        // noche o contra niebla.
        static readonly Color ColorEnemigo = new Color(1f, 0.05f, 0.05f, 1f);
        static readonly Color ColorAliado = new Color(0.1f, 0.45f, 1f, 1f);
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
        static void ResetearMaterialCompartido() => materialBordeCompartido = null;

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

        void OnEnable()
        {
            dead = false;
            if (soldier == null) soldier = GetComponent<Soldier>();
            if (soldier == null) { enabled = false; return; }
            if (marcador == null) Construir();
        }

        void OnDisable() { if (marcador != null) marcador.SetActive(false); }

        void OnDestroy()
        {
            if (materialInterior == null) return;
            if (Application.isPlaying) Destroy(materialInterior);
            else DestroyImmediate(materialInterior);
            materialInterior = null;
        }

        void Construir()
        {
            marcador = new GameObject(MarkerName);
            marcador.transform.SetParent(transform, false);
            marcador.transform.localPosition = new Vector3(0f, Altura, 0f);

            // Fondo blanco primero (mas grande, sin offset): al ser mas
            // grande que el interior, siempre asoma como un contorno parejo
            // alrededor del rombo de color.
            DiamondGizmo.CrearCara("Borde", marcador.transform, TamanoBorde, MaterialBorde());

            equipoPintado = soldier.Team;
            materialInterior = DiamondGizmo.NuevoMaterial(equipoPintado == TeamId.Enemy ? ColorEnemigo : ColorAliado);
            var interior = DiamondGizmo.CrearCara("Interior", marcador.transform, TamanoInterior, materialInterior);
            // Un pelo hacia la camara para que nunca compita en Z con el
            // borde (evita z-fighting entre los dos rombos coplanares).
            interior.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            marcador.SetActive(false);
        }

        void Update()
        {
            if (dead || marcador == null || soldier == null || soldier.Health == null) return;

            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam == null) { marcador.SetActive(false); return; }

            lodTimer -= Time.deltaTime;
            if (lodTimer <= 0f)
            {
                lodTimer = LodCheckInterval;

                if (!soldier.Health.IsAlive) { dead = true; marcador.SetActive(false); return; }
                // El propio poseido no necesita ubicarse a si mismo.
                if (soldier.Brain != null && soldier.Brain.IsPossessedByPlayer) { marcador.SetActive(false); return; }

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
                    var colorNuevo = equipoPintado == TeamId.Enemy ? ColorEnemigo : ColorAliado;
                    materialInterior.color = colorNuevo;
                    if (materialInterior.HasProperty("_BaseColor")) materialInterior.SetColor("_BaseColor", colorNuevo);
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

                bool anguloOk = angulo > AnguloDeMira;
                bool inteligenciaOk = !esEnemigo || SP.Core.InteligenciaDeEnemigos.EstaRevelado(soldier.Id);
                marcador.SetActive(anguloOk && inteligenciaOk);
            }

            // Billboard: hay que rehacerlo TODOS los frames que este
            // visible (no solo en el tick de LOD de arriba), si no el giro
            // se nota a los tirones cada 0.15 s en vez de verse fijo mirando
            // a camara mientras el jugador se mueve alrededor.
            if (marcador.activeSelf) marcador.transform.rotation = cam.transform.rotation;
        }
    }
}
