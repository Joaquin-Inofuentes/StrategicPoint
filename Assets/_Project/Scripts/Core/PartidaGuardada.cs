using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SP.Actors;
using SP.Mision;
using SP.Operacion;
using SP.Player;

namespace SP.Core
{
    [Serializable]
    public class PartidaGuardadaDatos
    {
        public int version;
        public string escena, fechaUtc, motivo, fase;
        public int dificultad, objetivo;
        public float tiempoDeJuego;
        public EstadoDeSesion estado;
        public List<string> cinematicasVistas = new List<string>();
        public string estadisticas;
    }

    // P10 (#120): guardar y continuar la partida. Se apoya en SesionLog (Capturar / Restaurar: soldados, vehiculos y el estado de la mision de cada
    // IEstadoGuardable) y le suma lo propio de una partida: version del archivo, motivo, dificultad, cinematicas vistas y estadisticas.
    //
    //  - Archivo: <SesionLog.Carpeta>/Partidas/partida_<escena>.json (escritura atomica: .tmp + reemplazo, con copia .bak del anterior).
    //    En el editor eso cae dentro del proyecto (Registros/), en un build en persistentDataPath: nunca se escribe fuera de ahi.
    //  - Semantica de checkpoint: se guarda la escuadra, los milicianos, el objetivo y su subfase; los enemigos de la zona actual se rearman por
    //    la fase (SaltarA) y las oleadas sin terminar se vuelven a lanzar.
    //  - ModoPrueba: los checks y OperacionPrueba escriben en partida_<escena>_prueba.json, jamas en la partida real del jugador.
    public static class PartidaGuardada
    {
        public const int VersionActual = 1;
        public const string EscenaDeLaOperacion = "SC_Operacion";

        // Solo checks / pruebas por CLI: otra ruta de archivo para no tocar la partida real. Se reinicia en cada Play.
        public static bool ModoPrueba;
        // Interruptor del autoguardado (los checks que miden el archivo lo usan; en el juego queda encendido).
        public static bool AutoguardadoActivo = true;
        public static bool Restaurando { get; private set; }
        public static PartidaGuardadaDatos PendienteDeRestaurar { get; private set; }
        public static string UltimoAviso { get; private set; } = "";
        public static string UltimoMotivoGuardado { get; private set; } = "";
        public static int GuardadosHechos { get; private set; }
        public static int GuardadosPorMotivoAuto { get; private set; }
        public static event Action<PartidaGuardadaDatos> Restaurada;

        public const string TextoDeCheckpoint = "SE GUARDA TU ESCUADRA Y EL OBJETIVO; LOS ENEMIGOS DE LA ZONA SE REINICIAN";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            ModoPrueba = false; AutoguardadoActivo = true; Restaurando = false; PendienteDeRestaurar = null;
            UltimoAviso = ""; UltimoMotivoGuardado = ""; GuardadosHechos = 0; GuardadosPorMotivoAuto = 0; Restaurada = null;
        }

        // ---------------------------------------------------------------- archivo
        public static string CarpetaDePartidas => Path.Combine(SesionLog.Carpeta, "Partidas");
        public static string RutaDe(string escena) => Path.Combine(CarpetaDePartidas, $"partida_{escena}{(ModoPrueba ? "_prueba" : "")}.json");

        public struct Info
        {
            public bool valida;
            public string escena, fechaUtc, motivo, fase;
            public int objetivo, dificultad;
            public string Texto => valida ? $"OBJETIVO {objetivo}/{OperacionDirector.TotalObjetivos} · {Fecha()}" : "";
            string Fecha() { return DateTime.TryParse(fechaUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var f) ? f.ToLocalTime().ToString("dd/MM HH:mm") : fechaUtc; }
        }

        public static Info UltimaInfo(string escena = EscenaDeLaOperacion)
        {
            var d = Leer(escena, false);
            if (d == null) return default;
            return new Info { valida = true, escena = d.escena, fechaUtc = d.fechaUtc, motivo = d.motivo, fase = d.fase, objetivo = d.objetivo, dificultad = d.dificultad };
        }

