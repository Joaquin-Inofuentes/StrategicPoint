using System;
using System.Collections;
using UnityEngine;
using SP.Core;
using SP.Vehicles;

namespace SP.Presentation
{
    // Reaccion visual/sonora propia del vehiculo al recibir daño. Antes un
    // impacto solo bajaba una barra de vida en el HUD -- ni un flash en el
    // chasis, ni un sonido distinto al "Hit" de un soldado (CubeFxReactor),
    // ni ninguna señal de que el vehiculo esta cada vez peor. Se auto-
    // inicializa en Awake para sobrevivir a un domain reload.
    [RequireComponent(typeof(AudioSource))]
    public class VehicleFxReactor : MonoBehaviour
    {
        Vehicle vehicle;
        AudioSource audioSource;
        Renderer[] chassisRenderers;
        Color[] baseColors;
        bool bootstrapped;

        IDisposable damageSub, destroyedSub;

        float healthFraction = 1f;
        float smokeTimer;

        static readonly Color SparkColor = new Color(1f, 0.85f, 0.5f);
        static readonly Color SmokeColor = new Color(0.16f, 0.16f, 0.16f);

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            // Las suscripciones van PRIMERO. Si algo de abajo tira (un
            // renderer sin material, por ejemplo), Awake se corta a mitad
            // pero bootstrapped ya quedo en true -- y el vehiculo se
            // quedaba mudo y sin flash para el resto de la partida, sin
            // ningun sintoma salvo la excepcion suelta en consola.
            damageSub = EventBus.Instance.Subscribe<VehicleDamagedEvent>(OnDamage);
            destroyedSub = EventBus.Instance.Subscribe<VehicleDestroyedEvent>(OnDestroyedEvt);

            vehicle = GetComponent<Vehicle>();
            chassisRenderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[chassisRenderers.Length];
            for (int i = 0; i < chassisRenderers.Length; i++)
                baseColors[i] = HasMaterial(chassisRenderers[i]) ? chassisRenderers[i].sharedMaterial.color : Color.white;

            // AudioSource propio, en un hijo dedicado. El del vehiculo lo
            // maneja VehicleAudioFeedback, que le reescribe pitch y volumen
            // CADA frame segun la velocidad: un PlayOneShot ahi sonaba
            // agudo o grave segun a que velocidad ibas cuando te pegaron.
            // Se busca por nombre antes de crear para que sea idempotente
            // (Awake corre tanto al armar la escena en el Editor como al
            // entrar a Play, y no queremos acumular AudioSources).
            var fxAudioTf = transform.Find(FxAudioChildName);
            if (fxAudioTf == null)
            {
                var fxAudioGo = new GameObject(FxAudioChildName);
                fxAudioGo.transform.SetParent(transform, false);
                fxAudioTf = fxAudioGo.transform;
            }
            audioSource = fxAudioTf.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = fxAudioTf.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        const string FxAudioChildName = "FxAudio";

        // Un renderer puede tener el material perdido (sharedMaterial null)
        // tras recargar un prefab: cualquier .color sobre eso tira
        // NullReference. Es un caso que este proyecto ya documenta.
        static bool HasMaterial(Renderer r) => r != null && r.sharedMaterial != null;

        void OnDestroy()
        {
            damageSub?.Dispose();
            destroyedSub?.Dispose();
        }

        bool IsMe(Vehicle v) => vehicle != null && v == vehicle;

        void OnDamage(VehicleDamagedEvent evt)
        {
            if (!Application.isPlaying || !IsMe(evt.Vehicle) || !gameObject.activeInHierarchy) return;
            healthFraction = evt.MaxHealth > 0 ? (float)evt.RemainingHealth / evt.MaxHealth : 0f;
            if (audioSource != null) audioSource.PlayOneShot(GenericSfx.Get(SfxKind.VehicleHit));
            // Restaura antes de cortar: si StopAllCoroutines mata un
            // SparkFlash a mitad, el chasis se quedaba pegado en el dorado
            // de chispa hasta el proximo impacto (o para siempre, si ese
            // impacto era el ultimo).
            RestoreBaseColors();
            StopAllCoroutines();
            StartCoroutine(SparkFlash());
        }

        void OnDestroyedEvt(VehicleDestroyedEvent evt)
        {
            if (!IsMe(evt.Vehicle)) return;
            healthFraction = 0f;
        }

        IEnumerator SparkFlash()
        {
            for (int i = 0; i < chassisRenderers.Length; i++)
                if (HasMaterial(chassisRenderers[i])) chassisRenderers[i].sharedMaterial.color = SparkColor;

            yield return new WaitForSeconds(0.12f);

            RestoreBaseColors();
        }

