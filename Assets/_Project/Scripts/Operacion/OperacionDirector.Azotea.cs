using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.UI;
using G = SP.Operacion.MansionGeometria;

namespace SP.Operacion
{
    // Fin del objetivo 5: RESISTIR EN LA AZOTEA hasta que llegue el helicoptero de rescate. Al liberar a los milicianos y subir, el Halcon enemigo
    // cae sobre el techo; durante SegundosDeResistenciaEnAzotea llegan oleadas por las otras escaleras y helicopteros que cuelgan cuerdas y bajan
    // soldados al techo (y despues ametrallan). El helicoptero propio se ve venir (la flecha cuenta los metros); al cumplirse el tiempo queda
    // sobre la azotea cubriendo, los helicopteros enemigos que quedan estallan y empieza la extraccion (Extraer) EN EL TECHO.
    public partial class OperacionDirector
    {
        public const float SegundosDeResistenciaEnAzotea = 100f;
        public static readonly float[] CuandoSuben = { 6f, 26f, 50f, 78f };
        public static readonly int[] TorreDelRefuerzo = { 3, 4, 2, 3 };
        public static readonly float[] CuandoCuelgan = { 14f, 40f, 66f };
        public static readonly int[] SoldadosPorCuerda = { 3, 4, 4 };
        static readonly float[] AnguloDeEntradaDeCuerda = { 200f, 330f, 100f };
        // Puntos del techo donde se cuelgan las cuerdas (se elige el que queda a mas de 9 m del jugador).
        static readonly Vector3[] PuntosDeCuerda =
        {
            new Vector3(306f, 19.5f, 256f), new Vector3(338f, 19.5f, 256f), new Vector3(308f, 19.5f, 277f), new Vector3(336f, 19.5f, 277f),
            new Vector3(322f, 19.5f, 252f), new Vector3(322f, 19.5f, 280f),
        };

        public int CuerdasLanzadas { get; private set; }
        public bool AzoteaEnCurso => MansionActiva && EtapaMansion == EtapaDeLaMansion.Azotea && azoteaIniciada;
        public float SegundosDeAzotea => tAzotea;
        public IReadOnlyList<HelicopteroEnemigo> HelisDeLaAzotea => helisDeAzotea;
        readonly List<HelicopteroEnemigo> helisDeAzotea = new List<HelicopteroEnemigo>();
        Vector3 heliAterrizajePlaza; bool heliAterrizajeGuardado;

        // La zona de aterrizaje de la Operacion es la plaza; con la mansion en juego pasa a ser el techo (el rescate y la subida son arriba).
        void HelipadEnLaAzotea()
        {
            if (heliAterrizaje == null) return;
            if (!heliAterrizajeGuardado) { heliAterrizajePlaza = heliAterrizaje.position; heliAterrizajeGuardado = true; }
            heliAterrizaje.position = G.CentroDeLaAzotea;
        }

        void ReiniciarAzotea()
        {
            foreach (var h in helisDeAzotea) if (h != null) Destroy(h.gameObject);
            helisDeAzotea.Clear();
            CuerdasLanzadas = 0;
        }

