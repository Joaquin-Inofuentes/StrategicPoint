using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // Arbustos y bosques del terreno jugable (la mancha incluye 1,5 m de margen alrededor de cada arbol y matorral). Pedido explicito: "los soldados enemigos si o si ataquen solamente afuera de los
    // arbustos y bosques". La regla vive aca: un mapa de celdas (1,5 m) que marca donde hay copa de arbol (radio 2,2 m alrededor
    // de cada arbol del terreno) o matorral (capas de detalle del terreno, radio 1,3 m). WeaponHolder.TryFire y la IA enemiga
    // (AiBrain.Sentidos / AiBrain) lo consultan: un enemigo dentro de esa mancha no puede disparar y sale a cielo abierto antes de pelear.
    // Solo rige en la partida principal (Dificultad.Activa): el tutorial y la suite headless no cambian.
    public static class Vegetacion
    {
        public const float Celda = 1.5f;
        public const float RadioDeArbol = 2.2f;
        public const float RadioDeArbusto = 1.3f;

        static bool[,] mapa;
        static int nx, nz;
        static Vector2 origen;
        static bool armado;
        static bool intentado;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            Invalidar();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        }

        static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { Invalidar(); }

        public static void Invalidar() { mapa = null; intentado = false; armado = false; }

        // La regla solo corre en la partida principal y si el terreno tiene vegetacion.
        public static bool Rige => Dificultad.Activa;

        public static bool HayMapa { get { Asegurar(); return mapa != null; } }

        // true si el punto esta dentro de la copa de un arbol o de un matorral.
        public static bool Dentro(Vector3 p)
        {
            if (!Rige) return false;
            Asegurar();
            return Mancha(p);
        }

        // Igual que Dentro pero sin mirar Dificultad.Activa (para herramientas de editor y capturas).
        public static bool DentroSinRegla(Vector3 p)
        {
            Asegurar();
            return Mancha(p);
        }

        static bool Mancha(Vector3 p)
        {
            if (mapa == null) return false;
            int ix = Mathf.FloorToInt((p.x - origen.x) / Celda), iz = Mathf.FloorToInt((p.z - origen.y) / Celda);
            return ix >= 0 && iz >= 0 && ix < nx && iz < nz && mapa[ix, iz];
        }

        // Centro de la celda libre mas cercana (con un margen de una celda libre alrededor) y alcanzable por la grilla de navegacion.
        public static bool TryPuntoLibre(Vector3 desde, out Vector3 libre)
        {
            libre = desde;
            Asegurar();
            if (mapa == null) return false;
            int cx = Mathf.FloorToInt((desde.x - origen.x) / Celda), cz = Mathf.FloorToInt((desde.z - origen.y) / Celda);
            const int maxAnillo = 40;
            for (int r = 1; r <= maxAnillo; r++)
            {
                float mejor = float.MaxValue; bool hay = false; Vector3 mejorP = desde;
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;   // solo el borde del anillo
                        int ix = cx + dx, iz = cz + dz;
                        if (!Libre(ix, iz)) continue;   // la mancha ya viene engordada: una celda libre es un claro con margen
                        var p = new Vector3(origen.x + (ix + 0.5f) * Celda, desde.y, origen.y + (iz + 0.5f) * Celda);
                        if (NavService.IsReady && NavService.Graph.IsBlockedAt(p)) continue;
                        float d = (p.x - desde.x) * (p.x - desde.x) + (p.z - desde.z) * (p.z - desde.z);
                        if (d < mejor) { mejor = d; mejorP = p; hay = true; }
                    }
                if (hay) { libre = mejorP; return true; }
            }
            return false;
        }

        static bool Libre(int ix, int iz) => ix >= 0 && iz >= 0 && ix < nx && iz < nz && !mapa[ix, iz];

        // ---------------- construccion ----------------
        static void Asegurar()
        {
            Terrain t = null;
            if (mapa != null && armado) return;   // ya armado
            if (intentado && mapa == null) return;
            foreach (var c in Terrain.activeTerrains)
                if (c != null && c.GetComponent<TerrainCollider>() != null) { t = c; break; }
            if (t == null) { intentado = true; return; }   // escena sin terreno jugable (suite, tutorial sin terreno)
            Construir(t);
        }

        static void Construir(Terrain t)
        {
            intentado = true;
            var d = t.terrainData;
            var pos = t.transform.position;
            origen = new Vector2(pos.x, pos.z);
            nx = Mathf.CeilToInt(d.size.x / Celda);
            nz = Mathf.CeilToInt(d.size.z / Celda);
            mapa = new bool[nx, nz];
            armado = true;

            foreach (var ti in d.treeInstances)
                Marcar(pos.x + ti.position.x * d.size.x, pos.z + ti.position.z * d.size.z, RadioDeArbol * Mathf.Clamp(ti.widthScale, 0.8f, 1.3f));

            int w = d.detailWidth, h = d.detailHeight;
            for (int layer = 0; layer < d.detailPrototypes.Length; layer++)
            {
                var capa = d.GetDetailLayer(0, 0, w, h, layer);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        if (capa[j, i] > 0)
                            Marcar(pos.x + (i + 0.5f) / w * d.size.x, pos.z + (j + 0.5f) / h * d.size.z, RadioDeArbusto);
            }

            // Se engorda la mancha una celda (1,5 m): un hueco de un paso entre dos matorrales sigue siendo "estar en el monte",
            // asi "afuera" quiere decir en un claro de verdad y no pegado a los arbustos.
            var engordado = new bool[nx, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (!mapa[ix, iz]) continue;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int jx = ix + dx, jz = iz + dz;
                            if (jx >= 0 && jz >= 0 && jx < nx && jz < nz) engordado[jx, jz] = true;
                        }
                }
            // ...y se vuelve a afinar una celda: el borde exterior queda donde estaba y solo se rellenan los huecos chicos.
            var cerrado = new bool[nx, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    bool todos = true;
                    for (int dx = -1; dx <= 1 && todos; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int jx = ix + dx, jz = iz + dz;
                            if (jx >= 0 && jz >= 0 && jx < nx && jz < nz && !engordado[jx, jz]) { todos = false; break; }
                        }
                    cerrado[ix, iz] = todos;
                }
            mapa = cerrado;
        }

        // Al empezar la partida: los enemigos que arrancan metidos en el monte y tienen un claro a pocos metros se corren ahi
        // (no quedan parados entre arbustos sin poder disparar). Devuelve cuantos se movieron.
        public static int DespejarEnemigosIniciales(float maxMetros = 9f)
        {
            if (!Rige) return 0;
            Asegurar();
            if (mapa == null) return 0;
            int n = 0;
            foreach (var s in SP.Core.ActorRegistry.All)
            {
                if (s == null || s.Team != SP.Combat.TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (s.Brain != null && s.Brain.MontadoEnVehiculo) continue;
                var p = s.transform.position;
                if (!Mancha(p) || !TryPuntoLibre(p, out var libre)) continue;
                var d = libre - p; d.y = 0f;
                if (d.magnitude > maxMetros) continue;
                s.transform.position = new Vector3(libre.x, p.y, libre.z);
                n++;
            }
            return n;
        }

        static void Marcar(float x, float z, float radio)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((x - radio - origen.x) / Celda)), x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((x + radio - origen.x) / Celda));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((z - radio - origen.y) / Celda)), z1 = Mathf.Min(nz - 1, Mathf.FloorToInt((z + radio - origen.y) / Celda));
            float r2 = radio * radio;
            for (int ix = x0; ix <= x1; ix++)
                for (int iz = z0; iz <= z1; iz++)
                {
                    float cx = origen.x + (ix + 0.5f) * Celda - x, cz = origen.y + (iz + 0.5f) * Celda - z;
                    if (cx * cx + cz * cz <= r2) mapa[ix, iz] = true;
                }
        }

        // Para informes y capturas: cuantas celdas son vegetacion y cuantas hay en total.
        public static string Resumen()
        {
            Asegurar();
            if (mapa == null) return "sin mapa";
            int n = 0;
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) if (mapa[i, j]) n++;
            return $"{n} de {nx * nz} celdas ({n * 100f / (nx * nz):0.0}%)";
        }
    }
}
