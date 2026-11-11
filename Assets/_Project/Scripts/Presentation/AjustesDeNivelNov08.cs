using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Ajustes del nivel pedidos en los bugs #369, #375, #382, #387 y #392 (2026-11-08). Se aplican al cargar SC_Operacion* sobre lo que ya
    // esta en la escena (incluye objetos inactivos: los grupos de cada fase arrancan apagados):
    //  - #369: las hojas y jambas de las puertas de acceso del centro de datos no tenian collider -> se las atravesaba.
    //  - #392: se quita la bandera de gala (mastil, tela, franja y su luz) de la azotea de la mansion.
    //  - #375: se quitan las luminarias chicas (faroles de ruta, farolillos y luces de hada).
    //  - #382/#387: las luces decorativas de la mansion y los reflectores bajan de intensidad (x0,6).
    public static class AjustesDeNivelNov08
    {
        public static int ColliderAgregados { get; private set; }
        public static int Apagados { get; private set; }
        public static int LucesAtenuadas { get; private set; }
        public const float FactorDeLuces = 0.6f;

        static readonly HashSet<string> Quitar = new HashSet<string> { "Bandera_Gala", "Bandera_Gala_Franja", "Mastil_Gala", "Luz_Mastil", "Farol_Brazo", "Farol_Luz", "Farol_Poste", "Farolillo", "Luz_Hada",
        "Lampara_Pie_Poste", "Lampara_Pie_Pantalla", "Luz_Lampara_Pie" };   // #407: las lamparas de pie del salon del 2.o piso
        static readonly HashSet<string> LucesBajar = new HashSet<string> { "Luz_Arana", "Luz_Lampara_Pie", "Luz_Aplique", "Reflector_Fachada" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { ColliderAgregados = Apagados = LucesAtenuadas = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribir()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargar;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode modo)
        {
            if (!s.name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            Aplicar();
            // Los grupos de cada fase (ruta, mansion, centro) se arman o se encienden despues: se reaplica cada pocos segundos mientras dure la escena.
            if (vigilante == null) { var go = new GameObject("VigilanteDeAjustesNov08") { hideFlags = HideFlags.DontSave }; vigilante = go.AddComponent<Vigilante>(); }
        }

        static Vigilante vigilante;
        // Hipo de ~1 s cada 3 s al caminar (perfil del 2026-11-11): este vigilante recorria los ~48.000 transforms de la escena leyendo t.name (una cadena
        // nueva por cada uno) y con EndsWith/StartsWith sensibles a cultura. Ahora: (1) los transforms ya procesados estando activos no se vuelven a mirar,
        // (2) la pasada se reparte en lotes de LoteDeTransforms por frame, (3) solo se lanza cuando cambia la fase de la mision (se encienden grupos
        // nuevos) o cada SegundosDeRespaldo, y (4) las comparaciones de nombre son ordinales.
        public const int LoteDeTransforms = 1500;
        public const float SegundosDeRespaldo = 10f;
        public static int PasadasPorLotes { get; private set; }
        public static int TransformsProcesados { get; private set; }
        static readonly HashSet<int> hechos = new HashSet<int>();
        static string firmaDeFase = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarPasadas() { hechos.Clear(); firmaDeFase = ""; PasadasPorLotes = TransformsProcesados = 0; vigilante = null; }

        static string FirmaDeFase()
        {
            var d = SP.Operacion.OperacionDirector.Instancia;
            return d == null ? "-" : d.Fase + "/" + d.Subfase + "/" + UnityEngine.SceneManagement.SceneManager.sceneCount;
        }

        sealed class Vigilante : MonoBehaviour
        {
            float proximo, ultimaPasada; bool pasando;
            void Update()
            {
                if (pasando || Time.unscaledTime < proximo) return;
                proximo = Time.unscaledTime + 1f;
                if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
                string f = FirmaDeFase();
                if (f == firmaDeFase && Time.unscaledTime - ultimaPasada < SegundosDeRespaldo) return;
                firmaDeFase = f; ultimaPasada = Time.unscaledTime;
                StartCoroutine(PasarPorLotes());
            }

            System.Collections.IEnumerator PasarPorLotes()
            {
                pasando = true;
                var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
                int enLote = 0;
                for (int i = 0; i < todos.Length; i++)
                {
                    var t = todos[i]; if (t == null) continue;
                    ProcesarSiHaceFalta(t);
                    if (++enLote >= LoteDeTransforms) { enLote = 0; yield return null; }
                }
                PasadasPorLotes++;
                yield return null;
                ProcesarLuces();
                yield return null;
                DestructibilidadDelNivel.Aplicar();   // #402
                pasando = false;
            }
        }

        // Un transform activo ya procesado no se vuelve a mirar; uno inactivo (grupo de una fase que aun no empezo) se reintenta en la proxima pasada.
        static void ProcesarSiHaceFalta(Transform t)
        {
            int h = t.GetHashCode();
            if (hechos.Contains(h)) return;
            bool activo = t.gameObject.activeInHierarchy;
            ProcesarTransform(t); TransformsProcesados++;
            if (activo) hechos.Add(h);
        }

        public static void Aplicar(bool registrar = true)
        {
            DestructibilidadDelNivel.Aplicar();   // #402
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null) ProcesarSiHaceFalta(t);
            ProcesarLuces();
            if (registrar) SP.Core.GameLog.Line($"AjustesDeNivelNov08: {ColliderAgregados} colliders, {Apagados} apagados, {LucesAtenuadas} luces atenuadas");
        }

        static void ProcesarTransform(Transform t)
        {
            string n = t.name;
            if (Quitar.Contains(n) && t.gameObject.activeSelf) { t.gameObject.SetActive(false); Apagados++; return; }
            if (AjustesDeNivelNov11.Quitar(t)) { Apagados++; return; }   // #415 (UPS y torre negra) y #422 (Azotea_Equipo)
            // #408: el escenario del salon del 2.o piso (plataforma, borde y telon, sin collider). "Escenario" tambien es un grupo de la Huida: se filtra por posicion.
            if ((n == "Escenario" || n == "Escenario_Borde" || n == "Escenario_Telon") && t.gameObject.activeSelf && EsDelSalonDelSegundoPiso(t))
            { t.gameObject.SetActive(false); Apagados++; EscenarioQuitado++; return; }
            if (n == "Azotea_Equipo" && t.GetComponent<MarcaEquipoMovido>() == null) SacarDelHuecoDeLaTorre(t);
            // #404: la guia del mando dibujaba un punteado amarillo sobre la alfombra. Quedaban ademas copias huerfanas de GuiaDeMando guardadas en la escena
            // (con la linea encendida): se apagan todas las que no son la instancia viva.
            if (n == "GuiaDeMando" && t.gameObject.activeSelf)
            {
                var g = t.GetComponent<SP.Operacion.GuiaDeMando>();
                if (g == null || g != SP.Operacion.GuiaDeMando.Instancia) { t.gameObject.SetActive(false); Apagados++; GuiasHuerfanasApagadas++; }
            }
            // #405 (z-fighting): el relleno sobre la puerta de cada torre tenia su cara +Z en el mismo plano (z=273,1) que la losa vecina: parpadeaba.
            // Se lo hunde 1 cm hacia atras, una sola vez (la marca evita repetirlo cada 3 s).
            if (n.EndsWith("Puerta_D2_Relleno", System.StringComparison.Ordinal) && t.GetComponent<MarcaEquipoMovido>() == null)
            { t.position += Vector3.back * 0.01f; t.gameObject.AddComponent<MarcaEquipoMovido>(); RellenosHundidos++; }
            // #405: mas caras coplanares medidas con DetectorDeSolapes (>= 1 m2): el muro interior de las torres B y C con el canto de la losa (x=336,6, cara +X)
            // y la cornisa E_1 con el rellano de servicio (x=348, cara -X). Se hunden 1 cm detras de la cara vecina.
            if ((n == "Torre_B_Lado_Interior" || n == "Torre_C_Lado_Interior" || n == "Cornisa_E_1") && t.GetComponent<MarcaEquipoMovido>() == null)
            { t.position += (n == "Cornisa_E_1" ? Vector3.right : Vector3.left) * 0.01f; t.gameObject.AddComponent<MarcaEquipoMovido>(); RellenosHundidos++; }
            // #405: los estantes pegados a la medianera norte (z=285,4) tenian la cara +Z en el mismo plano que el muro: se adelantan 1 cm.
            if (n == "Estante" && t.GetComponent<MarcaEquipoMovido>() == null)
            {
                var re = t.GetComponentInChildren<Renderer>();
                if (re != null && Mathf.Abs(re.bounds.max.z - 285.4f) < 0.05f) { t.position += Vector3.forward * 0.01f; RellenosHundidos++; }
                t.gameObject.AddComponent<MarcaEquipoMovido>();
            }
            // #405: los cuadros de lujo de las torres tenian la cara en el mismo plano que su marco: el A mira a +X, el B a -X; se adelantan 1 cm.
            if ((n.EndsWith("Lujo_Cuadro_A", System.StringComparison.Ordinal) || n.EndsWith("Lujo_Cuadro_B", System.StringComparison.Ordinal)) && t.GetComponent<MarcaEquipoMovido>() == null)
            { t.position += (n.EndsWith("_A", System.StringComparison.Ordinal) ? Vector3.right : Vector3.left) * 0.01f; t.gameObject.AddComponent<MarcaEquipoMovido>(); RellenosHundidos++; }
            // #405: tabla de caras coplanares medidas con DetectorDeSolapes (>= 1 m2): nombre -> desplazamiento de 1 cm hacia atras de la cara vecina.
            if (DesplazamientosZFighting.TryGetValue(n, out var corrimiento) && t.GetComponent<MarcaEquipoMovido>() == null)
            {
                bool aplica = true;
                if (n == "Marco_S") { var rm = t.GetComponentInChildren<Renderer>(); aplica = rm != null && Mathf.Abs(rm.bounds.max.z - 246f) < 0.05f; }
                if (aplica) { t.position += corrimiento; RellenosHundidos++; }
                t.gameObject.AddComponent<MarcaEquipoMovido>();
            }
            if ((n == "Fuente_Chorro" || n == "Fuente_Gota") && t.GetComponent<MarcaChorroAlineado>() == null) AlinearChorro(t);
            if (t.parent != null && t.parent.name == "PuertasDeAcceso" && (n.StartsWith("Hoja_", System.StringComparison.Ordinal) || n.StartsWith("Jamba_", System.StringComparison.Ordinal)) && !n.EndsWith("_Franja", System.StringComparison.Ordinal)
                && t.GetComponent<Collider>() == null && t.GetComponent<MeshRenderer>() != null)
            {
                t.gameObject.AddComponent<BoxCollider>(); ColliderAgregados++;
            }
        }

        static void ProcesarLuces()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null || !LucesBajar.Contains(l.name) || l.GetComponent<MarcaLuzAtenuada>() != null) continue;
                l.gameObject.AddComponent<MarcaLuzAtenuada>();
                l.intensity *= FactorDeLuces; LucesAtenuadas++;
            }
        }

        sealed class MarcaLuzAtenuada : MonoBehaviour { }
        sealed class MarcaEquipoMovido : MonoBehaviour { }
        public static int EscenarioQuitado { get; private set; }
        public static int GuiasHuerfanasApagadas { get; private set; }
        public static int RellenosHundidos { get; private set; }
        const float Cm = 0.01f;
        static readonly System.Collections.Generic.Dictionary<string, Vector3> DesplazamientosZFighting = new System.Collections.Generic.Dictionary<string, Vector3>
        {
            { "Torre_E_Lado_Interior", Vector3.right * Cm }, { "Torre_A_Lado_Interior", Vector3.right * Cm }, { "Torre_D_Lado_Interior", Vector3.right * Cm },
            { "Torre_B_Divisoria", Vector3.right * Cm }, { "Torre_C_Divisoria", Vector3.right * Cm },
            { "Torre_E_Divisoria", Vector3.left * Cm }, { "Torre_D_Divisoria", Vector3.left * Cm }, { "Torre_A_Divisoria", Vector3.left * Cm },
            { "Marco_S", Vector3.forward * Cm }, { "PB_Chimenea", Vector3.left * Cm }, { "Cornisa_O_1", Vector3.left * Cm },
        };

        public static int EquiposMovidos { get; private set; }

        static bool EsDelSalonDelSegundoPiso(Transform t)
        {
            var p = t.position;
            return SP.Operacion.MansionGeometria.Dentro(p) && Mathf.Abs(p.y - SP.Operacion.MansionGeometria.Nivel(2)) < 1.5f;
        }

        // #411 ("ese bloque negro"): las cajas de equipo de la azotea tapaban el hueco de las torres C, E y D (el tramo B de la escalera pegaba la cabeza
        // contra una caja negra enorme). Se las corre hacia el centro del techo hasta que dejan de estar sobre una torre. Se mueven, no se borran:
        // la azotea mantiene su cobertura.
        static void SacarDelHuecoDeLaTorre(Transform t)
        {
            var G = typeof(SP.Operacion.MansionGeometria);
            var r = t.GetComponent<Renderer>(); var c = t.GetComponent<Collider>();
            var b = r != null ? r.bounds : c != null ? c.bounds : new Bounds(t.position, Vector3.one);
            float margen = Mathf.Max(b.extents.x, b.extents.z) + 0.4f;
            var centroTecho = SP.Operacion.MansionGeometria.Centro(3);
            bool movido = false;
            for (int i = 0; i < 6 && SP.Operacion.MansionGeometria.EnTorre(t.position, out _, margen); i++)
            {
                var a = centroTecho - t.position; a.y = 0f;
                if (a.sqrMagnitude < 1f) break;
                t.position += a.normalized * 3f; movido = true;
            }
            t.gameObject.AddComponent<MarcaEquipoMovido>();
            if (movido) { EquiposMovidos++; Physics.SyncTransforms(); }
        }
        sealed class MarcaChorroAlineado : MonoBehaviour { }

        // #383 ("falta alinear mejor las lineas de agua de la fuente"): los 8 chorros y sus gotas quedan sobre un mismo anillo (2,0 m del eje, entre la
        // columna y el borde del agua), a 45 grados exactos uno del otro y con cada gota centrada sobre su chorro.
        public const float RadioDeChorros = 2.0f;
        static readonly Vector2 EjeDeLaFuente = new Vector2(322f, 215.5f);
        public static int ChorrosAlineados { get; private set; }

        static void AlinearChorro(Transform t)
        {
            var p = t.position;
            var d = new Vector2(p.x - EjeDeLaFuente.x, p.z - EjeDeLaFuente.y);
            if (d.sqrMagnitude < 0.01f || d.magnitude > 4f) return;   // no es el de la fuente de la plaza
            float ang = Mathf.Round(Mathf.Atan2(d.y, d.x) / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
            t.position = new Vector3(EjeDeLaFuente.x + Mathf.Cos(ang) * RadioDeChorros, p.y, EjeDeLaFuente.y + Mathf.Sin(ang) * RadioDeChorros);
            t.rotation = Quaternion.identity;
            t.gameObject.AddComponent<MarcaChorroAlineado>(); ChorrosAlineados++;
        }
    }
}
