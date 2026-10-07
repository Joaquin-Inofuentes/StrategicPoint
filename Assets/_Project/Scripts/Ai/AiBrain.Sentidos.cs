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
            // Un enemigo metido en un arbusto o en el bosque NO puede disparar: "si o si ataquen solamente afuera"
            // (ver Vegetacion). Sin linea de tiro la IA cae en Chase, y ahi sale a cielo abierto (SalirDeLaVegetacion).
            if (EnVegetacion) return false;
            return VeFisicamente(objetivo);
        }

        // La linea de tiro real (paredes), sin la regla de la vegetacion: sirve para no soltar al blanco mientras el enemigo sale del monte.
        // Solo para MEDIR (checks): vuelve a la definicion anterior de la linea de tiro (pivote a pivote, 0,8 m) para ver el antes/despues de #080.
        public static bool LineaDeTiroPivoteAPivote = false;

        bool VeFisicamente(Soldier objetivo)
        {
            if (objetivo == null || self == null) return false;
            if (LineaDeTiroPivoteAPivote) return SP.Core.NavService.HayLineaDeTiro(self.transform.position, objetivo.transform.position, self.transform, objetivo.transform);
            // WP4 (#080/#081): de la boca del arma PARADO (pivote+0,5) al pecho del blanco (pivote+0,5, o +0,1 si esta agachado). Es la misma
            // definicion que usa la eleccion de cobertura (Coberturas.PuntoDeTiro / HayLineaDeTiroAlPecho): antes aca iba pivote a pivote
            // (0,8 m) y una barricada de 1,05 m tapaba siempre el rayo aunque se pudiera disparar por encima.
            return SP.Core.NavService.HayLineaDeTiro(
                self.transform.position + Vector3.up * SP.Core.Coberturas.AlturaDeLaBocaDePie, SP.Core.Coberturas.PuntoDeTiro(objetivo),
                self.transform, objetivo.transform);
        }

        // WP4 (#080): a cielo abierto el soldado dispara agachado (presenta menos blanco) solo si desde agachado TAMBIEN ve al blanco; si
        // no (una barricada baja delante) se para a disparar por encima. Se re-evalua cada 0,25 s.
        bool PuedeDispararAgachado(float dt)
        {
            if (target == null) return true;
            relojAgachado -= dt;
            if (relojAgachado <= 0f)
            {
                relojAgachado = 0.25f;
                agachadoVeAlBlanco = SP.Core.NavService.HayLineaDeTiro(
                    self.transform.position + Vector3.up * SP.Core.Coberturas.AlturaDeLaBocaAgachado, SP.Core.Coberturas.PuntoDeTiro(target),
                    self.transform, target.transform);
            }
            return agachadoVeAlBlanco;
        }

        // WP4 (#086): punto de ataque PERSONAL. Sobre la recta blanco-soldado, girada un abanico de +-30 grados fijo por Id, a 0,85 del alcance
        // del blanco: los que vienen juntos terminan en un arco y no todos en el mismo punto.
        Vector3 PuntoDeAtaque(Soldier t)
        {
            var desde = self.transform.position - t.transform.position; desde.y = 0f;
            if (desde.sqrMagnitude < 0.25f) return t.transform.position;
            float ang = ((self.Id * 37) % 61) - 30f;
            var dir = Quaternion.Euler(0f, ang, 0f) * desde.normalized;
            var p = t.transform.position + dir * (EffectiveAttackRange * 0.85f);
            p.y = self.transform.position.y;
            return p;
        }

        // Enemigo parado dentro de un arbusto o del bosque (solo en la partida principal).
        public bool EnVegetacion => self != null && self.Team == TeamId.Enemy && SP.Core.Vegetacion.Dentro(self.transform.position);

        Vector3 salidaDelMonte;
        bool tieneSalidaDelMonte;
        float relojSalidaDelMonte;

        // Enemigo en el monte con un blanco fijado: camina al claro mas cercano antes de pelear. Devuelve true si este tick lo uso para eso.
        bool SalirDeLaVegetacion(float dt)
        {
            if (!EnVegetacion) { tieneSalidaDelMonte = false; return false; }
            relojSalidaDelMonte -= dt;
            if (!tieneSalidaDelMonte || relojSalidaDelMonte <= 0f)
            {
                relojSalidaDelMonte = 1.5f;
                tieneSalidaDelMonte = SP.Core.Vegetacion.TryPuntoLibre(self.transform.position, out salidaDelMonte);
                if (tieneSalidaDelMonte) PlanPathTo(salidaDelMonte);
            }
            if (!tieneSalidaDelMonte) return false;   // sin claro cerca: que pelee como pueda (igual no dispara, pero no se queda clavado)
            self.Motor.SetCrouching(false);
            AdvanceTo(salidaDelMonte, 0.5f, dt);
            return true;
        }

        // En el borde del monte: si el proximo paso hacia el punto lo metia adentro, se queda afuera encarando al blanco.
        bool EnemigoNoEntraAlMonte(Vector3 haciaPunto, float dt)
        {
            if (self.Team != TeamId.Enemy || !SP.Core.Vegetacion.Rige) return false;
            var d = haciaPunto - self.transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return false;
            var paso = self.transform.position + d.normalized * 1.8f;
            if (!SP.Core.Vegetacion.Dentro(paso)) return false;
            if (target != null) self.Motor.LookTowards(target.transform.position, dt);
            return true;
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
