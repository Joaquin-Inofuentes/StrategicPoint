using System;
using UnityEngine;
using UnityEngine.AI;
using SP.Core;
using SP.Actors;
using SP.Vehicles;
using SP.Combat;

namespace SP.Ai
{
    // AiBrain (parte): linea de tiro, posturas, cobertura y sensado del enemigo mas cercano.
    public partial class AiBrain
    {
        // ------------------------------------------------------------------
        // Linea de tiro
        // ------------------------------------------------------------------
        // BUG REAL: la IA no miraba NUNCA si habia algo en el medio. Sensaba
        // por distancia pura y disparaba con solo tener al enemigo en rango
        // y encarado. O sea que "veia" a traves del Muro, de las barricadas
        // y de los arboles.
        //
        // Mientras las balas atravesaban el escenario el sintoma era otro
        // (te mataban a traves de una pared). Ahora que el proyectil choca
        // de verdad, sin esto el soldado se queda parado descargando el
        // cargador contra la cobertura, sin hacerle un rasguño al enemigo y
        // sin moverse jamas: el combate se traba para siempre.
        //
        // El rayo va de cuerpo a cuerpo -- el transform del soldado ya esta
        // a la altura del pecho -- y usa la MISMA definicion de pared que
        // SoldierMotor y que el proyectil. Si las tres no coincidieran,
        // habria angulos donde la IA cree tener tiro, la bala choca y nadie
        // entiende por que.
        // El raycast en si vive en NavService.HayLineaDeTiro: para elegir
        // una cobertura hay que poder preguntar lo mismo desde un punto
        // cualquiera, no solo desde el propio cuerpo. Aca queda la version
        // comoda de "yo, hacia ese soldado".
        public bool TieneLineaDeTiro(Soldier objetivo)
        {
            if (objetivo == null || self == null) return false;
            return SP.Core.NavService.HayLineaDeTiro(
                self.transform.position, objetivo.transform.position,
                self.transform, objetivo.transform);
        }

        // ------------------------------------------------------------------
        // Item 212: modificadores de postura
        // ------------------------------------------------------------------
        // Ninguno de estos dos metodos reescribe una decision: se enchufan
        // como condicion sobre las decisiones que ya existian. La primera
        // linea de cada uno es la salida neutra de Libre, para que la
        // postura por defecto recorra el mismo camino de antes.
        bool StanceAllowsPursuit(Vector3 targetPosition)
        {
            // En cobertura por orden del jugador: se queda ahi.
            if (enCobertura && coberturaPorOrden) return false;
            if (stance == CombatStance.Libre) return true;
            if (stance == CombatStance.AltoElFuego) return false;
            // Defensiva: persigo mientras el objetivo siga dentro de la
            // burbuja alrededor de mi puesto. Se mide contra la posicion del
            // objetivo y no contra la mia para que el soldado no quede
            // oscilando justo sobre el borde de la correa.
            return Vector3.Distance(homePosition, targetPosition) <= defensiveLeashRadius;
        }

        bool StanceAllowsFire => stance != CombatStance.AltoElFuego;

        // Que hace cuando la postura le prohibe avanzar. Solo se llama con
        // target != null (lo garantiza el case de Chase).
        void HoldStancePosition(float dt)
        {
            if (enCobertura && coberturaPorOrden)
            {
                if (Vector3.Distance(self.transform.position, coberturaPunto) > 1.2f)
                    AvanzarConRodeo(coberturaPunto, 0.6f, dt, ref destinoRegresoAPuesto, ref tieneRegresoAPuesto, ref relojDeRegresoAPuesto);
                else if (target != null) self.Motor.LookTowards(target.transform.position, dt);
                return;
            }

            // Defensiva: si venia persiguiendo cuando le cambiaron la
            // postura, o lo arrastro una orden previa, vuelve caminando a su
            // puesto en vez de quedarse clavado lejos de casa.
            if (stance == CombatStance.Defensiva &&
                Vector3.Distance(self.transform.position, homePosition) > arriveThreshold)
            {
                AvanzarConRodeo(homePosition, arriveThreshold, dt, ref destinoRegresoAPuesto, ref tieneRegresoAPuesto, ref relojDeRegresoAPuesto);
                return;
            }

            // Ya esta en su puesto (o es AltoElFuego): no avanza, pero
            // mantiene al enemigo encarado. Sigue detectandolo, que es
            // justo lo que pide la postura.
            self.Motor.LookTowards(target.transform.position, dt);
        }

