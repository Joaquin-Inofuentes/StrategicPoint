using System;
using UnityEngine;

namespace SP.Tutorial
{
    // Todos los booleanos del tutorial, a la vista en el Inspector (componente
    // TutorialManager > Banderas). El cuadro de dialogo elige el mensaje
    // mirando estas banderas: el primer sub-paso cuya bandera sigue en false
    // es el que se le pide al jugador ahora.
    [Serializable]
    public class TutorialFlags
    {
        // 1 Camara
        public bool camaraHorizontal, camaraVertical;
        // 2 WASD
        public bool teclaW, teclaA, teclaS, teclaD;
        // 3 Disparar
        public bool disparoEnemigo, disparoPared, disparoDestruible;
        // 2b Correr
        public bool corre, caminaDeNuevo;
        // 6b Mira en primera persona
        public bool apuntaEnPrimeraPersona, sueltaLaMira;
        // 6c Vista tactica (coberturas y rutas)
        public bool verCoberturas, ocultaCoberturas;
        // 13b Reanimar a un caido
        public bool ordenDeReanimar, aliadoReanimado;
        // 14b Ametralladora fija
        public bool cercaDeLaTorreta, enTorretaFija, disparoTorretaFija, salioDeLaTorreta;
        // 14c Modo dios
        public bool modoDiosOn, modoDiosOff;
        // 14d Mira del tanque
        public bool miraDelCanon, miraDeMetralleta, sueltaMiraTanque;
        // 4 Cambiar de soldado (radial)
        public bool apuntoAlAliado, cambioDeSoldado, radialAbierto;
        // 5 Agacharse
        public bool agachado, levantado;
        // 6 Mira
        public bool apuntaConZoom, disparaConZoom;
        // 7 RTS
        public bool rtsActivo, aliadosSeleccionados, ordenDeMoverARts;
        // 8 Volver a FPS
        public bool vueltaAFps, mouseCapturado;
        // 9 Siganme y quietos (radial)
        public bool ordenDeSeguir, aliadosSiguen, ordenDeQuietos;
        // 10 Seleccionar en FPS
        public bool aliadoSeleccionadoEnFps;
        // 11 Mover en FPS
        public bool ordenDeMoverEnFps;
        // 11b Cubrirse, curar y demoler (radial)
        public bool ordenDeCubrirse, aliadosEnCobertura;
        public bool ordenDeCurar, aliadoCurado;
        public bool apuntaAlMuro, cargandoDemolicion, muroDemolido;
        // 12 Entrar al tanque
        public bool cercaDelTanque, dentroDelTanque;
        // 13 Aliados al tanque
        public bool ordenDeSubir, aliadosABordo;
        // 14 Torreta
        public bool enLaTorreta;
        // 15 Avanzar y disparar
        public bool ordenDeAvanzar, disparoCanon, enemigosEliminados;
        // 16 Final
        public bool llegoAlFinal;
        // 17 Victoria
        public bool victoria;

        public void Reiniciar() => JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new TutorialFlags()), this);
    }
}
