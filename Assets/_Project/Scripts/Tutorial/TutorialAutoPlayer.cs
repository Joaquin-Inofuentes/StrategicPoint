using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Player;
using SP.Vehicles;
using SP.CameraSystem;

namespace SP.Tutorial
{
    // Reproductor automatico y cronometrado del tutorial (pedido: "quiero un script con
    // timers para el tutorial que de manera mecanica haga todo el tutorial para testear
    // todas las funciones"). En vez de saltar pasos con [F8] SaltarPaso, ejecuta el GESTO
    // REAL de cada paso llamando a las mismas APIs de gameplay que HeadlessTestRunner.Fase19.cs
    // usa para sus "gestos reales" (Motor.RotateYaw, Motor.Move, SetRunning, SetCrouching,
    // Jump, TryFire, EjecutarOrdenRadial, etc.), esperando entre accion y accion con
    // WaitForSeconds y verificando con un timeout que TutorialManager.Indice efectivamente
    // avanza (igual de espiritu que Check()/TEST FALLIDO en HeadlessTestRunner).
    //
    // Los pasos que dependen de apuntar con el mouse a un punto exacto del mundo (mira en
    // primera persona sostenida sobre un objetivo variable, demoler, torretas, tanque,
    // suministros por contacto fisico) no tienen todavia un gesto 100% fiel disponible sin
    // simular Input System de mouse: para esos se usa TutorialManager.SaltarPaso() como
    // reserva, y queda logueado como "SALTADO" (no como gesto real), para que quede claro
    // que ese tramo del tutorial sigue dependiendo de la prueba manual en Play.
    //
    // Se agrega como componente en runtime (no vive en la escena) y se dispara desde el
    // menu de Editor "Strategic Point/Tutorial/Reproducir Tutorial Automatico (con timers)"
    // (ver Assets/_Project/Scripts/Editor/TutorialAutoPlayerMenu.cs).
    public partial class TutorialAutoPlayer : MonoBehaviour
    {
        public const float TimeoutPorPaso = 45f;   // si un paso no avanza en este tiempo, se reporta como fallo
        const float Pausa = 0.35f;                 // respiro entre gesto y gesto (ademas de pausaEntrePasos del tutorial)

        int pasosOk, pasosFallidos, pasosSaltados, pasosReintento;
        readonly StringBuilder resumen = new StringBuilder();
        AutoplayRegistro reg;
        bool gestoRealActual, cerrado;

        public int DesdePaso;   // pasos anteriores a este se dan por cumplidos (preparacion, no cuentan como gesto)
        public bool IgnorarHumano = true;   // mientras corre, teclado/mouse/mando reales quedan desactivados
        public bool ConCapturas = true;     // captura antes, durante (cada 6 s) y despues de cada paso
        public string RunId => reg != null ? reg.RunId : null;
        public string Carpeta => reg != null ? reg.Carpeta : null;

        // Devuelve el reproductor: RunId y Carpeta dicen donde queda el registro (Logs/Autoplay/<runId>/).
        public static TutorialAutoPlayer Lanzar(int desdePaso = 0, bool ignorarHumano = true, bool capturas = true)
        {
            var go = new GameObject("_TutorialAutoPlayer_Runtime");
            var c = go.AddComponent<TutorialAutoPlayer>();
            c.DesdePaso = desdePaso; c.IgnorarHumano = ignorarHumano; c.ConCapturas = capturas;
            // El registro y el bloqueo de la entrada humana arrancan ya, antes de que la corrida espere al tutorial.
            var tm = TutorialManager.Instance;
            var driver = FindFirstObjectByType<PlayerInputDriver>();
            if (ignorarHumano) { c.Entrada.SoltarTodo(); EntradaVirtual.IgnorarHumano = true; }
            c.reg = AutoplayRegistro.Iniciar(go, driver, tm, capturas, ignorarHumano);
            c.StartCoroutine(c.Envoltorio());
            return c;
        }

