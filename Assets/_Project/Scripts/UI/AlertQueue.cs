using UnityEngine;

namespace SP.UI
{
    // Prioridad de un aviso. El orden de los valores ES la regla de
    // desempate, asi que no se reordena ni se le meten valores nuevos en
    // el medio sin revisar SelectNext.
    public enum AlertPriority
    {
        Baja = 0,
        Media = 1,
        Alta = 2,
        Critica = 3,
    }

    // Un aviso esperando turno. Es struct y no clase para que la cola
    // entera viva en un solo array preasignado: cero basura por aviso, que
    // importa porque los avisos se disparan justo en los momentos de mas
    // carga (tiroteos, bajas en cadena).
    public struct PendingAlert
    {
        public string Message;
        public AlertPriority Priority;
        public float Seconds;
        public float EnqueuedAt;
    }

    // Cola con prioridad para los avisos del HUD.
    //
    // POR QUE EXISTE: hoy varias vistas (ModeToastView, InstructionBanner
    // View, PhaseBannerView, KillFeedView, DeadNoticeView...) deciden por
    // su cuenta cuando escribir en pantalla y se pisan entre si: un
    // "GRUPO 3 GUARDADO" tapa un "ESTAS MUERTO" porque llego medio frame
    // despues. Con esta cola quien produce el aviso solo dice QUE y CUANTO
    // importa; quien lo muestra pregunta que toca ahora.
    //
    // NO tiene estado de escena a proposito: es estatica, no es un
    // MonoBehaviour y no referencia ningun GameObject. Asi ninguna vista
    // es duenia de la cola y se puede probar sin abrir una escena.
    //
    // ALCANCE: la cola esta lista, pero NINGUNA vista esta migrada
    // todavia. Se enganchan a mano solo las que de verdad se pisan.
    public static class AlertQueue
    {
        // Tope de la cola. Mas alla de esto los avisos ya no son
        // informacion: son ruido acumulado que el jugador nunca va a
        // alcanzar a leer.
        public const int Capacity = 16;

        // Cuanto puede esperar un aviso antes de dejar de tener sentido.
        // Un "ENEMIGO A LA VISTA" de hace diez segundos describe una
        // situacion que ya termino; mostrarlo tarde confunde mas que
        // callarlo.
        public const float MaxWaitSeconds = 8f;

        // Duracion por defecto si quien empuja no especifica una.
        public const float DefaultSeconds = 1.5f;

        static readonly PendingAlert[] pending = new PendingAlert[Capacity];
        static int count;

        // Estado del aviso EN CURSO. Se guarda aca y no en la vista para
        // que el desplazamiento por prioridad se decida en un solo lugar,
        // sin que la vista tenga que colaborar.
        static AlertPriority currentPriority;
        static float currentUntil = float.NegativeInfinity;

        public static int PendingCount => count;
        public static bool IsBusy => Now() < currentUntil;
        public static AlertPriority CurrentPriority => currentPriority;

        // Los estaticos sobreviven al "Enter Play Mode" sin domain reload,
        // asi que sin este reset la cola arrancaria una partida con los
        // avisos de la anterior todavia adentro.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Clear();

        public static void Clear()
        {
            for (int i = 0; i < pending.Length; i++) pending[i] = default;
            count = 0;
            currentPriority = AlertPriority.Baja;
            currentUntil = float.NegativeInfinity;
        }

        public static void Push(string message, AlertPriority priority, float seconds)
            => PushAt(message, priority, seconds, Now());

        // Version con reloj explicito. Existe SOLO para poder ejercitar la
        // cola sin escena y sin depender de Time.unscaledTime, que en modo
        // edicion avanza con el editor y no con la partida.
        public static void PushAt(string message, AlertPriority priority, float seconds, float now)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (seconds <= 0f) seconds = DefaultSeconds;

            PurgeStale(now);

            // Mismo texto y misma prioridad ya en espera: se refresca en
            // vez de duplicar. Sin esto, un evento que se repite (un
            // contacto enemigo por soldado, con cincuenta soldados) llena
            // la cola el solo y deja afuera avisos distintos que si
            // importan.
            for (int i = 0; i < count; i++)
            {
                if (pending[i].Priority != priority) continue;
                if (!string.Equals(pending[i].Message, message)) continue;
                pending[i].Seconds = seconds;
                pending[i].EnqueuedAt = now;
                return;
            }

            if (count >= Capacity && !TryEvictFor(priority)) return;

            pending[count] = new PendingAlert
            {
                Message = message,
                Priority = priority,
                Seconds = seconds,
                EnqueuedAt = now,
            };
            count++;
        }

