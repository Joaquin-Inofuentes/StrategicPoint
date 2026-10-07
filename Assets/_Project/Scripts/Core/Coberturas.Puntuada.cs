using System.Collections.Generic;
using UnityEngine;
using SP.Actors;

namespace SP.Core
{
    // Eleccion PONDERADA de cobertura (pedido: "que la IA use las coberturas al atacar con decisiones ponderadas segun su vida,
    // distancia a coberturas, direccion de enemigo y direccion de cobertura", 5 iteraciones medidas).
    //
    // La logica vieja (version 0) era "la cobertura mas cercana con linea de tiro". Ahora cada punto candidato recibe un PUNTAJE:
    //
    //   v1  disparo posible (LOS) + ajuste a la distancia de tiro ideal - distancia a recorrer
    //   v2  + pesos segun la VIDA: sano pelea (premia el disparo), herido se protege (premia estar oculto y cerca)
    //   v3  + DIRECCION del enemigo contra la de la cobertura (que el obstaculo quede entre los dos) y asomarse por el borde
    //   v4  + multitud (no apilarse en la misma) + sentido del recorrido (avanzar sano, retroceder herido)
    //   v5  + varias amenazas (cuantos enemigos mas ven el punto), recarga (oculto mientras recarga) e histeresis (no cambiar por poco)
    //
    // Todo sale de datos del mundo real (colliders, linea de tiro del NavService), no de numeros magicos por mapa.
    public static class CoberturasPuntuadas
    {
        public const float AlturaDePie = 1.3f;       // altura de la boca del arma parado
        public const float AlturaAgachado = 0.85f;   // idem agachado (WP4: la boca agachada mide 0,85 sobre el piso, no 0,65)
        public const float AsomadaLateral = 0.6f;    // cuanto se corre al costado en una cobertura alta

        public struct Eleccion
        {
            public Vector3 punto;
            public Collider dueno;
            public float puntaje;
            public bool oculto;       // agachado el obstaculo lo tapa
            public bool puedeDisparar;
        }

        public static int Evaluaciones { get; private set; }

        // Caracteristicas del puntaje, una por bit. Cada VERSION es un conjunto de ellas (tabla de abajo): asi se mide una por una
        // contra la eleccion vieja (CoberturaBench) y se arman las versiones finales con las que sirven.
        public const int F_VIDA = 1;        // sano pelea (premia disparo), herido se protege (premia oculto y cerca)
        public const int F_GEOM = 2;        // el obstaculo entre el soldado y el enemigo + asomada lateral
        public const int F_MULTITUD = 4;    // no apilarse en la misma cobertura
        public const int F_SENTIDO = 8;     // sano avanza, herido retrocede
        public const int F_AMENAZAS = 16;   // cuantos OTROS enemigos ven el punto
        public const int F_RECARGA = 32;    // recargando: oculto
        public const int F_HISTERESIS = 64; // no cambiar por poco
        public const int F_MUDARSE = 128;   // ya cubierto: reevaluar y mudarse si cambio la situacion
        public const int F_DECIDIR = 256;   // DECIDE si cubrirse (vida, recarga, cuantos lo ven, cuan cerca esta el blanco); sano y sin presion pelea de pie
        public const int F_TIPO = 1024;     // prefiere la cobertura ALTA: asoma de costado y sigue AGACHADO (la punteria agachado es x0.4 de dispersion; sobre una baja hay que pararse)
        public const int F_CICLO = 512;     // el ciclo oculto/asoma depende de la vida: sano asoma casi siempre, herido se esconde

        // Decision de cubrirse: suma ponderada de presion. > UmbralDeCubrirse => buscar cobertura; si no, pelea de pie.
        // Bug #061: era 1,3 y con la vida llena la presion maxima es 1,3 (4 amenazas 1,0 + blanco cerca 0,3): un soldado sano no
        // se cubria NUNCA, aunque lo estuvieran mirando cuatro enemigos. Con 0,95 sano se cubre si lo ven 3+ y el blanco esta
        // cerca (o 4+), y herido (desde ~10% de vida perdida) con dos amenazas.
        public static float UmbralDeCubrirse = 0.6f;   // WP4 (#074/#081): era 0,95 (con 0,6 se cubre un sano al que ven 2 enemigos de cerca, o un herido con una amenaza)
        public static float PesoTipoAlta = 1.2f;
        public static float PesoVida = 2.0f, PesoRecarga = 0.8f, PesoAmenaza = 0.25f, PesoCerca = 0.3f;

