using UnityEngine;
using SP.Core;
using SP.Tutorial;

namespace SP.EditorTools
{
    // FASE 25 (Ronda 19, Docs/PLAN_R19_v2.md, tarea G4): logica pura de la R19, sin Play.
    //   H25.1 ProgresoDeNiveles (carril A)      H25.2 tutorial simple v2 (carril A)
    //   H25.3 RetiroDeEnemigosCaidos.FaseEn (carril D) y H25.4 UmbralDelBlindado.Cruzo (carril E): van en sus propios metodos,
    //   llamados desde RunPhase25 por quien los escriba.
    // PENDIENTE: RunPhase25() todavia no se llama desde RunAll (HeadlessTestRunner.cs, despues de RunPhase23): esa linea es de otro carril.
    public static partial class HeadlessTestRunner
    {
        static void RunPhase25()
        {
            TestLog.Phase("FASE 25 - Ronda 19: progreso de niveles, tutorial simple v2");
            RunPhase25_1_ProgresoDeNiveles();
            RunPhase25_2_TutorialSimple();
            RunPhase25_3_RetiroDeCaidos();
            RunPhase25_4_UmbralDelBlindado();
        }

        // H25.3 (carril D): fases del cadaver por tiempo, funcion pura.
        static void RunPhase25_3_RetiroDeCaidos()
        {
            const float e = SP.Presentation.RetiroDeEnemigosCaidos.SegundosAntesDeRetirar;
            Check("H25.3 a los 2 s sigue caido", SP.Presentation.RetiroDeEnemigosCaidos.FaseEn(2f, e) == SP.Presentation.RetiroDeEnemigosCaidos.Fase.Caido);
            Check("H25.3 justo tras la espera hay destello", SP.Presentation.RetiroDeEnemigosCaidos.FaseEn(e + 0.1f, e) == SP.Presentation.RetiroDeEnemigosCaidos.Fase.Destello);
            Check("H25.3 despues del destello se desintegra", SP.Presentation.RetiroDeEnemigosCaidos.FaseEn(e + 0.5f, e) == SP.Presentation.RetiroDeEnemigosCaidos.Fase.Desintegrando);
            Check("H25.3 a los 6 s ya esta retirado", SP.Presentation.RetiroDeEnemigosCaidos.FaseEn(6f, e) == SP.Presentation.RetiroDeEnemigosCaidos.Fase.Retirado);
        }

        // H25.4 (carril E): el umbral del blindado es una linea en z.
        static void RunPhase25_4_UmbralDelBlindado()
        {
            Check("H25.4 el umbral esta en z -8", Mathf.Approximately(SP.Operacion.OperacionDirector.ZParaLaToma, -8f));
            Check("H25.4 z -9 no cruzo", !SP.Operacion.UmbralDelBlindado.Cruzo(-9f));
            Check("H25.4 z -7 cruzo", SP.Operacion.UmbralDelBlindado.Cruzo(-7f));
        }

        // H25.1: guarda, no empeora el tiempo y borra. Usa un nivel de prueba (99) para no tocar el progreso real del jugador.
        static void RunPhase25_1_ProgresoDeNiveles()
        {
            const int n = 99;
            ProgresoDeNiveles.Borrar(n);
            Check("H25.1 sin progreso: el nivel no esta completo y no tiene mejor tiempo", !ProgresoDeNiveles.EstaCompleto(n) && ProgresoDeNiveles.MejorTiempo(n) < 0f);
            ProgresoDeNiveles.Completar(n, 125.5f);
            Check("H25.1 Completar marca el nivel y guarda el tiempo", ProgresoDeNiveles.EstaCompleto(n) && Mathf.Abs(ProgresoDeNiveles.MejorTiempo(n) - 125.5f) < 0.01f);
            ProgresoDeNiveles.Completar(n, 300f);
            Check("H25.1 un tiempo peor NO pisa el mejor", Mathf.Abs(ProgresoDeNiveles.MejorTiempo(n) - 125.5f) < 0.01f);
            ProgresoDeNiveles.Completar(n, 61f);
            Check("H25.1 un tiempo mejor SI lo reemplaza", Mathf.Abs(ProgresoDeNiveles.MejorTiempo(n) - 61f) < 0.01f);
            ProgresoDeNiveles.Completar(n, 0f);
            Check("H25.1 un tiempo nulo no borra el mejor", ProgresoDeNiveles.EstaCompleto(n) && Mathf.Abs(ProgresoDeNiveles.MejorTiempo(n) - 61f) < 0.01f);
            Check("H25.1 formato mm:ss", ProgresoDeNiveles.Formato(61f) == "01:01" && ProgresoDeNiveles.Formato(-1f) == "--:--");
            Check("H25.1 claves sp_nivel{n}_completo / sp_nivel{n}_mejor_s", ProgresoDeNiveles.ClaveCompleto(1) == "sp_nivel1_completo" && ProgresoDeNiveles.ClaveMejorTiempo(2) == "sp_nivel2_mejor_s");
            ProgresoDeNiveles.Borrar(n);
            Check("H25.1 Borrar deja el nivel sin progreso", !ProgresoDeNiveles.EstaCompleto(n) && ProgresoDeNiveles.MejorTiempo(n) < 0f && !PlayerPrefs.HasKey(ProgresoDeNiveles.ClaveCompleto(n)));
        }

        // H25.2: la lista del simple tiene PasosSimples ids, la clave de progreso es nueva y la ayuda del completo sigue en 16 s.
        static void RunPhase25_2_TutorialSimple()
        {
            Check("H25.2 IdsSimples.Count == PasosSimples (" + TutorialManager.IdsSimples.Count + " / " + TutorialManager.PasosSimples + ")", TutorialManager.IdsSimples.Count == TutorialManager.PasosSimples);
            Check("H25.2 el simple termina con canon_simple y victoria, y no tiene mira/seguir/fps/mira_tanque",
                TutorialManager.IdsSimples.Contains("canon_simple") && TutorialManager.IdsSimples.Contains("victoria")
                && !TutorialManager.IdsSimples.Contains("mira") && !TutorialManager.IdsSimples.Contains("seguir")
                && !TutorialManager.IdsSimples.Contains("fps") && !TutorialManager.IdsSimples.Contains("mira_tanque"));
            Check("H25.2 la clave de progreso del simple no es la vieja (el progreso se guarda por indice)",
                TutorialManager.ClaveProgresoSimple != TutorialManager.ClaveProgresoSimpleVieja && TutorialManager.ClaveProgresoSimple != TutorialManager.ClaveProgresoCompleto);
            Check("H25.2 ayuda: 16 s en el completo, 8 s en el simple",
                Mathf.Approximately(TutorialManager.SegundosParaAyuda, 16f) && Mathf.Approximately(TutorialManager.SegundosParaAyudaSimple, 8f));
        }
    }
}
