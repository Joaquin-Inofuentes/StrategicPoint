using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.EditorTools
{
    // Banco de la IA de coberturas (pedido: "iterar 5 veces con medicion"). Corre en Play (SC_Gameplay) con el tiempo acelerado:
    // 4 contra 4 en un campo con muros bajos y pilares ALTOS dispuestos en espejo (el campo es justo para los dos lados).
    // Cada configuracion enfrenta la version K de eleccion de cobertura contra la version 0 (la vieja: "la mas cercana con linea
    // de tiro"), alternando de que lado juega cada una. Ademas hay un CONTROL 0 contra 0 que da el ruido propio de la medicion.
    //
    //   Strategic Point > IA > Banco de coberturas (Play)
    public static class CoberturaBench
    {
        const string PrefabAliado = "Assets/_Project/Prefabs/P_Soldier_Ally.prefab";
        const string PrefabEnemigo = "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab";
        static readonly Vector3 Centro = new Vector3(10f, 0f, -6f);
        const float Fondo = 22f;   // distancia entre los dos grupos
        // Carriles libres entre los obstaculos (cada soldado ve de frente a su espejo y de costado se tapa con los muros).
        static readonly float[] Carriles = { -1f, 3.3f, 6.7f, 11.5f };

        public static bool Corriendo { get; private set; }
        public static string Informe { get; private set; } = "";
        public static string Progreso { get; private set; } = "";
        public static int Trials = 10;
        public static float Escala = 4f;
        public static int[] Versiones = { 0, 1, 2, 3, 4, 5 };   // la 0 es el control
        public static int Tamano = 4;
        public static float Limite = 50f;
        public static int Rival = 0;   // version contra la que se mide cada fila (-1 = sin cobertura)

        static IEnumerator rutina;
        static readonly Stack<IEnumerator> pila = new Stack<IEnumerator>();
        static readonly List<GameObject> vivos = new List<GameObject>();
        static readonly List<GameObject> obstaculos = new List<GameObject>();

        [MenuItem("Strategic Point/IA/Banco de coberturas (Play)")]
        public static void Correr()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[CobBench] Entra en Play mode (SC_Gameplay) primero."); return; }
            if (Corriendo) return;
            Iniciar();
        }

        public static void Iniciar()
        {
            Corriendo = true; Informe = ""; Progreso = "arrancando"; pila.Clear();
            rutina = Ejecutar();
            EditorApplication.update += Paso;
        }

        public static void Abortar()
        {
            EditorApplication.update -= Paso;
            Time.timeScale = 1f; Limpiar(); LimpiarCampo(); Corriendo = false; rutina = null;
        }

        static void Paso()
        {
            bool sigue = false;
            try
            {
                if (pila.Count == 0 && rutina != null) pila.Push(rutina);
                if (pila.Count > 0)
                {
                    var actual = pila.Peek();
                    if (actual.MoveNext()) { if (actual.Current is IEnumerator interna) pila.Push(interna); sigue = true; }
                    else { pila.Pop(); sigue = pila.Count > 0; }
                }
            }
            catch (System.Exception e) { Debug.LogError("[CobBench] " + e); sigue = false; Informe += "\nERROR: " + e.Message; }
            if (sigue) return;
            pila.Clear();
            EditorApplication.update -= Paso;
            Time.timeScale = 1f;
            Limpiar(); LimpiarCampo();
            Corriendo = false; rutina = null;
        }

        static void Limpiar()
        {
            foreach (var g in vivos) if (g != null) Object.Destroy(g);
            vivos.Clear();
        }

        static void LimpiarCampo()
        {
            foreach (var g in obstaculos) if (g != null) Object.Destroy(g);
            obstaculos.Clear();
            Coberturas.Registrar();
        }

        // Campo simetrico respecto del plano z = Centro.z + Fondo/2: muros bajos (1.1 m: se tira por encima, se esconde agachado)
        // y pilares altos (2 m: tapan de pie, hay que asomarse).
        static void ArmarCampo()
        {
            var bajos = new[] { new Vector2(-9f, 4f), new Vector2(1f, 6f), new Vector2(9f, 3.5f), new Vector2(-3f, 8f) };
            var altos = new[] { new Vector2(-6f, 9f), new Vector2(5f, 8f) };
            float zEspejo = Fondo;   // z' = Fondo - z
            void Poner(Vector2 o, Vector3 tam, float yaw)
            {
                foreach (int lado in new[] { 0, 1 })
                {
                    var off = lado == 0 ? o : new Vector2(o.x, zEspejo - o.y);
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "CobBench_Obstaculo";
                    go.transform.position = Centro + new Vector3(off.x, tam.y * 0.5f, off.y);
                    go.transform.rotation = Quaternion.Euler(0f, lado == 0 ? yaw : -yaw, 0f);
                    go.transform.localScale = tam;
                    var ob = go.AddComponent<NavMeshObstacle>();
                    ob.carving = true; ob.shape = NavMeshObstacleShape.Box; ob.size = Vector3.one;
                    obstaculos.Add(go);
                }
            }
            foreach (var b in bajos) Poner(b, new Vector3(2.8f, 1.1f, 0.6f), 8f);
            foreach (var a in altos) Poner(a, new Vector3(1.6f, 2.0f, 1.6f), 0f);
        }

        static Soldier Crear(GameObject prefab, string nombre, TeamId team, RoleType rol, Vector3 pos, float yaw, int version)
        {
            var go = Object.Instantiate(prefab, pos + Vector3.up * 0.8f, Quaternion.Euler(0f, yaw, 0f));
            go.name = nombre; vivos.Add(go);
            var s = go.GetComponent<Soldier>();
            s.Configure(nombre, team, rol, 180);
            var pool = Object.FindAnyObjectByType<ProjectilePool>();
            if (s.Weapon != null && pool != null) s.Weapon.SetPool(pool);
            if (s.Brain != null)
            {
                s.Brain.ConfigurarAlcances(24f, 13f);   // mismos para los dos lados
                s.Brain.VersionCobertura = Mathf.Max(0, version);
                s.Brain.CoberturaPropia = version >= 0;
                s.Brain.SinCobertura = version < 0;   // -1 = rival que NO usa cobertura (referencia para saber cuanto vale cubrirse)
            }
            return s;
        }

        struct Trial
        {
            public int version; public bool ganaVersion, empate; public int vivosVersion, vivosOtra;
            public float vidaPerdidaVersion, vidaPerdidaOtra, seg; public int elecciones, cambios;
        }

        static IEnumerator Pelear(int versionA, int versionB, int k, System.Action<Trial> fin)
        {
            // k par: la version A juega del lado Player; impar: del lado Enemy.
            bool aEsPlayer = (k % 2) == 0;
            var prefabA = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAliado);
            var prefabE = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemigo);
            var rolesA = new[] { RoleType.Assault, RoleType.Flanker, RoleType.Assault, RoleType.Flanker };
            var ladoP = new List<Soldier>(); var ladoE = new List<Soldier>();
            for (int i = 0; i < Tamano; i++)
            {
                float x = Carriles[i % Carriles.Length];
                ladoP.Add(Crear(prefabA, "CB_P" + i, TeamId.Player, rolesA[i % 4], Centro + new Vector3(x, 0f, 0f), 0f, aEsPlayer ? versionA : versionB));
                // Los enemigos usan el mismo prefab de cuerpo pero con rol Enemy: se arma con el prefab enemigo.
                ladoE.Add(Crear(prefabE, "CB_E" + i, TeamId.Enemy, RoleType.Enemy, Centro + new Vector3(x, 0f, Fondo), 180f, aEsPlayer ? versionB : versionA));
            }
            yield return null; yield return null;
            foreach (var s in ladoP) s.Brain.Stance = CombatStance.Libre;
            foreach (var s in ladoE) s.Brain.Stance = CombatStance.Libre;

            float t0 = Time.time;
            int Contar(List<Soldier> l) { int n = 0; foreach (var s in l) if (s != null && s.Health.IsAlive) n++; return n; }
            while (Time.time - t0 < Limite)
            {
                if (Contar(ladoP) == 0 || Contar(ladoE) == 0) break;
                yield return null;
            }
            var lA = aEsPlayer ? ladoP : ladoE; var lB = aEsPlayer ? ladoE : ladoP;
            int vA = Contar(lA), vB = Contar(lB);
            float hpA = 0f, hpB = 0f; int el = 0, ca = 0;
            foreach (var s in lA) { hpA += Mathf.Max(0, s.Health.Current); el += s.Brain.CoberturasElegidas; ca += s.Brain.CambiosDeCobertura; }
            foreach (var s in lB) hpB += Mathf.Max(0, s.Health.Current);
            float tot = Tamano * 180f;
            fin(new Trial
            {
                version = versionA, vivosVersion = vA, vivosOtra = vB,
                ganaVersion = vB == 0 && vA > 0, empate = (vA > 0 && vB > 0) || (vA == 0 && vB == 0),
                vidaPerdidaVersion = tot - hpA, vidaPerdidaOtra = tot - hpB, seg = Time.time - t0, elecciones = el, cambios = ca
            });
        }

        static IEnumerator Ejecutar()
        {
            var sb = new StringBuilder();
            var mision = Object.FindAnyObjectByType<SP.Mision.MisionDirector>();
            if (mision != null) Object.Destroy(mision.gameObject);
            var cinematica = Object.FindAnyObjectByType<SP.Mision.CinematicaDeIntro>();
            if (cinematica != null) Object.Destroy(cinematica);
            AiBrain.IAPausada = false;   // la cinematica de apertura congela a todos los cerebros
            var previos = new List<Soldier>(ActorRegistry.All);
            foreach (var s in previos) if (s != null) s.gameObject.SetActive(false);
            var vehiculos = new List<SP.Vehicles.Vehicle>(WorldSystemsRegistry.Vehicles);
            foreach (var v in vehiculos) if (v != null) v.gameObject.SetActive(false);
            var driver = Object.FindAnyObjectByType<SP.Player.PlayerInputDriver>();
            if (driver != null) driver.enabled = false;
            bool dificultadPrevia = Dificultad.Activa; var nivelPrevio = Dificultad.Actual;
            Dificultad.Activa = false;

            ArmarCampo();
            yield return null; yield return null; yield return null;
            int puntos = Coberturas.Registrar();
            sb.AppendLine($"Campo: {obstaculos.Count} obstaculos en espejo, {puntos} puntos de cobertura registrados. {Tamano} contra {Tamano}, {Trials} duelos por configuracion, tiempo x{Escala:0}.");
            sb.AppendLine();
            sb.AppendLine("| Version | Dif. vida (prop. - rival) | Gana | Empata | Pierde | Vivos prop. | Vivos rival | Vida perdida prop. | Vida perdida rival | Segundos | Coberturas elegidas | Cambios |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            Time.timeScale = Escala;

            foreach (int v in Versiones)
            {
                int gana = 0, empata = 0, pierde = 0; float vvA = 0, vvB = 0, hA = 0, hB = 0, seg = 0; int el = 0, ca = 0; var difs = new List<float>();
                for (int k = 0; k < Trials; k++)
                {
                    Progreso = $"version {v} vs {Rival} ({k + 1}/{Trials})";
                    Trial r = default;
                    yield return Pelear(v, Rival, k, x => r = x);
                    if (r.ganaVersion) gana++; else if (r.empate) empata++; else pierde++;
                    difs.Add(r.vidaPerdidaOtra - r.vidaPerdidaVersion); vvA += r.vivosVersion; vvB += r.vivosOtra; hA += r.vidaPerdidaVersion; hB += r.vidaPerdidaOtra; seg += r.seg; el += r.elecciones; ca += r.cambios;
                    Limpiar();
                    yield return null;
                }
                float n = Trials; float media = 0f; foreach (var d in difs) media += d; media /= n; float var2 = 0f; foreach (var d in difs) var2 += (d - media) * (d - media); float ee = Mathf.Sqrt(var2 / Mathf.Max(1f, n - 1f) / n);
                sb.AppendLine($"| v{v} vs v{Rival}{(v == Rival ? " (control)" : "")} | {media:+0;-0} ± {ee:0} | {gana} | {empata} | {pierde} | {vvA / n:0.0} | {vvB / n:0.0} | {hA / n:0} | {hB / n:0} | {seg / n:0} | {el / n:0.0} | {ca / n:0.0} |");
                Informe = sb.ToString();
            }

            Informe = sb.ToString();
            Progreso = "listo";
            Time.timeScale = 1f;
            LimpiarCampo();
            foreach (var s in previos) if (s != null) s.gameObject.SetActive(true);
            foreach (var v in vehiculos) if (v != null) v.gameObject.SetActive(true);
            if (driver != null) driver.enabled = true;
            Dificultad.Activa = dificultadPrevia; Dificultad.Actual = nivelPrevio;
        }
    }
}
