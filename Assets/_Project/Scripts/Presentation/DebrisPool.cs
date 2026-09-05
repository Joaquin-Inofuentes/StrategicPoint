using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Escombros con presupuesto FIJO. Instanciar por evento haria que un
    // combate masivo generase basura sin control y el recolector produjera
    // tirones -- el mismo motivo por el que los proyectiles ya van por
    // ObjectPool. Cuando el presupuesto se agota, recicla la pieza mas
    // vieja en vez de crear una nueva: el tope es duro, nunca se supera.
    public static class DebrisPool
    {
        public const int Budget = 64;

        static readonly List<Debris> all = new List<Debris>();
        static readonly Queue<Debris> free = new Queue<Debris>();
        // Orden de uso: el frente es la pieza activa mas vieja, la primera
        // en ser reciclada cuando hace falta lugar.
        static readonly List<Debris> inUse = new List<Debris>();
        static Transform root;

        public static int ActiveCount => inUse.Count;
        public static int TotalCount => all.Count;

        // El pool es estado de runtime: no sobrevive a un domain reload ni
        // a un cambio de escena, y las referencias quedan apuntando a
        // objetos destruidos. Se limpia y se rearma solo.
        public static void ResetIfStale()
        {
            if (root != null) return;
            all.Clear();
            free.Clear();
            inUse.Clear();
        }

        // Mismo bug y mismo arreglo que DecalPool.ClearAll: se llama al
        // salir de Play mode (Scripts/Editor/PlaymodeCleanup.cs) para que
        // los escombros no sobrevivan como huerfanos.
        static void Destruir(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        public static void ClearAll()
        {
            // DestroyImmediate dentro de Play, ahora que esto corre
            // tambien al cargar una escena y no solo al salir del
            // Editor, destruye el objeto en medio del recorrido de
            // otro sistema. Destroy espera al final del frame.
            foreach (var d in all)
                if (d != null) Destruir(d.gameObject);
            all.Clear();
            free.Clear();
            inUse.Clear();
            DestroyOrphans();
            if (root != null) { Destruir(root.gameObject); root = null; }
        }


        // Entrar en Play mode NO destruye los objetos de la escena, pero
        // SI reinicia los estaticos: el root creado en tiempo de edicion
        // sigue vivo mientras las listas que lo indexaban quedan vacias, y
        // sus hijos pasan a ser huerfanos fuera de todo cupo (verificado:
        // duplicaba los decals). Al rearmarse, el pool barre los roots
        // viejos que quedaron sueltos.
        static void DestroyOrphans()
        {
            foreach (var d in Object.FindObjectsByType<Debris>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (d == null || all.Contains(d)) continue;
                if (Application.isPlaying) Object.Destroy(d.gameObject);
                else Object.DestroyImmediate(d.gameObject);
            }
        }

        static void EnsureRoot()
        {
            if (root != null) return;
            DestroyOrphans();
            var go = new GameObject("DebrisPool");
            // Sin esto, lo creado en tiempo de edicion queda serializado en
            // la escena y al entrar en Play mode conviven los objetos
            // guardados con los que crea el pool nuevo: el presupuesto se
            // duplica en silencio.
            //
            // DontSaveInEditor|DontSaveInBuild y NO DontSave: DontSave
            // ademas impide destruir el objeto al cargar una escena, asi
            // que el root de tiempo de edicion sobrevivia al entrar en
            // Play mode como huerfano (los estaticos SI se reinician en el
            // domain reload, se creaba un root nuevo) y sus hijos quedaban
            // fuera de todo cupo. Verificado: pasaba con los decals.
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root = go.transform;
        }

        public static void Prewarm()
        {
            ResetIfStale();
            EnsureRoot();
            while (all.Count < Budget) Create();
        }

        static Debris Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Debris";
            // hideFlags va en CADA pieza, no solo en el root: los flags
            // NO se heredan. Con el flag solo en el padre, las piezas se
            // serializaban igual y al recargar la escena aparecian como
            // objetos sueltos de raiz (el padre no se guardaba), fuera de
            // todo cupo y acumulandose en cada build. Verificado: los
            // decals llegaron a triplicar su tope asi.
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.transform.SetParent(root, false);

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = SafeMaterial.CreateShared();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var d = go.AddComponent<Debris>();
            go.SetActive(false);
            all.Add(d);
            free.Enqueue(d);
            return d;
        }

        public static Debris Spawn(Vector3 position, Vector3 velocity, Color color, float size, float lifetime = 2.5f)
        {
            ResetIfStale();
            EnsureRoot();

            // Una reconstruccion de escena destruye los GameObjects pero
            // no vacia estas listas (son estaticas): quedan entradas
            // "fake-null" de Unity que pasan un chequeo de referencia de C#
            // pero explotan al tocarles el transform. Hay que purgarlas
            // antes de repartir, no confiar en que la lista este sana.
            all.RemoveAll(x => x == null);
            inUse.RemoveAll(x => x == null);

            Debris d = null;
            while (free.Count > 0 && d == null) d = free.Dequeue();

            if (d == null)
            {
                if (all.Count < Budget) { Create(); d = free.Dequeue(); }
                else
                {
                    // Presupuesto agotado: se recicla la mas vieja EN USO.
                    // Es lo que hace que el tope sea real y no una
                    // sugerencia.
                    d = inUse[0];
                    inUse.RemoveAt(0);
                    d.Recycle();
                }
            }

            inUse.Add(d);
            d.Launch(position, velocity, color, size, lifetime);
            return d;
        }

        public static void Release(Debris d)
        {
            if (d == null) return;
            inUse.Remove(d);
            if (!free.Contains(d)) free.Enqueue(d);
        }
    }

    // Pieza individual: vuela con gravedad simple, y al expirar se hunde
    // y se encoge en vez de desaparecer de golpe -- un salto visible seria
    // peor que no tener escombros.
    public class Debris : MonoBehaviour
    {
        Vector3 velocity;
        Vector3 spin;
        float lifetime;
        float age;
        float baseSize;
        bool active;

        const float Gravity = -14f;
        const float FadeSeconds = 0.6f;

        public void Launch(Vector3 position, Vector3 initialVelocity, Color color, float size, float life)
        {
            transform.position = position;
            transform.rotation = Random.rotation;
            baseSize = size;
            transform.localScale = Vector3.one * size;
            velocity = initialVelocity;
            spin = Random.insideUnitSphere * 360f;
            lifetime = life;
            age = 0f;
            active = true;

            var rend = GetComponent<MeshRenderer>();
            if (rend != null)
            {
                // El Material creado por codigo no es un asset guardado:
                // una reconstruccion de escena o un domain reload lo
                // destruye y deja al Renderer con sharedMaterial en null,
                // aunque el GameObject siga vivo. Hay que rehacerlo, no
                // asumir que sigue ahi.
                if (rend.sharedMaterial == null)
                    rend.sharedMaterial = SafeMaterial.CreateShared();
                rend.sharedMaterial.color = color;
            }
            gameObject.SetActive(true);
        }

        public void Recycle()
        {
            active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!active) return;
            float dt = Time.deltaTime;
            age += dt;

            velocity.y += Gravity * dt;
            transform.position += velocity * dt;
            transform.Rotate(spin * dt, Space.Self);

            // Rebota una vez contra el suelo perdiendo casi toda la
            // energia, y queda apoyado.
            if (transform.position.y < baseSize * 0.5f)
            {
                var p = transform.position;
                p.y = baseSize * 0.5f;
                transform.position = p;
                velocity = new Vector3(velocity.x * 0.3f, -velocity.y * 0.25f, velocity.z * 0.3f);
                spin *= 0.4f;
            }

            float remaining = lifetime - age;
            if (remaining <= FadeSeconds)
            {
                float k = Mathf.Clamp01(remaining / FadeSeconds);
                transform.localScale = Vector3.one * baseSize * k;
                // Se hunde en el suelo mientras se encoge: refuerza la
                // lectura de "se esta yendo" en vez de "parpadeo".
                var p = transform.position;
                p.y = Mathf.Min(p.y, baseSize * 0.5f * k);
                transform.position = p;
            }

            if (age >= lifetime)
            {
                Recycle();
                DebrisPool.Release(this);
            }
        }
    }
}
