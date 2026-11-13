using System.Collections;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.Operacion
{
    // Pedido 2026-11-07: "nuevo enemigo pesado y gordo con una minigun, 4 veces mas vida; ve al enemigo, se estaciona 2 segundos y empieza a disparar de
    // manera frenetica; recarga pesada; su minigun suena distinto, mas fuerte y retumbante". Mismo modelo y trimsheet del asalto, ensanchado (geometria
    // editada por escala: ancho x1,35, alto igual para no tocar el pivote); la minigun (WeaponKind.Minigun) trae la cadencia, el cargador de 160 y la
    // recarga de 4,2 s. El comportamiento es una maquina de tres estados encima del AiBrain.
    public sealed class EnemigoPesado : MonoBehaviour
    {
        // Bugs #385/#389 (2026-11-08): "que tenga 3 veces la vida que tiene ahora" -> de x4 a x12.
        public const int MultiplicadorDeVida = 12;
        public const float SegundosDeEstacionamiento = 2f, SegundosSinVerParaRelajarse = 3f;
        // 2026-11-13: "mas pesado, otro color muy distinto y un poquito mas grande" y despues "el pesado doble de tamano": el modelo mide el DOBLE de un enemigo
        // comun en las tres dimensiones (Ensanche), camina mas lento y cada paso retumba. AnchoDeCuerpo es lo que se ensancha la raiz (y el collider).
        public static readonly Vector3 Ensanche = new Vector3(2f, 2f, 2f);
        public const float FactorDeVelocidad = 0.72f, MetrosPorPaso = 2.2f, AnchoDeCuerpo = 1.5f;
        public enum Fase { Patrulla, Estacionando, Frenesi }

        public static int Creados { get; private set; }
        public Fase Estado { get; private set; } = Fase.Patrulla;
        // Time.time al entrar a cada fase (para medirlo desde afuera)
        public float InicioDeEstacionar { get; private set; } = -1f;
        public float InicioDeFrenesi { get; private set; } = -1f;
        public int VidaMaxima { get; private set; }
        public bool AnimacionPropia { get; private set; }
        Soldier soldado; AiBrain cerebro; float desde, ultimoVisto;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Creados = 0; }

        // Convierte a un soldado enemigo recien creado en pesado. Hay que llamarlo apenas se instancia (antes de su primer Start).
        public static EnemigoPesado Aplicar(Soldier s)
        {
            if (s == null || s.Team != TeamId.Enemy) return null;
            var ya = s.GetComponent<EnemigoPesado>(); if (ya != null) return ya;   // idempotente: un clon de un pesado ya lo es
            s.gameObject.name = "Pesado_" + (++Creados);
            // La raiz solo se ensancha un poco (AnchoDeCuerpo, para que entre por las puertas): el motor fija el pivote a 0,8 m del piso sin mirar la escala, asi
            // que escalar la raiz en Y hundiria el collider. El tamano doble se le da al modelo visual, estirandolo desde los pies para que sigan en el piso, y
            // al collider (alto y centro) para que los disparos le peguen en todo el cuerpo.
            s.transform.localScale = Vector3.Scale(s.transform.localScale, new Vector3(AnchoDeCuerpo, 1f, AnchoDeCuerpo));
            foreach (var r in s.GetComponents<SP.Presentation.CubeFxReactor>()) r.EscalaBase = s.transform.localScale;   // que el reactor de impactos no la devuelva a 1
            var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                var visual = smr.transform.parent != null && smr.transform.parent != s.transform ? smr.transform.parent : smr.transform;
                float piesAntes = smr.bounds.min.y, vy = visual.position.y;
                visual.localScale = new Vector3(visual.localScale.x * Ensanche.x / AnchoDeCuerpo, visual.localScale.y * Ensanche.y, visual.localScale.z * Ensanche.z / AnchoDeCuerpo);
                float piesDespues = vy + (piesAntes - vy) * Ensanche.y;
                visual.position -= Vector3.up * (piesDespues - piesAntes);   // los pies vuelven adonde estaban
            }
            var caja = s.GetComponent<BoxCollider>();
            if (caja != null)
            {
                float pies = caja.center.y - caja.size.y * 0.5f;   // la base de la caja no se mueve: crece hacia arriba
                var tam = caja.size; tam.y *= Ensanche.y; caja.size = tam;
                var cen = caja.center; cen.y = pies + tam.y * 0.5f; caja.center = cen;
            }
            return s.gameObject.AddComponent<EnemigoPesado>();
        }

        IEnumerator Start()
        {
            soldado = GetComponent<Soldier>(); cerebro = soldado != null ? soldado.Brain : null;
            yield return null; yield return null;   // despues de los Start de Soldier/AiBrain/SoldierLook, que pisan vida y arsenal
            if (soldado == null || soldado.Health == null) yield break;
            VidaMaxima = Mathf.Max(1, soldado.Health.MaxHealth) * MultiplicadorDeVida;
            soldado.Health.Initialize(soldado.Id, VidaMaxima);
            if (soldado.Motor != null) soldado.Motor.FactorDeClase = FactorDeVelocidad;
            ultimaPos = transform.position;
            var arma = soldado.Weapon;
            if (arma != null && !arma.Loadout.Contains(WeaponKind.Minigun)) { arma.Loadout.Insert(0, WeaponKind.Minigun); arma.EquipFromLoadout(0); }
            // Bug #389: en movimiento dispara rafaga continua (minigun); detenido, obuses explosivos como los tanques (el lanzacohetes: explosion de 5 m).
            if (arma != null && !arma.Loadout.Contains(WeaponKind.Rocket)) arma.Loadout.Add(WeaponKind.Rocket);
            // animacion propia (Mixamo "Gunplay": disparo sostenido): AC_Pesado es un override de AC_Soldado con el clip de disparo cambiado
            anim = GetComponentInChildren<Animator>(true);
            var ac = SP.Core.RecursosCache.Cargar<RuntimeAnimatorController>("Soldados/AC_Pesado");
            if (anim != null && ac != null) { anim.runtimeAnimatorController = ac; AnimacionPropia = true; for (int i = 0; i < anim.layerCount; i++) if (anim.GetLayerName(i) == "Recarga") capaRecarga = i; }
            GameLog.Line($"[Pesado] {name}: vida {VidaMaxima}, minigun, ensanche {Ensanche.x:0.00}");
        }

        Animator anim; int capaRecarga = -1; float pesoRecarga; bool recargandoAntes;
        public bool Recargando { get; private set; }
        void ManejarRecarga()
        {
            if (anim == null || capaRecarga < 0 || soldado == null || soldado.Weapon == null) return;
            bool recarga = soldado.Weapon.IsReloading && soldado.Health != null && soldado.Health.IsAlive;
            if (recarga && !recargandoAntes) anim.Play("Recarga", capaRecarga, 0f);   // la recarga siempre arranca desde el primer cuadro
            recargandoAntes = recarga;
            pesoRecarga = Mathf.MoveTowards(pesoRecarga, recarga ? 1f : 0f, Time.deltaTime * 4f);
            anim.SetLayerWeight(capaRecarga, pesoRecarga);
            Recargando = pesoRecarga > 0.5f;
        }

        // Arma segun el movimiento: en marcha la minigun, detenido (estacionando o en frenesi) el lanzacohetes.
        public WeaponKind ArmaEsperada => Estado == Fase.Frenesi ? WeaponKind.Rocket : WeaponKind.Minigun;
        void ElegirArma()
        {
            var arma = soldado != null ? soldado.Weapon : null;
            if (arma == null || VidaMaxima <= 0) return;   // VidaMaxima > 0 = Start ya corrio (arsenal armado)
            var quiero = ArmaEsperada;
            if (arma.CurrentWeaponKind == quiero || arma.IsReloading) return;
            int i = arma.Loadout.IndexOf(quiero);
            if (i >= 0) arma.EquipFromLoadout(i);
        }

        Vector3 ultimaPos; float recorrido;
        public int PasosPesados { get; private set; }
        // Cada MetrosPorPaso recorridos retumba el suelo: un golpe grave bajo y corto (el impacto pesado con el tono bajado).
        void PasosQueRetumban()
        {
            var p = transform.position; var d = p - ultimaPos; d.y = 0f; ultimaPos = p;
            if (soldado == null || soldado.Health == null || !soldado.Health.IsAlive || d.magnitude > 2f) return;
            recorrido += d.magnitude;
            if (recorrido < MetrosPorPaso) return;
            recorrido = 0f; PasosPesados++;
            SP.Presentation.AudioDirector.PlayAt(SP.Presentation.SfxKind.PasoPesado, p, 0.5f, 0.6f);
        }

        void Update()
        {
            PasosQueRetumban();
            ManejarRecarga();
            ElegirArma();
            if (cerebro == null || soldado == null || soldado.Health == null || !soldado.Health.IsAlive) return;
            var objetivo = cerebro.CurrentTarget;
            bool ve = objetivo != null && objetivo.Health != null && objetivo.Health.IsAlive;
            switch (Estado)
            {
                case Fase.Patrulla:
                    if (ve) { Estado = Fase.Estacionando; desde = Time.time; InicioDeEstacionar = desde; InicioDeFrenesi = -1f; ultimoVisto = Time.time; cerebro.Quieto = true; }   // lo ve: frena en seco
                    break;
                case Fase.Estacionando:
                    if (ve) ultimoVisto = Time.time;
                    if (Time.time - desde >= SegundosDeEstacionamiento) { Estado = Fase.Frenesi; desde = Time.time; InicioDeFrenesi = desde; }   // 2 s despues abre fuego sostenido
                    else if (Time.time - ultimoVisto > SegundosSinVerParaRelajarse) { Estado = Fase.Patrulla; cerebro.Quieto = false; }
                    break;
                case Fase.Frenesi:
                    cerebro.Quieto = true;
                    if (ve) ultimoVisto = Time.time;
                    else if (Time.time - ultimoVisto > SegundosSinVerParaRelajarse) { Estado = Fase.Patrulla; cerebro.Quieto = false; }
                    break;
            }
        }
    }
}
