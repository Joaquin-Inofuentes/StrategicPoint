using System;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.UI;

namespace SP.Presentation
{
    // Bug #079: "carteles DOBLE/TRIPLE/PENTA KILL; con 5 seguidas, mas velocidad y doble dano".
    //
    //  - MULTIKILL: bajas PROPIAS (las del soldado que manejas) con menos de 4 s entre una y la siguiente. 2 = DOBLE KILL,
    //    3 = TRIPLE KILL, 4 = CUADRUPLE KILL, 5 o mas = PENTA KILL. Cartel arriba al centro (CartelDeRacha) con tono que sube.
    //  - FURIA: 5 bajas seguidas, con 6 s o menos entre cada una, sin morir -> 12 s de FURIA: x2 dano (Projectile lo lee en
    //    MultiplicadorPara) y +30% de velocidad (SoldierMotor.FactorDeBuff; NO SpeedMultiplier, que la IA pisa). Aura roja bajo los
    //    pies y aviso fijo. No se puede reactivar hasta 20 s DESPUES de que termine.
    //  - Todo se corta al morir el soldado o al cambiar de posesion.
    // Se arma solo (como MarcaDeImpacto): no hace falta tocar escenas.
    public class RachaDeBajas : MonoBehaviour
    {
        public static RachaDeBajas Instancia { get; private set; }

        public const float VentanaMultikill = 4f, EntreBajasDeFuria = 6f, DuracionFuria = 12f, EnfriamientoFuria = 20f;
        public const int BajasParaFuria = 5;
        public const float MultiplicadorDeDano = 2f, FactorDeVelocidad = 1.3f;

        public int Multikill { get; private set; }
        public int Racha { get; private set; }
        public bool FuriaActiva => soldadoConFuria != null && Time.time < furiaHasta;
        public float FuriaRestante => FuriaActiva ? furiaHasta - Time.time : 0f;
        public float EnfriamientoRestante => Mathf.Max(0f, listaDesde - Time.time);
        public Soldier SoldadoConFuria => FuriaActiva ? soldadoConFuria : null;
        public bool AuraVisible => aura != null && aura.gameObject.activeSelf;

        float ultimaBaja = -99f, furiaHasta = -1f, listaDesde = -1f;
        Soldier soldadoConFuria, ultimoPoseido;
        LineRenderer aura;
        int ultimoSegundoMostrado = -1;
        IDisposable subMuerte;

        // Lo lee Projectile al configurar cada disparo: x2 solo para el soldado que esta en FURIA.
        public static float MultiplicadorPara(int duenoId)
        {
            var r = Instancia;
            return r != null && r.FuriaActiva && r.soldadoConFuria.Id == duenoId ? MultiplicadorDeDano : 1f;
        }

        public static string NombreDeRacha(int n) => n <= 1 ? "" : n == 2 ? "DOBLE KILL" : n == 3 ? "TRIPLE KILL" : n == 4 ? "CUÁDRUPLE KILL" : "PENTA KILL";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (Instancia != null) return;
            var go = new GameObject("RachaDeBajas");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<RachaDeBajas>();
        }

        void OnEnable() { subMuerte = EventBus.Instance.Subscribe<EntityDiedEvent>(OnMuerte); }
        void OnDisable() { subMuerte?.Dispose(); subMuerte = null; Cortar(); }

        static Soldier Poseido()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        void OnMuerte(EntityDiedEvent e)
        {
            var victima = ActorRegistry.FindById(e.ActorId);
            if (victima == null) return;
            var yo = Poseido();
            if (victima == yo || victima == soldadoConFuria) { Cortar(); return; }
            if (victima.Team != TeamId.Enemy || yo == null || victima.Health == null || victima.Health.LastAttackerId != yo.Id) return;
            RegistrarBajaPropia(yo);
        }

