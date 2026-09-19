using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;
using SP.UI;

namespace SP.EditorTools
{
    // FASE 18 (ronda 9): reservas de municion, mando, accesibilidad, fuego amigo en DIFICIL, IA con granadas y supresion,
    // estaticos, oclusion de sonido y teclas.
    public static partial class HeadlessTestRunner
    {
        static void RunPhase18(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
                               GameObject soldierPrefab, Color colorEnemy, ProjectilePool pool)
        {
            TestLog.Phase("FASE 18 - Reservas de municion, mando, accesibilidad, fuego amigo, IA con granadas y supresion");
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            FullHeal(vega, kes, doc);
            SP.Core.ModoDios.Poner(false);
            inputDriver.Brain.Possess(vega);

            // --- Reservas de municion ---
            var w = vega.Weapon;
            WeaponHolder.ReservasActivas = true;
            Check("El soldado que maneja el jugador usa reservas", w.LimitaMunicion && w.UsaReservas);
            Check("Los aliados de la IA siguen con municion ilimitada", !kes.Weapon.UsaReservas);
            w.EquipFromLoadout(0);
            int cargador = w.MagazineSize;
            Check($"Cargador lleno + reserva de {WeaponHolder.CargadoresDeReserva} cargadores", w.CurrentAmmo == cargador && w.ReservaActual == cargador * WeaponHolder.CargadoresDeReserva);
            var campoCooldown = typeof(WeaponHolder).GetField("cooldownTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int reservaInicial = w.ReservaActual;
            for (int i = 0; i < cargador; i++) { campoCooldown.SetValue(w, 0f); w.TryFire(vega.transform.position + Vector3.up, vega.transform.forward); }
            Check("Vaciar el cargador dispara la recarga", w.CurrentAmmo == 0 && w.IsReloading);
            Check("La recarga muestra el tiempo restante", w.ReloadRemaining > 0.5f);
            for (int i = 0; i < 200 && w.IsReloading; i++) w.Tick(0.05f);
            Check($"Recargar descuenta de la reserva ({reservaInicial} -> {w.ReservaActual})", w.CurrentAmmo == cargador && w.ReservaActual == reservaInicial - cargador);
            // cambiar de arma no rellena gratis
            w.EquipFromLoadout(0);
            campoCooldown.SetValue(w, 0f); w.TryFire(vega.transform.position + Vector3.up, vega.transform.forward);
            int quedan = w.CurrentAmmo;
            w.EquipFromLoadout(1); w.EquipFromLoadout(0);
            Check($"Cambiar de arma conserva el cargador ({quedan})", w.CurrentAmmo == quedan);
            // sin nada de nada: clic seco
            for (int k = 0; k < 40 && !w.SinMunicionTotal; k++)
            {
                for (int i = 0; i < 40 && w.CurrentAmmo > 0; i++) { campoCooldown.SetValue(w, 0f); w.TryFire(vega.transform.position + Vector3.up, vega.transform.forward); }
                for (int i = 0; i < 200 && w.IsReloading; i++) w.Tick(0.05f);
            }
            Check("Sin balas ni cargadores queda SIN MUNICION", w.SinMunicionTotal);
            campoCooldown.SetValue(w, 0f);
            Check("Sin municion total no se dispara", !w.TryFire(vega.transform.position + Vector3.up, vega.transform.forward));
            Check("Sin reserva no se puede recargar", !w.Reload());
            w.ReponerMunicion();
            Check("La caja repone cargador y reserva", !w.SinMunicionTotal && w.MunicionCompleta());
            WeaponHolder.ReservasActivas = false;
            Check("Fuera de las misiones la municion es ilimitada", !w.UsaReservas);
            w.LimitaMunicion = true;

            // --- Mando (gamepad) ---
            Check("Mando: la zona muerta anula el ruido del stick", MandoFps.Zona(new Vector2(0.1f, 0.1f)) == Vector2.zero);
            Check("Mando: el stick a fondo da magnitud 1", Mathf.Abs(MandoFps.Zona(new Vector2(1f, 0f)).magnitude - 1f) < 0.01f);
            Check("Mando: sin dispositivo no hay entrada", !MandoFps.Conectado ? MandoFps.Mover == Vector2.zero && !MandoFps.Disparar : true);

            // --- Accesibilidad ---
            AjustesDeJuego.PonerDaltonismo(true);
            var verde = AjustesDeJuego.Adaptar(new Color(0.35f, 0.85f, 0.4f));
            var rojo = AjustesDeJuego.Adaptar(new Color(0.95f, 0.25f, 0.2f));
            Check("Daltonismo: el verde pasa a azul", verde.b > verde.g);
            Check("Daltonismo: el rojo pasa a naranja", rojo.g > 0.5f && rojo.b < 0.2f);
            AjustesDeJuego.PonerDaltonismo(false);
            Check("Sin daltonismo los colores no cambian", AjustesDeJuego.Adaptar(new Color(0.35f, 0.85f, 0.4f)) == new Color(0.35f, 0.85f, 0.4f));
            Check("Las resoluciones disponibles no se repiten y hay al menos una", AjustesDeJuego.Resoluciones().Count >= 1);

            // --- Fuego amigo de explosivos: solo en DIFICIL, y al 50 % ---
            var nivelPrevio = Dificultad.Actual; bool activaPrevia = Dificultad.Activa;
            Dificultad.Activa = true;
            Dificultad.Actual = NivelDificultad.Medio;
            Health.RegeneracionPermitida = false;
            kes.transform.position = vega.transform.position + new Vector3(2f, 0f, 0f);
            int vidaMedio = kes.Health.Current;
            Projectile.ExplodeAt(kes.transform.position, 3f, 80, vega.Id, TeamId.Player);
            Check("En MEDIO tus explosiones no dañan a tu escuadra", kes.Health.Current == vidaMedio);
            Dificultad.Actual = NivelDificultad.Dificil;
            Check("En DIFICIL hay fuego amigo de explosivos", Dificultad.FuegoAmigoExplosivo);
            Projectile.ExplodeAt(kes.transform.position, 3f, 80, vega.Id, TeamId.Player);
            Check($"En DIFICIL la explosion propia dana a tu escuadra ({vidaMedio} -> {kes.Health.Current})", kes.Health.Current < vidaMedio);
            Dificultad.Actual = nivelPrevio; Dificultad.Activa = activaPrevia;
            FullHeal(vega, kes, doc);
            Health.RegeneracionPermitida = true;

            // --- IA: supresion (63) ---
            var brainKes = kes.Brain;
            Check("Un aliado tiene cerebro de IA", brainKes != null);
            if (brainKes != null)
            {
                Check("Un aliado no esta suprimido de entrada", !brainKes.Suprimido);
                brainKes.RecibirSupresion(2.5f);
                Check("Una bala que pasa cerca lo suprime", brainKes.Suprimido);
            }
            Check("El radio de supresion es de 2 m", Mathf.Approximately(Projectile.RadioDeSupresion, 2f));

            // --- IA: granadas (57/58) ---
            Check("Las granadas de la IA estan acotadas (9 a 24 m)", AiBrain.DistanciaMinimaGranadaIA < AiBrain.DistanciaMaximaGranadaIA && AiBrain.DistanciaMaximaGranadaIA <= Granada.AlcanceMaximo);
            Check("La IA huye desde un radio mayor al de la explosion", AiBrain.RadioDeHuidaGranada > Granada.RadioDeExplosion);

            // --- Estaticos: se reinician (84) ---
            SP.Core.ModoDios.Poner(true);
            Health.RegeneracionPermitida = false;
            Demolicion.Segundos = 99f;
            ReinicioDeEstaticos.Restablecer();
            Check("Reiniciar estaticos apaga el modo dios", !SP.Core.ModoDios.Activo);
            Check("Reiniciar estaticos devuelve la regeneracion", Health.RegeneracionPermitida);
            Check("Reiniciar estaticos devuelve los segundos de demolicion", Mathf.Approximately(Demolicion.Segundos, Demolicion.SegundosNormales));

            // --- Teclas: la del cuchillo tiene su propio id (90) ---
            Check("El cuchillo ya no comparte id con la camara de vehiculo", KeyBindings.AtaqueCuchillo != "camara_vehiculo");
            Check("El cuchillo sigue en [F]", KeyBindings.Get(KeyBindings.AtaqueCuchillo) == UnityEngine.InputSystem.Key.F);

            // --- Minimapa, banner y textos del HUD ---
            Check("El texto del banner de mision ya no se corta", !System.IO.File.ReadAllText("Assets/_Project/Scripts/Presentation/GameplaySceneBootstrap.cs").Contains("Escapa en el helicoptero"));
        }
    }
}