        void RestoreBaseColors()
        {
            // El vehiculo puede haber muerto (y su chasis puesto negro por
            // Vehicle.OnDestroyed) durante el flash de chispa -- no pisar
            // ese color con el original.
            if (vehicle != null && vehicle.IsDestroyed) return;

            // Delegar en el propio vehiculo en vez de repintar con el color
            // cacheado en Awake: el chasis se oscurece mientras hay gente
            // adentro (RefreshOccupancyColor), y restaurar "el color de
            // Awake" aclaraba un vehiculo ocupado en cada impacto recibido
            // -- te pegaban un tiro manejando y el tanque se aclaraba y
            // quedaba asi hasta que alguien subiera o bajara.
            if (vehicle != null) { vehicle.RefreshOccupancyColor(); return; }

            for (int i = 0; i < chassisRenderers.Length; i++)
                if (HasMaterial(chassisRenderers[i])) chassisRenderers[i].sharedMaterial.color = baseColors[i];
        }

        // Humo progresivo: por debajo del 60% de vida empieza a soltar
        // bocanadas cada vez mas seguidas cuanto menos vida le queda --
        // la señal de "esto esta por explotar" antes de que realmente
        // pase, en vez de pasar de "sano" a "destruido" sin aviso previo.
        // Cuanto sigue humeando la carcasa despues de explotar. Sin este
        // limite el vehiculo destruido escupia una bocanada cada 0.35 s
        // PARA SIEMPRE: medido en Play, se sostenia en 3-4 bocanadas vivas
        // indefinidamente, o sea ~3 GameObjects + 3 Materials por segundo
        // que nadie liberaba, por cada vehiculo destruido de la partida.
        const float WreckSmokeSeconds = 8f;
        float wreckSmokeRemaining = WreckSmokeSeconds;

        void Update()
        {
            if (vehicle == null) return;

            bool shouldSmoke;
            if (vehicle.IsDestroyed)
            {
                wreckSmokeRemaining -= Time.deltaTime;
                shouldSmoke = wreckSmokeRemaining > 0f;
            }
            else shouldSmoke = healthFraction < 0.6f;
            if (!shouldSmoke) return;

            smokeTimer -= Time.deltaTime;
            if (smokeTimer > 0f) return;

            float damageFrac = vehicle.IsDestroyed ? 1f : Mathf.Clamp01(1f - healthFraction / 0.6f);
            smokeTimer = Mathf.Lerp(2.2f, 0.35f, damageFrac);
            SpawnSmokePuff();
        }

        void SpawnSmokePuff()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VehicleSmoke";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Vector3 origin = transform.position + Vector3.up * 1.6f + UnityEngine.Random.insideUnitSphere * 0.3f;
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.15f;

            var rend = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            rend.sharedMaterial = new Material(shader) { color = SmokeColor };

            go.AddComponent<VehicleSmokePuff>().Begin(origin);
        }
    }

    // Bocanada individual: crece mientras deriva hacia arriba y se
    // desvanece encogiendose a 0 (mismo idioma visual que ImpactFx, sin
    // depender de un shader transparente que este proyecto no usa en
    // ningun otro lado).
    public class VehicleSmokePuff : MonoBehaviour
    {
        public void Begin(Vector3 origin) => StartCoroutine(Drift(origin));

        IEnumerator Drift(Vector3 origin)
        {
            const float duration = 1.3f;
            const float riseHeight = 1.6f;
            const float peakScale = 0.7f;
            float driftX = (UnityEngine.Random.value - 0.5f) * 0.5f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                transform.position = origin + new Vector3(driftX * k, riseHeight * k, 0f);
                float scaleK = k < 0.3f ? Mathf.Lerp(0.15f, peakScale, k / 0.3f) : Mathf.Lerp(peakScale, 0f, (k - 0.3f) / 0.7f);
                transform.localScale = Vector3.one * scaleK;
                yield return null;
            }

            // Destruir el GameObject NO libera el Material: un material
            // asignado por sharedMaterial queda huerfano en memoria hasta
            // salir de Play. Con una bocanada cada 0.35 s eso se acumula
            // rapido, asi que cada bocanada se lleva el suyo.
            var rend = GetComponent<MeshRenderer>();
            var mat = rend != null ? rend.sharedMaterial : null;

            if (Application.isPlaying)
            {
                if (mat != null) Destroy(mat);
                Destroy(gameObject);
            }
            else
            {
                if (mat != null) DestroyImmediate(mat);
                DestroyImmediate(gameObject);
            }
        }
    }
}
