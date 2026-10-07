using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Mini-explosion al impactar: una esfera que se agranda rapido y
    // despues se achica hasta desaparecer, en el punto exacto del choque.
    // Un color distinto por tipo de superficie (enemigo/vehiculo/obstaculo/
    // suelo) para que se note a simple vista que le pego a que, igual que
    // el flash de la mirilla pero en el mundo, no en la UI.
    //
    // PRESUPUESTO DE EFECTOS: esto se llama en CADA impacto de bala. Antes
    // cada llamada hacia CreatePrimitive + Shader.Find + new Material y
    // confiaba en una corrutina para autodestruirse. Shader.Find es de las
    // llamadas mas caras del motor: en un tiroteo de escuadra eran decenas
    // por segundo, mas decenas de materiales que despues junta el
    // recolector con el tiron consiguiente. Ahora hay UN material
    // compartido (Shader.Find resuelto una sola vez, perezoso) y dos pools
    // con tope duro que reciclan el efecto mas viejo en vez de crear uno
    // nuevo -- el mismo criterio que DebrisPool, DecalPool y
    // MuzzleLightPool.
    //
    // La animacion pasa de corrutina a Update() por dos motivos: una
    // corrutina por spawn tambien es basura por impacto, y un objeto
    // pooleado puede ser robado a mitad de animacion (habria que acordarse
    // de cortar la corrutina). Con un temporizador propio el reciclado es
    // trivial.
    public class ImpactFx : MonoBehaviour
    {
        public static readonly Color EnemyColor = new Color(0.95f, 0.25f, 0.15f);
        public static readonly Color VehicleColor = new Color(0.3f, 0.55f, 0.95f);
        public static readonly Color ObstacleColor = new Color(0.75f, 0.75f, 0.78f);
        public static readonly Color GroundColor = new Color(0.55f, 0.42f, 0.28f);
        public static readonly Color ExplosionColor = new Color(0.95f, 0.55f, 0.1f);

        static readonly Color DustColor = new Color(0.62f, 0.56f, 0.46f);
        static readonly Color ArmorSparkColor = new Color(1f, 0.9f, 0.55f);

        // CUPOS. Las esferas son el efecto por impacto de bala y viven
        // 0.35 s como mucho: 48 en simultaneo aguantan mas de 130 impactos
        // por segundo (una escuadra entera disparando) antes de tener que
        // reciclar. Los anillos de onda expansiva solo salen en granadas de
        // tanque, son mucho mas raros y viven 0.6 s: con 8 sobra, y si
        // alguna vez hay 9 explosiones a la vez el anillo mas viejo es
        // justamente el que ya termino de enseniar su radio.
        public const int SphereBudget = 48;
        public const int RingBudget = 8;

        // Contadores de solo lectura para verificacion: ActiveCount NUNCA
        // puede pasar de Budget, ese es el punto de un tope duro.
        public static int Budget => SphereBudget + RingBudget;
        public static int ActiveCount => spheres.ActiveCount + rings.ActiveCount;

        static readonly ImpactFxPool spheres = new ImpactFxPool("ImpactFxPool", SphereBudget, CreateSphere);
        static readonly ImpactFxPool rings = new ImpactFxPool("ShockwaveRingPool", RingBudget, CreateRing);

        // Anillos de REVIVIR (bug #089): pool propio, asi 3 anillos por revivido no le sacan el cupo a las explosiones. No cuenta en
        // Budget/ActiveCount (los tests de arriba hablan de las esferas y los anillos de explosion).
        public const int ReviveRingBudget = 8;
        static readonly ImpactFxPool reviveRings = new ImpactFxPool("ReviveRingPool", ReviveRingBudget, CreateReviveRing);
        public static int ReviveRingsActive => reviveRings.ActiveCount;
        static Material materialDeAnillo;
        static Material MaterialDeAnillo
        {
            get
            {
                if (materialDeAnillo == null) materialDeAnillo = SafeMaterial.CreateLinea(Color.white);
                return materialDeAnillo;
            }
        }

        // --- Material compartido -------------------------------------

        static Material sharedMaterial;

        // El Shader.Find se paga UNA vez en toda la partida, no una por
        // impacto. El material puede quedar destruido por una recarga de
        // escena (no es un asset guardado), por eso el getter lo rehace en
        // vez de asumir que sigue vivo.
        static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null) sharedMaterial = SafeMaterial.CreateShared();
                return sharedMaterial;
            }
        }

        static MaterialPropertyBlock propertyBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // Con el material COMPARTIDO, escribir renderer.sharedMaterial.color
        // pintaria de ese color TODOS los impactos vivos a la vez (el chispazo
        // de blindaje volveria naranja la explosion de al lado). El color va
        // por MaterialPropertyBlock, que es por-renderer.
        //
        // Se escriben las dos propiedades porque el shader puede ser el de
        // URP (_BaseColor) o el Unlit/Color de fallback (_Color): cual de los
        // dos existe depende de cual encontro el Shader.Find.
        static void ApplyColor(Renderer rend, Color c)
        {
            if (rend == null) return;
            if (rend.sharedMaterial == null) rend.sharedMaterial = SharedMaterial;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            propertyBlock.SetColor(ColorId, c);
            rend.SetPropertyBlock(propertyBlock);
        }

        // --- Fabricas de los dos tipos de efecto ---------------------

        static ImpactFx CreateSphere(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImpactFx";
            // hideFlags va en CADA pieza, no solo en el root: los flags NO
            // se heredan (ver DebrisPool). Sin esto lo creado en tiempo de
            // edicion queda serializado en la escena y al entrar en Play
            // mode conviven los guardados con los del pool nuevo.
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = SharedMaterial;

            var fx = go.AddComponent<ImpactFx>();
            go.SetActive(false);
            return fx;
        }

        static ImpactFx CreateRing(Transform parent)
        {
            var go = new GameObject("ShockwaveRing");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.18f;
            line.positionCount = 36;
            line.sharedMaterial = SharedMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fx = go.AddComponent<ImpactFx>();
            go.SetActive(false);
            return fx;
        }

        // Anillo plano en el piso, con alfa por vertice (material sin luz que respeta el color de la linea).
        static ImpactFx CreateReviveRing(Transform parent)
        {
            var go = new GameObject("ReviveRing");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = ReviveRingWidth;
            line.positionCount = 48;
            line.sharedMaterial = MaterialDeAnillo;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            var fx = go.AddComponent<ImpactFx>();
            go.SetActive(false);
            return fx;
        }

        // Entrar en Play mode NO destruye los objetos de la escena, pero SI
        // reinicia los estaticos: el root creado en tiempo de edicion sigue
        // vivo mientras las listas que lo indexaban quedan vacias, y sus
        // hijos pasan a ser huerfanos fuera de todo cupo. Al rearmarse, el
        // pool barre lo que quedo suelto. Ojo: las esferas y los anillos
        // comparten el componente ImpactFx, asi que hay que preguntarle a
        // los DOS pools antes de dar algo por huerfano.
        // Vaciado real, no reciclado: destruye los objetos y el root para
        // que una escena nueva empiece sin nada del anterior. Ver
        // SP.Presentation.LimpiezaDeEscena, que es quien lo llama.
        public static void ClearAll()
        {
            spheres.LimpiarTodo();
            rings.LimpiarTodo();
            reviveRings.LimpiarTodo();
            DestroyOrphans();
        }

        internal static void DestroyOrphans()
        {
            foreach (var fx in Object.FindObjectsByType<ImpactFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fx == null) continue;
                if (spheres != null && spheres.Contains(fx)) continue;
                if (rings != null && rings.Contains(fx)) continue;
                if (reviveRings != null && reviveRings.Contains(fx)) continue;
                if (Application.isPlaying) Object.Destroy(fx.gameObject);
                else Object.DestroyImmediate(fx.gameObject);
            }
        }

        // Devuelve todo lo activo al pool. Existe para el Edit mode, donde
        // Update() NO corre y por lo tanto ningun efecto se recicla solo:
        // sin esto, la suite headless dejaria el cupo ocupado para siempre.
        public static void RecycleAll()
        {
            spheres.RecycleAll();
            rings.RecycleAll();
            reviveRings.RecycleAll();
        }

        // --- API publica ---------------------------------------------

        public static void Spawn(Vector3 position, Color color, float peakScale = 0.55f, float duration = 0.35f)
        {
            var fx = spheres.Take();
            if (fx == null) return;
            fx.LaunchGrowShrink(position, color, peakScale, duration);
        }

        static bool shaderWarmed;

        // Igual que OrderMarkerFx.Prewarm: compila el shader lejos y chico
        // apenas se arma el nivel, asi el primer impacto real del jugador no
        // se traba (ni sale negro si justo se saca una captura ahi).
        public static void Prewarm()
        {
            if (shaderWarmed) return;
            shaderWarmed = true;
            Spawn(new Vector3(0f, -500f, 0f), EnemyColor, 0.1f, 0.05f);
            // En Edit mode nada avanza el temporizador: se devuelve a mano
            // para no dejar una plaza del cupo tomada.
            if (!Application.isPlaying) RecycleAll();
        }

        // Antes todos los impactos generaban el mismo efecto: un obus de
        // tanque se sentia igual que una bala de pistola y se perdia la
        // jerarquia entre armas. El danio ya viaja en el Projectile.
        public static void SpawnScaledByDamage(Vector3 position, Color color, int damage)
        {
            float scale = Mathf.Lerp(0.35f, 1.4f, Mathf.InverseLerp(5f, 60f, damage));
            Spawn(position, color, 0.55f * scale, 0.35f);
        }

        // EnvironmentHitKind ya distinguia el vehiculo, pero el efecto era el
        // mismo que contra el suelo: no se percibia que el blindaje resiste.
        // Chispas rapidas que salen rebotadas, no polvo de tierra.
        public static void SpawnArmorSparks(Vector3 position, Vector3 surfaceNormal)
        {
            Spawn(position, ArmorSparkColor, 0.3f, 0.12f);
            for (int i = 0; i < 5; i++)
            {
                var dir = Vector3.Slerp(surfaceNormal, Random.onUnitSphere, 0.55f).normalized;
                DebrisPool.Spawn(position, dir * Random.Range(6f, 11f), ArmorSparkColor, Random.Range(0.05f, 0.09f), 0.5f);
            }
        }

        // Granada de tanque: la esfera representa el radio de danio real (no
        // un tamanio cosmetico fijo), y se achica bruscamente en vez de un
        // lerp parejo como el impacto chico normal -- crece rapido, aguanta
        // un instante en el pico, y colapsa de golpe.
        public static void SpawnExplosion(Vector3 position, float radius) => SpawnExplosion(position, radius, true);

        // Bug #072: el crater/decal quedaba en y = 0,02-0,04 y la calle esta en 0,08: la marca del obus estaba ENTERRADA. Ahora se mide el
        // piso bajo el punto (asfalto incluido) y la marca y el anillo se apoyan encima; ademas queda un CRATER con borde (CraterPool).
        // conCrater = false cuando varias explosiones seguidas caen en el mismo lugar (un vehiculo que revienta en tres tiempos).
        public static void SpawnExplosion(Vector3 position, float radius, bool conCrater)
        {
            // Ronda 12: la esfera naranja era un placeholder. Ahora es una explosion de sprites reales (fuego, humo, tierra, chispas).
            // Si por algun motivo faltan los sprites (Resources), se cae a la esfera de antes.
            if (SpriteFx.Explosion(position, radius) == 0)
            {
                var fx = spheres.Take();
                if (fx != null) fx.LaunchExplosion(position, radius * 2f);
            }

            // La esfera dice DONDE, pero se colapsa rapido y es dificil leer
            // HASTA DONDE llego. El anillo se expande exactamente hasta
            // explosionRadius sobre el suelo y se queda ahi un instante: es
            // lo que permite aprender el alcance real y evitar el fuego
            // amigo.
            float pisoY = Application.isPlaying ? DestruccionDeVehiculo.AlturaDelPiso(position) : 0f;
            var anillo = rings.Take();
            if (anillo != null) anillo.LaunchRing(new Vector3(position.x, pisoY + 0.06f, position.z), radius);
            DecalPool.Spawn(DecalKind.Crater, new Vector3(position.x, Mathf.Max(pisoY, 0.02f), position.z), Vector3.up, radius * 1.4f);
            if (conCrater && radius >= 3.5f && position.y - pisoY <= 2.5f)
                CraterPool.Crear(position, pisoY, radius * 0.55f);
            SpawnDustCloud(position, radius);

            // Escombros del punto de impacto, del pool compartido.
            for (int i = 0; i < 10; i++)
            {
                var dir = (Random.insideUnitSphere + Vector3.up).normalized;
                DebrisPool.Spawn(position, dir * Random.Range(4f, 9f), new Color(0.4f, 0.32f, 0.24f), Random.Range(0.1f, 0.22f));
            }
        }

        public static void SpawnShockwaveRing(Vector3 center, float radius)
        {
            var fx = rings.Take();
            if (fx == null) return;
            fx.LaunchRing(new Vector3(center.x, 0.06f, center.z), radius);
        }

        // Anillo de color que se expande desde 'radioInicial' hasta 'radioFinal' en 'duracion' segundos y se desvanece (alfa 0.9 a 0),
        // plano sobre el piso. 'demora' lo escalona respecto de otros anillos. Va por su propio pool de ReviveRingBudget.
        public static void SpawnShockwaveRing(Vector3 center, Color color, float radioFinal, float duracion, float demora = 0f, float radioInicial = 0.4f)
        {
            var fx = reviveRings.Take();
            if (fx == null) return;
            fx.LaunchReviveRing(center, color, radioInicial, radioFinal, Mathf.Max(0.05f, duracion), Mathf.Max(0f, demora));
        }

        // Nube breve que ensucia la zona y se disipa. Va por el mismo
        // presupuesto de escombros para que no se acumule: una explosion no
        // puede costar mas que su cupo.
        static void SpawnDustCloud(Vector3 center, float radius)
        {
            for (int i = 0; i < 6; i++)
            {
                var offset = Random.insideUnitSphere * radius * 0.6f;
                offset.y = Mathf.Abs(offset.y) * 0.3f;
                DebrisPool.Spawn(center + offset, Vector3.up * Random.Range(0.4f, 1.1f), DustColor, Random.Range(0.5f, 0.9f), 1.4f);
            }
        }

        // --- Instancia ------------------------------------------------

        enum FxMode { None, GrowShrink, Explode, Ring, ReviveRing }

        FxMode mode;
        float age;
        float peak;
        float duration;
        Vector3 ringCenter;
        float ringRadius;
        float ringRadius0, ringDelay;
        Color ringColor;
        const float ReviveRingWidth = 0.28f;
        const float ReviveRingAlpha = 0.9f;

        Renderer cachedRenderer;
        LineRenderer cachedLine;

        // Los campos privados de un MonoBehaviour no sobreviven a un domain
        // reload: se vuelven a buscar en vez de darlos por cacheados.
        Renderer Rend
        {
            get
            {
                if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
                return cachedRenderer;
            }
        }

        LineRenderer Line
        {
            get
            {
                if (cachedLine == null) cachedLine = GetComponent<LineRenderer>();
                return cachedLine;
            }
        }

        void LaunchGrowShrink(Vector3 position, Color color, float peakScale, float durationSeconds)
        {
            gameObject.SetActive(true);
            transform.position = position;
            transform.localScale = Vector3.zero;
            ApplyColor(Rend, color);
            peak = peakScale;
            duration = Mathf.Max(0.01f, durationSeconds);
            age = 0f;
            mode = FxMode.GrowShrink;
        }

        void LaunchExplosion(Vector3 position, float peakDiameter)
        {
            gameObject.SetActive(true);
            transform.position = position;
            transform.localScale = Vector3.zero;
            ApplyColor(Rend, ExplosionColor);
            peak = peakDiameter;
            age = 0f;
            mode = FxMode.Explode;
        }

        void LaunchRing(Vector3 center, float radius)
        {
            gameObject.SetActive(true);
            transform.position = center;
            var line = Line;
            if (line == null) { Recycle(); rings.Release(this); return; }

            line.widthMultiplier = 0.18f;
            ApplyColor(line, ExplosionColor);
            ringCenter = center;
            ringRadius = radius;
            age = 0f;
            mode = FxMode.Ring;
            // Se redibuja YA en radio 0: si no, el primer frame mostraria
            // las posiciones (en coordenadas de mundo) del anillo anterior
            // que ocupaba este mismo objeto.
            DrawRing(line, center, 0f);
        }

        void LaunchReviveRing(Vector3 center, Color color, float r0, float r1, float dur, float delay)
        {
            gameObject.SetActive(true);
            var line = Line;
            if (line == null) { Recycle(); reviveRings.Release(this); return; }
            ringCenter = center;
            ringRadius0 = r0;
            ringRadius = r1;
            duration = dur;
            ringDelay = delay;
            ringColor = color;
            age = 0f;
            mode = FxMode.ReviveRing;
            line.enabled = false;   // en demora no se ve; el primer cuadro visible ya sale con el radio y el alfa correctos
            DrawRing(line, center, r0);
        }

        void TickReviveRing()
        {
            var line = Line;
            if (line == null) { Finish(); return; }
            float t = age - ringDelay;
            if (t < 0f) return;
            float k = Mathf.Clamp01(t / duration);
            if (!line.enabled) line.enabled = true;
            float e = 1f - (1f - k) * (1f - k);   // sale rapido y frena: se lee como una onda
            DrawRing(line, ringCenter, Mathf.Lerp(ringRadius0, ringRadius, e));
            var c = ringColor; c.a = ReviveRingAlpha * (1f - k);
            line.startColor = c; line.endColor = c;
            line.widthMultiplier = Mathf.Lerp(ReviveRingWidth, ReviveRingWidth * 0.5f, k);
            if (k >= 1f) Finish();
        }

        const float GrowFraction = 0.35f;

        const float ExplodeGrowTime = 0.12f;
        const float ExplodeHoldTime = 0.05f;
        const float ExplodeCollapseTime = 0.1f;

        const float RingExpandTime = 0.28f;
        const float RingHoldTime = 0.12f;
        const float RingFadeTime = 0.2f;
        const float RingWidth = 0.18f;

        void Update()
        {
            if (mode == FxMode.None) return;
            age += Time.deltaTime;
            switch (mode)
            {
                case FxMode.GrowShrink: TickGrowShrink(); break;
                case FxMode.Explode: TickExplode(); break;
                case FxMode.Ring: TickRing(); break;
                case FxMode.ReviveRing: TickReviveRing(); break;
            }
        }

        void TickGrowShrink()
        {
            float growTime = duration * GrowFraction;
            if (age < growTime)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(0f, peak, age / growTime);
            }
            else if (age < duration)
            {
                float k = (age - growTime) / (duration - growTime);
                transform.localScale = Vector3.one * Mathf.Lerp(peak, 0f, k);
            }
            else
            {
                Finish();
            }
        }

        void TickExplode()
        {
            const float holdEnd = ExplodeGrowTime + ExplodeHoldTime;
            const float total = holdEnd + ExplodeCollapseTime;

            if (age < ExplodeGrowTime)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(0f, peak, age / ExplodeGrowTime);
            }
            else if (age < holdEnd)
            {
                transform.localScale = Vector3.one * peak;
            }
            else if (age < total)
            {
                // Ease-in cubico: arranca lento y se precipita al final, se
                // siente mas "de golpe" que un lerp lineal parejo.
                float k = (age - holdEnd) / ExplodeCollapseTime;
                transform.localScale = Vector3.one * Mathf.Lerp(peak, 0f, k * k * k);
            }
            else
            {
                Finish();
            }
        }

        void TickRing()
        {
            const float holdEnd = RingExpandTime + RingHoldTime;
            const float total = holdEnd + RingFadeTime;

            var line = Line;
            if (line == null) { Finish(); return; }

            if (age < RingExpandTime)
            {
                DrawRing(line, ringCenter, Mathf.Lerp(0f, ringRadius, age / RingExpandTime));
            }
            else if (age < holdEnd)
            {
                // Se planta EXACTAMENTE en el radio real antes de irse: ese
                // instante es el que ensenia el alcance.
                DrawRing(line, ringCenter, ringRadius);
            }
            else if (age < total)
            {
                DrawRing(line, ringCenter, ringRadius);
                line.widthMultiplier = Mathf.Lerp(RingWidth, 0f, (age - holdEnd) / RingFadeTime);
            }
            else
            {
                Finish();
            }
        }

        static void DrawRing(LineRenderer line, Vector3 center, float radius)
        {
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = (float)i / n * Mathf.PI * 2f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        void Finish()
        {
            var anterior = mode;
            Recycle();
            if (anterior == FxMode.ReviveRing) reviveRings.Release(this);
            else if (anterior == FxMode.Ring) rings.Release(this);
            else spheres.Release(this);
        }

        // No destruye: apaga y deja el objeto listo para el proximo uso. Es
        // lo que convierte el cupo en un tope real en vez de una sugerencia.
        public void Recycle()
        {
            mode = FxMode.None;
            age = 0f;
            gameObject.SetActive(false);
        }
    }

    // Pool con tope duro, misma mecanica que DebrisPool (cola de libres,
    // lista de en-uso ordenada por antiguedad, reciclado del mas viejo al
    // llegar al tope) pero parametrizada por la fabrica, porque ImpactFx
    // maneja dos familias de objetos distintas (esferas y anillos) con el
    // mismo componente.
    sealed class ImpactFxPool
    {
        readonly string rootName;
        readonly System.Func<Transform, ImpactFx> factory;

        readonly List<ImpactFx> all = new List<ImpactFx>();
        readonly Queue<ImpactFx> free = new Queue<ImpactFx>();
        // Orden de uso: el frente es el efecto activo mas viejo, el primero
        // en ser reciclado cuando hace falta lugar.
        readonly List<ImpactFx> inUse = new List<ImpactFx>();
        Transform root;

        public int Budget { get; private set; }

        public ImpactFxPool(string rootName, int budget, System.Func<Transform, ImpactFx> factory)
        {
            this.rootName = rootName;
            this.factory = factory;
            Budget = budget;
        }

        public int ActiveCount { get { Purge(); return inUse.Count; } }
        public int TotalCount { get { Purge(); return all.Count; } }

        public bool Contains(ImpactFx fx) => all.Contains(fx);

        // El pool es estado de runtime: no sobrevive a un domain reload ni a
        // un cambio de escena, y las referencias quedan apuntando a objetos
        // destruidos. Se limpia y se rearma solo.
        public void ResetIfStale()
        {
            if (root != null) return;
            all.Clear();
            free.Clear();
            inUse.Clear();
        }

        // Una reconstruccion de escena destruye los GameObjects pero no
        // vacia estas listas: quedan entradas "fake-null" de Unity que pasan
        // un chequeo de referencia de C# pero explotan al tocarles el
        // transform. Hay que purgarlas antes de repartir.
        void Purge()
        {
            all.RemoveAll(x => x == null);
            inUse.RemoveAll(x => x == null);
        }

        void EnsureRoot()
        {
            if (root != null) return;
            ImpactFx.DestroyOrphans();
            var go = new GameObject(rootName);
            // DontSaveInEditor|DontSaveInBuild y NO DontSave: DontSave ademas
            // impide destruir el objeto al cargar una escena, con lo cual el
            // root de tiempo de edicion sobreviviria como huerfano. Ver el
            // comentario largo en DebrisPool.
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root = go.transform;
        }

        public ImpactFx Take()
        {
            ResetIfStale();
            EnsureRoot();
            Purge();

            ImpactFx fx = null;
            while (free.Count > 0 && fx == null) fx = free.Dequeue();

            if (fx == null)
            {
                // La segunda condicion es una red de seguridad: si por una
                // purga quedara el cupo lleno pero nada en uso, no habria a
                // quien reciclar y indexar inUse[0] reventaria.
                if (all.Count < Budget || inUse.Count == 0)
                {
                    fx = factory(root);
                    all.Add(fx);
                }
                else
                {
                    // Presupuesto agotado: se recicla el mas viejo EN USO. Es
                    // lo que hace que el tope sea real.
                    fx = inUse[0];
                    inUse.RemoveAt(0);
                    fx.Recycle();
                }
            }

            inUse.Add(fx);
            return fx;
        }

        public void Release(ImpactFx fx)
        {
            if (fx == null) return;
            inUse.Remove(fx);
            if (!free.Contains(fx)) free.Enqueue(fx);
        }

        // Destruye de verdad. RecycleAll solo devuelve al pool: los
        // objetos siguen existiendo, que es justo lo que sobrevivia a la
        // recarga de escena.
        public void LimpiarTodo()
        {
            for (int i = 0; i < all.Count; i++)
            {
                var fx = all[i];
                if (fx == null) continue;
                if (Application.isPlaying) Object.Destroy(fx.gameObject);
                else Object.DestroyImmediate(fx.gameObject);
            }
            all.Clear();
            free.Clear();
            inUse.Clear();
            if (root != null)
            {
                if (Application.isPlaying) Object.Destroy(root.gameObject);
                else Object.DestroyImmediate(root.gameObject);
                root = null;
            }
        }

        public void RecycleAll()
        {
            Purge();
            for (int i = inUse.Count - 1; i >= 0; i--)
            {
                var fx = inUse[i];
                if (fx == null) continue;
                fx.Recycle();
                if (!free.Contains(fx)) free.Enqueue(fx);
            }
            inUse.Clear();
        }
    }
}
