using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using G = SP.Operacion.MansionGeometria;

namespace SP.Operacion
{
    // Bug #013 (build, "no deberian poder bajar una vez que suben de piso y no deberian poder subir si faltan enemigos"): las escaleras de la mansion
    // estaban abiertas siempre, asi que se subia dejando enemigos atras, la escuadra se dispersaba entre pisos y se podia volver a bajar.
    // Cada torre tiene dos compuertas (una reja de seguridad, solida tambien para el NavMesh):
    //   - de SUBIDA (carril A, en el piso de abajo): cerrada mientras queden enemigos en el piso actual (y, en el 2.o, mientras no se liberen los milicianos);
    //   - de BAJADA (carril B, en el piso de arriba): se cierra apenas la mision pasa a ese piso y ya no vuelve a abrirse (no se vuelve atras).
    // Red de seguridad: pasados 150 s en un piso la de subida se abre igual (un enemigo trabado no puede dejar la mision sin salida).
    public partial class OperacionDirector
    {
        public const float AnchoDeCompuerta = 4.9f, AltoDeCompuerta = 4.4f, SegundosParaForzarCompuerta = 150f;

        sealed class CompuertaDeTorre
        {
            public GameObject go; public BoxCollider col; public UnityEngine.AI.NavMeshObstacle obs;
            public int torre, nivelBase; public bool subida, cerrada = true, trabada;
        }

        readonly List<CompuertaDeTorre> compuertasDeTorre = new List<CompuertaDeTorre>();
        int pisoDeLasCompuertas = -99; float inicioDelPiso;
        public int CompuertasCerradas { get { int n = 0; foreach (var c in compuertasDeTorre) if (c.cerrada) n++; return n; } }
        public int CompuertasTotales => compuertasDeTorre.Count;
        public bool CompuertaDeSubidaCerrada(int piso) { foreach (var c in compuertasDeTorre) if (c.subida && c.nivelBase == piso && c.cerrada) return true; return false; }
        public bool CompuertaDeBajadaCerrada(int piso) { foreach (var c in compuertasDeTorre) if (!c.subida && c.nivelBase + 1 == piso && c.cerrada) return true; return false; }

        bool HayEnemigoEnLaTorre(int torre)
        {
            for (int i = 0; i < enemigosMansion.Count; i++)
            {
                var e = enemigosMansion[i];
                if (e == null || e.Health == null || !e.Health.IsAlive) continue;
                if (G.EnTorre(e.transform.position, out int idx, 0.5f) && idx == torre) return true;
            }
            return false;
        }

        void QuitarCompuertas()
        {
            foreach (var c in compuertasDeTorre) if (c.go != null) Destroy(c.go);
            compuertasDeTorre.Clear(); pisoDeLasCompuertas = -99;
        }

        CompuertaDeTorre CrearCompuerta(int torre, bool subida)
        {
            var t = G.Torres[torre];
            int nivel = subida ? t.nivelBase : t.nivelBase + 1;
            var p = t.Mundo(subida ? G.CarrilA : G.CarrilB, -0.6f);
            var centro = new Vector3(p.x, G.Nivel(nivel) + AltoDeCompuerta * 0.5f, p.y);
            var go = new GameObject($"Mansion_Compuerta_{t.nombre}_{(subida ? "subida" : "bajada")}");
            go.transform.position = centro;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(t.v.x, 0f, t.v.y), Vector3.up);
            var size = new Vector3(AnchoDeCompuerta, AltoDeCompuerta, 0.8f);
            var col = go.AddComponent<BoxCollider>(); col.size = size;
            var obs = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box; obs.size = size; obs.carving = true; obs.carveOnlyStationary = false;
            // La reja se ve: una placa fina rojiza (no un muro opaco) para que se lea como "cerrado".
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Reja";
            var vc = vis.GetComponent<Collider>(); if (vc != null) Destroy(vc);
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = new Vector3(AnchoDeCompuerta, AltoDeCompuerta, 0.12f);
            var mr = vis.GetComponent<Renderer>();
            if (mr != null) { mr.sharedMaterial = SP.Presentation.SafeMaterial.Create(new Color(0.85f, 0.1f, 0.07f)); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            return new CompuertaDeTorre { go = go, col = col, obs = obs, torre = torre, nivelBase = t.nivelBase, subida = subida };
        }

        static void Poner(CompuertaDeTorre c, bool cerrada)
        {
            if (c.cerrada == cerrada && c.go != null && c.col != null && c.col.enabled == cerrada) return;
            c.cerrada = cerrada;
            if (c.col != null) c.col.enabled = cerrada;
            if (c.obs != null) c.obs.enabled = cerrada;
            if (c.go != null) foreach (Transform h in c.go.transform) h.gameObject.SetActive(cerrada);
        }

