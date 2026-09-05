using UnityEngine;
using SP.Actors;

namespace SP.Core
{
    // Nadie deberia estar flotando ni enterrado, y habia de los dos.
    //
    // En SC_TestLevel -- la escena que arma la suite headless -- los diez
    // soldados estaban a CINCO alturas distintas respecto del piso. Medido
    // con un rayo hacia abajo desde cada uno, comparando la base de su
    // collider contra el suelo que encuentra:
    //
    //     Enemigo_Patrulla_2, _3, _4      0,00  (bien apoyados)
    //     Enemigo_Patrulla_1              +0,80 flotando
    //     Enemigo_1                       +0,80 flotando
    //     Enemigo_2, Enemigo_3            +1,60 flotando
    //     Soldado_1_Vega, _2_Kes, _3_Doc  -0,20 hundidos
    //
    // Dos enemigos flotando 1,60 m son un cuerpo entero por encima del
    // suelo. Y no es solo estetico: la altura del cuerpo decide desde
    // donde salen los rayos de linea de tiro y a que altura pasan las
    // balas, asi que la suite estaba comprobando puntería y coberturas
    // sobre soldados en el aire.
    //
    // En SC_Gameplay los soldados SI estaban bien apoyados (medido: los
    // cuatro a 0,00); lo que estaba mal ahi era el vehiculo, 10 cm
    // HUNDIDO, o sea con las ruedas enterradas.
    //
    // La causa se ve en la misma medicion: los colliders no coinciden
    // entre grupos. En Patrulla_2/3/4 la base del collider cae 0,80 por
    // debajo de la raiz y en Patrulla_1 cae EN la raiz, o sea que se
    // armaron en momentos distintos con centros distintos y las
    // posiciones se ajustaron a mano contra el collider de entonces.
    //
    // Por eso esto no corrige posiciones una por una: apoya a cada cuerpo
    // usando SU PROPIO collider, que es la unica medida que no depende de
    // como se armo. Corre una sola vez, al empezar la partida.
    public static class ApoyoEnElPiso
    {
        // Desde cuanto mas arriba se tira el rayo. Tiene que superar al que
        // flota mas alto (1,60) con margen.
        public const float AlturaDeSondeo = 4f;

        // Hasta donde se busca piso hacia abajo.
        public const float AlcanceDeSondeo = 20f;

        // Por debajo de esto no vale la pena mover a nadie: es ruido de
        // punto flotante, no un cuerpo mal puesto.
        public const float ToleranciaEnMetros = 0.02f;

        static readonly RaycastHit[] Buffer = new RaycastHit[16];

        public static int ApoyarATodos()
        {
            int corregidos = 0;
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include))
                if (s != null && Apoyar(s.transform)) corregidos++;

            // El vehiculo entra por la misma puerta a proposito: dos reglas
            // distintas de "estar en el piso" es como se llega a esto.
            foreach (var v in Object.FindObjectsByType<SP.Vehicles.Vehicle>(FindObjectsInactive.Include))
                if (v != null && Apoyar(v.transform)) corregidos++;

            return corregidos;
        }

        // Devuelve true si de verdad lo movio. Publico para poder medirlo.
        public static bool Apoyar(Transform cuerpo)
        {
            if (cuerpo == null) return false;
            var col = cuerpo.GetComponent<Collider>();
            if (col == null) return false;

            var pos = cuerpo.position;
            var desde = new Vector3(pos.x, col.bounds.max.y + AlturaDeSondeo, pos.z);

            // NonAlloc y descartando el propio cuerpo: un rayo simple se
            // choca consigo mismo en el primer metro y el "piso" le sale
            // siendo su propia cabeza.
            int n = Physics.RaycastNonAlloc(desde, Vector3.down, Buffer, AlcanceDeSondeo + AlturaDeSondeo, ~0, QueryTriggerInteraction.Ignore);
            float piso = float.NegativeInfinity;
            bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var c = Buffer[i].collider;
                if (c == null || c == col || c.transform.IsChildOf(cuerpo)) continue;
                // El piso es el punto MAS ALTO por debajo suyo: si hay una
                // caja abajo, se apoya sobre la caja y no la atraviesa
                // hasta el terreno. El margen de 0,3 tolera al que ya
                // estaba levemente hundido.
                float y = Buffer[i].point.y;
                if (y > col.bounds.min.y + 0.3f) continue;
                if (y > piso) { piso = y; hay = true; }
            }
            if (!hay) return false;

            float desfase = col.bounds.min.y - piso;
            if (Mathf.Abs(desfase) < ToleranciaEnMetros) return false;

            cuerpo.position = new Vector3(pos.x, pos.y - desfase, pos.z);
            return true;
        }
    }
}
