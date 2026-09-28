using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Combat;
using SP.Ai;

namespace SP.Presentation
{
    // Circulo chato que representa a un soldado/vehiculo en el minimapa.
    // Vive en su propia capa (Minimap), que la cámara principal no ve y la
    // cámara del minimapa sí — así el minimapa no muestra el terreno ni la
    // geometría real, solo estos íconos de colores sobre fondo negro.
    //
    // [ExecuteAlways]: OnEnable pinta el equipo y arma el cono de vision
    // (ver comentario ahi) usando solo datos ya serializados -- sin esto la
    // escena horneada en el editor se veia sin pintar hasta apretar Play.
    // No hay Update/LateUpdate propio (TickFollow lo llama WorldUiDirector
    // a mano), asi que esto no agrega ningun tick nuevo en Edit mode.
    [ExecuteAlways]
    public class MinimapIcon : MonoBehaviour
    {
        [SerializeField] Transform target;
        // Propiedad (no campo) a proposito: BUG REAL encontrado al escribir
        // esto -- Spawn() hace "AddComponent<MinimapIcon>()" y RECIEN
        // DESPUES asigna Target; OnEnable ya corrio para entonces (Unity lo
        // llama en el mismo AddComponent), asi que DetectarSoldado() se
        // encontraba con Target en null y nunca coloreaba por equipo a los
        // iconos creados en runtime (los de la escena horneada no tenian
        // este problema: su Target ya viene serializado ANTES de OnEnable).
        // El setter vuelve a intentarlo apenas Target llega de verdad.
        public Transform Target
        {
            get => target;
            set { target = value; if (Application.isPlaying) DetectarTarget(); }
        }
        [SerializeField] float height = 55f;
        // El icono es un circulo chato: rotarlo no cambia nada visible.
        // Para que el minimapa diga "hacia donde estas mirando" (no solo
        // "donde estas"), el icono del jugador suma una cuña que sí
        // rota con el yaw del mundo -- desde la camara cenital del
        // minimapa, la rotacion en Y es exactamente lo que se ve girar.
        // [SerializeField] a proposito, aunque son de uso interno: se
        // asignan por codigo al armar la escena en el Editor (fuera de
        // Play mode), y un campo privado comun NO sobrevive al domain
        // reload al entrar en Play -- ya paso con `arrow` en
        // DamageDirectionView, con `brain` en varias vistas, etc. Como
        // Unity SI serializa los campos marcados [SerializeField] junto
        // con la escena, esto evita tener que reconstruir un self-heal
        // por nombre en OnEnable para algo que ya es una referencia
        // directa al objeto correcto.
        [SerializeField] Transform directionMarker;
        MeshRenderer selfRenderer;

        // El minimapa mostraba a TODOS los enemigos del mapa siempre,
        // incluso a los que la escuadra nunca vio -- eso elimina la
        // exploracion y cualquier sorpresa. Con esto activado, el icono
        // solo se ve mientras algun soldado propio vivo lo tiene dentro
        // de su alcance de vision (el mismo valor que usa AiBrain para
        // sensar).
        [SerializeField] bool fogEnabled;
        // Publica porque quien resuelve la niebla ahora es WorldUiDirector:
        // arma UNA sola lista de observadores vivos por pase y la compara
        // contra todos los iconos, en vez de que cada icono dispare su
        // propia consulta a la grilla espacial con su propio timer.
        public const float FogVisionRange = 10f;

        public bool FogEnabled => fogEnabled;

        public void EnableFogOfWar()
        {
            fogEnabled = true;
            EnsureRenderer();
            if (selfRenderer != null) selfRenderer.enabled = false;
        }

        // Pedido explicito: "que haya muchisimo contraste entre environment
        // y soldados e interactuables" + "los enemigos, aliados y resto que
        // resalten con doble triangulo y colores iguales a los rombos". Los
        // iconos de soldado en la escena real (SC_Gameplay) venian con su
        // color pintado A MANO en el material de la escena -- pedirle al
        // jugador o a un editor que retoque decenas de soldados a mano para
        // este cambio de paleta seria fragil (el proximo soldado que se
        // agregue quedaria con el color viejo). En cambio, cualquier icono
        // cuyo Target tenga un Soldier se repinta solo por equipo (mismos
        // colores que DiamondGizmo) y se convierte a la malla de doble
        // triangulo, sin importar que traiga serializado.
        Soldier soldierDetectado;
        TeamId equipoPintadoMinimapa;
        bool autoColoreado;
        // Cacheado en DetectarTarget (una vez, no por frame): de aca sale el
        // radio real del cono de vision, el mismo alcance que usa la IA para
        // sensar (AiBrain.EffectiveVisionRange), no un numero inventado.
        AiBrain brainDetectado;

