using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // Una MonoBehaviour que se agrega a la escena desde el builder debe vivir en un archivo con su mismo nombre: si no, el player
    // no encuentra el script y la escena sale 'corrupta' (bug del nivel 2 en la build). Antes estaba dentro de FrancotiradorEnTorre.cs.
    public class ArtilleroDeTorreta : TiradorDeTorre
    {
        public const int Dano = 10;
        public const float Cadencia = 0.1f;
        public const int Rafaga = 8;
        public const float AlcanceDeLaTorreta = 55f;
        public const float ArcoDeLaTorreta = 120f;

        float reaccion, pausa;
        int restantes;
        public int Rafagas { get; private set; }

        protected override void Awake() { base.Awake(); alcance = AlcanceDeLaTorreta; arcoGrados = ArcoDeLaTorreta; }

        protected override WeaponKind ArmaEsperada => WeaponKind.Smg;
        protected override void Equipar()
        {
            var w = soldado.Weapon;
            w.EquipWeapon(WeaponKind.Smg, Dano, Cadencia, new Color(1f, 0.85f, 0.3f));
            w.ConfigurarCargador(400, 3.2f);
            w.MultiplicadorTorretaFija = 0.5f;   // montada sobre un pivote: mas estable que a mano
        }

        void Update()
        {
            if (!Application.isPlaying || soldado == null || !Activo) return;
            ReafirmarArma();
            float dt = Time.deltaTime;
            if (Time.time >= proximaBusqueda)
            {
                proximaBusqueda = Time.time + 0.25f;
                var nuevo = Buscar();
                if (nuevo != Objetivo) { reaccion = 0.5f; }
                Objetivo = nuevo;
            }
            if (Objetivo == null) { restantes = 0; return; }
            if (!Valido(Objetivo)) { Objetivo = null; return; }
            Girar(Pecho(Objetivo), dt);
            if (reaccion > 0f) { reaccion -= dt; return; }
            if (pausa > 0f) { pausa -= dt; return; }
            if (restantes <= 0) { restantes = Rafaga; Rafagas++; }
            var boca = Boca;
            var dir = (Pecho(Objetivo) - boca).normalized;
            if (soldado.Weapon.TryFire(boca, dir, 2.2f))
            {
                Disparos++;
                restantes--;
                if (restantes <= 0) pausa = Random.Range(1.4f, 2.4f);
            }
        }
    }
}
