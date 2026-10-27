using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Presentation;
using SP.Player;
using SP.UI;

namespace SP.Operacion
{
    // Bug #295 ("rediseno de la defensa de la plaza"): UN solo puesto, la plaza. Un civil al centro al que hay que proteger, los aliados quedan
    // anclados ahi (se pueden desplegar a mano), los enemigos entran por los bordes del mapa y vienen a la vista, y los francotiradores
    // aparecen de a uno en las azoteas. Este archivo tiene todo lo nuevo; el resto del mando (OperacionDirector.Mando.cs) sigue igual con 1 sector.
    public partial class OperacionDirector
    {
        // ---- la plaza ----
        public const float RadioDeLaPlaza = 26f;
        public const float RadioDeAncla = 22f, SegundosAntesDeVolver = 20f;

        // ---- el civil ----
        public const int VidaDelCivilDeLaPlaza = 600;
        public const float RadioDeAtaqueAlCivil = 10f, DanoAlCivilPorEnemigoPorSegundo = 14f;
        public const float SegundosDeCalmaParaCurar = 6f, CuracionDelCivilPorSegundo = 6f;
        public Soldier CivilDeLaPlaza { get; private set; }
        public bool CivilVivo => CivilDeLaPlaza != null && CivilDeLaPlaza.Health != null && CivilDeLaPlaza.Health.IsAlive;
        public float VidaDelCivilPorcentaje => CivilDeLaPlaza != null && CivilDeLaPlaza.Health != null ? 100f * CivilDeLaPlaza.Health.Current / Mathf.Max(1, CivilDeLaPlaza.Health.MaxHealth) : 0f;
        float danoAcumuladoAlCivil, curacionAcumulada, calmaDelCivil, proximoTickDelCivil;

        // ---- entradas desde fuera del mapa ----
        // Puntos justo afuera de LimiteDeLaZonaJugable (258..386 x 158..304) sobre las calles que salen del mapa. Si el punto no esta sobre la malla
        // se usa el punto de malla mas cercano (el borde). 0 NORTE, 1 ESTE, 2 OESTE, 3 SUR.
        public static readonly Vector3[] EntradasDelMapa =
        {
            new Vector3(322f, 0f, 312f), new Vector3(394f, 0f, 228f), new Vector3(250f, 0f, 228f), new Vector3(322f, 0f, 150f),
        };
        public static readonly string[] NombresDeEntrada = { "NORTE", "ESTE", "OESTE", "SUR" };

