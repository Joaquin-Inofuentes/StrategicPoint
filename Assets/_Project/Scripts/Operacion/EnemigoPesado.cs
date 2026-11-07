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
        public const int MultiplicadorDeVida = 4;
        public const float SegundosDeEstacionamiento = 2f, SegundosSinVerParaRelajarse = 3f;
        public static readonly Vector3 Ensanche = new Vector3(1.12f, 1.04f, 1.12f);
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
            s.transform.localScale = Vector3.Scale(s.transform.localScale, Ensanche);
            return s.gameObject.AddComponent<EnemigoPesado>();
        }

        IEnumerator Start()
        {
            soldado = GetComponent<Soldier>(); cerebro = soldado != null ? soldado.Brain : null;
            yield return null; yield return null;   // despues de los Start de Soldier/AiBrain/SoldierLook, que pisan vida y arsenal
            if (soldado == null || soldado.Health == null) yield break;
            VidaMaxima = Mathf.Max(1, soldado.Health.MaxHealth) * MultiplicadorDeVida;
            soldado.Health.Initialize(soldado.Id, VidaMaxima);
            var arma = soldado.Weapon;
            if (arma != null && !arma.Loadout.Contains(WeaponKind.Minigun)) { arma.Loadout.Insert(0, WeaponKind.Minigun); arma.EquipFromLoadout(0); }
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

        void Update()
        {
            ManejarRecarga();
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
