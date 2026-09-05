using UnityEngine;

namespace SP.Player
{
    // Decide si un click derecho fue una ORDEN o un paneo de camara.
    //
    // Vive afuera de PlayerInputDriver y sin nada de Unity adentro (solo
    // Vector2) para poder probarlo: el reporte era "de cada quince click
    // derechos en RTS, uno se pierde y la orden no se emite", y un bug de
    // ese tipo no se caza mirando el codigo -- hay que poder correrle
    // quince clicks sinteticos con temblor de mano y contar.
    //
    // Los DOS defectos que tenia la version de adentro del driver:
    //
    //  1. El punto de inicio del arrastre solo se guardaba si la
    //     pulsacion NO caia sobre la UI. Si caia sobre un boton, quedaba
    //     el inicio del click ANTERIOR -- normalmente en la otra punta de
    //     la pantalla -- y la comparacion contra ese punto viejo daba
    //     "arrastro cientos de pixeles" al instante. La orden se comia
    //     como si fuera un paneo.
    //
    //  2. El umbral eran 6 pixeles, el mismo que usa el recuadro de
    //     seleccion del click IZQUIERDO. Pero ese umbral existe para
    //     decidir "recuadro o click", donde el usuario apunta y suelta
    //     con cuidado. El click derecho en pleno combate se da rapido y
    //     la mano se mueve: seis pixeles es menos de dos milimetros de
    //     mouse. Cualquier temblor convertia la orden en un paneo de
    //     camara de dos pixeles que el jugador ni veia -- y la orden
    //     desaparecia sin ningun aviso.
    public struct ArrastreDerecho
    {
        // Cuanto tiene que moverse el mouse CON EL BOTON APRETADO para que
        // deje de ser un click y pase a ser un paneo. Mas alto que el
        // umbral de la seleccion por recuadro a proposito (ver arriba).
        public const float UmbralPorDefecto = 18f;

        // Y la regla que de verdad elimina la perdida silenciosa: por
        // debajo de este tiempo, la pulsacion es un CLICK aunque el mouse
        // se haya movido. Medido con clicks sinteticos, subir el umbral de
        // pixeles solo corre el problema de lugar -- con temblor de 20 px
        // se seguia perdiendo el 42% -- porque el comportamiento es casi
        // binario: si en algun frame se pasa del umbral, se pierde.
        //
        // Un paneo de camara es un gesto SOSTENIDO. Nadie panea en 150
        // milisegundos. Con las dos condiciones juntas (mover mucho Y
        // mantener), una orden dada rapido no se puede perder por temblor
        // por mas que la mano salte.
        public const float TiempoMinimoDePaneo = 0.18f;

        Vector2 inicio;
        float tiempoDePulsacion;
        bool moviosuficiente;
        bool empezoSobreUi;
        bool valido;

        // Solo cuenta como paneo si se movio lo suficiente Y ya se
        // mantiene apretado el tiempo minimo.
        public bool Arrastrando => valido && moviosuficiente && SegundosApretado >= TiempoMinimoDePaneo;

        float ahora;
        float SegundosApretado => ahora - tiempoDePulsacion;

        public void AlPresionar(Vector2 posicion, bool sobreUi, float tiempo = 0f)
        {
            // SIEMPRE se guarda, tambien cuando la pulsacion cae sobre la
            // UI: si no, queda el inicio del click anterior (defecto 1).
            inicio = posicion;
            moviosuficiente = false;
            empezoSobreUi = sobreUi;
            tiempoDePulsacion = tiempo;
            ahora = tiempo;
            valido = true;
        }

        public void AlMover(Vector2 posicion, float umbral, float tiempo = 0f)
        {
            if (!valido) return;
            ahora = tiempo;
            if (moviosuficiente) return;
            if (Vector2.Distance(posicion, inicio) > umbral) moviosuficiente = true;
        }

        // true si esto fue una orden. Un paneo (movimiento grande Y
        // sostenido), o una pulsacion que empezo sobre la UI, no lo son.
        public bool AlSoltar()
        {
            bool fueOrden = valido && !Arrastrando && !empezoSobreUi;
            moviosuficiente = false;
            empezoSobreUi = false;
            valido = false;
            return fueOrden;
        }
    }
}