        SP.Vehicles.Vehicle vehiculoDetectado;
        void DetectarTarget()
        {
            if (Target == null) return;
            soldierDetectado = Target.GetComponent<Soldier>();
            vehiculoDetectado = Target.GetComponent<SP.Vehicles.Vehicle>();

            if (soldierDetectado != null)
            {
                equipoPintadoMinimapa = soldierDetectado.Team;
                var color = equipoPintadoMinimapa == TeamId.Enemy ? DiamondGizmo.ColorEnemigo : DiamondGizmo.ColorAliado;
                if (equipoPintadoMinimapa == TeamId.Enemy) ConvertirEnTriangulo();
                else ConvertirEnCirculo();

                EnsureRenderer();
                if (selfRenderer != null) selfRenderer.sharedMaterial = DiamondGizmo.NuevoMaterial(color);
                autoColoreado = true;

                brainDetectado = Target.GetComponent<AiBrain>();
                CrearOActualizarConoDeVision(color);
                return;
            }

            if (vehiculoDetectado != null)
            {
                ConvertirEnCuadrado();
                Color color = Color.gray;
                if (vehiculoDetectado.Occupants.Count > 0)
                {
                    color = vehiculoDetectado.Bando == TeamId.Enemy ? DiamondGizmo.ColorEnemigo : DiamondGizmo.ColorAliado;
                }
                EnsureRenderer();
                if (selfRenderer != null) selfRenderer.sharedMaterial = DiamondGizmo.NuevoMaterial(color);
                return;
            }
        }

        public void RepintarPorEquipo(bool forzar = false)
        {
            if (Target == null) return;
            var vehicle = Target.GetComponent<SP.Vehicles.Vehicle>();
            if (vehicle != null)
            {
                Color color = Color.gray;
                if (vehicle.Occupants.Count > 0)
                    color = vehicle.Bando == TeamId.Enemy ? DiamondGizmo.ColorEnemigo : DiamondGizmo.ColorAliado;
                EnsureRenderer();
                if (selfRenderer != null) selfRenderer.sharedMaterial = DiamondGizmo.NuevoMaterial(color);
                return;
            }

            if (soldierDetectado == null) return;
            if (!forzar && soldierDetectado.Team == equipoPintadoMinimapa) return;
            equipoPintadoMinimapa = soldierDetectado.Team;
            var colorSoldier = equipoPintadoMinimapa == TeamId.Enemy ? DiamondGizmo.ColorEnemigo : DiamondGizmo.ColorAliado;
            if (equipoPintadoMinimapa == TeamId.Enemy) ConvertirEnTriangulo();
            else ConvertirEnCirculo();

            EnsureRenderer();
            if (selfRenderer != null) selfRenderer.sharedMaterial = DiamondGizmo.NuevoMaterial(colorSoldier);
            CrearOActualizarConoDeVision(colorSoldier);
        }

        // --- Cono de vision (D1: "para saber a donde apunto" cada soldado) ---
        //
        // Un unico wedge compartido (radio 1, ver MallaCono) escalado por
        // instancia al alcance REAL de vision del soldado
        // (AiBrain.EffectiveVisionRange) -- no un numero cosmetico aparte,
        // asi el cono siempre dice la verdad sobre lo que ese soldado puede
        // llegar a ver. El poseido por el jugador no tiene su AiBrain
        // corriendo (queda deshabilitado, no destruido) pero la propiedad se
        // sigue pudiendo leer igual: ConoRadioFallback solo cubre el caso sin
        // AiBrain en absoluto (por ejemplo un test sintetico).
        //
        // Medio angulo mas angosto que el cono real de deteccion de la IA
        // (SemiconoDeVision = 100 grados, casi un semicirculo) a proposito:
        // pintar el cono REAL con 50 soldados en pantalla seria un
        // amontonamiento de abanicos ilegible. Este es un indicador
        // direccional legible ("hacia aca apunta"), no una copia exacta del
        // hitbox de percepcion.
        public const float ConoMedioAnguloGrados = 35f;
        public const float ConoRadioFallback = 16f;
        // Pedido explicito: "quiero q los conos de vision sean 95% de
        // transparencia" -- 0.16 (84% transparente) se notaba muy solido
        // con 30+ conos superpuestos en pantalla a la vez.
        const float ConoAlpha = 0.05f;
        Transform conoDeVision;
        MeshRenderer conoRenderer;
        float conoRadioActual = -1f;

