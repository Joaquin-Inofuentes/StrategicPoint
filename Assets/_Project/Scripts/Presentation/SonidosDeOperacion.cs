using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Vehicles;

namespace SP.Presentation
{
    // Sonidos "vivos" del nivel, todos generados por codigo (ver SfxSintetico.Operacion):
    //   - LOGRO al caer un enemigo (sube de tono con la racha, como un combo) y al volar un vehiculo enemigo
    //   - ORBITA 3D de cada vehiculo: orugas de los tanques y motor de las camionetas, mas fuertes y agudos con la velocidad;
    //     como cada uno es un AudioSource 3D con doppler, varios a la vez suman y se sienten pasar
    //   - AMBIENTE de desierto (lazo de viento seco) cuando una escena lo pide (SonidosDeOperacion.IniciarAmbiente)
    public class SonidosDeOperacion : MonoBehaviour
    {
        public static SonidosDeOperacion Instancia { get; private set; }
        public static int Logros { get; private set; }
        public static int VehiculosConSonido { get; private set; }
        public static bool AmbienteActivo => Instancia != null && Instancia.ambiente != null && Instancia.ambiente.isPlaying;

        IDisposable subMuerte, subVehiculo;
        float ultimoLogro = -99f; int racha;
        AudioSource ambiente;
        float ambienteBase = 0.5f;
        float proximoBarrido;
        readonly Dictionary<Vehicle, AudioSource> motores = new Dictionary<Vehicle, AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            if (Instancia != null) return;
            var go = new GameObject("SonidosDeOperacion");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<SonidosDeOperacion>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Instancia = null; Logros = 0; VehiculosConSonido = 0; }

        void OnEnable()
        {
            subMuerte?.Dispose(); subVehiculo?.Dispose();
            subMuerte = EventBus.Instance.Subscribe<EntityDiedEvent>(OnMuerte);
            subVehiculo = EventBus.Instance.Subscribe<VehicleDestroyedEvent>(OnVehiculoDestruido);
        }

        void OnDisable() { subMuerte?.Dispose(); subVehiculo?.Dispose(); }

        // ---- logro ----
        // Bug #065: el "logro" de cada baja salia 2D (PlayOneShot2D, spatialBlend 0) y a volumen alto, asi que la muerte de un
        // enemigo lejano sonaba "al lado". Ahora suena 3D en la posicion de la victima, con perfil de voz y bajo (0.35). Si la baja
        // es del jugador queda ademas un tick de interfaz 2D muy suave (0.15): es feedback propio, no un sonido del mundo.
        public const float VolumenDeLogro3D = 0.35f;
        public const float VolumenDeTickPropio = 0.15f;

        void OnMuerte(EntityDiedEvent e)
        {
            var s = ActorRegistry.FindById(e.ActorId);
            if (s == null || s.Team != TeamId.Enemy || s.Role == RoleType.Civilian) return;
            var d = SP.Player.PlayerInputDriver.Activo;
            var yo = d != null && d.Brain != null ? d.Brain.Current : null;
            bool propia = yo != null && s.Health != null && s.Health.LastAttackerId == yo.Id;
            Logro(VolumenDeLogro3D, s.transform.position + Vector3.up * 1f, PerfilEspacial.Voz, propia);
        }

        void OnVehiculoDestruido(VehicleDestroyedEvent e)
        {
            if (e.Vehicle != null && e.Vehicle.Bando == TeamId.Enemy)
                Logro(VolumenDeLogro3D, e.Vehicle.transform.position + Vector3.up * 1f, PerfilEspacial.Explosion, false);
        }

        void Logro(float volumen, Vector3 posicion, PerfilEspacial perfil, bool propia)
        {
            if (!Application.isPlaying) return;
            racha = Time.unscaledTime - ultimoLogro < 3.5f ? Mathf.Min(racha + 1, 7) : 0;
            ultimoLogro = Time.unscaledTime;
            Logros++;
            // Cada baja seguida sube medio tono: se siente como un combo.
            float pitch = Mathf.Pow(1.0595f, racha);
            var clip = GenericSfx.Get(SfxKind.Logro);
            AudioDirector.PlayClipAt(clip, posicion, volumen, 0.6f, perfil, pitch);
            if (propia && AudioDirector.Instance != null)
                AudioDirector.Instance.PlayFlat(clip, SfxChannel.Ui, VolumenDeTickPropio, 0.5f, pitch);
        }