        // Devuelve el proximo aviso a mostrar, o false si no hay nada que
        // mostrar AHORA. Devuelve false tambien mientras el aviso en curso
        // sigue en pantalla y nada pendiente lo supera en prioridad: la
        // vista puede llamar a esto todos los frames sin pensar.
        public static bool TryDequeue(out string message, out float seconds)
        {
            message = null;
            seconds = 0f;

            float now = Now();
            PurgeStale(now);

            int index = SelectNext(pending, now);
            if (index < 0) return false;

            // Desplazamiento: solo una prioridad ESTRICTAMENTE mayor corta
            // un aviso en curso. Entre iguales la nueva espera su turno; si
            // no, dos avisos del mismo rango se pisarian mutuamente y
            // ninguno de los dos llegaria a leerse.
            if (now < currentUntil && pending[index].Priority <= currentPriority) return false;

            PendingAlert chosen = pending[index];
            RemoveAt(index);

            message = chosen.Message;
            seconds = chosen.Seconds;
            currentPriority = chosen.Priority;
            currentUntil = now + Mathf.Max(0f, chosen.Seconds);
            return true;
        }

        // La vista que estaba mostrando el aviso termino antes de tiempo
        // (el jugador lo cerro, cambio de modo, etc). Libera el turno para
        // que el siguiente entre sin esperar a que venza el reloj.
        public static void NotifyFinished()
        {
            currentUntil = float.NegativeInfinity;
            currentPriority = AlertPriority.Baja;
        }

        // LOGICA DE SELECCION, FUNCION PURA. No lee relojes, no toca
        // estado estatico y no depende de la escena: recibe la lista y el
        // instante, y devuelve el indice elegido (o -1). Todo el resto de
        // la clase es plomeria alrededor de esta funcion, que es la unica
        // parte con reglas de negocio y por lo tanto la unica que hace
        // falta probar.
        //
        // Reglas, en orden:
        //   1. Se ignoran los huecos (Message vacio) y los avisos vencidos.
        //   2. Gana la prioridad mas alta.
        //   3. Entre iguales, gana el que se encolo primero (FIFO): el que
        //      llega despues espera su turno.
        public static int SelectNext(PendingAlert[] pending, float now)
        {
            if (pending == null) return -1;

            int best = -1;
            for (int i = 0; i < pending.Length; i++)
            {
                PendingAlert candidate = pending[i];
                if (string.IsNullOrEmpty(candidate.Message)) continue;
                if (IsStale(candidate, now)) continue;

                if (best < 0) { best = i; continue; }

                PendingAlert current = pending[best];
                if (candidate.Priority > current.Priority) { best = i; continue; }
                if (candidate.Priority < current.Priority) continue;
                if (candidate.EnqueuedAt < current.EnqueuedAt) best = i;
            }
            return best;
        }

        // Critica nunca caduca: si el aviso era "MISION FALLIDA", mostrarlo
        // tarde sigue siendo mejor que no mostrarlo nunca.
        public static bool IsStale(PendingAlert alert, float now)
        {
            if (alert.Priority == AlertPriority.Critica) return false;
            return now - alert.EnqueuedAt > MaxWaitSeconds;
        }

        // Se descarta al de prioridad MAS BAJA (y entre iguales, al mas
        // viejo), y solo si el que entra lo supera. Consecuencia buscada:
        // un aviso Baja que llega con la cola llena se descarta solo,
        // porque nunca va a superar al minimo.
        static bool TryEvictFor(AlertPriority incoming)
        {
            int worst = -1;
            for (int i = 0; i < count; i++)
            {
                if (worst < 0) { worst = i; continue; }
                if (pending[i].Priority < pending[worst].Priority) { worst = i; continue; }
                if (pending[i].Priority == pending[worst].Priority &&
                    pending[i].EnqueuedAt < pending[worst].EnqueuedAt) worst = i;
            }

            if (worst < 0) return false;
            if (pending[worst].Priority >= incoming) return false;

            RemoveAt(worst);
            return true;
        }

        // Los vencidos se sacan de verdad y no solo se saltean: si se
        // quedaran adentro ocuparian cupo y harian que la cola se declare
        // llena por avisos que ya nadie va a ver.
        static void PurgeStale(float now)
        {
            for (int i = count - 1; i >= 0; i--)
                if (IsStale(pending[i], now)) RemoveAt(i);
        }

        // Se compacta desplazando, no intercambiando con el ultimo: el
        // orden de llegada es parte de la regla de desempate y un swap lo
        // rompe.
        static void RemoveAt(int index)
        {
            if (index < 0 || index >= count) return;
            for (int i = index; i < count - 1; i++) pending[i] = pending[i + 1];
            count--;
            pending[count] = default;
        }

        // unscaledTime y no time: un aviso tiene que seguir contando
        // mientras el juego esta en pausa o en camara lenta, si no se
        // congela en pantalla para siempre.
        static float Now() => Time.unscaledTime;
    }
}
