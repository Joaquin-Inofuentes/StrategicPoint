using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Analisis y saneado de los pesos de piel de las mallas de soldado (#109). Las mallas del arte son cajas de bajo poligonaje: la
    // transferencia de pesos por vertice (SoldierVariantBuilder.PesoPorSuperficie) podia dejar caras con vertices pesados a la cadera
    // y otros al antebrazo/mano, y con la animacion de accion se estiraba una astilla mano->cadera. Aca vive la clasificacion de huesos
    // por cadena, el recuento de caras mezcladas y el post-proceso que las corrige.
    public static class PielSoldadoAnalisis
    {
        public enum Cadena { Nucleo, Cabeza, BrazoIzq, BrazoDer, PiernaIzq, PiernaDer }

        const string MallaRiggeada = "Assets/ARTS/Slim Shooter Pack/lego.fbx";

        // Nombres de hueso (sin "mixamorig:") del rig; se llena de lego.fbx.
        public static string[] NombresDeHueso()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(MallaRiggeada);
            var piel = go != null ? go.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (piel == null) return null;
            var r = new string[piel.bones.Length];
            for (int i = 0; i < r.Length; i++) r[i] = piel.bones[i] != null ? piel.bones[i].name.Replace("mixamorig:", "") : "?";
            return r;
        }

        public static Cadena CadenaDe(string hueso)
        {
            bool izq = hueso.Contains("Left"), der = hueso.Contains("Right");
            if (hueso.Contains("UpLeg") || hueso.Contains("Leg") || hueso.Contains("Foot") || hueso.Contains("Toe"))
                return izq ? Cadena.PiernaIzq : Cadena.PiernaDer;
            if (hueso.Contains("Shoulder") || hueso.Contains("Arm") || hueso.Contains("Hand"))
                return izq ? Cadena.BrazoIzq : Cadena.BrazoDer;
            if (hueso.Contains("Head") || hueso.Contains("Neck")) return Cadena.Cabeza;
            return Cadena.Nucleo; // Hips, Spine*
        }

        // Hueso "distal" del brazo (el que se mueve lejos del tronco): antebrazo, mano y dedos.
        public static bool EsAntebrazoOMano(string hueso) => hueso.Contains("ForeArm") || hueso.Contains("Hand");
        public static bool EsCadera(string hueso) => hueso == "Hips" || hueso.Contains("UpLeg");

        static int Dominante(BoneWeight w)
        {
            return w.weight0 >= w.weight1 && w.weight0 >= w.weight2 && w.weight0 >= w.weight3 ? w.boneIndex0 :
                   w.weight1 >= w.weight2 && w.weight1 >= w.weight3 ? w.boneIndex1 :
                   w.weight2 >= w.weight3 ? w.boneIndex2 : w.boneIndex3;
        }

        // Cadenas con peso > 0.02 en el vertice.
        static HashSet<Cadena> CadenasDelVertice(BoneWeight w, string[] n)
        {
            var s = new HashSet<Cadena>();
            if (w.weight0 > 0.02f) s.Add(CadenaDe(n[w.boneIndex0]));
            if (w.weight1 > 0.02f) s.Add(CadenaDe(n[w.boneIndex1]));
            if (w.weight2 > 0.02f) s.Add(CadenaDe(n[w.boneIndex2]));
            if (w.weight3 > 0.02f) s.Add(CadenaDe(n[w.boneIndex3]));
            return s;
        }

        // Adyacencia entre cadenas: nucleo toca cabeza y brazos (hombro) y piernas (cadera). Brazo vs pierna, brazo vs brazo y pierna vs pierna no.
        static bool Adyacentes(Cadena a, Cadena b)
        {
            if (a == b) return true;
            if (a == Cadena.Nucleo || b == Cadena.Nucleo) return true;
            return false;
        }

        // Una cara es "mezclada" si el hueso dominante de alguno de sus vertices es antebrazo/mano y otro es cadera/pierna (o del otro
        // brazo), o si un solo vertice mezcla antebrazo/mano con cadera/pierna.
        public static int CarasMezcladas(Mesh m, string[] n, out List<int> vertices)
        {
            vertices = new List<int>();
            var bw = m.boneWeights; var tr = m.triangles;
            int malas = 0;
            for (int t = 0; t < tr.Length; t += 3)
            {
                bool mala = false;
                for (int e = 0; e < 3 && !mala; e++)
                {
                    int a = tr[t + e], b = tr[t + (e + 1) % 3];
                    string ha = n[Dominante(bw[a])], hb = n[Dominante(bw[b])];
                    if (AristaMala(ha, hb)) mala = true;
                }
                if (mala) { malas++; vertices.Add(tr[t]); vertices.Add(tr[t + 1]); vertices.Add(tr[t + 2]); }
            }
            // vertices individuales con mezcla brazo-distal + cadera/pierna
            for (int i = 0; i < bw.Length; i++)
            {
                var c = CadenasDelVertice(bw[i], n);
                bool distal = false, cad = false;
                void ver(int idx, float w) { if (w <= 0.02f) return; var h = n[idx]; if (EsAntebrazoOMano(h)) distal = true; if (EsCadera(h) || CadenaDe(h) == Cadena.PiernaIzq || CadenaDe(h) == Cadena.PiernaDer) cad = true; }
                ver(bw[i].boneIndex0, bw[i].weight0); ver(bw[i].boneIndex1, bw[i].weight1); ver(bw[i].boneIndex2, bw[i].weight2); ver(bw[i].boneIndex3, bw[i].weight3);
                if (distal && cad) { malas++; vertices.Add(i); }
            }
            return malas;
        }

        // ---------- saneado (lo llama SoldierVariantBuilder.Generar) ----------
        // Repara las caras mezcladas: se suelda por posicion (los cubos tienen vertices duplicados por cara), se agrupan los vertices en
        // conflicto por las aristas malas y, por cada grupo, gana la cadena mayoritaria del entorno (2 anillos de triangulos; en empate
        // gana el brazo, que es lo que cuelga junto a la cadera en reposo). Los vertices perdedores copian los pesos del vertice
        // ganador mas cercano, asi la pieza entera se mueve rigida con un solo hueso. Devuelve cuantos vertices se corrigieron.
        public static int Sanear(Mesh m, string[] n)
        {
            var vs = m.vertices; var tr = m.triangles; var bw = m.boneWeights;
            int total = 0;
            for (int pasada = 0; pasada < 6; pasada++)
            {
                int cambios = SanearPasada(vs, tr, bw, n);
                total += cambios;
                if (cambios == 0) break;
            }
            m.boneWeights = bw;
            return total;
        }

        static int SanearPasada(Vector3[] vs, int[] tr, BoneWeight[] bw, string[] n)
        {
            // soldadura por posicion
            var clave = new Dictionary<Vector3Int, int>();
            var sol = new int[vs.Length];
            var miembros = new List<List<int>>();
            for (int i = 0; i < vs.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(vs[i].x * 2000f), Mathf.RoundToInt(vs[i].y * 2000f), Mathf.RoundToInt(vs[i].z * 2000f));
                if (!clave.TryGetValue(k, out var id)) { id = clave.Count; clave[k] = id; miembros.Add(new List<int>()); }
                sol[i] = id; miembros[id].Add(i);
            }
            int nw = clave.Count;
            // chequeo en el espacio soldado: la cadena del vertice soldado = la del primer miembro (los duplicados comparten pesos casi siempre)
            var vecinos = new List<int>[nw];
            for (int i = 0; i < nw; i++) vecinos[i] = new List<int>();
            for (int t = 0; t < tr.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = sol[tr[t + e]], b = sol[tr[t + (e + 1) % 3]];
                    if (a == b) continue;
                    if (!vecinos[a].Contains(b)) vecinos[a].Add(b);
                    if (!vecinos[b].Contains(a)) vecinos[b].Add(a);
                }
            // cadena y "distal/cadera" de cada soldado; un vertice soldado con miembros de huesos distintos queda en conflicto consigo mismo
            var hueso = new string[nw];
            for (int i = 0; i < nw; i++) hueso[i] = n[Dominante(bw[miembros[i][0]])];

            // aristas malas (entre soldados) -> componentes
            var padre = new int[nw]; for (int i = 0; i < nw; i++) padre[i] = i;
            int Raiz(int x) { while (padre[x] != x) { padre[x] = padre[padre[x]]; x = padre[x]; } return x; }
            var enConflicto = new bool[nw];
            for (int t = 0; t < tr.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = sol[tr[t + e]], b = sol[tr[t + (e + 1) % 3]];
                    if (a == b || !AristaMala(hueso[a], hueso[b])) continue;
                    enConflicto[a] = enConflicto[b] = true;
                    padre[Raiz(a)] = Raiz(b);
                }
            var comps = new Dictionary<int, List<int>>();
            for (int i = 0; i < nw; i++)
                if (enConflicto[i]) { int r = Raiz(i); if (!comps.TryGetValue(r, out var l)) { l = new List<int>(); comps[r] = l; } l.Add(i); }

            int cambios = 0;
            foreach (var comp in comps.Values)
            {
                // entorno: componente + 2 anillos
                var entorno = new HashSet<int>(comp);
                var frontera = new List<int>(comp);
                for (int anillo = 0; anillo < 2; anillo++)
                {
                    var sig = new List<int>();
                    foreach (var v in frontera) foreach (var w in vecinos[v]) if (entorno.Add(w)) sig.Add(w);
                    frontera = sig;
                }
                var cuentas = new Dictionary<Cadena, int>();
                foreach (var v in entorno) { var c = CadenaDe(hueso[v]); cuentas[c] = cuentas.TryGetValue(c, out var q) ? q + 1 : 1; }
                // cadenas en juego = las de los vertices en conflicto; ganadora: mas votos, empate -> brazo
                Cadena? ganadora = null; int mejor = -1;
                var enJuego = new HashSet<Cadena>(); foreach (var v in comp) enJuego.Add(CadenaDe(hueso[v]));
                foreach (var c in enJuego)
                {
                    int votos = cuentas.TryGetValue(c, out var q) ? q : 0;
                    bool brazo = c == Cadena.BrazoIzq || c == Cadena.BrazoDer;
                    int puntaje = votos * 2 + (brazo ? 1 : 0);
                    if (puntaje > mejor) { mejor = puntaje; ganadora = c; }
                }
                // la cadena ganadora aporta los pesos; los vertices de las otras cadenas (en conflicto) copian del ganador mas cercano
                var donantes = new List<int>();
                foreach (var v in entorno) if (CadenaDe(hueso[v]) == ganadora.Value && !(v < 0)) donantes.Add(v);
                if (donantes.Count == 0) continue;
                foreach (var v in comp)
                {
                    if (CadenaDe(hueso[v]) == ganadora.Value) continue;
                    var p = vs[miembros[v][0]];
                    int mejorD = donantes[0]; float dm = float.MaxValue;
                    foreach (var d in donantes) { float dd = (vs[miembros[d][0]] - p).sqrMagnitude; if (dd < dm) { dm = dd; mejorD = d; } }
                    var peso = bw[miembros[mejorD][0]];
                    foreach (var idx in miembros[v]) bw[idx] = peso;
                    cambios++;
                }
            }
            // vertices individuales que mezclan antebrazo/mano con cadera/pierna: nos quedamos con el grupo del hueso dominante
            for (int i = 0; i < bw.Length; i++)
            {
                bool distal = false, cad = false;
                void ver(int idx, float w) { if (w <= 0.02f) return; var h = n[idx]; if (EsAntebrazoOMano(h)) distal = true; var c = CadenaDe(h); if (EsCadera(h) || c == Cadena.PiernaIzq || c == Cadena.PiernaDer) cad = true; }
                ver(bw[i].boneIndex0, bw[i].weight0); ver(bw[i].boneIndex1, bw[i].weight1); ver(bw[i].boneIndex2, bw[i].weight2); ver(bw[i].boneIndex3, bw[i].weight3);
                if (!(distal && cad)) continue;
                var dom = n[Dominante(bw[i])];
                var nuevo = new BoneWeight { boneIndex0 = Dominante(bw[i]), weight0 = 1f };
                bw[i] = nuevo; cambios++;
            }
            return cambios;
        }

        static bool AristaMala(string ha, string hb)
        {
            var ca = CadenaDe(ha); var cb = CadenaDe(hb);
            if (ca == cb) return false;
            bool distal = EsAntebrazoOMano(ha) || EsAntebrazoOMano(hb);
            bool lejos = !Adyacentes(ca, cb) || EsCadera(ha) || EsCadera(hb);
            return distal && lejos;
        }

        // ---------- diagnostico por texto ----------
        public static string Diagnostico(string nombreMalla)
        {
            var n = NombresDeHueso(); if (n == null) return "sin rig";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/_Project/Resources/Soldados/{nombreMalla}.asset");
            if (m == null) return "sin malla " + nombreMalla;
            var sb = new StringBuilder();
            int malas = CarasMezcladas(m, n, out var vs);
            sb.AppendLine($"{nombreMalla}: verts={m.vertexCount} tris={m.triangles.Length / 3} carasMezcladas={malas} distintosVertices={new HashSet<int>(vs).Count}");
            var islas = Islas(m);
            sb.AppendLine("islas=" + islas.Count);
            var bw = m.boneWeights;
            foreach (var isla in islas)
            {
                var hist = new Dictionary<string, int>();
                foreach (var v in isla) { var h = n[Dominante(bw[v])]; hist[h] = hist.TryGetValue(h, out var q) ? q + 1 : 1; }
                var s = new StringBuilder();
                foreach (var kv in hist) s.Append(kv.Key + ":" + kv.Value + " ");
                var b = new Bounds(m.vertices[isla[0]], Vector3.zero); foreach (var v in isla) b.Encapsulate(m.vertices[v]);
                sb.AppendLine($"  isla v={isla.Count} centro={b.center.ToString("F2")} tam={b.size.ToString("F2")} -> {s}");
            }
            return sb.ToString();
        }

        // Islas conectadas: se sueldan los vertices por posicion (los cubos de bajo poligonaje tienen vertices duplicados por cara).
        public static List<List<int>> Islas(Mesh m)
        {
            var vs = m.vertices; var tr = m.triangles;
            var clave = new Dictionary<Vector3Int, int>();
            var soldado = new int[vs.Length];
            for (int i = 0; i < vs.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(vs[i].x * 2000f), Mathf.RoundToInt(vs[i].y * 2000f), Mathf.RoundToInt(vs[i].z * 2000f));
                if (!clave.TryGetValue(k, out var id)) { id = clave.Count; clave[k] = id; }
                soldado[i] = id;
            }
            var padre = new int[clave.Count]; for (int i = 0; i < padre.Length; i++) padre[i] = i;
            int Raiz(int x) { while (padre[x] != x) { padre[x] = padre[padre[x]]; x = padre[x]; } return x; }
            for (int t = 0; t < tr.Length; t += 3)
            {
                int a = Raiz(soldado[tr[t]]), b = Raiz(soldado[tr[t + 1]]), c = Raiz(soldado[tr[t + 2]]);
                padre[b] = a; padre[Raiz(c)] = Raiz(a);
            }
            var grupos = new Dictionary<int, List<int>>();
            for (int i = 0; i < vs.Length; i++)
            {
                int r = Raiz(soldado[i]);
                if (!grupos.TryGetValue(r, out var l)) { l = new List<int>(); grupos[r] = l; }
                l.Add(i);
            }
            return new List<List<int>>(grupos.Values);
        }
    }
}