        // ---- ambiente ----
        public static void IniciarAmbiente(float volumen = 0.5f)
        {
            if (Instancia == null) Auto();
            var inst = Instancia;
            if (inst == null) return;
            if (inst.ambiente == null)
            {
                inst.ambiente = inst.gameObject.AddComponent<AudioSource>();
                inst.ambiente.clip = GenericSfx.Get(SfxKind.AmbienteDesierto);
                inst.ambiente.loop = true;
                inst.ambiente.spatialBlend = 0f;
                inst.ambiente.playOnAwake = false;
            }
            inst.ambienteBase = volumen;
            inst.ambiente.volume = volumen * AudioDirector.GainFor(SfxChannel.Ambient);
            if (!inst.ambiente.isPlaying) inst.ambiente.Play();
        }

        public static void DetenerAmbiente()
        {
            if (Instancia != null && Instancia.ambiente != null) Instancia.ambiente.Stop();
        }

        // ---- motores ----
        void Update()
        {
            if (!Application.isPlaying) return;
            if (Time.unscaledTime >= proximoBarrido) { proximoBarrido = Time.unscaledTime + 0.75f; Barrer(); }
            // Durante la musica de victoria el viento baja a un cuarto (el himno corre en timeScale 0: este Update es de reloj real).
            if (ambiente != null && ambiente.isPlaying)
                ambiente.volume = ambienteBase * AudioDirector.GainFor(SfxChannel.Ambient) * Mathf.Lerp(0.25f, 1f, MusicDirector.FactorOtros);
            float gain = AudioDirector.GainFor(SfxChannel.Sfx);
            VehiculosConSonido = 0;
            foreach (var kv in motores)
            {
                var v = kv.Key; var src = kv.Value;
                if (v == null || src == null) continue;
                if (v.IsDestroyed) { if (src.isPlaying) src.Stop(); continue; }
                float vel, max;
                bool camioneta = v.GetComponent<SP.Operacion.OperacionAuto>() != null;
                if (camioneta)
                {
                    var a = v.GetComponent<SP.Operacion.OperacionAuto>();
                    vel = a.VelocidadActual; max = 34f;
                }
                else
                {
                    var m = v.GetComponent<VehicleMotor>();
                    vel = m != null ? Mathf.Abs(m.CurrentSpeed) : 0f; max = m != null ? Mathf.Max(1f, m.MaxSpeed) : 12f;
                }
                float f = Mathf.Clamp01(vel / max);
                bool ocupado = v.OccupantCount > 0 || camioneta;
                // Ralenti suave si hay alguien adentro, rugido a fondo cuando va rapido.
                float vol = ocupado ? Mathf.Lerp(0.18f, 1f, f) : 0.05f;
                src.volume = Mathf.Clamp01(vol * (camioneta ? 0.8f : 1f)) * gain;
                src.pitch = camioneta ? Mathf.Lerp(0.8f, 1.5f, f) : Mathf.Lerp(0.75f, 1.35f, f);
                if (!src.isPlaying) src.Play();
                VehiculosConSonido++;
            }
        }

        void Barrer()
        {
            var lista = Vehicle.Todos;
            for (int i = 0; i < lista.Count; i++)
            {
                var v = lista[i];
                if (v == null || motores.ContainsKey(v) || v.IsDestroyed) continue;
                bool camioneta = v.GetComponent<SP.Operacion.OperacionAuto>() != null;
                if (!camioneta && v.GetComponent<VehicleMotor>() == null) continue;
                var go = new GameObject("SonidoMotor");
                go.transform.SetParent(v.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.clip = GenericSfx.Get(camioneta ? SfxKind.MotorCamioneta : SfxKind.Orugas);
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = camioneta ? 5f : 8f;
                src.maxDistance = 150f;
                src.dopplerLevel = 0.7f;
                src.volume = 0f;
                src.time = UnityEngine.Random.value * src.clip.length;
                motores[v] = src;
                // Tanques: capa de chirrido REAL de orugas (VehicleAudioFeedback, muda con el tanque quieto) en un hijo propio,
                // asi no toma prestado ningun otro AudioSource del vehiculo.
                if (!camioneta && v.GetComponentInChildren<VehicleAudioFeedback>(true) == null)
                {
                    var capa = new GameObject("SonidoOrugas");
                    capa.transform.SetParent(v.transform, false);
                    capa.AddComponent<VehicleAudioFeedback>();
                }
            }
            // Limpieza de los que ya no existen.
            var muertos = new List<Vehicle>();
            foreach (var kv in motores) if (kv.Key == null) muertos.Add(kv.Key);
            foreach (var k in muertos) motores.Remove(k);
        }
    }
}
