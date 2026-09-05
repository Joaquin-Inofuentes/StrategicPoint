using System.Collections.Generic;
using UnityEngine;
using SP.Actors;

namespace SP.Core
{
    // Los puntos de cobertura del mapa: donde pararse para tener un
    // obstaculo entre uno y el enemigo.
    //
    // Que cuenta como obstaculo NO se define aca de nuevo: sale de
    // NavService.BlocksMovement, la misma definicion de "esto es una
    // pared" que ya usan el deslizador, la ruta y la linea de tiro. Lo
    // unico que se agrega es el filtro de tamaño, para dejar afuera el
    // piso (160 x 160) y las armas tiradas (0,5 x 0,5): un punto de
    // cobertura al lado de un rifle en el suelo no cubre de nada.
    public static class Coberturas
    {
        // A que distancia de la cara del obstaculo se para el soldado.
        // Pegado a la cara el deslizador lo empuja y oscila; mas lejos
        // deja de estar cubierto.
        public const float DistanciaDeLaCara = 1f;
        // El soldado mide 0,90 de ancho, asi que medio cuerpo.
        public const float RadioLibre = 0.45f;

        public const float LadoMinimo = 1f;
        public const float LadoMaximo = 30f;
        public const float AlturaMinima = 0.5f;

        static readonly List<Vector3> puntos = new List<Vector3>();
        public static IReadOnlyList<Vector3> Puntos => puntos;
        public static int Cantidad => puntos.Count;

        public const string NombreDelRoot = "CoberturasRoot";
        static Transform root;

        // Los obstaculos que dan cobertura, hoy, en la escena. Publico
        // para que el test pueda contar lo mismo sin repetir el criterio.
        public static List<Collider> Solidos()
        {
            var lista = new List<Collider>();
            var todos = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            for (int i = 0; i < todos.Length; i++)
            {
                var c = todos[i];
                if (!NavService.BlocksMovement(c)) continue;
                var t = c.bounds.size;
                if (t.y < AlturaMinima) continue;
                float mayor = Mathf.Max(t.x, t.z);
                float menor = Mathf.Min(t.x, t.z);
                if (mayor < LadoMinimo) continue;      // armas en el piso
                if (mayor > LadoMaximo) continue;      // el piso mismo
                if (menor < 0.2f) continue;
                lista.Add(c);
            }
            return lista;
        }

        // Los cuatro costados de cada obstaculo, a DistanciaDeLaCara de su
        // cara. Devuelve cuantos quedaron: un candidato que cae adentro de
        // otro collider se descarta, asi que no siempre son 4 por
        // obstaculo cuando hay dos pegados.
        public static int Registrar()
        {
            // Collider.bounds sale del estado que la fisica tiene guardado,
            // no del transform: un obstaculo recien creado o recien movido
            // devuelve una caja vieja (o de tamaño cero) hasta que se
            // sincroniza, y ahi el filtro de tamaño lo descarta en silencio.
            Physics.SyncTransforms();
            puntos.Clear();
            var solidos = Solidos();
            for (int i = 0; i < solidos.Count; i++)
            {
                var b = solidos[i].bounds;
                float dx = b.extents.x + DistanciaDeLaCara;
                float dz = b.extents.z + DistanciaDeLaCara;
                var centro = new Vector3(b.center.x, b.min.y, b.center.z);

                Candidato(centro + new Vector3(dx, 0f, 0f));
                Candidato(centro + new Vector3(-dx, 0f, 0f));
                Candidato(centro + new Vector3(0f, 0f, dz));
                Candidato(centro + new Vector3(0f, 0f, -dz));
            }
            Redibujar();
            return puntos.Count;
        }

        static void Candidato(Vector3 p)
        {
            // Sin este corte, dos obstaculos pegados generan puntos
            // adentro del vecino: el soldado camina hasta una pared y se
            // queda ahi porque su "cobertura" es solida.
            var alrededor = Physics.OverlapSphere(p + Vector3.up * 0.5f, RadioLibre, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < alrededor.Length; i++)
                if (NavService.BlocksMovement(alrededor[i])) return;
            puntos.Add(p);
        }