        // Pase lo que pase (fin normal, error, se detuvo Play), la entrada humana vuelve y el registro se cierra.
        IEnumerator Envoltorio()
        {
            yield return Reproducir();
            Cerrar(pasosFallidos == 0 ? "COMPLETA" : "COMPLETA_CON_FALLOS");
        }

        void Cerrar(string resultado)
        {
            if (cerrado) return;
            cerrado = true;
            if (EntradaVirtual.Actual != null) EntradaVirtual.Actual.SoltarTodo();
            EntradaVirtual.IgnorarHumano = false;
            if (reg != null) reg.Finalizar(resultado);
        }

        void OnDestroy() { Cerrar("INTERRUMPIDA"); }

        IEnumerator Reproducir()
        {
            Log("[AUTOPLAY] Arrancando reproduccion automatica y cronometrada del tutorial...");
            float t0 = Time.time;

            // Espera a que el tutorial exista y arranque su primer paso.
            float esperaMax = Time.time + 15f;
            while ((TutorialManager.Instance == null || TutorialManager.Instance.Indice < 0) && Time.time < esperaMax) yield return null;
            var tm = TutorialManager.Instance;
            if (tm == null) { Log("[AUTOPLAY] FALLO: no aparecio TutorialManager.Instance (¿la escena activa es SC_Tutorial?)"); yield break; }

            var driver = FindFirstObjectByType<PlayerInputDriver>();
            if (driver == null) { Log("[AUTOPLAY] FALLO: no hay PlayerInputDriver en la escena"); yield break; }
            if (reg != null) reg.Corrida.pasosTotal = tm.Total;

            int totalPasos = tm.Total;
            while (tm.Indice < DesdePaso && !tm.Terminado) { int i0 = tm.Indice; yield return new WaitForSeconds(0.3f); tm.SaltarPaso(); float l0 = Time.time + 4f; while (tm.Indice == i0 && Time.time < l0) yield return null; }
            Log($"[AUTOPLAY] TutorialManager listo. {totalPasos} pasos a recorrer.");

            while (tm != null && !tm.Terminado && tm.Indice >= 0 && tm.Indice < tm.Total)
            {
                int indiceAntes = tm.Indice;
                var paso = tm.PasoActual;
                string id = paso != null ? paso.Id : "?";
                string titulo = paso != null ? paso.Titulo : "?";
                float tPasoInicio = Time.time;
                Log($"[AUTOPLAY] Paso {indiceAntes + 1}/{totalPasos} '{id}' ({titulo})...");
                float tope = id == "avanzar_disparar" || id == "final" || id == "entrar_tanque" ? 100f : TimeoutPorPaso;

                if (reg != null)
                {
                    reg.PasoInicio(indiceAntes, paso);
                    reg.Tope(tope);
                    yield return reg.Capturar("antes");
                    reg.IniciarDurante();
                }

                // Gesto + espera a que el propio TutorialManager lo reconozca; si no avanza, un reintento antes de darlo por fallido.
                int intentos = 0; bool avanzo = false;
                while (true)
                {
                    intentos++;
                    yield return EjecutarGesto(id, driver, tm);
                    float limite = Time.time + (intentos == 1 ? tope : tope * 0.5f);
                    while (tm.Indice == indiceAntes && !tm.Terminado && Time.time < limite) yield return null;
                    avanzo = tm.Indice > indiceAntes || tm.Terminado;
                    if (avanzo || intentos >= 2 || !gestoRealActual) break;
                    Log($"[AUTOPLAY]   REINTENTO: el paso '{id}' no avanzo con el primer gesto; se repite una vez.");
                    if (reg != null) reg.Reintento("no avanzo tras el primer gesto");
                }

                float dur = Time.time - tPasoInicio;
                string resultado;
                if (avanzo)
                {
                    if (gestoRealActual)
                    {
                        if (intentos > 1) { pasosReintento++; resultado = "OK_REINTENTO"; } else { pasosOk++; resultado = "OK"; }
                        Log($"[AUTOPLAY]   OK (gesto real{(intentos > 1 ? ", con reintento" : "")}) paso '{id}' avanzo en {dur:0.0}s");
                    }
                    else { pasosSaltados++; resultado = "SALTADO"; Log($"[AUTOPLAY]   SALTADO paso '{id}' (sin gesto real automatizado; se marco cumplido con SaltarPaso) en {dur:0.0}s"); }
                    resumen.AppendLine($"  {indiceAntes + 1,2}. {id,-18} {resultado}  {dur:0.0}s");
                }
                else
                {
                    pasosFallidos++; resultado = "FALLIDO_FORZADO";
                    Log($"[AUTOPLAY]   TEST FALLIDO: el paso '{id}' no avanzo en {dur:0}s (tope {tope:0}s, {intentos} intentos) pese al gesto. Se fuerza con SaltarPaso para no trabar la reproduccion.");
                    resumen.AppendLine($"  {indiceAntes + 1,2}. {id,-18} FALLO (timeout) -> forzado");
                    tm.SaltarPaso();
                    float limite2 = Time.time + 3f;
                    while (tm.Indice == indiceAntes && Time.time < limite2) yield return null;
                }
                if (reg != null) { reg.ActualizarIntentos(intentos); yield return reg.PasoFin(resultado); }
            }

            float total = Time.time - t0;
            Log("==================================================================");
            Log($"[AUTOPLAY] TUTORIAL COMPLETO en {total:0.0}s. OK(gesto real)={pasosOk}  OK con reintento={pasosReintento}  Saltados={pasosSaltados}  Fallidos={pasosFallidos}  Total pasos={totalPasos}");
            Log(resumen.ToString());
            Log("==================================================================");
        }

