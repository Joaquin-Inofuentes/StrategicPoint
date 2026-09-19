using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.EditorTools
{
    // Banco de balance: corre en Play mode (SC_Gameplay) y hace pelear a los soldados entre
    // si con el tiempo acelerado, para medir cuanto tarda cada clase en ganar un duelo y como
    // le va a la escuadra contra un grupo, en cada dificultad. Deja el informe en
    // BalanceBench.Informe / Docs.
    //
    //   Strategic Point > Balance > Correr banco de balance (Play)
    public static class BalanceBench
    {
        const string PrefabAliado = "Assets/_Project/Prefabs/P_Soldier_Ally.prefab";
        const string PrefabEnemigo = "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab";

        public static bool Corriendo { get; private set; }
        public static string Informe { get; private set; } = "";
        public static string Progreso { get; private set; } = "";
        public static int Trials = 4;
        public static float Escala = 3f;
        public static bool SoloEscuadra, SoloDuelos;
        public static int TrialsEscuadra = 6;

        static IEnumerator rutina;
        static GameObject prefabAliado, prefabEnemigo;
        static readonly List<GameObject> vivos = new List<GameObject>();
        static readonly Vector3 Centro = new Vector3(10f, 0f, -6f);

        [MenuItem("Strategic Point/Balance/Correr banco de balance (Play)")]
        public static void Correr()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Balance] Entra en Play mode (SC_Gameplay) primero."); return; }
            if (Corriendo) return;
            Iniciar();
        }

        public static void Iniciar()
        {
            Corriendo = true;
            Informe = "";
            pila.Clear();
            rutina = Ejecutar();
            EditorApplication.update += Paso;
        }

        static readonly Stack<IEnumerator> pila = new Stack<IEnumerator>();

        // Corre la rutina con corrutinas anidadas (yield return OtraRutina()) un paso por frame.
        static void Paso()
        {
            if (pila.Count == 0 && rutina != null) pila.Push(rutina);
            bool sigue = false;
            if (pila.Count > 0)
            {
                var actual = pila.Peek();
                if (actual.MoveNext())
                {
                    if (actual.Current is IEnumerator interna) pila.Push(interna);
                    sigue = true;
                }
                else
                {
                    pila.Pop();
                    sigue = pila.Count > 0;
                }
            }
            if (sigue) return;

            EditorApplication.update -= Paso;
            Time.timeScale = 1f;
            Limpiar();
            Corriendo = false;
            rutina = null;
        }

        // ---------------- utilidades ----------------
        static string NombreDeVariante(string variante, int ordinal = 0)
        {
            // El enemigo elige variante por el hash de su nombre: se busca un nombre que de la pedida.
            int visto = 0;
            for (int i = 0; i < 800; i++)
            {
                string n = "Bench_" + i;
                int h = 0; foreach (char c in n) h = h * 31 + c;
                int idx = Mathf.Abs(h) % 6;
                string v = idx == 1 ? "COMANDO" : idx == 3 ? "EXPLORADOR" : idx == 4 ? "FRANCOTIRADOR" : "FUSILERO";
                if (v == variante && visto++ == ordinal) return n;
            }
            return "Bench_0";
        }

        static Soldier Crear(GameObject prefab, string nombre, TeamId team, RoleType rol, int vida, Vector3 pos, float yaw)
        {
            var go = Object.Instantiate(prefab, pos + Vector3.up * 0.8f, Quaternion.Euler(0f, yaw, 0f));
            go.name = nombre;
            vivos.Add(go);
            var s = go.GetComponent<Soldier>();
            s.Configure(nombre, team, rol, vida);
            // Mismos alcances que en la partida: aliados 20/12, enemigos 22/13.
            var pool = Object.FindAnyObjectByType<ProjectilePool>();
            if (s.Weapon != null && pool != null) s.Weapon.SetPool(pool);
            if (s.Brain != null) { if (team == TeamId.Enemy) s.Brain.ConfigurarAlcances(22f, 13f); else s.Brain.ConfigurarAlcances(20f, 12f); }
            Dificultad.AjustarVida(s);
            return s;
        }

        static void Limpiar()
        {
            foreach (var g in vivos) if (g != null) Object.Destroy(g);
            vivos.Clear();
        }

        struct Resultado { public bool GanaAliado; public float Segundos; public int AliadosVivos, EnemigosVivos; }

        // Duelo o batalla: espera a que un bando quede sin vivos (o al limite de tiempo de juego).
        static IEnumerator Pelear(List<Soldier> aliados, List<Soldier> enemigos, float limite, System.Action<Resultado> fin)
        {
            yield return null; yield return null;   // SoldierLook viste y arma en Start
            foreach (var s in aliados) { s.Brain.Stance = CombatStance.Libre; }
            foreach (var s in enemigos) { s.Brain.Stance = CombatStance.Libre; }
            float t0 = Time.time;
            while (Time.time - t0 < limite)
            {
                int a = 0, e = 0;
                foreach (var s in aliados) if (s != null && s.Health.IsAlive) a++;
                foreach (var s in enemigos) if (s != null && s.Health.IsAlive) e++;
                if (a == 0 || e == 0) { fin(new Resultado { GanaAliado = e == 0 && a > 0, Segundos = Time.time - t0, AliadosVivos = a, EnemigosVivos = e }); yield break; }
                yield return null;
            }
            int a2 = 0, e2 = 0;
            foreach (var s in aliados) if (s != null && s.Health.IsAlive) a2++;
            foreach (var s in enemigos) if (s != null && s.Health.IsAlive) e2++;
            fin(new Resultado { GanaAliado = false, Segundos = limite, AliadosVivos = a2, EnemigosVivos = e2 });
        }

        // ---------------- el banco ----------------
        static IEnumerator Ejecutar()
        {
            prefabAliado = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAliado);
            prefabEnemigo = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemigo);
            var sb = new StringBuilder();

            // Se apaga todo lo que ya esta en la escena para que nadie moleste el duelo.
            var mision = Object.FindAnyObjectByType<SP.Mision.MisionDirector>();
            if (mision != null) Object.Destroy(mision.gameObject);
            var previos = new List<Soldier>(ActorRegistry.All);
            foreach (var s in previos) if (s != null) s.gameObject.SetActive(false);
            var vehiculos = new List<SP.Vehicles.Vehicle>(WorldSystemsRegistry.Vehicles);
            foreach (var v in vehiculos) if (v != null) v.gameObject.SetActive(false);
            var driver = Object.FindAnyObjectByType<SP.Player.PlayerInputDriver>();
            if (driver != null) driver.enabled = false;

            bool dificultadPrevia = Dificultad.Activa;
            var nivelPrevio = Dificultad.Actual;
            Time.timeScale = Escala;

            // ------------- 1. Duelos 1 contra 1 (dificultad MEDIO) -------------
            Dificultad.Activa = true; Dificultad.Actual = NivelDificultad.Medio;
            if (!SoloEscuadra) {
            var clasesAliadas = new[] { ("ASALTO", RoleType.Assault), ("FLANQUEADOR", RoleType.Flanker), ("MEDICO", RoleType.Medic) };
            var variantes = new[] { "FUSILERO", "COMANDO", "EXPLORADOR", "FRANCOTIRADOR" };
            var distancias = new[] { 12f };

            sb.AppendLine("## Duelos 1 contra 1 (dificultad MEDIO)");
            sb.AppendLine();
            sb.AppendLine("Aliado (180 de vida) contra un enemigo (180 de vida, como los del nivel). Cada celda: % de duelos que gana el aliado · segundos promedio que dura el duelo · vida media que le queda al ganador.");
            sb.AppendLine();
            sb.AppendLine("| Aliado | Distancia | vs Fusilero | vs Comando | vs Explorador | vs Francotirador |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var (nombreClase, rol) in clasesAliadas)
                foreach (float dist in distancias)
                {
                    var fila = new StringBuilder($"| {nombreClase} | {dist:0} m |");
                    foreach (var variante in variantes)
                    {
                        int gana = 0; float tSum = 0f, vidaGanador = 0f; int ganadores = 0;
                        for (int k = 0; k < Trials; k++)
                        {
                            Progreso = $"duelo {nombreClase} vs {variante} a {dist} m ({k + 1}/{Trials})";
                            var a = Crear(prefabAliado, "Bench_Aliado", TeamId.Player, rol, 180, Centro + new Vector3(0f, 0f, 0f), 0f);
                            var e = Crear(prefabEnemigo, NombreDeVariante(variante), TeamId.Enemy, RoleType.Enemy, 180, Centro + new Vector3(0f, 0f, dist), 180f);
                            Resultado r = default;
                            yield return Pelear(new List<Soldier> { a }, new List<Soldier> { e }, 50f, x => r = x);
                            if (r.GanaAliado) { gana++; vidaGanador += a.Health.Current; ganadores++; }
                            else if (r.EnemigosVivos > 0 && r.AliadosVivos == 0) { vidaGanador += e.Health.Current; ganadores++; }
                            tSum += r.Segundos;
                            Limpiar();
                            yield return null;
                        }
                        fila.Append($" {100 * gana / Trials}% · {tSum / Trials:0.0} s · {(ganadores > 0 ? vidaGanador / ganadores : 0f):0} |");
                    }
                    sb.AppendLine(fila.ToString());
                }
            sb.AppendLine();

            }

            if (!SoloDuelos) {
            // ------------- 2. Escuadra (3) contra grupos de 3, 4, 5 y 6, por dificultad -------------
            sb.AppendLine("## Escuadra (Asalto + Flanqueador + Medico) contra un grupo enemigo, por dificultad");
            sb.AppendLine();
            sb.AppendLine($"Campo abierto, grupo a 30 m, {TrialsEscuadra} batallas por celda. Los grupos se arman en este orden: fusilero, fusilero, comando, explorador, francotirador, fusilero. Aliados y enemigos pelean solos (sin jugador). Cada celda: % de batallas que gana la escuadra · aliados vivos al final (de 3) · segundos.");
            sb.AppendLine();
            sb.AppendLine("| Dificultad | vs 3 | vs 4 | vs 5 |");
            sb.AppendLine("|---|---|---|---|");
            string[] orden = { "FUSILERO", "FUSILERO", "COMANDO", "EXPLORADOR", "FRANCOTIRADOR", "FUSILERO" };
            foreach (var nivel in new[] { NivelDificultad.Facil, NivelDificultad.Medio, NivelDificultad.Dificil })
            {
                Dificultad.Actual = nivel; Dificultad.Activa = true;
                var fila = new StringBuilder($"| {Dificultad.Datos(nivel).Nombre} |");
                foreach (int tam in new[] { 3, 4, 5 })
                {
                    int gana = 0; float tSum = 0f, aSum = 0f;
                    for (int k = 0; k < TrialsEscuadra; k++)
                    {
                        Progreso = $"escuadra vs {tam} enemigos {nivel} ({k + 1}/{TrialsEscuadra})";
                        var al = new List<Soldier>
                        {
                            Crear(prefabAliado, "Bench_Vega", TeamId.Player, RoleType.Assault, 180, Centro + new Vector3(-3f, 0f, 0f), 0f),
                            Crear(prefabAliado, "Bench_Kes", TeamId.Player, RoleType.Flanker, 180, Centro + new Vector3(0f, 0f, -1f), 0f),
                            Crear(prefabAliado, "Bench_Doc", TeamId.Player, RoleType.Medic, 180, Centro + new Vector3(3f, 0f, 0f), 0f),
                        };
                        var en = new List<Soldier>();
                        for (int i = 0; i < tam; i++)
                            en.Add(Crear(prefabEnemigo, NombreDeVariante(orden[i], i), TeamId.Enemy, RoleType.Enemy, 180, Centro + new Vector3(-8f + i * 3.5f, 0f, 30f), 180f));
                        Resultado r = default;
                        yield return Pelear(al, en, 60f, x => r = x);
                        if (r.GanaAliado) gana++;
                        tSum += r.Segundos; aSum += r.AliadosVivos;
                        Limpiar();
                        yield return null;
                    }
                    fila.Append($" {100 * gana / TrialsEscuadra}% · {aSum / TrialsEscuadra:0.0} · {tSum / TrialsEscuadra:0} s |");
                }
                sb.AppendLine(fila.ToString());
            }
            sb.AppendLine();

            }
            // ------------- 3. Tiempo para matar (analitico, con el catalogo real) -------------
            sb.AppendLine("## Tiempo para matar por arma (calculo con acierto 100% y con la cadencia real)");
            sb.AppendLine();
            sb.AppendLine("| Arma | Daño | Cadencia | Cargador | DPS sostenido | Tiros para matar (180 de vida) | Tiros para matar con 100 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (WeaponKind k in System.Enum.GetValues(typeof(WeaponKind)))
            {
                var e = WeaponCatalog.Get(k);
                float ciclo = e.MagazineSize * e.Cooldown + e.ReloadDuration;
                float dano = e.Damage * Mathf.Max(1, e.Pellets);
                float dps = dano * e.MagazineSize / Mathf.Max(0.01f, ciclo);
                sb.AppendLine($"| {e.DisplayName} | {e.Damage}{(e.Pellets > 1 ? " x" + e.Pellets : "")} | {1f / e.Cooldown:0.0}/s | {e.MagazineSize} | {dps:0} | {Mathf.CeilToInt(180f / dano)} | {Mathf.CeilToInt(100f / dano)} |");
            }

            Informe = sb.ToString();
            Progreso = "listo";

            // se restaura la escena
            foreach (var s in previos) if (s != null) s.gameObject.SetActive(true);
            foreach (var v in vehiculos) if (v != null) v.gameObject.SetActive(true);
            if (driver != null) driver.enabled = true;
            Dificultad.Activa = dificultadPrevia; Dificultad.Actual = nivelPrevio;
        }
    }
}