        // Cada tick de la mansion. 'faltan' = enemigos vivos + los que todavia van a salir.
        void ActualizarCompuertas(int faltan, Soldier yo)
        {
            if (compuertasDeTorre.Count == 0)
                for (int i = 0; i < G.Torres.Length; i++) { compuertasDeTorre.Add(CrearCompuerta(i, true)); compuertasDeTorre.Add(CrearCompuerta(i, false)); }
            int piso;
            switch (EtapaMansion)
            {
                case EtapaDeLaMansion.PlantaBaja: piso = 0; break;
                case EtapaDeLaMansion.PrimerPiso: piso = 1; break;
                case EtapaDeLaMansion.SegundoPiso: piso = 2; break;
                case EtapaDeLaMansion.Azotea: case EtapaDeLaMansion.Cerrada: piso = 3; break;
                default: piso = -1; break;
            }
            if (piso != pisoDeLasCompuertas) { pisoDeLasCompuertas = piso; inicioDelPiso = relojMansion; }
            bool forzar = relojMansion - inicioDelPiso > SegundosParaForzarCompuerta;
            bool pisoLimpio = faltan == 0 && (piso != 2 || MilicianosLiberados);
            // La torre es un zigzag (los dos tramos suben): la reja de BAJADA esta en la boca del tramo B, por donde tambien se sale al subir. Solo se
            // cierra cuando el jugador ya esta en el piso de arriba y fuera de toda torre (se traba: ya no se reabre).
            bool yoAfuera = yo != null && !G.EnTorre(yo.transform.position, out _, 0.8f);
            foreach (var c in compuertasDeTorre)
            {
                bool dentro = HayEnemigoEnLaTorre(c.torre);   // los que salen del rellano (bajan o suben) no se quedan encerrados por la reja
                if (c.subida) { Poner(c, c.nivelBase == piso && !pisoLimpio && !forzar && !dentro); continue; }
                if (!c.trabada && piso >= c.nivelBase + 1 && yoAfuera && yo.transform.position.y >= G.Nivel(c.nivelBase + 1) - 0.3f) c.trabada = true;
                Poner(c, c.trabada && !dentro);
            }
            // Aviso cuando el que manejas se para frente a una reja de subida cerrada.
            if (yo != null && hud != null)
                foreach (var c in compuertasDeTorre)
                {
                    if (!c.cerrada || !c.subida || c.nivelBase != G.NivelDe(yo.transform.position)) continue;
                    if (Plano(yo.transform.position, c.go.transform.position).magnitude < 5f)
                    {
                        hud.Prompt(faltan > 0 ? $"ESCALERA CERRADA · ELIMINA A LOS {faltan} ENEMIGOS DEL PISO" : "ESCALERA CERRADA · LIBERA A LOS MILICIANOS");
                        break;
                    }
                }
        }

        // Bug #013 (build, Extraer): "no logran subir los pisos para subirse al helicoptero y se perdieron". Con el helicoptero posado en la azotea, el
        // aliado (escuadra o miliciano) que lleva mas de 8 s sin llegar al techo (trabado en una escalera o en otro piso, sin camino) se
        // acerca al helicoptero; asi nadie queda atras y se puede subir.
        public const float SegundosDeRezagoAlHeli = 8f;
        readonly Dictionary<int, float> rezagadosDelHeli = new Dictionary<int, float>();
        float proximaAyudaAlHeli;
        public int AliadosAyudadosAlHeli { get; private set; }

        void AyudarALaEscuadraAlHeli()
        {
            if (heli == null || Time.time < proximaAyudaAlHeli) return;
            proximaAyudaAlHeli = Time.time + 1.5f;
            // El helicoptero de rescate espera en el aire (pad + 16 m): la altura del techo es la del helipad, no la del helicoptero.
            float techo = heliAterrizaje != null ? heliAterrizaje.position.y : heli.position.y;
            if (techo < 10f) return;   // el helicoptero no esta en la azotea: nada que subir
            foreach (var a in AliadosAEvacuar(false))
            {
                if (a == null || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy || a.Brain == null || a.Brain.IsPossessedByPlayer) continue;
                if (a.transform.position.y >= techo - 4f) { rezagadosDelHeli.Remove(a.Id); continue; }
                rezagadosDelHeli.TryGetValue(a.Id, out float t);
                t += 1.5f;
                if (t < SegundosDeRezagoAlHeli) { rezagadosDelHeli[a.Id] = t; continue; }
                rezagadosDelHeli.Remove(a.Id);
                float ang = UnityEngine.Random.value * Mathf.PI * 2f, r = UnityEngine.Random.Range(3.5f, 7f);
                var destino = new Vector3(heli.position.x + Mathf.Cos(ang) * r, techo - 0.3f, heli.position.z + Mathf.Sin(ang) * r);
                if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var h, 3f, UnityEngine.AI.NavMesh.AllAreas)) destino = h.position;
                ColocarEnElPiso(a.transform, destino);
                a.Brain.CancelOrder(); a.Brain.ReactivarNavegacion();
                AliadosAyudadosAlHeli++;
                GameLog.Line($"[Operacion] Extraccion: {a.DisplayName} no llegaba a la azotea, se lo acerca al helicoptero");
            }
        }
    }
}
