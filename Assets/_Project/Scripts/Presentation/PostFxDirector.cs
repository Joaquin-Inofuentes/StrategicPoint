using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SP.Presentation
{
    // Items 176 (aberracion cromatica por daño) y 178 (desenfoque de
    // movimiento en el vehiculo). Los dos necesitan post-procesado de URP,
    // que en este proyecto estaba APAGADO.
    //
    // Encenderlo a secas NO era una opcion: el perfil de volumen por
    // defecto de la plantilla URP (Assets/Settings/DefaultVolumeProfile)
    // tiene 28 componentes, TODOS activos y con sus 135 overrides
    // prendidos -- Bloom, DepthOfField, FilmGrain, Vignette, Tonemapping,
    // LensDistortion, PaniniProjection y mas. Prender el post-procesado
    // habria cambiado de golpe el look de todo el juego, que es un efecto
    // que nadie pidio.
    //
    // Por eso este director crea su PROPIO perfil, en un Volume global de
    // prioridad alta, que hace dos cosas:
    //   1) neutraliza explicitamente los efectos visibles de la plantilla,
    //      dejando la imagen igual a como se veia SIN post-procesado;
    //   2) agrega solamente los dos efectos que el backlog pide, ambos
    //      arrancando en cero y manejados por codigo.
    //
    // El costo es un pase full-screen que antes no existia. Se paga solo
    // mientras alguno de los dos efectos esta por encima de cero: cuando
    // los dos estan en cero, el Volume queda con weight 0 y URP puede
    // saltearse el trabajo.
    public class PostFxDirector : MonoBehaviour
    {
        public static PostFxDirector Instance { get; private set; }

        Volume volume;
        VolumeProfile profile;
        ChromaticAberration aberration;
        MotionBlur motionBlur;

        float damageAberration;
        float speedBlur;

        // La aberracion por daño decae sola; el desenfoque lo reescribe el
        // conductor cada frame mientras maneja.
        const float AberrationDecayPerSec = 1.6f;

        void OnEnable()
        {
            Instance = this;
            EnsureBuilt();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void EnsureBuilt()
        {
            var holder = transform.Find("PostFxVolume");
            if (holder == null)
            {
                var go = new GameObject("PostFxVolume");
                go.transform.SetParent(transform, false);
                go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                holder = go.transform;
            }

            volume = holder.GetComponent<Volume>();
            if (volume == null) volume = holder.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;

            if (volume.profile != null)
            {
                profile = volume.profile;
                profile.TryGet(out aberration);
                profile.TryGet(out motionBlur);
                return;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SP_RuntimePostFx";
            volume.profile = profile;

            NeutralizeTemplateLook();

            aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0f);

            motionBlur = profile.Add<MotionBlur>(true);
            motionBlur.intensity.Override(0f);

            ApplyWeight();
        }

        // Deja la imagen como estaba ANTES de encender el post-procesado.
        // Cada override apaga un efecto que la plantilla traia prendido.
        void NeutralizeTemplateLook()
        {
            profile.Add<Bloom>(true).intensity.Override(0f);
            profile.Add<FilmGrain>(true).intensity.Override(0f);
            profile.Add<Vignette>(true).intensity.Override(0f);
            profile.Add<LensDistortion>(true).intensity.Override(0f);
            profile.Add<PaniniProjection>(true).distance.Override(0f);
            profile.Add<DepthOfField>(true).mode.Override(DepthOfFieldMode.Off);
            // Sin tonemapping: el juego venia renderizando sin el, asi que
            // dejarlo en Neutral/ACES cambiaria todos los colores.
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.contrast.Override(0f);
            color.saturation.Override(0f);
            color.hueShift.Override(0f);
        }

        // 176: la intensidad de la aberracion crece con el daño recibido y
        // decae sola. La llama el HUD de daño.
        public void PulseDamageAberration(float amount01)
        {
            damageAberration = Mathf.Clamp01(Mathf.Max(damageAberration, amount01));
        }

        // 178: lo escribe el conductor cada frame con su fraccion de
        // velocidad. Al bajarse deja de escribirlo y cae solo.
        public void SetSpeedBlur(float amount01)
        {
            speedBlur = Mathf.Clamp01(amount01);
        }

        void Update()
        {
            if (aberration == null || motionBlur == null) return;

            bool fxOn = SP.CameraSystem.CameraFxSettings.Enabled;

            damageAberration = Mathf.MoveTowards(damageAberration, 0f, Time.deltaTime * AberrationDecayPerSec);
            // El desenfoque decae solo tambien: si el jugador se baja del
            // vehiculo, nadie vuelve a llamar SetSpeedBlur y sin esto
            // quedaria congelado en el ultimo valor.
            speedBlur = Mathf.MoveTowards(speedBlur, 0f, Time.deltaTime * 2f);

            aberration.intensity.Override(fxOn ? damageAberration : 0f);
            motionBlur.intensity.Override(fxOn ? speedBlur * 0.35f : 0f);

            ApplyWeight();
        }

        // Weight 0 cuando no hay nada que mostrar: es lo que permite que
        // el pase full-screen no se pague en el caso normal.
        void ApplyWeight()
        {
            if (volume == null) return;
            bool anything = damageAberration > 0.001f || speedBlur > 0.001f;
            volume.weight = anything && SP.CameraSystem.CameraFxSettings.Enabled ? 1f : 0f;
        }

        public float AberrationIntensity => aberration != null ? aberration.intensity.value : -1f;
        public float BlurIntensity => motionBlur != null ? motionBlur.intensity.value : -1f;
        public float VolumeWeight => volume != null ? volume.weight : -1f;

        // Enciende el post-procesado en la camara. Se llama desde el
        // constructor de escena: la camara se crea con AddComponent<Camera>
        // pelado y sin UniversalAdditionalCameraData.
        public static void EnableOnCamera(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }
    }
}