        void CrearOActualizarConoDeVision(Color colorEquipo)
        {
            if (conoDeVision == null)
            {
                var go = new GameObject("ConoDeVision", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                conoDeVision = go.transform;
                go.GetComponent<MeshFilter>().sharedMesh = MallaCono();
                conoRenderer = go.GetComponent<MeshRenderer>();
                conoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                conoRenderer.receiveShadows = false;
            }
            var tinte = colorEquipo;
            tinte.a = ConoAlpha;
            conoRenderer.sharedMaterial = NuevoMaterialTransparente(tinte);
            conoRadioActual = -1f; // fuerza a TickFollow a re-escalar con el radio actual
        }

        // Llamado cada frame desde TickFollow (mismo recorrido que ya mueve
        // el icono): un simple SetLocalScale es barato, y el alcance de
        // vision cambia con la postura/mira (StanceVisionMultiplier,
        // AmpliacionDeVisionActual), asi que no alcanza con fijarlo una sola
        // vez al detectar el target.
        void ActualizarRadioCono()
        {
            if (conoDeVision == null) return;
            float radio = brainDetectado != null ? brainDetectado.EffectiveVisionRange : ConoRadioFallback;
            if (Mathf.Abs(radio - conoRadioActual) < 0.05f) return;
            conoRadioActual = radio;
            var escalaIcono = transform.localScale;
            float sx = Mathf.Abs(escalaIcono.x) > 0.0001f ? radio / escalaIcono.x : radio;
            float sz = Mathf.Abs(escalaIcono.z) > 0.0001f ? radio / escalaIcono.z : radio;
            conoDeVision.localScale = new Vector3(sx, 1f, sz);
        }

        static Material NuevoMaterialTransparente(Color c)
        {
            var m = DiamondGizmo.NuevoMaterial(c);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
            }
            m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        static Mesh mallaCono;
        static Mesh MallaCono()
        {
            if (mallaCono != null) return mallaCono;
            const int segmentos = 10;
            float medioAnguloRad = ConoMedioAnguloGrados * Mathf.Deg2Rad;
            var m = new Mesh { name = "MinimapConoDeVision" };
            var verts = new Vector3[segmentos + 2];
            verts[0] = Vector3.zero; // apice, en el propio soldado
            for (int i = 0; i <= segmentos; i++)
            {
                float t = (float)i / segmentos;
                float ang = Mathf.Lerp(-medioAnguloRad, medioAnguloRad, t);
                // +Z es "adelante", mismo frente que MallaTriangulo.
                verts[i + 1] = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
            }
            var tris = new int[segmentos * 3 * 2];
            int k = 0;
            for (int i = 0; i < segmentos; i++)
            {
                tris[k++] = 0; tris[k++] = i + 1; tris[k++] = i + 2;
                tris[k++] = 0; tris[k++] = i + 2; tris[k++] = i + 1; // segunda cara, visto desde abajo
            }
            m.vertices = verts;
            m.triangles = tris;
            var normals = new Vector3[verts.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            m.normals = normals;
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            mallaCono = m;
            return m;
        }

        void EnsureRenderer()
        {
            if (selfRenderer == null) selfRenderer = GetComponent<MeshRenderer>();
        }

        // Alta y baja en el unico recorrido de UI de mundo. Mismo patron
        // que SP.Core.WorldSystemsRegistry.
        void OnEnable()
        {
            int expectedLayer = LayerMask.NameToLayer("Minimap");
            if (expectedLayer < 0) expectedLayer = 8;
            gameObject.layer = expectedLayer;

            EnsureRenderer();
            // Se arregla la escena en vivo y no solo la construccion nueva:
            // los iconos de SC_Gameplay estan serializados con la cuña, y
            // reconstruirlos a mano seria un diff de escena por soldado.
            //
            // Pedido explicito: "quiero q la UI de editor sea estimando los
            // valores pronosticados... antes del play y despues del play se
            // vea igual". Antes esto corria SOLO en Play, asi que el
            // minimapa en el editor mostraba los iconos sin pintar y sin
            // cono de vision hasta apretar Play. Como DetectarTarget() solo
            // lee datos ya serializados (Team, AiBrain) y no requiere
            // simulacion corriendo, es seguro correrlo tambien en Edit mode
            // (ver [ExecuteAlways] en la clase).
            if (esTriangulo || directionMarker != null) ConvertirEnTriangulo();
            DetectarTarget();
            WorldUiDirector.Register(this);
        }

        void OnDisable() => WorldUiDirector.Unregister(this);

        public bool IsRendered
        {
            get
            {
                EnsureRenderer();
                return selfRenderer != null && selfRenderer.enabled;
            }
        }

        // El director necesita el punto del mundo que representa el icono
        // sin tener que tocar Target por su cuenta.
        public Vector3 TargetPosition => Target != null ? Target.position : transform.position;

        // Antes esto era un LateUpdate propio: con cincuenta unidades eran
        // cincuenta LateUpdate por frame solo para copiar una posicion.
        // Ahora lo llama WorldUiDirector desde un unico recorrido.
        // Devuelve false si el icono se destruyo por quedarse sin objetivo.
        public bool TickFollow()
        {
            if (Target == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return false;
            }
            // BUG REAL reportado jugando: "al eliminar al enemigo no
            // desaparece del minimapa". El cuerpo del soldado muerto sigue
            // en la escena (no se destruye), asi que Target nunca pasaba a
            // null y el icono seguia seguido al cadaver para siempre.
            if (soldierDetectado != null && soldierDetectado.Health != null && !soldierDetectado.Health.IsAlive)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return false;
            }
            transform.position = new Vector3(Target.position.x, height, Target.position.z);
            // Antes esto solo giraba el icono si ya era un triangulo
            // (enemigos): un aliado es un circulo simetrico que no
            // necesitaba rotar. El cono de vision SI necesita la rotacion
            // real aunque el icono de abajo sea un circulo.
            if (esTriangulo || esDobleTriangulo || directionMarker != null || conoDeVision != null)
                transform.rotation = Quaternion.Euler(0f, Target.eulerAngles.y, 0f);
            if (autoColoreado) RepintarPorEquipo();
            ActualizarRadioCono();
            return true;
        }

        // Solo se llama en los pases de reevaluacion del director (cada
        // ~0.25 s), el mismo espaciado que tenia el timer propio de cada
        // icono: la niebla no necesita resolverse por frame. El calculo de
        // "spotted" vive en el director porque ahi se hace por lote para
        // todos los iconos a la vez.
        public void ApplyFog(bool spotted)
        {
            // Sin el guard de Application.isPlaying: WorldUiDirector no
            // tiene [ExecuteAlways], asi que su LateUpdate (el unico
            // camino real hacia este metodo) ya no corre en Edit mode por
            // si solo. El guard no protegia ningun caso real -- solo le
            // impedia a la suite headless (D1) verificar la niebla, que
            // hasta ahora no tenia ningun Check().
            if (!fogEnabled) return;
            EnsureRenderer();
            if (selfRenderer == null) return;
            selfRenderer.enabled = spotted;
            if (directionMarker != null) directionMarker.gameObject.SetActive(false); // los enemigos no llevan flecha
        }

        // Del plan del usuario: "En el minimapa hay 2 cubitos por soldado.
        // Corregirlo para q sea solo un triangulo simple".
        //
        // Eran dos de verdad: el disco (un Cylinder) MAS una cuña blanca
        // (un Cube) colgada adelante para indicar hacia donde mira. Desde
        // la camara cenital del minimapa eso no se lee como una unidad con
        // frente: se lee como dos manchas por soldado, y con la escuadra
        // junta el minimapa era un amontonamiento.
        //
        // Ahora es UNA sola pieza: el propio icono pasa a ser un triangulo
        // que apunta adonde mira la unidad. Misma cantidad de objetos que
        // un enemigo, la mitad que antes, y el frente se lee de un vistazo.
        [SerializeField] bool esTriangulo;

        // Bandera de un "doble triangulo"/bowtie que se documento (ver mas
        // abajo) pero cuya malla nunca se llego a escribir: nada la pone en
        // true todavia. Se declara igual porque TickFollow ya la consulta
        // para decidir si el icono necesita rotar con la unidad.
        bool esDobleTriangulo;

        // Compartida por todos los iconos: no tiene sentido una malla de
        // tres vertices por soldado.
        static Mesh mallaTriangulo;

        static Mesh MallaTriangulo()
        {
            if (mallaTriangulo != null) return mallaTriangulo;
            var m = new Mesh();
            m.name = "MinimapTriangulo";
            // Chato en XZ y apuntando a +Z, que es el frente de la unidad.
            // Encaja en el mismo circulo de radio 0,5 que ocupaba el disco,
            // asi que el localScale que ya tenia el icono sigue sirviendo.
            m.vertices = new[]
            {
                new Vector3(0f, 0f, 0.55f),
                new Vector3(-0.45f, 0f, -0.40f),
                new Vector3(0.45f, 0f, -0.40f),
            };
            // Las dos vueltas: el triangulo se ve desde arriba y desde
            // abajo. Una sola cara obliga a acertar el sentido de giro, y
            // si se erra el icono desaparece sin ningun error en consola.
            m.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            mallaTriangulo = m;
            return m;
        }

        // El nombre historico se conserva: sigue significando "esta unidad
        // muestra hacia donde mira". Lo que cambio es COMO lo muestra.
        public void EnableDirectionMarker(int layer, float iconRadius)
        {
            ConvertirEnTriangulo();
        }

        public bool ConvertirEnTriangulo()
        {
            // La cuña vieja se borra aunque ya no la cree nadie: los
            // soldados de la escena YA la tienen guardada en el .unity, y
            // sin esto seguirian con los dos cubitos para siempre.
            if (directionMarker != null)
            {
                var go = directionMarker.gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                directionMarker = null;
            }

            var filtro = GetComponent<MeshFilter>();
            if (filtro == null) return false;
            filtro.sharedMesh = MallaTriangulo();
            esTriangulo = true;
            return true;
        }

        // Interior mas chico del "triangulo anidado" de abajo. Propio (no
        // reusa directionMarker): ese campo lo destruye OnEnable en cuanto
        // detecta uno serializado desde la escena horneada (limpieza de la
        // cuña vieja, ver ConvertirEnTriangulo mas arriba) y se llevaria
        // puesto el relleno de color apenas el icono se reactive.
        Transform trianguloAnidadoInterior;

        static Mesh mallaTrianguloChico;
        static Mesh MallaTrianguloChico()
        {
            if (mallaTrianguloChico != null) return mallaTrianguloChico;
            var m = new Mesh { name = "MinimapTrianguloChico" };
            m.vertices = new[]
            {
                new Vector3(0f, 0f, 0.40f),
                new Vector3(-0.32f, 0f, -0.30f),
                new Vector3(0.32f, 0f, -0.30f),
            };
            m.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            mallaTrianguloChico = m;
            return m;
        }

        // Pedido explicito (ObjectiveDiamondMarker): "triangulo anidado" --
        // blanco de fondo (mas grande, contorno) + relleno de color (mas
        // chico, encima), mismo patron de capas que el rombo de mundo
        // (DiamondGizmo) pero chato en XZ para verse desde la camara
        // cenital del minimapa en vez de billboardeado a camara.
        public bool ConvertirEnTrianguloAnidado(Color colorInterior)
        {
            var filtro = GetComponent<MeshFilter>();
            if (filtro == null) return false;
            filtro.sharedMesh = MallaTriangulo();
            EnsureRenderer();
            if (selfRenderer != null) selfRenderer.sharedMaterial = DiamondGizmo.NuevoMaterial(Color.white);

            if (trianguloAnidadoInterior == null)
            {
                var go = new GameObject("TrianguloInterior", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                // Un pelo mas arriba (no adelante, como en los rombos
                // billboardeados) para no competir en Z contra el
                // contorno: aca ambas piezas quedan chatas en el mismo
                // plano XZ, vistas desde arriba.
                go.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                trianguloAnidadoInterior = go.transform;
            }
            var mf = trianguloAnidadoInterior.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = MallaTrianguloChico();
            var mr = trianguloAnidadoInterior.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = DiamondGizmo.NuevoMaterial(colorInterior);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            esTriangulo = true;
            return true;
        }

        // Pedido explicito: "doble triangulo" -- un icono con mas superficie
        // y mas contraste que la flecha fina de un solo triangulo, leyendose
        // como un "moño"/reloj de arena: una punta apunta hacia donde mira la
        // unidad (igual que el triangulo simple) y la otra, mas chica, hacia
        // atras, para que el blip se note incluso a los zooms mas alejados
        // del minimapa.
        static Mesh mallaCirculo;
        static Mesh MallaCirculo()
        {
            if (mallaCirculo != null) return mallaCirculo;
            var m = new Mesh { name = "MinimapCirculo" };
            int segments = 16;
            var v = new Vector3[segments + 1];
            v[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                v[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
            }
            m.vertices = v;
            var t = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = i + 1 < segments ? i + 2 : 1;
                t[i * 6] = 0; t[i * 6 + 1] = i + 1; t[i * 6 + 2] = next;
                t[i * 6 + 3] = 0; t[i * 6 + 4] = next; t[i * 6 + 5] = i + 1;
            }
            m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            mallaCirculo = m;
            return m;
        }

        public bool ConvertirEnCirculo()
        {
            if (directionMarker != null)
            {
                var go = directionMarker.gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                directionMarker = null;
            }
            var filtro = GetComponent<MeshFilter>();
            if (filtro == null) return false;
            filtro.sharedMesh = MallaCirculo();
            esTriangulo = false;
            return true;
        }

        static Mesh mallaRombo;
        static Mesh MallaRombo()
        {
            if (mallaRombo != null) return mallaRombo;
            var m = new Mesh { name = "MinimapRombo" };
            const float r = 0.5f;
            m.vertices = new[]
            {
                new Vector3(0f, 0f, r),
                new Vector3(r, 0f, 0f),
                new Vector3(0f, 0f, -r),
                new Vector3(-r, 0f, 0f),
            };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            mallaRombo = m;
            return m;
        }

        public bool ConvertirEnRombo()
        {
            if (directionMarker != null)
            {
                var go = directionMarker.gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                directionMarker = null;
            }
            var filtro = GetComponent<MeshFilter>();
            if (filtro == null) return false;
            filtro.sharedMesh = MallaRombo();
            esTriangulo = false;
            return true;
        }

        [SerializeField] bool esCuadrado;
        public bool EsCuadrado => esCuadrado;

        static Mesh mallaCuadrado;

        static Mesh MallaCuadrado()
        {
            if (mallaCuadrado != null) return mallaCuadrado;
            var m = new Mesh();
            m.name = "MinimapCuadrado";
            // Mismo radio 0,5 que el disco/triangulo, para que el
            // localScale del icono (radius, 0.2, radius) siga sirviendo.
            const float lado = 0.5f;
            m.vertices = new[]
            {
                new Vector3(-lado, 0f, -lado),
                new Vector3(-lado, 0f, lado),
                new Vector3(lado, 0f, lado),
                new Vector3(lado, 0f, -lado),
            };
            // Dos caras (arriba y abajo), mismo motivo que MallaTriangulo:
            // una sola cara desaparece el icono si se erra el sentido de
            // giro visto desde la camara cenital.
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            mallaCuadrado = m;
            return m;
        }

        public bool ConvertirEnCuadrado()
        {
            var filtro = GetComponent<MeshFilter>();
            if (filtro == null) return false;
            filtro.sharedMesh = MallaCuadrado();
            esCuadrado = true;
            return true;
        }

        // Color fijo para los obstaculos del minimapa (D1): no son un
        // bando -- no atacan, no se poseen -- asi que no comparten paleta
        // con el azul de la escuadra ni el rojo enemigo. Pedido explicito:
        // "muchisimo contraste" y "los rectangulos gris claro, fondo negro
        // oscuro" -- el gris piedra apagado de antes casi se fundia con el
        // fondo del minimapa; un gris CLARO sobre el fondo casi negro
        // (MinimapFollow.ColorDeFondo) se lee de un vistazo.
        public static readonly Color ObstacleMinimapColor = new Color(0.80f, 0.82f, 0.85f);

        const string ObstaclesRootName = "ObstaculoIconosRoot";

        // Un icono de minimapa por cada ObstacleMarker de la escena (D1).
        // Los soldados y vehiculos ya traian el suyo puesto a mano en
        // SC_Gameplay, pero nadie armaba el de los obstaculos: el
        // minimapa no decia nada del terreno hasta acercarse. Idempotente
        // por destruir-y-rearmar (mismo patron que
        // SP.Core.Coberturas.Registrar): llamarlo de nuevo no duplica.
        public static int RegistrarVehiculosLibres()
        {
            int count = 0;
            int layer = LayerMask.NameToLayer("Minimap");
            if (layer < 0) layer = 8;
            var root = SP.Core.RaicesDeEscena.Buscar("VehiculosIconosRoot");
            if (root == null) root = new GameObject("VehiculosIconosRoot");

            foreach (var v in SP.Vehicles.Vehicle.Todos)
            {
                if (v.GetComponentInChildren<MinimapIcon>() == null)
                {
                    var icon = Spawn(v.transform, Color.gray, layer, 2.0f);
                    icon.transform.SetParent(root.transform, true);
                    icon.ConvertirEnCuadrado();
                    count++;
                }
            }
            return count;
        }

        public static int RegistrarObstaculos(Color color, float radius = 1.4f)
        {
            var previo = SP.Core.RaicesDeEscena.Buscar(ObstaclesRootName);
            if (previo != null)
            {
                if (Application.isPlaying) Destroy(previo);
                else DestroyImmediate(previo);
            }

            int layer = LayerMask.NameToLayer("Minimap");
            if (layer < 0) layer = 8; // TagManager trae "Minimap" fijo en el indice 8.

            var root = new GameObject(ObstaclesRootName).transform;
            SP.Core.WorldSystemsRegistry.EnsurePopulated();
            var marcas = SP.Core.WorldSystemsRegistry.Obstacles;
            foreach (var marca in marcas)
            {
                var icon = Spawn(marca.transform, color, layer, radius);
                icon.transform.SetParent(root, true);
                // C2: cuadrado, no circulo -- lo interactuable se
                // distingue de una unidad por la FORMA, no solo el color.
                icon.ConvertirEnCuadrado();

                // Un muro de 20 m o una casa no pueden verse como el mismo
                // cuadradito que un barril: el icono toma la huella real
                // (en el plano XZ) del obstaculo, con el tamaño de siempre
                // como piso.
                var col = marca.GetComponent<Collider>();
                if (col != null)
                {
                    var tam = col.bounds.size;
                    icon.transform.localScale = new Vector3(Mathf.Max(radius, tam.x), 0.2f, Mathf.Max(radius, tam.z));
                }
            }
            return marcas.Count;
        }

        public static MinimapIcon Spawn(Transform target, Color color, int layer, float radius = 1.6f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "MinimapIcon";
            go.layer = layer;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.transform.localScale = new Vector3(radius, 0.2f, radius);

            var rend = go.GetComponent<MeshRenderer>();
            // Unlit (no SafeMaterial.Create, que es Lit): mismo bug de fondo
            // que ya se encontro en los rombos de mundo -- de noche, con
            // ambiente casi negro y luz de luna azulada, un material Lit
            // apaga los colores que no tengan componente azul (el rojo de
            // un enemigo, el gris claro de un obstaculo) justo cuando mas
            // contraste hace falta en el minimapa.
            rend.sharedMaterial = DiamondGizmo.NuevoMaterial(color);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var icon = go.AddComponent<MinimapIcon>();
            icon.Target = target;
            return icon;
        }

        // Evita el doble icono cuando la escena ya trae uno horneado por
        // MinimapIconBakePipeline (editor) y ademas algo llama Spawn() de
        // nuevo al entrar en Play (domain reload -> OnEnable de vuelta en
        // el spawneador, ver UnitLocatorCylinder). Barrido directo por
        // Target en vez de confiar en el orden de OnEnable entre objetos
        // distintos, que Unity no garantiza.
        public static bool ExisteIconoPara(Transform target) => BuscarPorTarget(target) != null;

        // El icono de un vehiculo NO es su hijo (ver MinimapIconBakePipeline:
        // ponerlo como hijo hacia que Vehicle.RefreshOccupancyColor lo
        // agarrara con GetComponentsInChildren<Renderer>() y le pisara el
        // material al re-tintar el chasis -- BUG REAL, NullReferenceException
        // en Vehicle.cs:250 la primera vez que alguien subia/bajaba).
        // Vehicle.cs usa esto para encontrar "su" icono sin depender de la
        // jerarquia.
        public static MinimapIcon BuscarPorTarget(Transform target)
        {
            if (target == null) return null;
            foreach (var icon in FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (icon.Target == target) return icon;
            return null;
        }
    }
}