        void TickAzotea(float dt, int faltan)
        {
            tAzotea += dt;
            // Oleadas por las otras escaleras: cada una asoma desde el rellano y sube por el carril de bajada.
            while (ondasDeAzotea < CuandoSuben.Length && tAzotea >= CuandoSuben[ondasDeAzotea])
            {
                int i = TorreDelRefuerzo[ondasDeAzotea++];
                var p = G.RellanoDeTorre(i);
                int cuantos = ondasDeAzotea >= 3 ? 5 : 4;
                for (int k = 0; k < cuantos; k++) pendientesMansion.Add(new SpawnMansion { pos = p, radio = 0f, dispararEn = relojMansion + k * 1.1f, nivel = 3, desdeEscalera = true });
                if (hud != null) hud.Aviso($"REFUERZOS POR LA ESCALERA {G.Torres[i].nombre}", 2.6f);
            }
            while (CuerdasLanzadas < CuandoCuelgan.Length && tAzotea >= CuandoCuelgan[CuerdasLanzadas]) LanzarHeliDeCuerdas(CuerdasLanzadas++);

            float u = Mathf.Clamp01(tAzotea / SegundosDeResistenciaEnAzotea);
            if (heli != null) VolarHeli(u * FraccionAproximacion);   // llega a quedar en estacionario sobre el techo justo al cumplirse el tiempo

            bool heliVivo = HeliDeLaAzotea != null && !HeliDeLaAzotea.Derribado;
            int seg = Mathf.CeilToInt(Mathf.Max(0f, SegundosDeResistenciaEnAzotea - tAzotea));
            if (hud != null)
            {
                string extra = heliVivo ? " · derriba al Halcon" : "";
                hud.Objetivo(TituloObjetivo(5, "RESISTIR EN LA AZOTEA"), $"Aguanta {seg} s hasta que llegue el helicoptero de rescate · {faltan} enemigos{extra}", u, new Color(1f, 0.4f, 0.3f));
                hud.Timer("RESCATE", seg, seg <= 8 ? new Color(0.45f, 1f, 0.55f) : Color.white);
                if (heliVivo) hud.BarraDeJefe(HelicopteroEnemigo.Nombre, HeliDeLaAzotea.Fraccion01);
                else hud.OcultarBarraDeJefe();
            }
            if (tAzotea >= SegundosDeResistenciaEnAzotea) LlegaElRescate();
        }

        // Un helicoptero enemigo viene por 'indice', queda a 10 m sobre un punto del techo (el mas lejano al jugador que cumpla) y baja a N soldados por
        // dos cuerdas; despues orbita la azotea ametrallando.
        HelicopteroEnemigo LanzarHeliDeCuerdas(int indice)
        {
            if (halcon == null || plantillaDeRefuerzo == null || anclaDelHeli == null) return null;
            var yo = Poseido();
            var punto = PuntosDeCuerda[indice % PuntosDeCuerda.Length];
            float mejor = -1f;
            foreach (var c in PuntosDeCuerda)
            {
                float d = yo != null ? Plano(c, yo.transform.position).magnitude : 20f;
                if (d < 9f) continue;
                float azar = d + Random.Range(0f, 14f);
                if (azar > mejor) { mejor = azar; punto = c; }
            }
            if (UnityEngine.AI.NavMesh.SamplePosition(punto + Vector3.up * 0.3f, out var nv, 3f, UnityEngine.AI.NavMesh.AllAreas) && Mathf.Abs(nv.position.y - punto.y) < 1f) punto = nv.position;
            int soldados = SoldadosPorCuerda[indice % SoldadosPorCuerda.Length];
            var h = HelicopteroEnemigo.IniciarDespliegueEnAncla(halcon, anclaDelHeli, plantillaDeRefuerzo.gameObject, punto, soldados,
                AnguloDeEntradaDeCuerda[indice % AnguloDeEntradaDeCuerda.Length], G.Nivel(3), 30f, AlBajarPorLaCuerda);
            if (h == null) return null;
            helisDeAzotea.Add(h);
            HalconesCreados++;
            h.AlDerribarse += () => HalconesDerribados++;
            if (hud != null) hud.Aviso("¡HELICOPTERO ENEMIGO!\nSUELTA TROPAS EN CUERDAS SOBRE LA AZOTEA", 2.8f);
            SesionLog.Evento($"MANSION: helicoptero de cuerdas {indice + 1} sobre {punto} ({soldados} soldados)");
            return h;
        }

        void AlBajarPorLaCuerda(Soldier s)
        {
            if (s == null) return;
            MansionEnemigosCreados++;
            s.name = "Mansion_Enemigo_" + MansionEnemigosCreados;
            enemigosMansion.Add(s);
            CazaDeEnemigos.Marcar(new[] { s });
            var yo = Poseido();
            if (s.Brain != null && yo != null) s.Brain.SetPatrolRoute(new[] { yo.transform.position, yo.transform.position });
        }

