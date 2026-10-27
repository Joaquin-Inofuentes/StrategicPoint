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
        int torretasAvisadas;

        // La baliza (y el rombo de objetivo) apunta a la torreta de artilleria que sigue en pie; con las dos caidas, al porton norte.
        void BalizaDeInfiltrar()
        {
            TorreDestruible mejor = null;
            if (generador != null && !generador.EstaCaido) { PonerBaliza("GENERADOR", new Color(1f, 0.82f, 0.3f), generador.transform.position, null); return; }
            if (torres != null)
                foreach (var t in torres)
                    if (t != null && t.tipo == TorreDestruible.Tipo.Torreta && !t.EstaCaida && (mejor == null || t.transform.position.z < mejor.transform.position.z)) mejor = t;
            if (mejor != null) PonerBaliza("TORRETA", new Color(1f, 0.55f, 0.3f), mejor.transform.position, null);
            else PonerBaliza("SALIDA NORTE", new Color(1f, 0.82f, 0.3f), portonCuartel != null ? portonCuartel.transform.position : transform.position, null);
        }
        float tAvisoDePaso;
        void TickInfiltrar()
        {
            int vivos = Vivos(enemigosCuartel);
            // Bug #139: "al querer pasar me resalte abajo que debo cumplir el objetivo primero": cerca del porton cerrado, un aviso explica que falta.
            var yoPaso = PlayerInputDriver.Activo != null && PlayerInputDriver.Activo.Brain != null ? PlayerInputDriver.Activo.Brain.Current : null;
            if (yoPaso != null && portonCuartel != null && portonCuartel.activeSelf && Time.time >= tAvisoDePaso)
            {
                var dp = portonCuartel.transform.position - yoPaso.transform.position; dp.y = 0f;
                if (dp.magnitude < 15f)
                {
                    tAvisoDePaso = Time.time + 5f;
                    AvisoUnico.Mostrar(generador != null ? "PRIMERO CORTÁ LA ENERGÍA: DESTRUÍ EL GENERADOR DEL COMANDO" : "PRIMERO CUMPLÍ EL OBJETIVO: DERRIBÁ LAS 2 TORRETAS DE ARTILLERÍA", 4f, new Color(1f, 0.7f, 0.3f), AvisoUnico.PrioridadAlta);
                }
            }
            // Pedido: "primero debes detonar los 2 artilleros". Bug #169 ("es muy molesto tener que matar a todos los soldados, que solo sea destruir los 2 puestos"):
            // el objetivo del cuartel es SOLO derribar las 2 torretas de artilleria (solo explosivos); los soldados que queden no bloquean el portón.
            int torretas = TorresCaidas(TorreDestruible.Tipo.Torreta);
            if (torretas != torretasAvisadas)
            {
                torretasAvisadas = torretas;
                BalizaDeInfiltrar();
                if (torretas > 0) AvisoUnico.Mostrar(torretas >= TorresDeTorreta ? "TORRETAS DESTRUIDAS · EL PORTÓN NORTE SE ABRE" : $"TORRETA DESTRUIDA {torretas}/{TorresDeTorreta}", 3.5f, new Color(1f, 0.7f, 0.3f), AvisoUnico.PrioridadAlta);
            }
            bool sinEnergia = generador != null && generador.EstaCaido;
            if (sinEnergia && !apagonHecho) ApagarCuartel();
            if (hud != null && generador != null)
            {
                hud.Objetivo(TituloObjetivo(1, "CORTÁ LA ENERGÍA DEL CUARTEL"), $"Destruí el GENERADOR del comando (norte) · apaga los reflectores y abre el portón · torretas {torretas}/{TorresDeTorreta} (opcional)", sinEnergia ? 1f : 0f, new Color(1f, 0.82f, 0.3f));
                if (!sinEnergia) hud.Subobjetivos(LineaDeSubobjetivos(vivos));
            }
            else if (hud != null)
            {
                hud.Objetivo(TituloObjetivo(1, "DETONÁ LAS 2 TORRETAS"), $"Artillería antiaérea destruida {torretas}/{TorresDeTorreta} · solo explosivos (el ASALTO las demuele con [Q])", torretas / (float)TorresDeTorreta, new Color(1f, 0.55f, 0.3f));
                if (torretas < TorresDeTorreta) hud.Subobjetivos(LineaDeSubobjetivos(vivos));
            }
            if (torretas >= TorresDeTorreta || sinEnergia) { AbrirPorton(); PuestoActual = 0; EntrarFase(FaseOperacion.Puestos); }
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

        bool apagonHecho;

        // Sin energia: los 4 reflectores se apagan (el cuartel queda en sombra) y suena la alarma baja.
        void ApagarCuartel()
        {
            apagonHecho = true;
            if (reflectores != null) foreach (var r in reflectores) if (r != null) r.RomperYa();
            AvisoUnico.Mostrar("ENERGÍA CORTADA · REFLECTORES APAGADOS · EL PORTÓN NORTE SE ABRE", 4f, new Color(1f, 0.82f, 0.3f), AvisoUnico.PrioridadAlta);
            SesionLog.Evento("Cuartel sin energia: generador destruido");
        }

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
