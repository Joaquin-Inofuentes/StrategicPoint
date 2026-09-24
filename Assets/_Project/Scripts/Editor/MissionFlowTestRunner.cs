using System.Collections;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido explicito: "testea el completar la mision inicial" + "testea el
    // nivel entero hasta terminarlo y quiero que diseñes un codigo para
    // testear todo". HeadlessTestRunner (el resto de este mismo directorio)
    // ya cubre en profundidad la MECANICA de cada sistema (armas, granadas,
    // vehiculos, radial, UI...) armando su propia escena sintetica sin
    // depender de Play Mode. Esto es COMPLEMENTARIO y de otro tipo: corre
    // DENTRO de Play Mode, sobre la escena real (SC_Gameplay), y verifica
    // el FLUJO completo de la mision de punta a punta -- que un jugador
    // realmente pueda llegar de Infiltrar a Victoria, usando el mismo
    // camino de codigo que un jugador real (SaltarAFase solo salta las
    // fases; la condicion de victoria de TickEscapar es la de siempre, no
    // se fuerza).
    //
    // Uso: entrar en Play Mode sobre SC_Gameplay y correr "Strategic Point
    // / Probar flujo de mision completo (Play Mode)". El resultado queda en
    // la consola y en GameFlowLog.txt (mismo archivo que ya usa GameLog),
    // asi que se puede levantar desde afuera del Editor (CI, un script, la
    // pipeline de Unity) sin tener que leer la consola.
    public static class MissionFlowTestRunner
    {
        [MenuItem("Strategic Point/Probar flujo de mision completo (Play Mode)")]
        public static void ProbarFlujoCompleto()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[MissionFlowTest] Hay que estar en Play Mode (con SC_Gameplay cargada) para correr esto.");
                return;
            }

            var go = new GameObject("MissionFlowTestRunner_Temp") { hideFlags = HideFlags.DontSave };
            go.AddComponent<MissionFlowTestBehaviour>();
        }
    }

    // MenuItem no puede arrancar una corrutina directo (no es un
    // MonoBehaviour): este componente temporal existe solo para eso y se
    // destruye solo al terminar.
    class MissionFlowTestBehaviour : MonoBehaviour
    {
        void Start() => StartCoroutine(Correr());

        IEnumerator Correr()
        {
            var md = SP.Mision.MisionDirector.Instancia;
            if (md == null) { Fallo("No hay MisionDirector en la escena (¿es SC_Gameplay?)."); yield break; }

            var driver = SP.Player.PlayerInputDriver.Activo;
            if (driver == null || driver.Brain == null || driver.Brain.Current == null)
            { Fallo("No hay un soldado poseido por el jugador todavia."); yield break; }

            Debug.Log($"[MissionFlowTest] Arrancando. Fase actual: {md.Fase}.");

            // Modo dios durante la prueba: lo que se verifica es que el
            // FLUJO de fases y la condicion de victoria funcionen, no si el
            // jugador sobrevive al camino -- eso ya lo cubre jugar en serio
            // (y HeadlessTestRunner para el combate en si). Se restaura el
            // estado previo al terminar, sea cual sea.
            bool godModePrevio = SP.Core.ModoDios.Activo;
            SP.Core.ModoDios.Poner(true);

            // Salta derecho a Escapar: SaltarAFase ya hace aparecer al
            // civil y lo marca rescatado (ver su comentario en
            // MisionDirector.cs), asi que no hace falta jugar Infiltrar/
            // Resistir/Rescatar a mano para probar que la EXTRACCION
            // funciona -- que es la parte que de verdad cierra la mision.
            md.SaltarAFase(SP.Mision.FaseDeMision.Escapar);
            yield return null;
            if (md.Fase != SP.Mision.FaseDeMision.Escapar)
            { Fallo($"SaltarAFase(Escapar) no dejo la fase en Escapar (quedo en {md.Fase})."); SP.Core.ModoDios.Poner(godModePrevio); yield break; }

            if (md.Civil == null) { Fallo("SaltarAFase(Escapar) no genero al civil."); SP.Core.ModoDios.Poner(godModePrevio); yield break; }

            // Jugador y civil, los dos cerca del helipuerto: la condicion
            // real de Ganar() (TickEscapar en MisionDirector.cs) es de
            // distancia a Helipuerto para ambos -- no se llama a Ganar()
            // a mano, se deja que el propio tick del juego la dispare sola.
            var yo = driver.Brain.Current;
            yo.transform.position = md.Helipuerto + new Vector3(2f, 0.8f, 0f);
            md.Civil.transform.position = md.Helipuerto + new Vector3(-2f, 0.8f, 0f);

            float limite = Time.time + 8f;
            while (Time.time < limite && md.Fase != SP.Mision.FaseDeMision.Victoria && md.Fase != SP.Mision.FaseDeMision.Derrota)
                yield return null;

            SP.Core.ModoDios.Poner(godModePrevio);

            if (md.Fase == SP.Mision.FaseDeMision.Victoria)
                SP.Core.GameLog.Line("[MissionFlowTest] EXITO: la mision se completo de punta a punta (jugador + civil llegaron al helipuerto, Fase=Victoria).");
            else
                Fallo($"La mision no llego a Victoria a tiempo (fase final: {md.Fase}).");

            Destroy(gameObject);
        }

        void Fallo(string motivo)
        {
            SP.Core.GameLog.Line("[MissionFlowTest] FALLO: " + motivo);
            Debug.LogError("[MissionFlowTest] FALLO: " + motivo);
            Destroy(gameObject);
        }
    }
}