        public static void Limpiar()
        {
            puntos.Clear();
            BorrarRoot();
        }

        // --- Elegir una ---

        // La cobertura mas cercana a 'desde' que ademas TENGA linea de
        // tiro al objetivo. Sin esa segunda condicion el soldado se
        // esconde donde no puede disparar, que es peor que quedarse al
        // descubierto: deja de hacer daño y encima no vuelve a salir.
        public static bool TryMejorCobertura(Vector3 desde, Soldier objetivo, Soldier quien,
                                            float radioMaximo, float distanciaDeTiro, out Vector3 elegida)
        {
            elegida = Vector3.zero;
            if (objetivo == null || puntos.Count == 0) return false;

            // Dos pasadas, y el orden importa. Primero se buscan POSICIONES
            // DE TIRO: coberturas desde las que ademas el enemigo queda en
            // alcance. Esas son las unicas que dejan al soldado disparando
            // desde atras del obstaculo. Una cobertura con linea de tiro
            // pero fuera de alcance solo sirve como escala hacia adelante,
            // asi que va segunda.
            if (Buscar(desde, objetivo, quien, radioMaximo, distanciaDeTiro, out elegida)) return true;
            return Buscar(desde, objetivo, quien, radioMaximo, float.MaxValue, out elegida);
        }

        static bool Buscar(Vector3 desde, Soldier objetivo, Soldier quien,
                           float radioMaximo, float distanciaDeTiro, out Vector3 elegida)
        {
            elegida = Vector3.zero;
            float mejor = radioMaximo * radioMaximo;
            bool hay = false;
            var alturaDeTiro = Vector3.up * 1f;

            for (int i = 0; i < puntos.Count; i++)
            {
                float d = (puntos[i] - desde).sqrMagnitude;
                if (d > mejor) continue;
                if (distanciaDeTiro < float.MaxValue
                    && (puntos[i] - objetivo.transform.position).sqrMagnitude > distanciaDeTiro * distanciaDeTiro)
                    continue;
                if (!HayLineaDeTiroDesde(puntos[i] + alturaDeTiro, objetivo, quien)) continue;
                mejor = d;
                elegida = puntos[i];
                hay = true;
            }
            return hay;
        }

        // F2: se puede disparar al objetivo parado en ese punto. 'quien'
        // se ignora en el rayo porque su cuerpo todavia esta en otro lado
        // y no tiene por que tapar la linea que se esta evaluando.
        public static bool HayLineaDeTiroDesde(Vector3 punto, Soldier objetivo, Soldier quien)
        {
            if (objetivo == null) return false;
            return NavService.HayLineaDeTiro(punto, objetivo.transform.position + Vector3.up * 1f,
                quien != null ? quien.transform : null, objetivo.transform);
        }

        // --- Marcarlas en el mapa ---

        static void BorrarRoot()
        {
            if (root == null)
            {
                var buscado = GameObject.Find(NombreDelRoot);
                if (buscado != null) root = buscado.transform;
            }
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root.gameObject);
            else Object.DestroyImmediate(root.gameObject);
            root = null;
        }

        static void Redibujar()
        {
            BorrarRoot();
            if (puntos.Count == 0) return;

            root = new GameObject(NombreDelRoot).transform;
            var material = SP.Presentation.SafeMaterial.Create(new Color(0.25f, 0.75f, 1f, 1f));
            for (int i = 0; i < puntos.Count; i++)
            {
                var marca = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marca.name = "Cobertura_" + i;
                // Sin collider: es una marca en el piso, no algo con lo que
                // chocar. Un disco solido en cada cobertura seria
                // exactamente lo contrario de lo que hace falta.
                var col = marca.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Object.Destroy(col);
                    else Object.DestroyImmediate(col);
                }
                marca.transform.SetParent(root, false);
                marca.transform.position = puntos[i] + Vector3.up * 0.02f;
                marca.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
                var rend = marca.GetComponent<MeshRenderer>();
                if (rend != null) rend.sharedMaterial = material;
            }
        }
    }
}