        // Tabla: Id del paso -> gesto real (o SaltarPaso como reserva). Deja en gestoRealActual si fue gesto real.
        IEnumerator EjecutarGesto(string id, PlayerInputDriver driver, TutorialManager tm)
        {
            gestoRealActual = true;
            switch (id)
            {
                case "camara": yield return GestoCamara(driver); break;
                case "wasd": yield return GestoWasdReal(); break;
                case "correr": yield return GestoCorrerReal(); break;
                case "disparar": yield return GestoDispararReal(driver); break;
                case "cambiar": yield return GestoCambiarDeSoldado(driver); break;
                case "agacharse": yield return GestoAgacharseReal(); break;
                case "saltar": yield return GestoSaltar(driver); break;
                case "mira": yield return GestoMiraReal(driver); break;
                case "arsenal": yield return GestoArsenalReal(driver); break;
                case "cuchillo": yield return GestoCuchilloReal(driver); break;
                case "granada": yield return GestoGranadaReal(driver); break;
                case "suministros": yield return GestoSuministrosReal(driver); break;
                case "vista_tactica": yield return GestoVistaTacticaReal(); break;
                case "rts": yield return GestoRts(driver); break;
                case "fps": yield return GestoFps(driver); break;
                case "seguir": yield return GestoSeguirYQuietos(driver); break;
                case "seleccionar": yield return GestoSeleccionarReal(driver); break;
                case "mover_fps": yield return GestoMoverFpsReal(driver); break;
                case "ir_atacar": yield return GestoIrYAtacarReal(driver); break;
                case "cubrirse": yield return GestoCubrirseReal(driver); break;
                case "curar": yield return GestoCurarAliadoReal(driver); break;
                case "reanimar": yield return GestoReanimarReal(driver); break;
                case "demoler": yield return GestoDemolerReal(driver); break;
                case "bomba_aliado": yield return GestoBombaAliadoReal(driver); break;
                case "torreta_fija": yield return GestoTorretaFijaReal(driver); break;
                case "entrar_tanque": yield return GestoEntrarTanqueReal(driver); break;
                case "mira_tanque": yield return GestoMiraTanqueReal(); break;
                case "avanzar_disparar": yield return GestoAvanzarYDispararReal(driver); break;
                case "final": yield return GestoFinalReal(driver); break;
                case "formaciones": yield return GestoFormaciones(driver); break;
                case "curarme": yield return GestoCurarme(driver); break;
                case "modo_dios": yield return GestoModoDios(); break;
                case "aliados_tanque": yield return GestoAliadosAlTanqueReal(driver); break;
                case "torreta": yield return GestoCambiarATorretaReal(); break;
                case "bajar_tanque": yield return GestoBajarTanqueReal(driver); break;
                default:
                    gestoRealActual = false;
                    yield return new WaitForSeconds(Pausa);
                    tm.SaltarPaso();
                    break;
            }
        }

