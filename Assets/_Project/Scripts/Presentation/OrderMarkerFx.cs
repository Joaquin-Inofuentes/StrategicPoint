using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Cilindro que aparece en el punto de una orden y se achica con un lerp
    // hasta desaparecer. El color indica que tipo de orden fue: mover,
    // atacar o subir a un vehiculo. Puramente cosmetico, no afecta logica.
    //
    // PRESUPUESTO DE EFECTOS: esto se llama UNA VEZ POR SOLDADO. Ordenar
    // mover a 50 unidades hacia 50 CreatePrimitive + 50 Shader.Find + 50
    // new Material en un solo frame, y Shader.Find es de las llamadas mas
    // caras del motor: era un tiron garantizado en el peor momento posible
    // (justo despues de que el jugador dio la orden). Ahora hay UN material
    // compartido con el Shader.Find resuelto una sola vez, y un pool con
    // tope duro que recicla el marcador mas viejo en vez de crear uno
    // nuevo -- el mismo criterio que DebrisPool, DecalPool y
    // MuzzleLightPool.
    //
    // La animacion pasa de corrutina a Update(): una corrutina por orden
    // tambien es basura por orden, y un objeto pooleado puede ser robado a
    // mitad de animacion (habria que acordarse de cortar la corrutina).
    public class OrderMarkerFx : MonoBehaviour
    {
        public static readonly Color MoveColor = new Color(0.35f, 0.85f, 0.35f);
        public static readonly Color AttackColor = new Color(0.92f, 0.2f, 0.18f);
        public static readonly Color MountColor = new Color(0.25f, 0.55f, 0.95f);

        // CUPO. Un lote de ordenes es un marcador POR SOLDADO, asi que el
        // tope tiene que cubrir la escuadra entera de una: 64 aguanta el
        // caso citado de 50 unidades con margen, y es el mismo numero que
        // DebrisPool. Los marcadores inmediatos viven 0.6 s, asi que en
        // juego real el pool casi nunca esta lleno; el tope existe para que
        // el peor caso (ordenes en cola, que NO se autodestruyen) tampoco
        // pueda crecer sin limite.
        public static int Budget => 64;

        // Contadores de solo lectura para verificacion: ActiveCount NUNCA
        // puede pasar de Budget, ese es el punto de un tope duro.
        public static int ActiveCount { get { Purge(); return inUse.Count; } }
        public static int TotalCount { get { Purge(); return all.Count; } }

        // Mas de 8 marcas verticales en un mismo marcador ya son ilegibles
        // (se solapan entre si): a partir de ahi la cola se lee por los
        // marcadores, no por los pips. El tope tambien acota cuantos cubos
        // hijos puede acumular un marcador reusado.
        const int MaxPips = 8;

        // --- Pool -----------------------------------------------------

        static readonly List<OrderMarkerFx> all = new List<OrderMarkerFx>();
        static readonly Queue<OrderMarkerFx> free = new Queue<OrderMarkerFx>();
        // Orden de uso: el frente es el marcador activo mas viejo, el
        // primero en ser reciclado cuando hace falta lugar.
        static readonly List<OrderMarkerFx> inUse = new List<OrderMarkerFx>();
        static Transform root;

        // El pool es estado de runtime: no sobrevive a un domain reload ni a
        // un cambio de escena, y las referencias quedan apuntando a objetos
        // destruidos. Se limpia y se rearma solo.
        public static void ResetIfStale()
        {
            if (root != null) return;
            all.Clear();
            free.Clear();
            inUse.Clear();
            QueuedMarkers.Clear();
        }

        // Una reconstruccion de escena destruye los GameObjects pero no
        // vacia estas listas (son estaticas): quedan entradas "fake-null" de
        // Unity que pasan un chequeo de referencia de C# pero explotan al
        // tocarles el transform. Hay que purgarlas antes de repartir, no
        // confiar en que la lista este sana.
        static void Purge()
        {
            all.RemoveAll(x => x == null);
            inUse.RemoveAll(x => x == null);
            QueuedMarkers.RemoveAll(x => x == null);
        }

        // Entrar en Play mode NO destruye los objetos de la escena, pero SI
        // reinicia los estaticos: el root creado en tiempo de edicion sigue
        // vivo mientras las listas que lo indexaban quedan vacias, y sus
        // hijos pasan a ser huerfanos fuera de todo cupo. Al rearmarse, el
        // pool barre lo que quedo suelto -- incluidos los marcadores que las
        // versiones viejas de este archivo dejaban serializados en la
        // escena.
        static void DestroyOrphans()
        {
            foreach (var m in Object.FindObjectsByType<OrderMarkerFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (m == null || all.Contains(m)) continue;
                if (Application.isPlaying) Object.Destroy(m.gameObject);
                else Object.DestroyImmediate(m.gameObject);
            }
        }

        static void EnsureRoot()
        {
            if (root != null) return;
            DestroyOrphans();
            var go = new GameObject("OrderMarkerPool");
            // DontSaveInEditor|DontSaveInBuild y NO DontSave: DontSave ademas
            // impide destruir el objeto al cargar una escena, con lo cual el
            // root de tiempo de edicion sobreviviria como huerfano. Ver el
            // comentario largo en DebrisPool.
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root = go.transform;
        }

        static OrderMarkerFx Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "OrderMarker";
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
            go.transform.SetParent(root, false);

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = SharedMaterial;

            var fx = go.AddComponent<OrderMarkerFx>();
            go.SetActive(false);
            all.Add(fx);
            return fx;
        }

        static OrderMarkerFx Take()
        {
            ResetIfStale();
            EnsureRoot();
            Purge();

            OrderMarkerFx m = null;
            while (free.Count > 0 && m == null) m = free.Dequeue();

            if (m == null)
            {
                // La segunda condicion es una red de seguridad: si por una
                // purga quedara el cupo lleno pero nada en uso, no habria a
                // quien reciclar y indexar inUse[0] reventaria.
                if (all.Count < Budget || inUse.Count == 0)
                {
                    m = Create();
                }
                else
                {
                    // Presupuesto agotado: se recicla el mas viejo EN USO. Es
                    // lo que hace que el tope sea real y no una sugerencia.
                    m = inUse[0];
                    inUse.RemoveAt(0);
                    m.Recycle();
                }
            }

            inUse.Add(m);
            return m;
        }

        static void Release(OrderMarkerFx m)
        {
            if (m == null) return;
            inUse.Remove(m);
            if (!free.Contains(m)) free.Enqueue(m);
        }

        // Devuelve todo lo activo al pool. Existe para el Edit mode, donde
        // Update() NO corre y por lo tanto ningun marcador se recicla solo.
        public static void RecycleAll()
        {
            Purge();
            for (int i = inUse.Count - 1; i >= 0; i--)
            {
                var m = inUse[i];
                if (m == null) continue;
                m.Recycle();
                if (!free.Contains(m)) free.Enqueue(m);
            }
            inUse.Clear();
        }

        // --- Material compartido -------------------------------------

        static Material sharedMaterial;

        // El Shader.Find se paga UNA vez en toda la partida, no una por
        // soldado ordenado. El material puede quedar destruido por una
        // recarga de escena (no es un asset guardado), por eso el getter lo
        // rehace en vez de asumir que sigue vivo.
        static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    sharedMaterial = new Material(shader);
                    if (sharedMaterial.HasProperty("_Smoothness")) sharedMaterial.SetFloat("_Smoothness", 0.1f);
                }
                return sharedMaterial;
            }
        }

        static MaterialPropertyBlock propertyBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // Con el material COMPARTIDO, escribir renderer.sharedMaterial.color
        // pintaria de ese color TODOS los marcadores vivos a la vez (una
        // orden de ataque volveria rojos los marcadores de movimiento que
        // siguen en pantalla). El color va por MaterialPropertyBlock, que es
        // por-renderer.
        //
        // Se escriben las dos propiedades porque el shader puede ser el de
        // URP (_BaseColor) o el Standard de fallback (_Color): cual de los
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

        // --- API publica ---------------------------------------------

        // orderIndex 0 = orden inmediata (marcador normal, se desvanece).
        // 1..n = posicion en la cola planificada: el marcador se queda fijo
        // hasta que ese tramo se cumple, y se dibuja con tantas marcas
        // verticales como su numero de orden -- con varios puntos encolados
        // los marcadores eran indistinguibles entre si y no se podia leer la
        // secuencia planificada.
        public static void Spawn(Vector3 position, Color color, int orderIndex, float duration = 0.6f)
        {
            if (orderIndex <= 0) { Spawn(position, color, duration); return; }
            var m = Take();
            if (m == null) return;
            m.LaunchQueued(position, color, orderIndex);
            QueuedMarkers.Add(m.gameObject);
        }

        public static void Spawn(Vector3 position, Color color, float duration = 0.6f)
        {
            var m = Take();
            if (m == null) return;
            m.LaunchFading(position, color, duration);
        }

        // Los marcadores de cola no se desvanecen solos: representan un plan
        // todavia pendiente. Los limpia quien cancela o consume la orden.
        public static readonly List<GameObject> QueuedMarkers = new List<GameObject>();

        public static void ClearQueuedMarkers()
        {
            // Recycle() se auto-quita de QueuedMarkers, asi que se recorre
            // hacia atras por indice: modificar la lista mientras se itera
            // hacia adelante saltearia elementos.
            for (int i = QueuedMarkers.Count - 1; i >= 0; i--)
            {
                var go = QueuedMarkers[i];
                if (go == null) continue;
                var fx = go.GetComponent<OrderMarkerFx>();
                if (fx != null)
                {
                    // Se devuelve al pool, NO se destruye: destruirlo haria
                    // que la proxima orden tenga que crear el primitivo otra
                    // vez, que es exactamente lo que este pool evita.
                    fx.Recycle();
                    Release(fx);
                }
                else
                {
                    // Resto de una version vieja (sin pool): se destruye.
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
            }
            QueuedMarkers.Clear();
        }

        static bool shaderWarmed;

        // La primera vez que se pinta un cilindro con este shader, Unity
        // compila esa variante y el frame se traba (a veces sale
        // directamente negro si justo se saca una captura ahi). Se
        // precalienta uno, bien lejos y chiquito, apenas se arma el nivel,
        // para que la primera orden real del jugador ya encuentre el shader
        // listo.
        public static void Prewarm()
        {
            if (shaderWarmed) return;
            shaderWarmed = true;
            Spawn(new Vector3(0f, -500f, 0f), MoveColor, 0.05f);

            // Update() NO corre en Edit mode, asi que el marcador de
            // precalentamiento se quedaria ocupando una plaza del cupo para
            // siempre. Antes esto se resolvia destruyendo TODOS los
            // OrderMarkerFx de la escena; ahora eso arrasaria con el pool,
            // asi que simplemente se devuelve lo activo a la cola de libres.
            // (Ya no queda basura serializada: los objetos del pool llevan
            // DontSaveInEditor.)
            if (!Application.isPlaying) RecycleAll();
        }

        // --- Instancia ------------------------------------------------

        bool fading;
        float age;
        float duration;
        Vector3 startScale;

        MeshRenderer cachedRenderer;

        // Los campos privados de un MonoBehaviour no sobreviven a un domain
        // reload: se vuelve a buscar el renderer en vez de darlo por
        // cacheado.
        MeshRenderer Rend
        {
            get
            {
                if (cachedRenderer == null) cachedRenderer = GetComponent<MeshRenderer>();
                return cachedRenderer;
            }
        }

        void LaunchFading(Vector3 position, Color color, float durationSeconds)
        {
            gameObject.name = "OrderMarker";
            gameObject.SetActive(true);
            // Siempre a nivel del piso, sin importar la altura del punto de
            // origen (un ataque usa la posicion del pecho del enemigo, subir
            // usa el centro del vehiculo -- ninguno es "el suelo").
            transform.position = new Vector3(position.x, 0.05f, position.z);
            transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);

            ApplyColor(Rend, color);
            SetPipCount(0, color);

            startScale = transform.localScale;
            duration = Mathf.Max(0.01f, durationSeconds);
            age = 0f;
            fading = true;
        }

        void LaunchQueued(Vector3 position, Color color, int orderIndex)
        {
            gameObject.name = "OrderMarker_Queued_" + orderIndex;
            gameObject.SetActive(true);
            transform.position = new Vector3(position.x, 0.05f, position.z);
            transform.localScale = new Vector3(1.1f, 0.05f, 1.1f);

            ApplyColor(Rend, color);
            SetPipCount(Mathf.Min(orderIndex, MaxPips), color);

            age = 0f;
            fading = false;
        }

        // Los pips son hijos del marcador y se reusan CON el: se activan los
        // primeros `count` y se apagan los demas, en vez de crear y destruir
        // cubos en cada orden. Se cuentan por transform.childCount y no por
        // una lista cacheada porque los campos privados se pierden en el
        // domain reload y los hijos no: la lista quedaria vacia con los
        // cubos todavia colgando, y se duplicarian.
        void SetPipCount(int count, Color color)
        {
            while (transform.childCount < count) CreatePip();

            for (int i = 0; i < transform.childCount; i++)
            {
                var pip = transform.GetChild(i);
                bool on = i < count;
                if (pip.gameObject.activeSelf != on) pip.gameObject.SetActive(on);
                if (!on) continue;

                // El padre esta aplastado en Y (0.05), asi que una altura
                // util en el mundo pide una escala local enorme en Y.
                pip.localScale = new Vector3(0.12f, 12f, 0.12f);
                pip.localPosition = new Vector3((i - (count - 1) * 0.5f) * 0.22f, 6f, 0f);
                ApplyColor(pip.GetComponent<MeshRenderer>(), color);
            }
        }

        void CreatePip()
        {
            var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pip.name = "OrderMarkerPip";
            pip.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var col = pip.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            pip.transform.SetParent(transform, false);
            pip.GetComponent<MeshRenderer>().sharedMaterial = SharedMaterial;
            pip.SetActive(false);
        }

        void Update()
        {
            if (!fading) return;
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / duration);
            transform.localScale = Vector3.Lerp(startScale, new Vector3(0f, startScale.y, 0f), k);
            if (age < duration) return;

            // El test automatico (HeadlessTestRunner) dispara ordenes en Edit
            // mode, donde ni las corrutinas ni Update corren: alla el
            // marcador no llega nunca a este punto y lo unico que lo acota es
            // el tope duro del pool (o RecycleAll).
            Recycle();
            Release(this);
        }

        // No destruye: apaga y deja el marcador listo para el proximo uso.
        // Es lo que convierte el cupo en un tope real en vez de una
        // sugerencia.
        public void Recycle()
        {
            fading = false;
            age = 0f;
            // Si estaba haciendo de marcador de cola, deja de representar un
            // plan pendiente: sale de la lista publica antes de apagarse.
            QueuedMarkers.Remove(gameObject);
            gameObject.SetActive(false);
        }
    }
}