        // Lee y valida el archivo. null = no hay partida usable (no existe, JSON roto o version distinta; en esos dos ultimos casos avisa en el log).
        static PartidaGuardadaDatos Leer(string escena, bool avisar)
        {
            string ruta = RutaDe(escena);
            try
            {
                if (!File.Exists(ruta)) return null;
                var d = JsonUtility.FromJson<PartidaGuardadaDatos>(File.ReadAllText(ruta));
                if (d == null || d.estado == null) { Aviso("la partida guardada esta vacia o danada: se ignora", avisar); return null; }
                if (d.version != VersionActual) { Aviso($"la partida guardada es de otra version ({d.version}, esta es la {VersionActual}): se ignora", avisar); return null; }
                if (d.escena != escena) { Aviso("la partida guardada es de otra escena: se ignora", avisar); return null; }
                return d;
            }
            catch (Exception e) { Aviso("no se pudo leer la partida guardada (" + e.Message + "): se ignora", avisar); return null; }
        }

        static void Aviso(string t, bool log)
        {
            UltimoAviso = t;
            if (log) Debug.LogWarning("[PartidaGuardada] " + t);
        }

        public static bool HayPartida(string escena = EscenaDeLaOperacion) => Leer(escena, false) != null;

        public static PartidaGuardadaDatos Cargar(string escena = EscenaDeLaOperacion) => Leer(escena, true);

        public static void Borrar(string escena = EscenaDeLaOperacion)
        {
            try
            {
                string ruta = RutaDe(escena);
                if (File.Exists(ruta)) File.Delete(ruta);
                if (File.Exists(ruta + ".bak")) File.Delete(ruta + ".bak");
                if (File.Exists(ruta + ".tmp")) File.Delete(ruta + ".tmp");
                SesionLog.Evento("PARTIDA: borrada (" + Path.GetFileName(ruta) + ")");
            }
            catch (Exception e) { Debug.LogWarning("[PartidaGuardada] no se pudo borrar: " + e.Message); }
        }

        // ---------------------------------------------------------------- guardar
        // Por que no se puede guardar ahora (null = se puede). Lo usa el boton del menu de pausa y el autoguardado.
        public static string MotivoDeBloqueo()
        {
            var d = OperacionDirector.Instancia;
            if (d == null || !Application.isPlaying) return "SOLO SE GUARDA DENTRO DE LA OPERACION";
            if (Restaurando) return "CARGANDO LA PARTIDA";
            if (d.Fase == FaseOperacion.Victoria || d.Fase == FaseOperacion.Derrota) return "LA MISION YA TERMINO";
            if (CinematicaDeOperacion.IntroActiva || CineDeHuida.Activa || d.TomaDelJefeActiva) return "NO SE PUEDE GUARDAR DURANTE UNA CINEMATICA";
            if (d.Fase == FaseOperacion.Huir && d.Subfase >= (int)SubfaseHuida.CinematicaFinal) return "NO SE PUEDE GUARDAR DURANTE UNA CINEMATICA";
            if (d.SubiendoAlHeli) return "NO SE PUEDE GUARDAR MIENTRAS SUBIS AL HELICOPTERO";
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (yo == null || yo.Health == null || !yo.Health.IsAlive) return "NO SE PUEDE GUARDAR CON EL SOLDADO CAIDO";
            if (yo.Motor != null && yo.Motor.IsJumping) return "NO SE PUEDE GUARDAR EN EL AIRE";
            return null;
        }

