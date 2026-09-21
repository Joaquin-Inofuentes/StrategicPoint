using UnityEngine;
using UnityEngine.InputSystem;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Vehicles;
using SP.UI;
using SP.Presentation;

namespace SP.Player
{
    // PlayerInputDriver (parte): orden de la escuadra. El lugar en la lista fija a que numero de roster y a que [F1..F9]
    // responde cada soldado; [ y ] suben o bajan al soldado que manejas.
    public partial class PlayerInputDriver
    {
        void TickReordenarEscuadra(Keyboard kb)
        {
            if (kb.leftBracketKey.wasPressedThisFrame) MoverEnEscuadra(Brain != null ? Brain.Current : null, -1);
            if (kb.rightBracketKey.wasPressedThisFrame) MoverEnEscuadra(Brain != null ? Brain.Current : null, +1);
        }

        float proximoAvisoArsenal;

        // [,] y [.] cambian el arma principal (ranura 1) cuando estas junto a una caja de suministros.
        void TickArsenal(Keyboard kb)
        {
            var yo = Brain != null ? Brain.Current : null;
            if (yo == null || yo.Weapon == null) return;
            bool cerca = CajaDeSuministros.MasCercana(yo.transform.position, CajaDeSuministros.RadioDeArsenal) != null;
            if (cerca && Time.time >= proximoAvisoArsenal)
            {
                proximoAvisoArsenal = Time.time + 12f;
                SP.UI.AlertQueue.Push("ARSENAL: [,] [.] CAMBIAN EL ARMA PRINCIPAL", SP.UI.AlertPriority.Baja, 3f);
            }
            int dir = kb.commaKey.wasPressedThisFrame ? -1 : kb.periodKey.wasPressedThisFrame ? +1 : 0;
            if (dir == 0) return;
            if (!cerca) { SP.UI.AlertQueue.Push("ACERCATE A UNA CAJA DE SUMINISTROS PARA CAMBIAR DE ARMA", SP.UI.AlertPriority.Media, 2f); return; }
            if (yo.Weapon.CambiarArmaPrincipal(dir))
                SP.UI.AlertQueue.Push("ARMA PRINCIPAL: " + yo.Weapon.CurrentWeaponKind.ToString().ToUpperInvariant(), SP.UI.AlertPriority.Media, 2f);
        }

        // Intercambia al soldado con su vecino (direccion -1 = hacia arriba). Devuelve si cambio algo.
        public bool MoverEnEscuadra(Soldier s, int direccion)
        {
            if (Squad == null || s == null) return false;
            int i = Squad.IndexOf(s);
            int j = i + direccion;
            if (i < 0 || j < 0 || j >= Squad.Count) return false;
            (Squad[i], Squad[j]) = (Squad[j], Squad[i]);
            var roster = RosterView.Activo;
            if (roster != null) roster.Rebuild();
            SP.UI.AlertQueue.Push($"{s.DisplayName.ToUpperInvariant()} ES AHORA EL Nº {j + 1}", SP.UI.AlertPriority.Baja, 1.6f);
            return true;
        }

        // Ronda 11 (punto 16): cursor contextual en RTS con soldados seleccionados.
        void ActualizarCursorRts(AimResult r)
        {
            var tipo = CursorTipo.Normal;
            if (Selection != null && Selection.Selected.Count > 0 && !PunteroSobreUiInteractiva(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero))
            {
                if (r.Type == AimTargetType.Enemy) tipo = CursorTipo.Atacar;
                else if (r.Type == AimTargetType.Vehicle || r.Type == AimTargetType.Torreta || r.Type == AimTargetType.Caido) tipo = CursorTipo.Interactuable;
            }
            CursorContextual.Aplicar(tipo);
        }

        // [F1]-[F3] hacen lo mismo que [1]-[3] en RTS.
        void SeleccionarConFuncion(Keyboard kb)
        {
            if (OrdenesMenu != null && OrdenesMenu.Abierto) return;
            bool sumar = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.f1Key.wasPressedThisFrame) SeleccionarSoldadoDeEscuadra(0, sumar);
            if (kb.f2Key.wasPressedThisFrame) SeleccionarSoldadoDeEscuadra(1, sumar);
            if (kb.f3Key.wasPressedThisFrame) SeleccionarSoldadoDeEscuadra(2, sumar);
        }

        public bool SeleccionarSoldadoDeEscuadra(int indice, bool sumar)
        {
            if (Squad == null || indice < 0 || indice >= Squad.Count) return false;
            var s = Squad[indice];
            if (s == null || s.Health == null || !s.Health.IsAlive) { RejectOrder($"SOLDADO {indice + 1} CAIDO"); return false; }
            if (sumar) Selection.AddToSelection(s); else Selection.SelectSingle(s);
            bool doble = lastRecalledGroup == -(indice + 10) && Time.time - lastRecallTime <= GroupDoubleTapSeconds;
            lastRecalledGroup = -(indice + 10);
            lastRecallTime = Time.time;
            if (doble) Rig.RecenterOn(s.transform.position);
            return true;
        }

        // RTS + [Shift] apuntando a una cobertura: holograma del primer
        // seleccionado.
        void UpdateCoverPreviewRts(Keyboard kb, Ray screenRay)
        {
            bool ver = KeyBindings.IsPressed(KeyBindings.VerTactico) || (OrdenesMenu != null && OrdenesMenu.Abierto && OrdenesMenu.Seleccion == MenuDeOrdenes.Cubrirse);
            if (!ver || Selection.Selected.Count == 0)
            {
                CoverHologram.Ocultar();
                // Ronda 11 (punto 19): sin mantener [C], apuntar a un obstaculo con cobertura lo resalta (feedback de "aca hay cobertura").
                if (Selection.Selected.Count > 0)
                {
                    var rc = Aim.Evaluate(screenRay, null);
                    if (rc.Type == AimTargetType.Obstacle && TryResolverCobertura(rc, out _, out _)) CoverHologram.ResaltarSolo(rc.HitTransform);
                }
                return;
            }
            var r = Aim.Evaluate(screenRay, null);
            if (!TryResolverCobertura(r, out var punto, out var dueno)) { CoverHologram.Ocultar(); return; }
            Soldier primero = null;
            foreach (var s in Selection.Selected) if (s != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) { primero = s; break; }
            if (primero == null) { CoverHologram.Ocultar(); return; }
            var to = r.Type == AimTargetType.Obstacle ? r.HitTransform : (dueno != null ? dueno.transform : null);
            CoverHologram.Mostrar(primero, punto, Coberturas.FrenteDe(punto, dueno), to);
        }
    }
}