        public static float Presion(float vida01, bool recargando, int amenazas, float distanciaAlBlanco, float rango)
        {
            float herido = 1f - Mathf.Clamp01(vida01);
            float cerca = distanciaAlBlanco < rango * 0.5f ? 1f : 0f;
            return PesoVida * herido + (recargando ? PesoRecarga : 0f) + PesoAmenaza * Mathf.Min(4, amenazas) + PesoCerca * cerca;
        }

        // Cuantos enemigos vivos lo ven hoy desde el pecho (los que lo pueden herir).
        public static int AmenazasSobre(Soldier quien, float radio = 26f)
        {
            if (quien == null) return 0;
            int n = 0; var todos = ActorRegistry.All; var origen = quien.transform.position + Vector3.up * 1.2f;
            for (int j = 0; j < todos.Count && n < 4; j++)
            {
                var s = todos[j];
                if (s == null || s.Team == quien.Team || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (s.Role == SP.Combat.RoleType.Civilian) continue;
                if (Plano(s.transform.position - quien.transform.position).sqrMagnitude > radio * radio) continue;
                if (NavService.HayLineaDeTiro(origen, s.transform.position + Vector3.up, quien.transform, s.transform)) n++;
            }
            return n;
        }

        // Version -> caracteristicas. 0 = eleccion vieja (no pasa por aca). 1 = base (disparo posible + ajuste de distancia - recorrido).
        public static int Caracteristicas(int version)
        {
            switch (version)
            {
                case 1: return 0;
                case 2: return F_VIDA;
                case 3: return F_VIDA | F_GEOM;
                case 4: return F_VIDA | F_GEOM | F_MULTITUD | F_SENTIDO;
                case 5: return F_DECIDIR | F_CICLO;   // la ganadora medida (ver Capturas_Operacion/Cobertura): decide si cubrirse + ciclo segun vida
                case 13: return F_VIDA | F_GEOM | F_MULTITUD | F_SENTIDO | F_AMENAZAS | F_RECARGA | F_HISTERESIS | F_MUDARSE;   // la v5 original (descartada: -429 contra no cubrirse)
                // 6..13: una caracteristica sola sobre la base (ablacion)
                case 6: return F_GEOM;
                case 7: return F_VIDA;
                case 8: return F_SENTIDO;
                case 9: return F_AMENAZAS;
                case 10: return F_MULTITUD;
                case 11: return F_RECARGA;
                case 12: return F_HISTERESIS | F_MUDARSE;
                default: return version >= 100 ? version - 100 : 0;   // 100+mascara: combinaciones libres para el banco
            }
        }

        // WP4: version por defecto = la v5 (DECIDIR + CICLO) + multitud (no apilarse en la misma cobertura) y sentido (el sano avanza, el herido
        // retrocede): 100 + mascara.
        public const int VersionNueva = 100 + (F_DECIDIR | F_CICLO | F_MULTITUD | F_SENTIDO);

        public static bool Usa(int version, int caracteristica) => version >= 1 && (Caracteristicas(version) & caracteristica) != 0;

        static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

        // maxDistAlBlanco / minDistAlBlanco (WP4): filtran los candidatos por su distancia al blanco. "No alejarse" = max (distancia
        // actual + margen); "no acercarse" (repliegue de un herido) = min.
        public static bool TryElegir(Vector3 desde, Soldier objetivo, Soldier quien, float radio, float rango, float vida01,
                                     int version, bool recargando, Vector3? actual, out Eleccion eleccion,
                                     float maxDistAlBlanco = float.MaxValue, float minDistAlBlanco = 0f)
        {
            eleccion = default;
            var puntos = Coberturas.Puntos; var duenos = Coberturas.Duenos;
            if (objetivo == null || puntos.Count == 0 || version < 1) return false;
            Evaluaciones++;

            float herido = 1f - Mathf.Clamp01(vida01);
            var posObj = objetivo.transform.position;
            var haciaObj = Plano(posObj - desde); float dObj0 = Mathf.Max(0.01f, haciaObj.magnitude); haciaObj /= dObj0;

            // Pesos segun la version.
            float wFuego = 1f, wRango = 0.8f, wDist = 1.2f, wOculto = 0f, wGeom = 0f, wMulti = 0f, wSentido = 0f, wMultitud = 0f, wAmenazas = 0f;
            if (Usa(version, F_VIDA))
            {
                wFuego = Mathf.Lerp(1.3f, 0.5f, herido);
                wDist = Mathf.Lerp(1.0f, 1.9f, herido);
                wOculto = Mathf.Lerp(0.3f, 1.6f, herido);
            }
            if (Usa(version, F_GEOM)) wGeom = Mathf.Lerp(0.5f, 1.1f, herido);
            if (Usa(version, F_MULTITUD)) wMultitud = 1.4f;
            if (Usa(version, F_SENTIDO)) wSentido = 0.5f;
            if (Usa(version, F_AMENAZAS)) wAmenazas = Mathf.Lerp(0.4f, 1.0f, herido);
            if (Usa(version, F_RECARGA) && recargando) { wFuego *= 0.3f; wOculto = Mathf.Max(wOculto, 0.3f) * 1.8f; }
            if (Usa(version, F_HISTERESIS)) wMulti = 0.6f;

            var candidatos = new List<(int i, float pre)>(24);
            for (int i = 0; i < puntos.Count; i++)
            {
                var p = puntos[i];
                float d = Plano(p - desde).magnitude;
                if (d > radio) continue;
                float dEn = Plano(p - posObj).magnitude;
                if (dEn > rango * 1.05f || dEn < rango * 0.2f) continue;   // fuera de alcance / pegado al blanco
                if (dEn > maxDistAlBlanco || dEn < minDistAlBlanco) continue;
                if (quien != null && quien.Team == SP.Combat.TeamId.Enemy && Vegetacion.Dentro(p)) continue;
                float nd = d / Mathf.Max(1f, radio);
                float rf = Mathf.Clamp01(1f - Mathf.Abs(dEn - 0.7f * rango) / (0.7f * rango));
                candidatos.Add((i, wRango * rf - wDist * nd));
            }
            if (candidatos.Count == 0) return false;
            // Solo se evalua en serio (rayos) a los mejores por puntaje barato.
            candidatos.Sort((a, b) => b.pre.CompareTo(a.pre));
            int tope = Mathf.Min(candidatos.Count, 8);

            float mejor = float.MinValue; bool hay = false;
            for (int k = 0; k < tope; k++)
            {
                int i = candidatos[k].i;
                var p = puntos[i];
                var dueno = duenos[i];
                float d = Plano(p - desde).magnitude;
                float nd = d / Mathf.Max(1f, radio);
                float dEn = Plano(p - posObj).magnitude;
                float rf = Mathf.Clamp01(1f - Mathf.Abs(dEn - 0.7f * rango) / (0.7f * rango));

                bool losPie = Coberturas.HayLineaDeTiroAlPecho(p + Vector3.up * AlturaDePie, objetivo, quien);
                bool losAgachado = (Usa(version, F_VIDA) || Usa(version, F_RECARGA)) ? Coberturas.HayLineaDeTiroAlPecho(p + Vector3.up * AlturaAgachado, objetivo, quien) : losPie;
                bool alta = dueno != null && dueno.bounds.size.y >= 1.4f;
                var frente = Coberturas.FrenteDe(p, dueno);
                bool losAsomada = false;
                if (alta && !losPie)
                {
                    var derecha = Vector3.Cross(Vector3.up, frente).normalized;
                    if (Vector3.Dot(posObj - p, derecha) < 0f) derecha = -derecha;
                    losAsomada = Coberturas.HayLineaDeTiroAlPecho(p + derecha * AsomadaLateral + Vector3.up * AlturaDePie, objetivo, quien);
                }
                bool puedeDisparar = losPie || losAsomada;
                if (!puedeDisparar) continue;   // esconderse donde no se puede tirar es peor que quedarse al descubierto

                float fuego = losPie ? 1f : 0.85f;
                float oculto = !losAgachado ? 1f : 0f;
                if (alta && !losPie) oculto = 1f;

                float score = wFuego * fuego + wRango * rf - wDist * nd + wOculto * oculto;
                if (Usa(version, F_TIPO)) score += PesoTipoAlta * (alta ? 1f : 0f);

                if (Usa(version, F_GEOM))
                {
                    // El obstaculo tiene que quedar ENTRE el soldado y el enemigo: el frente de la cobertura (de donde se para)
                    // apunta al lado contrario al enemigo.
                    var haciaEn = Plano(posObj - p).normalized;
                    float alineacion = Mathf.Clamp(-Vector3.Dot(frente, haciaEn), -1f, 1f);
                    score += wGeom * alineacion;
                }
                if (wMultitud > 0f || wSentido > 0f)
                {
                    int vecinos = 0;
                    bool ocupada = false;
                    var todos = ActorRegistry.All;
                    for (int j = 0; j < todos.Count; j++)
                    {
                        var s = todos[j];
                        if (s == null || s == quien || quien == null || s.Team != quien.Team || s.Health == null || !s.Health.IsAlive) continue;
                        var b = s.Brain;
                        // WP4 (#086): un punto YA ocupado por un companero en cobertura no se elige: dos soldados queriendo el mismo punto se
                        // quedaban apilados (uno ocupandolo, el otro tironeando contra la separacion).
                        if (b != null && b.EnCobertura && Plano(b.CoberturaPunto - p).sqrMagnitude < 1.0f * 1.0f) { ocupada = true; break; }
                        if (b != null && b.VaHaciaUnaCobertura && Plano(b.CoberturaElegida - p).sqrMagnitude < 1.0f * 1.0f) { ocupada = true; break; }
                        if (Plano(s.transform.position - p).sqrMagnitude < 1.8f * 1.8f) { vecinos++; continue; }
                        if (b != null && b.YendoACobertura && Plano(b.CoberturaPunto - p).sqrMagnitude < 1.2f * 1.2f) vecinos++;
                    }
                    if (ocupada) continue;
                    score -= wMultitud * Mathf.Min(2, vecinos);
                    // Sentido: sano prefiere avanzar (cobertura hacia el enemigo), herido retroceder.
                    var haciaPunto = Plano(p - desde);
                    float sentido = haciaPunto.sqrMagnitude > 0.01f ? Vector3.Dot(haciaPunto.normalized, haciaObj) : 0f;
                    score += wSentido * (sentido * (1f - herido) - sentido * herido);
                }
                if (wAmenazas > 0f || wMulti > 0f)
                {
                    // Cuantos OTROS enemigos ven este punto agachado: una cobertura expuesta a dos flancos no cubre.
                    int ven = 0;
                    var todos = ActorRegistry.All;
                    var origen = p + Vector3.up * AlturaAgachado;
                    for (int j = 0; j < todos.Count && ven < 3 && wAmenazas > 0f; j++)
                    {
                        var s = todos[j];
                        if (s == null || s == objetivo || quien == null || s.Team == quien.Team || s.Health == null || !s.Health.IsAlive) continue;
                        if (Plano(s.transform.position - p).sqrMagnitude > 30f * 30f) continue;
                        if (NavService.HayLineaDeTiro(origen, s.transform.position + Vector3.up, quien != null ? quien.transform : null, s.transform)) ven++;
                    }
                    score -= wAmenazas * ven;
                    // Histeresis: la cobertura que ya ocupa o a la que va vale un poco mas (no cambiar por poco).
                    if (actual.HasValue && Plano(actual.Value - p).sqrMagnitude < 1f) score += wMulti;
                }

                if (!hay || score > mejor)
                {
                    mejor = score; hay = true;
                    eleccion = new Eleccion { punto = p, dueno = dueno, puntaje = score, oculto = oculto > 0f, puedeDisparar = true };
                }
            }
            return hay;
        }
    }
}