        // motivo: "manual" o "auto: ...". Devuelve true si el archivo quedo escrito.
        public static bool Guardar(string motivo)
        {
            string bloqueo = MotivoDeBloqueo();
            if (bloqueo != null) { Aviso(bloqueo, false); return false; }
            if (SesionLog.Instancia == null) return false;
            var d = OperacionDirector.Instancia;
            try
            {
                var estado = SesionLog.Instancia.Capturar("partida: " + motivo);
                var datos = new PartidaGuardadaDatos
                {
                    version = VersionActual,
                    escena = SceneManager.GetActiveScene().name,
                    fechaUtc = DateTime.UtcNow.ToString("o"),
                    motivo = motivo,
                    dificultad = (int)Dificultad.Actual,
                    objetivo = OperacionDirector.Indice(d.Fase) + 1,
                    fase = d.Fase.ToString(),
                    tiempoDeJuego = Time.time,
                    estado = estado,
                    estadisticas = EstadisticasDeMision.Instancia != null ? EstadisticasDeMision.Instancia.Serializar() : "",
                };
                if (CinematicaDeRapel.YaVista) datos.cinematicasVistas.Add("rapel");
                if (CinematicaDeOperacion.YaVista) datos.cinematicasVistas.Add("pasos");
                string ruta = RutaDe(datos.escena);
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                string tmp = ruta + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(datos, false), new UTF8Encoding(false));
                if (File.Exists(ruta)) File.Replace(tmp, ruta, ruta + ".bak"); else File.Move(tmp, ruta);
                GuardadosHechos++;
                if (motivo.StartsWith("auto")) GuardadosPorMotivoAuto++;
                UltimoMotivoGuardado = motivo;
                SesionLog.Evento($"PARTIDA: guardada ({motivo}) objetivo {datos.objetivo} fase {datos.fase}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PartidaGuardada] no se pudo guardar: " + e.Message);
                Aviso("NO SE PUDO GUARDAR: " + e.Message, false);
                return false;
            }
        }

        // Guardado manual del menu de pausa: con indicador. Devuelve el texto del resultado.
        public static string GuardarManual()
        {
            if (Guardar("manual")) { IndicadorDeGuardado.Mostrar("PARTIDA GUARDADA", 2.2f, true); return "PARTIDA GUARDADA"; }
            string m = MotivoDeBloqueo() ?? UltimoAviso;
            IndicadorDeGuardado.Mostrar(m, 2.6f, false);
            return m;
        }

        // Autoguardado: sin hacer nada si esta apagado o el momento no es valido. Con indicador "GUARDANDO...".
        public static bool AutoGuardar(string motivo)
        {
            if (!AutoguardadoActivo || Restaurando) return false;
            if (MotivoDeBloqueo() != null) return false;
            IndicadorDeGuardado.Mostrar("GUARDANDO...", 1.5f, false);
            return Guardar("auto: " + motivo);
        }

        // ---------------------------------------------------------------- continuar
        // Deja la partida pendiente y carga la escena (con la pantalla de carga). La restauracion corre al arrancar la escena.
        public static bool Continuar(string escena = EscenaDeLaOperacion)
        {
            var d = Leer(escena, true);
            if (d == null) return false;
            PendienteDeRestaurar = d;
            Dificultad.Actual = (NivelDificultad)Mathf.Clamp(d.dificultad, 0, 2);
            Time.timeScale = 1f; AudioListener.pause = false;
            SceneLoader.Cargar(escena);
            return true;
        }

        // Para los checks: la deja pendiente y recarga la escena directo (sin SC_Loading).
        public static bool ContinuarSinPantallaDeCarga(string escena = EscenaDeLaOperacion)
        {
            var d = Leer(escena, true);
            if (d == null) return false;
            PendienteDeRestaurar = d;
            Dificultad.Actual = (NivelDificultad)Mathf.Clamp(d.dificultad, 0, 2);
            SceneManager.LoadScene(escena);
            return true;
        }

        public static bool HayPendiente => PendienteDeRestaurar != null && PendienteDeRestaurar.escena == SceneManager.GetActiveScene().name;

        // Lo llama OperacionDirector.Start: las cinematicas de apertura ya no se repiten y la restauracion se programa en un host que sobrevive
        // a SaltarA (que hace StopAllCoroutines en el director).
        public static void ProgramarRestauracion()
        {
            var d = PendienteDeRestaurar;
            if (d == null || SesionLog.Instancia == null) return;
            Restaurando = true;
            SesionLog.Instancia.StartCoroutine(Restaurar(d));
        }

        static IEnumerator Restaurar(PartidaGuardadaDatos d)
        {
            // Los directores arrancan en su Start: se espera a que el soldado poseido exista.
            for (int i = 0; i < 60; i++)
            {
                var drv = PlayerInputDriver.Activo;
                if (i >= 4 && drv != null && drv.Brain != null && drv.Brain.Current != null && OperacionDirector.Instancia != null) break;
                yield return null;
            }
            var dir = OperacionDirector.Instancia;
            string r;
            try
            {
                if (d.cinematicasVistas.Contains("rapel")) CinematicaDeRapel.YaVista = true;
                if (d.cinematicasVistas.Contains("pasos")) CinematicaDeOperacion.YaVista = true;
                r = SesionLog.Restaurar(d.estado);
                if (dir != null) dir.AlRestaurarPartida(d);
                if (EstadisticasDeMision.Instancia != null) EstadisticasDeMision.Instancia.Restaurar(d.estadisticas);
            }
            catch (Exception e) { r = "ERROR restaurando: " + e; Debug.LogError("[PartidaGuardada] " + r); }
            for (int i = 0; i < 3; i++) yield return null;
            PendienteDeRestaurar = null;
            Restaurando = false;
            SesionLog.Evento("PARTIDA: restaurada (" + d.motivo + "): " + r);
            IndicadorDeGuardado.Mostrar("PARTIDA CARGADA · OBJETIVO " + d.objetivo, 3f, true);
            Restaurada?.Invoke(d);
        }
    }

    // Cartelito arriba a la derecha: "GUARDANDO..." / "PARTIDA GUARDADA". Vive en un canvas propio (no depende del HUD ni de la pausa).
    public class IndicadorDeGuardado : MonoBehaviour
    {
        static IndicadorDeGuardado instancia;
        public static string TextoActual => instancia != null && instancia.visible ? instancia.texto.text : "";
        public static bool Visible => instancia != null && instancia.visible;

        Text texto; CanvasGroup grupo; float hasta; bool visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; }

        public static void Mostrar(string t, float segundos, bool ok)
        {
            if (instancia == null)
            {
                var go = new GameObject("IndicadorDeGuardado", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
                if (SesionLog.Instancia != null) go.transform.SetParent(SesionLog.Instancia.transform, false);
                else DontDestroyOnLoad(go);
                var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 5950;
                var cs = go.GetComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1280f, 720f);
                var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(go.transform, false);
                var img = panel.GetComponent<Image>(); img.color = new Color(0.04f, 0.05f, 0.08f, 0.82f); img.raycastTarget = false;
                var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-22f, -22f); rt.sizeDelta = new Vector2(380f, 40f);
                var tgo = new GameObject("Texto", typeof(RectTransform), typeof(Text));
                tgo.transform.SetParent(panel.transform, false);
                var tx = tgo.GetComponent<Text>();
                tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tx.fontSize = 20; tx.fontStyle = FontStyle.Bold; tx.alignment = TextAnchor.MiddleCenter; tx.raycastTarget = false;
                var trt = tx.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                var c = go.AddComponent<IndicadorDeGuardado>();
                c.texto = tx; c.grupo = go.GetComponent<CanvasGroup>(); c.grupo.alpha = 0f; c.grupo.blocksRaycasts = false; c.grupo.interactable = false;
                instancia = c;
            }
            instancia.texto.text = t;
            instancia.texto.color = ok ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.82f, 0.3f);
            instancia.hasta = Time.unscaledTime + segundos;
            instancia.visible = true;
            instancia.grupo.alpha = 1f;
        }

        void Update()
        {
            if (!visible) return;
            float resto = hasta - Time.unscaledTime;
            if (resto <= 0f) { visible = false; grupo.alpha = 0f; return; }
            grupo.alpha = Mathf.Clamp01(resto / 0.4f);
        }
    }
}