        // ------------------------------------------------------------------
        // Item 224: sensado repartido en el tiempo
        // ------------------------------------------------------------------
        // Se reparte SOLO esta consulta ("cual es el enemigo mas cercano"),
        // que es lo caro y lo que se puede diferir. La maquina de estados y
        // el movimiento siguen corriendo todos los ticks: mover a un soldado
        // 1 de cada N frames se ve como un tartamudeo, y eso seria un precio
        // visible a cambio de un ahorro invisible.
        //
        // OBSOLESCENCIA ACOTADA: el objetivo que devuelve este metodo puede
        // tener hasta SenseIntervalTicks - 1 ticks de antiguedad (con N=3,
        // hasta 2 ticks; a 60 fps, 33 ms). Es aceptable porque las escalas
        // no se parecen: un soldado camina a 5 m/s y el rango de vision es
        // de 10 m, asi que en 2 ticks recorre unos 17 cm -- necesita cientos
        // de frames para entrar o salir del rango de vision. El unico efecto
        // observable es reaccionar hasta 2 frames tarde a un enemigo que
        // aparece, y el desfasaje por soldado hace que ni siquiera reaccionen
        // todos tarde a la vez.
        //
        // El desfasaje es Id % N y no un random a proposito: reparte la carga
        // igual de bien pero es determinista, asi dos corridas identicas dan
        // el mismo resultado y las pruebas headless siguen siendo repetibles.
        Soldier SenseNearestEnemy()
        {
            if (Pasivo) return null;
            // CRITICO: si el objetivo cacheado murio o se desactivo (se subio
            // a un vehiculo) se descarta AHORA y se re-sensa sin esperar el
            // intervalo. Un soldado apuntandole 3 frames a un cadaver es un
            // bug que se ve en pantalla.
            //
            // Ojo con lo que esto NO es: no es un filtro de la busqueda. La
            // consulta sigue siendo la misma de siempre y sigue SIN filtrar
            // por activeInHierarchy (ver el comentario de SpatialGrid.
            // Rebuild: el barrido original tampoco lo filtraba, y un soldado
            // montado igual podia ser sensado). Si la busqueda original
            // hubiera devuelto a ese soldado inactivo, esta tambien lo
            // devuelve: lo unico que se fuerza es volver a preguntarle al
            // mundo en vez de servir un puntero viejo sin revisar. La regla
            // de a quien se detecta no cambia.
            if (sensedTarget != null && !IsCachedSenseUsable(sensedTarget))
            {
                sensedTarget = null;
                forceSense = true;
            }

            if (forceSense || senseIntervalTicks <= 1 ||
                (tickCount + (self.Id % senseIntervalTicks)) % senseIntervalTicks == 0)
            {
                forceSense = false;
                lastSenseTick = tickCount;
                sensedTarget = MejorObjetivoVisible();

                // Pedido explicito: el rombo rojo del locator solo se
                // muestra para enemigos que el bando del jugador ya
                // detecto -- si un ALIADO (no otro enemigo sensando al
                // jugador) acaba de ver a este soldado, queda revelado
                // para siempre.
                if (sensedTarget != null && self.Team == TeamId.Player)
                    SP.Core.InteligenciaDeEnemigos.Revelar(sensedTarget.Id);
            }

            return sensedTarget;
        }

        // Se evalua sobre el CACHE, nunca sobre los candidatos de la
        // busqueda. Un objetivo cacheado sirve mientras siga existiendo,
        // vivo y activo; si no, se re-sensa en el acto.
        static bool IsCachedSenseUsable(Soldier s)
        {
            return s != null && s.Health != null && s.Health.IsAlive &&
                   s.gameObject.activeInHierarchy;
        }
    }
}
