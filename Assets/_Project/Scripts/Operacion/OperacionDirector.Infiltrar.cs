using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.Tutorial;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    // Fase 1 (INFILTRAR): cuartel militar. Hundir() lo comparten el porton y las barreras de los puestos.
    public partial class OperacionDirector
    {
        // ---- 1. cuartel ----
        void TickInfiltrar()
        {
            int vivos = Vivos(enemigosCuartel);
            if (hud != null) hud.Objetivo(TituloObjetivo(1, "INFILTRAR EL CUARTEL"), vivos > 0 ? $"Elimina a TODOS los soldados del cuartel · quedan {vivos}" : "Cuartel limpio: el porton norte se abre", 1f - (float)vivos / Mathf.Max(1, enemigosCuartel.Length), new Color(1f, 0.82f, 0.3f));
            // WP8 (#078): la linea de estructuras. Destruirlas es opcional (el objetivo son los soldados), pero mata a quien esta arriba.
            if (hud != null && vivos > 0) hud.Subobjetivos(LineaDeSubobjetivos(vivos));
            if (vivos == 0) { AbrirPorton(); PuestoActual = 0; EntrarFase(FaseOperacion.Puestos); }
        }

        public const int TorresDeFrancotirador = 2, TorresDeTorreta = 2, TotalDeReflectores = 4;

        public int TorresCaidas(TorreDestruible.Tipo tipo)
        {
            int n = 0;
            if (torres != null) foreach (var t in torres) if (t != null && t.tipo == tipo && t.EstaCaida) n++;
            return n;
        }

        public int ReflectoresRotos
        {
            get { int n = 0; if (reflectores != null) foreach (var r in reflectores) if (r != null && r.EstaRoto) n++; return n; }
        }

        public string LineaDeSubobjetivos(int vivos)
            => $"Torres {TorresCaidas(TorreDestruible.Tipo.Francotirador)}/{TorresDeFrancotirador} · Torretas {TorresCaidas(TorreDestruible.Tipo.Torreta)}/{TorresDeTorreta} · Reflectores {ReflectoresRotos}/{TotalDeReflectores} · Soldados restantes: {vivos}";

        void AbrirPorton()
        {
            if (portonCuartel != null && portonCuartel.activeSelf) StartCoroutine(Hundir(portonCuartel));
        }

        // La barrera baja al piso (no se rompe: es un puesto, no una pared) y se avisa a la navegacion.
        IEnumerator Hundir(GameObject cubo)
        {
            var t = cubo.transform;
            var inicio = t.position;
            var alto = t.lossyScale.y;
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / 1.1f;
                t.position = inicio + Vector3.down * (alto + 0.2f) * Mathf.SmoothStep(0f, 1f, k);
                yield return null;
            }
            cubo.SetActive(false);
            NavService.Invalidate();
            NavMeshViva.Solicitar();
            Coberturas.Registrar();
        }
    }
}