        void RegistrarBajaPropia(Soldier yo)
        {
            float ahora = Time.time;
            float desdeLaUltima = ahora - ultimaBaja;
            Multikill = desdeLaUltima <= VentanaMultikill ? Multikill + 1 : 1;
            Racha = desdeLaUltima <= EntreBajasDeFuria ? Racha + 1 : 1;
            ultimaBaja = ahora;

            if (Multikill >= 2)
            {
                CartelDeRacha.Mostrar(NombreDeRacha(Multikill), Multikill);
                // Tono que sube con el conteo (2D de interfaz: no es un sonido del mundo).
                AudioDirector.Instance?.PlayFlat(GenericSfx.Get(SfxKind.HealDone), SfxChannel.Ui, 0.6f, 0.9f, Mathf.Min(2f, 0.85f + 0.2f * (Multikill - 1)));
            }
            if (!FuriaActiva && Racha >= BajasParaFuria && ahora >= listaDesde) ActivarFuria(yo);
        }

        void ActivarFuria(Soldier yo)
        {
            soldadoConFuria = yo;
            ultimoSegundoMostrado = -1;
            furiaHasta = Time.time + DuracionFuria;
            if (yo.Motor != null) yo.Motor.FactorDeBuff = FactorDeVelocidad;
            CartelDeRacha.MostrarFuria("FURIA ×2 DAÑO · +30% VELOCIDAD");
            AudioDirector.Instance?.PlayFlat(GenericSfx.Get(SfxKind.ObjetivoCumplido), SfxChannel.Ui, 0.5f, 0.9f, 1.1f);
            PrepararAura();
            aura.gameObject.SetActive(true);
        }

        void TerminarFuria()
        {
            if (soldadoConFuria != null && soldadoConFuria.Motor != null) soldadoConFuria.Motor.FactorDeBuff = 1f;
            bool estaba = furiaHasta > 0f;
            furiaHasta = -1f;
            soldadoConFuria = null;
            if (estaba) listaDesde = Time.time + EnfriamientoFuria;
            if (aura != null) aura.gameObject.SetActive(false);
            CartelDeRacha.MostrarFuria(null);
        }

        // Corta multikill, racha y FURIA (muerte del soldado o cambio de posesion).
        void Cortar()
        {
            Multikill = 0; Racha = 0; ultimaBaja = -99f;
            if (soldadoConFuria != null || furiaHasta > 0f) TerminarFuria();
        }

        // Para checks: levanta el enfriamiento de la FURIA.
        public void SinEnfriamiento() { listaDesde = -1f; }

        void PrepararAura()
        {
            if (aura != null) return;
            var go = new GameObject("AuraDeFuria");
            go.transform.SetParent(transform, false);
            aura = go.AddComponent<LineRenderer>();
            int n = 48;
            aura.useWorldSpace = false; aura.loop = true; aura.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                aura.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.95f, Mathf.Sin(a) * 0.95f, 0f));
            }
            aura.widthMultiplier = 0.14f;
            aura.material = SafeMaterial.CreateLinea(new Color(1f, 0.1f, 0.05f, 0.55f));
            aura.startColor = aura.endColor = new Color(1f, 0.1f, 0.05f, 0.55f);
            aura.alignment = LineAlignment.TransformZ;
            aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aura.receiveShadows = false;
            go.SetActive(false);
        }

        void Update()
        {
            var yo = Poseido();
            if (yo != ultimoPoseido)
            {
                bool habiaAlguien = ultimoPoseido != null;
                ultimoPoseido = yo;
                if (habiaAlguien) Cortar();   // cambio de posesion: la racha no se hereda
            }
            if (!FuriaActiva)
            {
                if (furiaHasta > 0f || soldadoConFuria != null) TerminarFuria();
                return;
            }
            // Mantener el buff si algo lo piso y mover el aura bajo los pies (el pivote esta ~0,8 m sobre el piso).
            if (soldadoConFuria.Motor != null && !Mathf.Approximately(soldadoConFuria.Motor.FactorDeBuff, FactorDeVelocidad)) soldadoConFuria.Motor.FactorDeBuff = FactorDeVelocidad;
            if (aura != null)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                aura.transform.position = soldadoConFuria.transform.position + Vector3.down * 0.76f;
                aura.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                aura.transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.12f, p);
                var c = new Color(1f, 0.1f, 0.05f, Mathf.Lerp(0.3f, 0.65f, p));
                aura.startColor = aura.endColor = c;
            }
            int seg = Mathf.CeilToInt(FuriaRestante);
            if (seg != ultimoSegundoMostrado) { ultimoSegundoMostrado = seg; CartelDeRacha.MostrarFuria($"FURIA ×2 DAÑO · +30% VELOCIDAD   {seg} s"); }
        }
    }
}