        void LlegaElRescate()
        {
            EtapaMansion = EtapaDeLaMansion.Cerrada;
            pendientesMansion.Clear();
            // El helicoptero de rescate llega con fuego pesado: los enemigos que siguen en el aire estallan.
            if (HeliDeLaAzotea != null) HeliDeLaAzotea.ExplotarEnElAire();
            foreach (var h in helisDeAzotea) if (h != null) h.ExplotarEnElAire();
            if (hud != null) { hud.OcultarTimer(); hud.OcultarBarraDeJefe(); }
            Aviso("¡LLEGO EL HELICOPTERO DE RESCATE!\nREUNI A TODOS EN LA AZOTEA");
            SesionLog.Evento("MANSION: llega el rescate");
            if (heli != null) { var q = heli.rotation.eulerAngles; heli.rotation = Quaternion.Euler(0f, q.y, 0f); }
            HeliAterrizo = false;
            EntrarFase(FaseOperacion.Extraer);
        }

        // Pone a todos los aliados vivos (escuadra y milicianos) sobre el piso pedido, con el que manejas en el centro y el resto alrededor.
        void PonerLaEscuadraEnElPiso(Vector3 centro)
        {
            var lista = Escuadra(false);
            var yo = Poseido();
            lista.Remove(yo);
            if (yo != null) lista.Insert(0, yo);
            foreach (var m in milicianosActivos) if (m != null && !lista.Contains(m)) lista.Add(m);
            for (int i = 0; i < lista.Count; i++)
            {
                var s = lista[i];
                if (s == null) continue;
                float a = i * 1.9f;
                ColocarEnElPiso(s.transform, centro + new Vector3(Mathf.Cos(a) * (1.5f + i * 0.6f), 0f, Mathf.Sin(a) * (1.5f + i * 0.6f)));
                if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); }
            }
        }

        // Atajo de pruebas y F10 ("OBJ 5 · Resistir en la azotea"): deja hechas la plaza, las tres plantas y las celdas, y arranca la resistencia en el techo.
        public void SaltarALaAzotea()
        {
            SaltarA(FaseOperacion.Resistir, true);
            if (!MansionActiva) return;
            StopAllCoroutines();
            foreach (var s in enemigosMansion) if (s != null) Destroy(s.gameObject);
            enemigosMansion.Clear(); pendientesMansion.Clear();
            plazaRestantes = 0; porton = true; segundoIniciado = true; CineDeMansionEnCurso = false;
            foreach (var h in hojasDelPorton) if (h != null) h.Abrir();
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            PonerPrisioneros();
            LiberarMilicianos();
            PonerLaEscuadraEnElPiso(G.CentroDeLaAzotea + new Vector3(-8f, 0f, 3f));
            ArrancarLaAzotea(false);
        }

        // Bug #352 ("los enemigos no logran pasar; el obstaculo es solo para mi y mis aliados"): las barandas y el equipo del techo frenan al jugador y a la
        // escuadra pero NO a los enemigos (Deslizador ya ignora los colliders con BarreraSoloEnemigos cuando quien se mueve es un enemigo).
        public int ObstaculosDelTechoSoloParaAliados { get; private set; }
        void HacerPasablesParaEnemigosLosObstaculosDelTecho()
        {
            int n = 0;
            foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c == null || c.isTrigger || !c.enabled) continue;
                string nom = c.name;
                if (!(nom.Contains("Baranda") || nom.Contains("Equipo") || nom.Contains("Parapeto") || nom.Contains("Claraboya"))) continue;
                var b = c.bounds;
                if (b.center.y < 18f || b.center.y > 30f || !G.Dentro(b.center, 2f)) continue;
                if (c.GetComponent<SP.Core.BarreraSoloEnemigos>() == null) { c.gameObject.AddComponent<SP.Core.BarreraSoloEnemigos>(); n++; }
            }
            ObstaculosDelTechoSoloParaAliados += n;
        }

        void ArrancarLaAzotea(bool conCinematica)
        {
            azoteaIniciada = true;
            EtapaMansion = EtapaDeLaMansion.Azotea;
            HacerPasablesParaEnemigosLosObstaculosDelTecho();
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            ReiniciarAzotea();
            ondasDeAzotea = 0; tAzotea = 0f;
            HelipadEnLaAzotea();
            ArrancarHeli();   // el helicoptero de rescate sale de lejos; TickAzotea lo hace volar hasta quedar sobre el techo
            SesionLog.Evento("MANSION: azotea");
            if (conCinematica) StartCoroutine(CinematicaDelHelicoptero());
            else IniciarHalconDeLaAzotea();
        }
    }
}