        static void Log(string s) => Debug.Log(s);

        // ---------------------------------------------------------------
        // Gestos reales: mismas APIs que HeadlessTestRunner.Fase19.cs, pero durante Play,
        // con timers reales (WaitForSeconds) en vez de una sola llamada instantanea, porque
        // el Evaluar() del tutorial corre en Update() con Time.deltaTime real.
        // ---------------------------------------------------------------

        IEnumerator GestoCamara(PlayerInputDriver driver)
        {
            var motor = driver.Brain.Current.Motor;
            var rig = driver.Rig;
            // Yaw: el paso exige acumular >=50 grados (ver TutorialManager.DefinirPasos, paso "camara").
            for (int i = 0; i < 6 && rig != null; i++) { motor.RotateYaw(12f); yield return null; }
            yield return new WaitForSeconds(Pausa);
            // Pitch: >=18 grados acumulados.
            if (rig != null) { rig.AddPitch(10f); yield return null; rig.AddPitch(10f); }
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoWasd(PlayerInputDriver driver)
        {
            var motor = driver.Brain.Current.Motor;
            Vector3[] dirs = { Vector3.forward, Vector3.left, Vector3.back, Vector3.right };
            foreach (var d in dirs)
            {
                for (int i = 0; i < 6; i++) { motor.Move(d, Time.deltaTime > 0f ? Time.deltaTime : 0.02f); yield return null; }
                yield return new WaitForSeconds(0.15f);
            }
        }

        IEnumerator GestoCorrer(PlayerInputDriver driver)
        {
            var motor = driver.Brain.Current.Motor;
            motor.SetRunning(true);
            float hasta = Time.time + 1.4f;
            while (Time.time < hasta) { motor.Move(Vector3.forward, Time.deltaTime); yield return null; }
            motor.SetRunning(false);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoDisparar(PlayerInputDriver driver)
        {
            var yo = driver.Brain.Current;
            var dummy = GameObject.Find("Tut_Enemigo_Estatico");
            var pared = GameObject.Find("Tut_Pared");
            var destruible = GameObject.Find("Tut_Destruible");
            foreach (var obj in new[] { dummy, pared, destruible })
            {
                if (obj == null) continue;
                var origen = yo.transform.position + Vector3.up * 1.5f;
                var dir = (obj.transform.position + Vector3.up * 0.9f - origen).normalized;
                float hasta = Time.time + 2.5f;
                while (Time.time < hasta)
                {
                    yo.Weapon.TryFire(origen, dir);
                    yield return new WaitForSeconds(0.08f);
                }
                yield return new WaitForSeconds(Pausa);
            }
        }

        IEnumerator GestoCambiarDeSoldado(PlayerInputDriver driver)
        {
            Soldier otro = null;
            foreach (var s in driver.Squad) if (s != null && s != driver.Brain.Current && s.Health.IsAlive) { otro = s; break; }
            if (otro != null) driver.TryPossess(otro);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoAgacharse(PlayerInputDriver driver)
        {
            var motor = driver.Brain.Current.Motor;
            motor.SetCrouching(true);
            yield return new WaitForSeconds(0.6f);
            motor.SetCrouching(false);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoSaltar(PlayerInputDriver driver)
        {
            var motor = driver.Brain.Current.Motor;
            motor.Jump();
            yield return new WaitForSeconds(0.9f);
        }

        IEnumerator GestoMira(PlayerInputDriver driver)
        {
            var yo = driver.Brain.Current;
            var rig = driver.Rig;
            rig.SetZoomed(true);
            float hasta = Time.time + 1.2f;
            while (Time.time < hasta) yield return null;   // deja que AdsBlendSuave llegue a >0.9
            var dummy = GameObject.Find("Tut_Enemigo_Estatico");
            var origen = yo.transform.position + Vector3.up * 1.5f;
            var dir = dummy != null ? (dummy.transform.position + Vector3.up * 0.9f - origen).normalized : yo.transform.forward;
            yo.Weapon.TryFire(origen, dir);
            yield return new WaitForSeconds(0.3f);
            rig.SetZoomed(false);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoArsenal(PlayerInputDriver driver)
        {
            var w = driver.Brain.Current.Weapon;
            w.CambiarArmaPrincipal(+1);
            yield return new WaitForSeconds(Pausa);
            w.TryFire(driver.Brain.Current.transform.position + Vector3.up * 1.5f, driver.Brain.Current.transform.forward);
            yield return new WaitForSeconds(0.15f);
            w.Reload();
            yield return new WaitForSeconds(0.6f);
            driver.Rig.SetZoomed(true);
            yield return new WaitForSeconds(0.7f);
            driver.Rig.SetZoomed(false);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoVistaTactica()
        {
            Coberturas.MostrarMarcas(true);
            yield return new WaitForSeconds(0.6f);
            Coberturas.MostrarMarcas(false);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoRts(PlayerInputDriver driver)
        {
            driver.Rig.SetMode(ControlMode.Rts);
            yield return new WaitForSeconds(0.3f);
            if (driver.Selection != null) driver.Selection.SelectAll(driver.Squad);
            yield return new WaitForSeconds(0.2f);
            var zonaA = GameObject.Find("Tut_ZonaA");
            if (zonaA != null)
                foreach (var s in driver.Squad)
                    if (s != null && s != driver.Brain.Current && s.Health.IsAlive)
                        SP.Player.OrderService.IssueMoveOrder(s, zonaA.transform.position);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoFps(PlayerInputDriver driver)
        {
            driver.Rig.SetMode(ControlMode.Fps);
            Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoSeguirYQuietos(PlayerInputDriver driver)
        {
            driver.EjecutarOrdenRadial(3, 1);   // POSICION -> SIGANME
            float hasta = Time.time + 3f;
            while (Time.time < hasta) yield return null;
            driver.EjecutarOrdenRadial(3, 0);   // POSICION -> TODOS QUIETOS
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoFormaciones(PlayerInputDriver driver)
        {
            driver.EjecutarOrdenRadial(3, 2);   // FORMAR LINEA
            yield return new WaitForSeconds(0.8f);
            driver.EjecutarOrdenRadial(3, 3);   // FORMAR CUÑA
            yield return new WaitForSeconds(0.8f);
            driver.EjecutarOrdenRadial(3, 4);   // RETIRADA
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoCurarme(PlayerInputDriver driver)
        {
            driver.EjecutarOrdenRadial(4, 0);   // CURAR -> CURARME
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoModoDios()
        {
            ModoDios.Poner(true);
            yield return new WaitForSeconds(0.4f);
            ModoDios.Poner(false);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoAliadosAlTanque(PlayerInputDriver driver)
        {
            driver.EjecutarOrdenRadial(5, 0);   // TANQUE -> SUBIR TODOS
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoCambiarATorreta(PlayerInputDriver driver)
        {
            driver.SwitchSeat(VehicleSeatRole.Gunner);
            yield return new WaitForSeconds(Pausa);
        }

        IEnumerator GestoBajarDelTanque(PlayerInputDriver driver)
        {
            driver.EjecutarOrdenRadial(5, 1);   // TANQUE -> BAJAR TODOS
            yield return new WaitForSeconds(0.8f);
            driver.EjecutarOrdenRadial(5, 4);   // TANQUE -> BAJARME YO
            yield return new WaitForSeconds(Pausa);
        }
    }
}
