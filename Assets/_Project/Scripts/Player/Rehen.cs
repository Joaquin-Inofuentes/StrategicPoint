using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Presentation;

namespace SP.Player
{
    // Hace que el civil de la mision de rescate (RoleType.Civilian, creado por
    // MisionDirector.AparecerCivil) se distinga a simple vista y de oido, ademas
    // del material propio que ya le pone SoldierLook. Componente chico y aparte
    // (no se toca SoldierLook) para no arriesgar el resto del vestuario de
    // soldados: si algun dia el civil deja de llevar este componente, vuelve a
    // verse como un soldado mas, sin romper nada.
    //
    // Mismo patron que CajaDeSuministros: proximidad por DISTANCIA en Update,
    // no OnTriggerEnter -- los soldados de este proyecto no llevan Rigidbody
    // (se mueven a mano, no por fisica), asi que un Collider en modo trigger
    // nunca dispara sus eventos contra ellos. El marcador SI lleva su propio
    // Collider, pero puesto en isTrigger (y sin fisica real) solo para no
    // bloquear a nadie -- la deteccion real es la misma distancia de siempre.
    [DisallowMultipleComponent]
    public class Rehen : MonoBehaviour
    {
        public const float RadioDeAviso = 3.5f;
        public const float SegundosEntreAvisos = 8f;

        static readonly Color TinteMarcador = new Color(1f, 0.95f, 0.15f); // amarillo llamativo, distinto del tinte propio del civil

        Soldier soldier;
        PlayerInputDriver driver;
        AudioSource audioSource;
        AudioClip tonoAviso;
        float proximoAviso;

        void Awake()
        {
            soldier = GetComponent<Soldier>();

            // Tinte por instancia via MaterialPropertyBlock: no toca el
            // Material compartido que SoldierLook.MaterialCivil() ya
            // clono para TODOS los civiles -- si hubiera mas de uno,
            // pintar el sharedMaterial los pintaria a todos igual.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                CubeFxReactor.WriteTint(r, TinteMarcador);

            CrearMarcador();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = 0.8f;
        }

        // Esfera flotando arriba del rehen, 100% por codigo: sin prefabs ni
        // assets externos. Se le saca el Collider real y se le pone uno
        // propio en isTrigger solo para no interferir con la fisica/rayos
        // del resto del juego (raycasts de apuntado, Physics.CheckBox, etc.).
        void CrearMarcador()
        {
            var marcador = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marcador.name = "MarcadorDeRehen";
            marcador.transform.SetParent(transform, false);
            marcador.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            marcador.transform.localScale = Vector3.one * 0.35f;

            var colViejo = marcador.GetComponent<Collider>();
            if (colViejo != null) { if (Application.isPlaying) Destroy(colViejo); else DestroyImmediate(colViejo); }
            var col = marcador.AddComponent<SphereCollider>();
            col.isTrigger = true;

            var mat = SafeMaterial.Create(TinteMarcador);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", TinteMarcador * 2.2f);
            }
            marcador.GetComponent<Renderer>().sharedMaterial = mat;

            var giro = marcador.AddComponent<GiroDeMarcador>();
            giro.Base = marcador.transform.localPosition;
        }

        void Update()
        {
            if (soldier == null || soldier.Health == null || !soldier.Health.IsAlive) return;
            if (driver == null) driver = FindAnyObjectByType<PlayerInputDriver>();
            var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
            if (yo == null || yo == soldier || !yo.Health.IsAlive) return;
            if (Time.time < proximoAviso) return;

            var d = yo.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > RadioDeAviso * RadioDeAviso) return;

            proximoAviso = Time.time + SegundosEntreAvisos;
            if (tonoAviso == null) tonoAviso = CrearTonoAviso();
            if (audioSource != null && tonoAviso != null) audioSource.PlayOneShot(tonoAviso, 0.9f);
            GameLogRehen();
        }

        static void GameLogRehen() => SP.Core.GameLog.Line("Rehen: el jugador esta cerca");

        // No hay ningun AudioClip.Create reutilizable para esto en el proyecto
        // (SfxSintetico.cs ya sintetiza TODO por codigo con el mismo patron:
        // sinusoide con AudioClip.Create, deterministico), asi que se sigue
        // ese mismo estilo aca en vez de inventar uno nuevo: dos notas simples
        // que suben, para que se distinga claramente de los demas sonidos.
        static AudioClip CrearTonoAviso()
        {
            const int sr = 44100;
            const float duracion = 0.35f;
            int muestras = (int)(duracion * sr);
            var datos = new float[muestras];
            float[] notas = { 660f, 990f };
            for (int i = 0; i < muestras; i++)
            {
                float t = (float)i / sr;
                float k = (float)i / muestras;
                float f = k < 0.5f ? notas[0] : notas[1];
                float env = Mathf.Exp(-6f * (t % (duracion * 0.5f)));
                datos[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.6f;
            }
            var clip = AudioClip.Create("RehenAviso", muestras, 1, sr, false);
            clip.SetData(datos, 0);
            return clip;
        }
    }

    // Sube y baja despacio y gira: mismo tipo de animacion de "esto es
    // interactuable" que ya usa CajaDeSuministros para su propio marcador.
    class GiroDeMarcador : MonoBehaviour
    {
        public Vector3 Base;
        void Update()
        {
            transform.localPosition = Base + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.15f);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }
    }
}