        public static Vector3 EntradaDelMapa(int i)
        {
            var p = EntradasDelMapa[Mathf.Clamp(i, 0, EntradasDelMapa.Length - 1)];
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 30f, UnityEngine.AI.NavMesh.AllAreas)) return new Vector3(h.position.x, 0f, h.position.z);
            return p;
        }

        // ---- azoteas: donde se para cada tirador (x, altura del techo, z) ----
        // Casa_15, Casa_2, Casa_10, Casa_5, Casa_6 y Casa_1 (techos planos de 5-6 m a 33-50 m de la plaza), el tirador en el borde del lado de la plaza.
        public static readonly Vector3[] Azoteas =
        {
            new Vector3(344.5f, 5f, 210f), new Vector3(300.5f, 6f, 252.5f), new Vector3(302f, 6f, 195f),
            new Vector3(341f, 5f, 251.5f), new Vector3(358f, 6f, 253f), new Vector3(283.5f, 5f, 252.5f),
        };
        // Cuando sale cada uno, como fraccion del reloj efectivo, y cuantos pueden estar vivos a la vez.
        public static readonly float[] FraccionDeFrancotirador = { 0.12f, 0.28f, 0.44f, 0.58f, 0.72f, 0.85f };
        public const int FrancotiradoresVivosMaximos = 3;
        readonly List<FrancotiradorDeAzotea> francotiradores = new List<FrancotiradorDeAzotea>();
        public int AzoteasActivadas { get; private set; }
        public IReadOnlyList<FrancotiradorDeAzotea> FrancotiradoresDeAzotea => francotiradores;
        public int FrancotiradoresVivos { get { int n = 0; foreach (var f in francotiradores) if (f != null && f.Activo) n++; return n; } }

        public Vector3 CentroDeLaPlaza => plaza != null ? new Vector3(plaza.position.x, 0f, plaza.position.z) : Vector3.zero;

        // Al empezar el mando: el civil al centro y todo en cero.
        void IniciarLaPlaza()
        {
            QuitarLaPlaza();
            AzoteasActivadas = 0;
            danoAcumuladoAlCivil = curacionAcumulada = calmaDelCivil = 0f;
            CrearCivilDeLaPlaza();
        }

        void CrearCivilDeLaPlaza()
        {
            if (prefabsDeMiliciano == null || prefabsDeMiliciano.Length == 0 || prefabsDeMiliciano[0] == null || plaza == null) return;
            var p = CentroDeLaPlaza;
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 6f, UnityEngine.AI.NavMesh.AllAreas)) p = h.position;
            var go = Instantiate(prefabsDeMiliciano[0], new Vector3(p.x, 0.8f, p.z), Quaternion.Euler(0f, 180f, 0f));
            go.name = "Civil_Plaza";
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return; }
            s.Configure("Civil", TeamId.Player, RoleType.Civilian, VidaDelCivilDeLaPlaza);
            if (s.Brain != null) { s.Brain.IsPossessedByPlayer = false; s.Brain.Pasivo = true; s.Brain.Quieto = true; s.Brain.CancelOrder(); }
            ApoyoEnElPiso.Apoyar(s.transform);
            go.AddComponent<SP.Player.Rehen>();   // tinte propio y marcador flotante: se distingue de un soldado
            CivilDeLaPlaza = s;
            ActorRegistry.Invalidate();
            SesionLog.Evento("PLAZA: civil al centro (" + VidaDelCivilDeLaPlaza + " HP)");
        }

        // Los enemigos pegados al civil lo van matando (la IA no dispara a civiles, asi que el dano es por presencia: "la plaza se toma").
        void TickCivilDeLaPlaza(float dt)
        {
            if (CivilDeLaPlaza == null) return;
            proximoTickDelCivil -= dt;
            if (proximoTickDelCivil > 0f) return;
            float paso = 0.25f; proximoTickDelCivil = paso;
            var h = CivilDeLaPlaza.Health;
            if (h == null || !h.IsAlive) { if (h != null && !h.IsAlive) PerderPorElCivil(); return; }
            int cerca = 0;
            var todos = ActorRegistry.All; var c = CivilDeLaPlaza.transform.position;
            for (int i = 0; i < todos.Count; i++)
            {
                var e = todos[i];
                if (e == null || e.Team != TeamId.Enemy || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy) continue;
                if (Plano(e.transform.position, c).sqrMagnitude <= RadioDeAtaqueAlCivil * RadioDeAtaqueAlCivil) cerca++;
            }
            if (cerca > 0)
            {
                calmaDelCivil = 0f;
                danoAcumuladoAlCivil += cerca * DanoAlCivilPorEnemigoPorSegundo * paso;
                int d = Mathf.FloorToInt(danoAcumuladoAlCivil);
                if (d > 0)
                {
                    danoAcumuladoAlCivil -= d;
                    h.TakeDamage(d, 0);
                    if (hud != null && Time.time >= proximoAvisoCivil) { proximoAvisoCivil = Time.time + 4f; hud.Aviso("¡EL CIVIL ESTA BAJO ATAQUE!\nSACA A LOS ENEMIGOS DE LA PLAZA", 2.2f); }
                }
            }
            else
            {
                calmaDelCivil += paso;
                if (calmaDelCivil >= SegundosDeCalmaParaCurar && h.Current < h.MaxHealth)
                {
                    curacionAcumulada += CuracionDelCivilPorSegundo * paso;
                    int c2 = Mathf.FloorToInt(curacionAcumulada);
                    if (c2 > 0) { curacionAcumulada -= c2; h.Heal(c2); }
                }
            }
            if (!h.IsAlive) PerderPorElCivil();
        }
        float proximoAvisoCivil;

        void PerderPorElCivil()
        {
            if (Fase != FaseOperacion.Resistir) return;
            SesionLog.Evento("PLAZA: el civil murio");
            Perder("EL CIVIL DE LA PLAZA MURIO");
        }

        // ---- los aliados quedan en la plaza ----
        // Quien termino lo que hacia, esta fuera del anillo (RadioDeAncla) mas de SegundosAntesDeVolver y NO fue desplegado por el jugador vuelve
        // solo. Desplegar = una orden de movimiento a un punto fuera de la plaza (ordenSector == -1): ahi se queda hasta nueva orden. El campanario
        // (arriba, a 40 m) tampoco se abandona.
        readonly Dictionary<int, float> ociososFueraDeLaPlaza = new Dictionary<int, float>();
        float proximaAncla;
        public int AliadosQueVolvieron { get; private set; }

        void TickAnclaDeLaPlaza(float dt)
        {
            proximaAncla -= dt;
            if (proximaAncla > 0f || plaza == null) return;
            const float paso = 0.5f; proximaAncla = paso;
            var centro = CentroDeLaPlaza;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                var b = a.Brain;
                if (b == null || b.IsPossessedByPlayer || b.MontadoEnVehiculo || b.Pasivo || MandoTactico.EsOperadorDeRadio(a)) { ociososFueraDeLaPlaza.Remove(a.Id); continue; }
                bool fuera = Plano(a.transform.position, centro).magnitude > RadioDeAncla;
                bool arriba = a.transform.position.y > AlturaDelCampanario - 1.2f;
                bool desplegado = ordenSector.TryGetValue(a.Id, out int sec) && sec < 0;
                bool libre = !b.TieneOrden && !b.Quieto && b.CurrentTarget == null && b.State != AiState.Chase && b.State != AiState.Attack;
                if (!fuera || arriba || desplegado || !libre) { ociososFueraDeLaPlaza.Remove(a.Id); continue; }
                ociososFueraDeLaPlaza.TryGetValue(a.Id, out float t);
                t += paso;
                if (t < SegundosAntesDeVolver) { ociososFueraDeLaPlaza[a.Id] = t; continue; }
                ociososFueraDeLaPlaza.Remove(a.Id);
                float ang = (a.Id * 2.399963f) % (Mathf.PI * 2f);
                var p = centro + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (6f + (a.Id % 3) * 3f);
                if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) p = hit.position;
                b.IssueMoveOrder(p);   // sin ordenSector: no es una orden del jugador (no debe cumplir los pasos guiados del mando)
                AliadosQueVolvieron++;
            }
        }

        // ---- francotiradores graduales ----
        void TickFrancotiradoresDeAzotea()
        {
            if (AzoteasActivadas >= Azoteas.Length || plantillaDeRefuerzo == null) return;
            float t = FraccionDeFrancotirador[AzoteasActivadas] * segundosDeResistencia;
            if (RelojEfectivo < t || FrancotiradoresVivos >= FrancotiradoresVivosMaximos) return;
            ActivarFrancotirador(AzoteasActivadas, true);
            AzoteasActivadas++;
        }

        FrancotiradorDeAzotea ActivarFrancotirador(int i, bool avisar)
        {
            if (i < 0 || i >= Azoteas.Length || plantillaDeRefuerzo == null || plaza == null) return null;
            var pies = Azoteas[i];
            var haciaLaPlaza = Plano(CentroDeLaPlaza, pies);
            float yaw = Mathf.Atan2(haciaLaPlaza.x, haciaLaPlaza.z) * Mathf.Rad2Deg;
            // El soldado nace en el suelo, al pie de la casa (en la malla, para que su agente nazca bien) y PuestoElevado.Montar lo sube en Start.
            var suelo = new Vector3(pies.x, 0f, pies.z);
            if (UnityEngine.AI.NavMesh.SamplePosition(suelo, out var h, 14f, UnityEngine.AI.NavMesh.AllAreas)) suelo = h.position;
            var go = Instantiate(plantillaDeRefuerzo, suelo, Quaternion.Euler(0f, yaw, 0f), plantillaDeRefuerzo.transform.parent);
            go.name = "Francotirador_Azotea_" + (i + 1);
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return null; }
            s.Configure("Francotirador " + (i + 1), TeamId.Enemy, RoleType.Sniper, 120);
            var raiz = new GameObject("Azotea_" + (i + 1));
            raiz.transform.position = pies;
            var tirador = raiz.AddComponent<FrancotiradorDeAzotea>();
            tirador.soldado = s; tirador.pies = pies; tirador.yawBase = yaw;
            FrancotiradorDeAzotea.ArmarParapeto(raiz.transform, pies, yaw);
            francotiradores.Add(tirador);
            ActorRegistry.Invalidate();
            if (avisar)
            {
                AlertQueue.Push($"¡FRANCOTIRADOR EN UNA AZOTEA! · {FrancotiradoresVivos + 1}", AlertPriority.Alta, 3f);
                SesionLog.Evento("PLAZA: francotirador " + (i + 1) + " en la azotea");
            }
            return tirador;
        }

        // Quita al civil, a los francotiradores y lo que cuelga del mando de la plaza (al terminar la fase, perder o reiniciar).
        void QuitarLaPlaza()
        {
            foreach (var f in francotiradores)
            {
                if (f == null) continue;
                if (f.soldado != null) Destroy(f.soldado.gameObject);
                Destroy(f.gameObject);
            }
            francotiradores.Clear();
            if (CivilDeLaPlaza != null) Destroy(CivilDeLaPlaza.gameObject);
            CivilDeLaPlaza = null;
            ociososFueraDeLaPlaza.Clear();
            ActorRegistry.Invalidate();
        }
    }
}
