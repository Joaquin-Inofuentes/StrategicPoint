using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.CameraSystem;
using SP.Core;
using SP.UI;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Player
{
    // PlayerInputDriver (parte): vista RTS -- UpdateRts, vista previa de formacion, grupos de control y seleccion por arrastre.
    // Se separo del archivo principal para que vuelva a quedar por debajo de 3500 lineas (item 82 de la auditoria).
    public partial class PlayerInputDriver
    {
        // -----------------------------------------------------------
        // RTS
        // -----------------------------------------------------------
        void UpdateRts(Keyboard kb, Mouse mouse)
        {
            // El balanceo es solo de caminar en primera persona: sin
            // apagarlo aca seguiria oscilando la camara en RTS y dentro
            // del vehiculo, donde no hay pisadas que representar.
            Rig.SetWalking(false);
            Rig.SetZoomed(false); // el zoom de mirilla es solo a pie
            if (WeaponStatus != null) WeaponStatus.gameObject.SetActive(false);
            if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
            if (TurretAim != null) TurretAim.SetVisible(false);
            if (weaponViewmodel != null) weaponViewmodel.SetActive(false);
            if (AimUiRef != null) AimUiRef.SetVisible(false);
            if (PlayerHealth != null) PlayerHealth.gameObject.SetActive(false); MirillaView.Instancia?.Ocultar();
            if (SelectionCount != null) SelectionCount.SetModeVisible(true);
            HideFpsOnlyIndicators();
            if (bodyHiddenFor != null) { bodyHiddenFor.SetBodyVisible(true); bodyHiddenFor.Motor.SetCrouching(false); bodyHiddenFor = null; }
            UpdateVehicleSelectionRing();
            bool ctrlHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            // [Ctrl+A] selecciona a toda la escuadra viva, el estandar de
            // cualquier RTS -- antes solo se podia arrastrar un cuadro
            // que los abarcara a todos, lo que obligaba a alejar la
            // camara primero.
            if (ctrlHeld && kb.aKey.wasPressedThisFrame && Squad != null)
            {
                Selection.SelectAll(Squad);
                GameLog.Line("Se selecciono toda la escuadra");
            }

            Vector3 pan = Vector3.zero;
            if (kb.wKey.isPressed) pan += Vector3.forward;
            if (kb.sKey.isPressed) pan += Vector3.back;
            if (kb.dKey.isPressed) pan += Vector3.right;
            // Con Ctrl apretado, A es el atajo de "seleccionar todo", no
            // panear -- sin este corte, Ctrl+A tambien empujaria la
            // camara a la izquierda en el mismo instante.
            if (kb.aKey.isPressed && !ctrlHeld) pan += Vector3.left;
            if (pan.sqrMagnitude > 0.0001f)
            {
                bool shiftHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                PanearRts(pan, shiftHeld, Time.deltaTime);
            }

            // #124: [Q] baja y [E] sube la camara RTS (la accion contextual de [Q] queda solo en FPS; la radio ya no se suelta con [E]).
            float ejeAltura = 0f;
            if (OrdenesMenu == null || !OrdenesMenu.Abierto)
            {
                if (KeyBindings.IsPressed(KeyBindings.Interactuar) || AlturaRtsPruebaE) ejeAltura += 1f;
                if (KeyBindings.IsPressed(KeyBindings.CiclarPosesion) || AlturaRtsPruebaQ) ejeAltura -= 1f;
            }
            Rig.MoverAlturaRts(ejeAltura, Time.deltaTime);

            if (KeyBindings.WasPressed(KeyBindings.FocalizarRts))
            {
                Vector3 focusPos = Vector3.zero;
                bool doFocus = false;
                if (Selection.SelectedVehicle != null)
                {
                    focusPos = Selection.SelectedVehicle.transform.position;
                    doFocus = true;
                }
                else if (Selection.Selected.Count > 0)
                {
                    focusPos = Selection.Selected[0].transform.position;
                    doFocus = true;
                }
                else if (Brain != null && Brain.Current != null)
                {
                    focusPos = Brain.Current.transform.position;
                    doFocus = true;
                }

                if (doFocus) Rig.RecenterOn(focusPos);
            }

            bool orbitando = false;
            if (mouse != null)
            {
                if (KeyBindings.IsPressed(KeyBindings.RotarCamaraRts))
                {
                    orbitando = true;
                    // Mientras se orbita el cursor queda CENTRADO Y OCULTO (bloqueado): asi el mouse
                    // gira sin limite de pantalla. Al soltar vuelve al pixel donde estaba.
                    if (!orbitandoRts)
                    {
                        orbitandoRts = true;
                        cursorAntesDeOrbitar = mouse.position.ReadValue();
                        framesOrbitando = 0;
                    }
                    framesOrbitando++;
                    // Los primeros frames bloqueados traen el salto de centrado del cursor: no se aplican.
                    if (framesOrbitando > 2)
                    {
                        // Orbita alrededor del punto central de la pantalla: X gira, Y inclina.
                        var d = mouse.delta.ReadValue() * sensibilidadOrbitaRts;
                        Rig.OrbitarRts(d.x, d.y);
                    }
                }
                else if (orbitandoRts)
                {
                    orbitandoRts = false;
                    devolverCursorFrames = 2;
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Rig.ZoomHaciaCursor(scroll * rtsZoomSpeed * 3f * Time.deltaTime, mouse.position.ReadValue());
            }

            // Bug #188: se quita el cartel de controles del RTS ("nadie lo lee"); los atajos siguen en la pausa > controles.
            SetInstructionText("");

            if (mouse == null || Rig.Cam == null) return;

            UpdateDragSelection(kb, mouse);

            var screenRay = Rig.Cam.ScreenPointToRay(mouse.position.ReadValue());

            // B4 y C3 en RTS: mismo raycast que ya se calculaba para
            // resolver el click, reusado para el anillo bajo el cursor y
            // la marca de montable sin pagar un segundo Physics.Raycast
            // por frame.
            var resultRts = Aim.Evaluate(screenRay, null);
            if (OrdenesMenu != null && OrdenesMenu.Abierto) OrdenesMenu.MoverSeleccion(mouse.delta.ReadValue());
            else ultimoResultadoDeMira = resultRts;
            UpdateAimRing(resultRts);
            TickGestoMoverAliado(kb, resultRts, screenRay);   // bug #205
            // Bug #189: con soldados seleccionados y el cursor sobre una cobertura se iluminan los puntos que tomarian.
            if (Selection.SelectedVehicle == null && Selection.Selected.Count > 0 && (resultRts.Type == AimTargetType.Obstacle || resultRts.Type == AimTargetType.Ground || resultRts.Type == AimTargetType.Cubrirse))
                PrevisualizacionDeCoberturas.Mostrar(Selection.Selected, resultRts.Point, resultRts.Type == AimTargetType.Ground ? null : resultRts.HitTransform,
                    resultRts.Type == AimTargetType.Ground ? RadioCoberturaPiso : 0f);
            else PrevisualizacionDeCoberturas.Ocultar();
            UpdateVehicleMountIndicatorRts(resultRts);
            ActualizarCursorRts(resultRts);

            // Pedido explicito: mantener click derecho apretado NO mueve
            // la camara. Antes, sostenerlo y mover la mano de mas (aunque
            // fuera solo para terminar de apuntar la orden, no para
            // panear) corria la vista -- y si ademas se sostenia mas de
            // 180ms, ArrastreDerecho lo clasificaba como paneo y la orden
            // ni se emitia al soltar. El paneo de camara sigue existiendo
            // (WASD, mas abajo), simplemente click derecho ya no es una
            // de sus formas de dispararlo.
            if (mouse.rightButton.wasPressedThisFrame)
                rightPressStartedOverUi = PunteroSobreUiInteractiva(mouse.position.ReadValue());

            bool rightClickOrder = mouse.rightButton.wasReleasedThisFrame && !rightPressStartedOverUi;

            // Feedback de seleccion (sonido + aviso) cuando cambia la cantidad.
            int cantSel = Selection.Selected.Count;
            if (cantSel != ultimaSeleccion)
            {
                if (cantSel > 0)
                    Feedback.Accion(SfxKind.Select, cantSel == 1 ? "1 SELECCIONADO" : $"{cantSel} SELECCIONADOS", null, Feedback.Info, aviso: true, pulso: false, volumen: 0.35f);
                ultimaSeleccion = cantSel;
            }
            UpdateCoverPreviewRts(kb, screenRay);

            // [T] o click derecho: mover a todos los seleccionados ahí --
            // o al vehículo, si es él quien está seleccionado (requiere
            // conductor propio adentro, como en FPS). "!dragging" es el
            // recuadro de selección por click IZQUIERDO, no el derecho.
            bool pidioOrden = (AtajosDeTecladoHeredados && kb.tKey.wasPressedThisFrame) || (rightClickOrder && !dragging);

            // Con Ctrl apretado el mismo gesto NO ordena: marca un punto
            // del recorrido, que no arranca hasta [Espacio]. Se resuelve
            // antes y apaga el pedido de orden -- si cayera adentro del
            // bloque de abajo, el mismo click emitiria la orden ademas de
            // marcar el punto.
            if (pidioOrden && ctrlHeld)
            {
                pidioOrden = false;
                var marca = Aim.Evaluate(screenRay, null);
                if (marca.Type != AimTargetType.Ground) RejectOrder("MARCA EN EL PISO");
                else if (!TrazadoDeCamino.Marcar(marca.Point))
                    RejectOrder(TrazadoDeCamino.Cantidad >= TrazadoDeCamino.MaximoDePuntos
                        ? "RECORRIDO LLENO" : "PUNTO NO VALIDO");
                else if (ModeToast != null)
                    ModeToast.Show($"PUNTO {TrazadoDeCamino.Cantidad}  ·  [ESPACIO] ARRANCA", 1.0f);
            }

            if (pidioOrden)
            {
                var result = Aim.Evaluate(screenRay, null);
                // Con Shift la orden se ENCOLA detras de lo ya planificado
                // en vez de reemplazarlo: es lo que permite trazar una ruta
                // de varios tramos.
                bool queued = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

                // Bug #180: sobre un interactuable ordenable (computadora, panel, carga) la orden es operarlo.
                var ordenableRts = Selection.SelectedVehicle == null ? OrdenableBajoRayoRts(screenRay) : null;

                // Bug #197: clic derecho sobre un aliado herido o caido = el medico va a curarlo / reanimarlo (antes el clic caia en "mover").
                var aliadoACurar = Selection.SelectedVehicle == null && (result.Type == AimTargetType.Ally || result.Type == AimTargetType.Caido) && NecesitaCuracion(result.Soldier) ? result.Soldier : null;

                // Sobre una COBERTURA (el obstaculo o su disco celeste): la
                // seleccion va a cubrirse, un punto para cada uno.
                bool esCobertura = Selection.SelectedVehicle == null && Selection.Selected.Count > 0
                    && TryResolverCobertura(result, out _, out _);

                // BUG REAL ("en RTS a veces no toma la orden de moverse"): la orden de mover exigia que el
                // cursor cayera justo sobre el PISO. Con el cursor sobre un aliado, un cuerpo caido, una torreta,
                // un techo o un muro (o sobre nada), no pasaba absolutamente nada y no se avisaba. Ahora el
                // destino es el piso que queda bajo el cursor, salvo enemigos y vehiculos (que tienen su propia orden).
                if (!esCobertura && result.Type != AimTargetType.Ground && result.Type != AimTargetType.Enemy && result.Type != AimTargetType.Vehicle && result.Type != AimTargetType.Torreta && result.Type != AimTargetType.Radio
                    && AimTargeting.PisoBajoRayo(screenRay, out var pisoBajoCursor))
                    result = new AimResult { Type = AimTargetType.Ground, Point = pisoBajoCursor };

                if (Selection.SelectedVehicle == null && Selection.Selected.Count == 0)
                {
                    RejectOrder("NADIE SELECCIONADO: ARRASTRA UN CUADRO O CLICK SOBRE UN ALIADO");
                    pidioOrden = false;
                }

                // Bug #213: clic derecho sobre un muro/cobertura demolible con un ASALTO seleccionado = el asalto va a demolerlo (antes caia en 'cubrirse').
                bool demolerRts = ordenableRts == null && aliadoACurar == null && DemolerDesdeRts(result);

                if (demolerRts) { }
                else if (ordenableRts != null)
                {
                    EnviarAInteractuarDesdeRts(ordenableRts);
                }
                else if (aliadoACurar != null)
                {
                    EnviarACurarDesdeRts(aliadoACurar);
                }
                else if (esCobertura)
                {
                    int cubiertos = IssueCoverOrderForSelection(Selection.Selected, result.Point,
                        result.Type == AimTargetType.Obstacle ? result.HitTransform : null);
                    if (cubiertos == 0) RejectOrder("NO HAY COBERTURA LIBRE AHI");
                }
                else if (result.Type == AimTargetType.Radio)
                {
                    // #119: clic derecho sobre la radio con soldados seleccionados = el primero que pueda va a operarla.
                    var dir = SP.Operacion.OperacionDirector.Instancia;
                    string motivoRadio = "NO SE PUEDE OPERAR LA RADIO AHORA";
                    if (Selection.SelectedVehicle != null) RejectOrder("SELECCIONA UN SOLDADO PARA LA RADIO");
                    else if (Selection.Selected.Count == 0) RejectOrder("NADIE SELECCIONADO: ELEGI QUIEN OPERA LA RADIO");
                    else if (dir == null || !dir.OrdenarOperarLaRadio(Selection.Selected, out motivoRadio)) RejectOrder(motivoRadio);
                }
                else if (result.Type == AimTargetType.Torreta)
                {
                    if (result.Torreta != null && !result.Torreta.Libre) { RejectOrder("TORRETA OCUPADA"); }
                    else if (result.Torreta != null)
                    {
                        var boardable = new List<Soldier>();
                        foreach (var s in Selection.Selected)
                            if (s != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && result.Torreta.Ocupante != s)
                                boardable.Add(s);
                        if (boardable.Count > 0)
                        {
                            StartCoroutine(EsperarYMontarTorreta(boardable[0], result.Torreta));
                            Avisar(boardable[0].DisplayName.ToUpperInvariant() + " VA A LA TORRETA");
                        }
                        else RejectOrder("NO HAY MAS ALIADOS PARA SUBIR");
                    }
                }
                else if (result.Type == AimTargetType.Ground)
                {
                    if (Selection.SelectedVehicle != null)
                    {
                        // Antes esto fallaba en silencio si nadie manejaba
                        // el vehiculo (TryIssueVehicleMoveOrder devuelve
                        // false sin avisar por que): el jugador seleccionaba
                        // el tanque, le daba la orden de moverse, y no
                        // pasaba nada -- parecia que la seleccion O el
                        // movimiento estaban rotos cuando en realidad hacia
                        // falta un conductor adentro primero.
                        if (Selection.SelectedVehicle.Driver == null)
                            RejectOrder("EL VEHICULO NECESITA UN CONDUCTOR");
                        else
                            TryIssueVehicleMoveOrder(result.Point, Selection.SelectedVehicle);
                    }
                    else if (Selection.Selected.Count > 0)
                    {
                        // Ordenar sobre un obstaculo no hacia nada y no
                        // avisaba: el soldado se trababa contra el borde y
                        // el jugador creia que la orden se habia dado.
                        // Bug #196: sobre un solido van al lugar libre mas cercano; solo se rechaza si no hay ninguno en 12 m.
                        if (!OrderService.TryResolveDestination(result.Point, out var destinoLibre)) RejectOrder("DESTINO BLOQUEADO");
                        else OrderService.IssueFormationOrderForSelection(Selection.Selected, destinoLibre, Vector3.forward, currentFormation, queued);
                    }
                }
                // IssueAttackOrder existia y funcionaba pero no estaba
                // cableada a ninguna entrada en RTS: una capacidad ya
                // implementada que el jugador no podia usar.
                else if (result.Type == AimTargetType.Enemy && Selection.Selected.Count > 0)
                {
                    // Lote: la confirmacion sonora y el log van UNA vez, no
                    // una por soldado (con 50 seleccionados eran 50 tonos
                    // superpuestos). El log ya lo emite el metodo de lote.
                    OrderService.IssueAttackOrderForSelection(Selection.Selected, result.Soldier);
                }
                // Bug #071: clic derecho sobre un vehiculo ENEMIGO = los seleccionados lo atacan (cursor Atacar).
                else if (result.Type == AimTargetType.Vehicle && result.Vehicle != null && result.Vehicle.Hostil(TeamId.Player) && Selection.Selected.Count > 0)
                {
                    if (OrderService.IssueAttackVehicleOrderForSelection(Selection.Selected, result.Vehicle) == 0) RejectOrder("NADIE PUEDE ATACARLO");
                }
                else if (result.Type == AimTargetType.Vehicle && result.Vehicle.IsDestroyed)
                {
                    RejectOrder("VEHICULO DESTRUIDO");
                }
                // Bug #215: clic derecho sobre un vehiculo PROPIO (el tanque) = los seleccionados suben (antes solo andaba con la tecla [G]).
                else if (result.Type == AimTargetType.Vehicle && result.Vehicle != null && Selection.Selected.Count > 0 && Selection.SelectedVehicle == null)
                    MontarDesdeRts(result);
            }

            // Mismo criterio que la version FPS de [G] (ver GOrderOnVehicle):
            // suma a la seleccion que todavia no este adentro, no expulsa a
            // nadie -- [I] es la unica tecla que baja gente.
            if (AtajosDeTecladoHeredados && kb.gKey.wasPressedThisFrame)
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Vehicle && result.Vehicle.Bando == TeamId.Enemy && !result.Vehicle.IsDestroyed)
                {
                    RejectOrder(Vehicle.MotivoEnemigo);   // ronda 13 (punto 12)
                }
                else if (result.Type == AimTargetType.Vehicle && !result.Vehicle.IsDestroyed)
                {
                    if (!result.Vehicle.HasAnyRoom) { RejectOrder("VEHICULO LLENO"); }
                    else
                    {
                        var boardable = new List<Soldier>();
                        foreach (var s in Selection.Selected)
                            if (s != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && result.Vehicle.RoleOf(s) == null)
                                boardable.Add(s);
                        if (boardable.Count > 0) OrderService.IssueMountOrderForSelection(boardable, result.Vehicle);
                        else RejectOrder("NO HAY MAS ALIADOS PARA SUBIR");
                    }
                }
            }

            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.Poseer))
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Ally) TryPossess(result.Soldier);
                // Apuntando al vehículo con la escuadra (o parte de ella)
                // ya adentro: [F] toma control de manejo en vez de
                // requerir que primero le apuntes a un soldado -- los
                // ocupantes están inactivos/ocultos, no se les puede
                // apuntar directamente.
                else if (result.Type == AimTargetType.Vehicle && result.Vehicle.OccupantCount > 0 && result.Vehicle.Bando != TeamId.Enemy)
                {
                    EnterVehicleViewFromRts(result.Vehicle);
                }
            }

            // Una orden dada por error obligaba a esperar a que el
            // soldado llegara a destino para recien ahi poder
            // redirigirlo. [X] la cancela y devuelve a la seleccion
            // actual a Patrol sin tener que darle una orden nueva encima.
            // --- Ordenes de escuadra nuevas ---
            // [Z] reagrupar dispersos (219)
            // 213: clic sobre el minimapa emite la orden en el punto del
            // MUNDO correspondiente, sin tener que panear la camara hasta
            // ahi. Se chequea antes que el clic normal para que el clic
            // sobre el minimapa no sea interpretado como clic en el mundo.
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && MinimapRef != null && Selection.Selected.Count == 0)
            {
                var minimapSel = MinimapRef.transform as RectTransform;
                if (minimapSel != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(minimapSel, mouse.position.ReadValue(), null, out var localSel)
                    && MinimapRef.TryMinimapPointToWorld(localSel, out var mundoSel))
                {
                    Soldier cercano = null; float mejor = 9f;
                    var todos = ActorRegistry.All;
                    for (int i = 0; i < todos.Count; i++)
                    {
                        var a = todos[i] as Soldier;
                        if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                        float d = new Vector2(a.transform.position.x - mundoSel.x, a.transform.position.z - mundoSel.z).magnitude;
                        if (d < mejor) { mejor = d; cercano = a; }
                    }
                    if (cercano != null) { Selection.SelectSingle(cercano); return; }
                }
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && MinimapRef != null && Selection.Selected.Count > 0)
            {
                var minimapRt = MinimapRef.transform as RectTransform;
                if (minimapRt != null)
                {
                    Vector2 local;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            minimapRt, mouse.position.ReadValue(), null, out local))
                    {
                        Vector3 destino;
                        if (MinimapRef.TryMinimapPointToWorld(local, out destino))
                        {
                            OrderService.IssueFormationOrderForSelection(
                                Selection.Selected, destino, Vector3.forward, currentFormation);
                            AlertQueue.Push("ORDEN DESDE EL MINIMAPA", AlertPriority.Media, 1.2f);
                            return;
                        }
                    }
                }
            }

            if (KeyBindings.WasPressed(KeyBindings.Reagrupar) && Selection.Selected.Count > 0)
            {
                OrderService.RegroupSelection(Selection.Selected, currentFormation);
                if (ModeToast != null) ModeToast.Show("REAGRUPANDO");
            }

            // [B] retirada: alejarse del enemigo mas cercano (217)
            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.Retirada) && Selection.Selected.Count > 0)
            {
                OrderService.IssueRetreatOrderForSelection(Selection.Selected);
                if (ModeToast != null) ModeToast.Show("RETIRADA");
            }

            // [K] cicla la formacion con la que se emiten las ordenes (210)
            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.CiclarFormacion))
            {
                currentFormation = (FormationKind)(((int)currentFormation + 1) % 4);
                if (ModeToast != null) ModeToast.Show("FORMACION: " + currentFormation.ToString().ToUpper());
            }

            // [J] seleccionar solo los heridos (220)
            if (KeyBindings.WasPressed(KeyBindings.SeleccionarHeridos))
            {
                if (!Selection.SelectWoundedOnly() && ModeToast != null) ModeToast.Show("NADIE HERIDO");
            }

            // [N] seleccionar a todos los del mismo tipo en pantalla (214)
            if (KeyBindings.WasPressed(KeyBindings.SeleccionarMismoTipo) && Selection.Selected.Count > 0)
            {
                Selection.SelectSameTypeOnScreen(Selection.Selected[0], Rig.Cam);
            }

            // Descartar un recorrido todavia sin arrancar es lo mismo que
            // cancelar una orden, y no necesita seleccion: los puntos son
            // del jugador, no de nadie en particular.
            if (KeyBindings.WasPressed(KeyBindings.CancelarOrden) && TrazadoDeCamino.HayTrazado)
            {
                TrazadoDeCamino.Limpiar();
                if (ModeToast != null) ModeToast.Show("RECORRIDO DESCARTADO");
            }

            if (KeyBindings.WasPressed(KeyBindings.CancelarOrden) && Selection.Selected.Count > 0)
            {
                foreach (var s in Selection.Selected)
                {
                    var b = s.Brain;
                    if (b != null) b.CancelOrder();
                }
                // Los marcadores de cola son permanentes (representan un
                // plan pendiente): cancelar la orden tiene que borrarlos,
                // si no queda un plan dibujado que ya nadie va a cumplir.
                OrderMarkerFx.ClearQueuedMarkers();
                GameLog.Line("Se cancelo la orden de la seleccion");
                Feedback.Accion(SfxKind.EmptyClick, "ORDEN CANCELADA", null, Feedback.Warn, aviso: true, pulso: false, volumen: 0.5f);
            }

            UpdateControlGroups(kb);
            UpdateFormationPreview(mouse, screenRay);

            // [Espacio] recentra la camara en el centroide de la escuadra
            // viva -- la tecla mas grande y accesible para la accion mas
            // repetida en vista tactica, para cuando la camara se pierde
            // paneando por el mapa.
            // [Espacio] tiene dos trabajos y el recorrido gana: si hay uno
            // trazado, recentrar la camara es lo ultimo que el jugador
            // quiere en ese momento. Sin trazado se comporta igual que
            // siempre.
            if (KeyBindings.WasPressed(KeyBindings.Recentrar) && TrazadoDeCamino.HayTrazado)
            {
                int tramos = TrazadoDeCamino.Ejecutar(Selection.Selected);
                if (tramos > 0) { if (ModeToast != null) ModeToast.Show($"RECORRIDO DE {tramos} TRAMOS"); }
                else { RejectOrder("NADIE SELECCIONADO"); TrazadoDeCamino.Limpiar(); }
            }
            else if (KeyBindings.WasPressed(KeyBindings.Recentrar) && Squad != null)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (var s in Squad)
                {
                    if (s == null || !s.Health.IsAlive) continue;
                    sum += s.transform.position;
                    count++;
                }
                if (count > 0) Rig.RecenterOn(sum / count);
            }
        }

        // Anillo de selección para el vehículo (mismo look que el de los
        // soldados, SelectionRingFx, solo que esto no pasa por
        // SelectionRingManager porque ese escucha SelectionChangedEvent,
        // que es pura selección de soldados).
        Vehicle ringedVehicle;
        SelectionRingFx vehicleSelectionRing;
        static readonly Color VehicleSelectionRingColor = new Color(0.3f, 0.75f, 0.95f);

        // El jugador no veia donde iba a quedar cada soldado hasta DESPUES
        // de dar la orden, cuando ya no podia corregirla. Mientras se
        // mantiene el boton derecho apretado se dibujan los puestos
        // fantasma; se descartan al soltar (la orden real, que se emite en
        // wasPressedThisFrame, dibuja sus propios marcadores).
        readonly List<GameObject> formationGhosts = new List<GameObject>();

        void UpdateFormationPreview(Mouse mouse, Ray screenRay)
        {
            bool showing = mouse.rightButton.isPressed && !dragging
                && Selection.Selected.Count > 1 && Selection.SelectedVehicle == null;

            if (!showing)
            {
                if (formationGhosts.Count > 0) ClearFormationGhosts();
                // Item 218: la linea de ruta tiene que desaparecer en el
                // mismo instante que los fantasmas de destino, no un frame
                // despues -- si no, quedaba una linea vieja pegada en
                // pantalla apuntando a un destino que ya no se estaba
                // previsualizando.
                if (SP.Ai.PathPreview.Instance != null) SP.Ai.PathPreview.Instance.Hide();
                return;
            }

            var result = Aim.Evaluate(screenRay, null);
            if (result.Type != AimTargetType.Ground) { ClearFormationGhosts(); if (SP.Ai.PathPreview.Instance != null) SP.Ai.PathPreview.Instance.Hide(); return; }

            var spots = OrderService.FormationPoints(result.Point, Vector3.forward, Selection.Selected.Count, currentFormation);
            EnsureGhostCount(spots.Length);
            for (int i = 0; i < spots.Length; i++)
                formationGhosts[i].transform.position = new Vector3(spots[i].x, 0.06f, spots[i].z);

            // 218: vista previa de la ruta, no solo del destino. Antes el
            // jugador veia DONDE iban a terminar los fantasmas pero no
            // POR DONDE iba a ir la escuadra -- si habia un obstaculo en
            // el medio, se enteraba recien cuando los soldados chocaban
            // contra el. El origen es el centroide de la seleccion, que
            // es de donde parte la orden en conjunto.
            if (SP.Ai.PathPreview.Instance != null)
            {
                Vector3 centroide = Vector3.zero;
                int vivos = 0;
                foreach (var s in Selection.Selected)
                {
                    if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                    centroide += s.transform.position;
                    vivos++;
                }
                if (vivos > 0) SP.Ai.PathPreview.Instance.Show(centroide / vivos, result.Point);
            }
        }

        void EnsureGhostCount(int count)
        {
            while (formationGhosts.Count < count)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "FormationGhost";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.localScale = new Vector3(0.9f, 0.03f, 0.9f);
                var rendGhost = go.GetComponent<MeshRenderer>();
                rendGhost.sharedMaterial = SP.Presentation.SafeMaterial.Create(new Color(0.35f, 0.85f, 0.35f));
                rendGhost.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                formationGhosts.Add(go);
            }
            while (formationGhosts.Count > count)
            {
                var last = formationGhosts[formationGhosts.Count - 1];
                formationGhosts.RemoveAt(formationGhosts.Count - 1);
                if (last != null) Destroy(last);
            }
        }

        void ClearFormationGhosts()
        {
            foreach (var g in formationGhosts) if (g != null) Destroy(g);
            formationGhosts.Clear();
        }

        public void AvisarRechazo(string reason) => RejectOrder(reason);

        void RejectOrder(string reason)
        {
            if (ModeToast != null) ModeToast.Show(reason, 1.2f);
            OrderService.PlayRejectSound();
            GameLog.Line($"Orden rechazada: {reason}");
        }

        // Se guardan IDS y no referencias a Soldier: si un miembro del
        // grupo cae, su id simplemente no resuelve al recuperarlo, en vez
        // de arrastrar una referencia a un objeto muerto para siempre.
        readonly Dictionary<int, List<int>> controlGroups = new Dictionary<int, List<int>>();
        int lastRecalledGroup = -1;
        float lastRecallTime = -99f;
        const float GroupDoubleTapSeconds = 0.4f;

        // Traduce los ids guardados a soldados vivos para las tarjetas.
        // Se llama al GUARDAR un grupo y a intervalo desde la vista, no por
        // frame desde aca.
        void RefreshGroupCards()
        {
            if (GroupCards == null) return;
            var listas = new System.Collections.Generic.List<System.Collections.Generic.List<Soldier>>();
            for (int g = 1; g <= SP.UI.GroupCardsView.SlotCount; g++)
            {
                var miembros = new System.Collections.Generic.List<Soldier>();
                if (controlGroups.TryGetValue(g, out var ids))
                    foreach (var id in ids)
                    {
                        var s = SP.Core.ActorRegistry.FindById(id);
                        if (s != null) miembros.Add(s);
                    }
                listas.Add(miembros);
            }
            GroupCards.SetGroups(listas);
        }

        void UpdateControlGroups(Keyboard kb)
        {
            // BUG REAL: wasPressedThisFrame no "consume" la tecla -- el
            // mismo [1]-[5] usado para elegir una orden del menu ([Q]
            // sostenido) tambien llegaba aca en el mismo frame y
            // guardaba/recuperaba un grupo de control sin que el jugador
            // lo pidiera (y con [Ctrl] sostenido, hasta PISABA un grupo
            // guardado). Mientras el menu esta abierto, estas teclas son
            // suyas.
            if (OrdenesMenu != null && OrdenesMenu.Abierto) return;

            var digitKeys = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key,
                                    kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            for (int i = 0; i < digitKeys.Length; i++)
            {
                if (!digitKeys[i].wasPressedThisFrame) continue;
                int group = i + 1;

                // Ronda 11 (punto 18): en RTS [1] [2] [3] seleccionan al soldado 1/2/3 de la escuadra (Shift suma; doble toque centra).
                // Los grupos de control pasan a [4]-[9] (Ctrl + numero guarda, numero recupera).
                if (i < 3)
                {
                    if (!ctrl) SeleccionarSoldadoDeEscuadra(i, kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    continue;
                }

                if (ctrl)
                {
                    if (Selection.Selected.Count == 0) continue;
                    var ids = new List<int>();
                    foreach (var s in Selection.Selected) ids.Add(s.Id);
                    controlGroups[group] = ids;
                    RefreshGroupCards();
                    if (ModeToast != null) ModeToast.Show($"GRUPO {group} GUARDADO ({ids.Count})", 1.0f);
                    GameLog.Line($"Se guardo el grupo de control {group} con {ids.Count} soldados");
                    continue;
                }

                if (!controlGroups.TryGetValue(group, out var savedIds)) continue;

                var alive = new List<Soldier>();
                foreach (var id in savedIds)
                {
                    var s = ActorRegistry.FindById(id);
                    if (s != null && s.Health.IsAlive) alive.Add(s);
                }
                if (alive.Count == 0) { RejectOrder($"GRUPO {group} SIN SOBREVIVIENTES"); continue; }

                Selection.SelectAll(alive);

                // Doble pulsacion de la MISMA tecla: ademas de seleccionar,
                // lleva la vista hasta el grupo. La primera solo selecciona
                // -- recuperar un grupo no deberia mover la camara sin que
                // el jugador lo pida.
                bool doubleTap = lastRecalledGroup == group && Time.time - lastRecallTime <= GroupDoubleTapSeconds;
                lastRecalledGroup = group;
                lastRecallTime = Time.time;

                if (doubleTap)
                {
                    Vector3 sum = Vector3.zero;
                    foreach (var s in alive) sum += s.transform.position;
                    Rig.RecenterOn(sum / alive.Count);
                }
            }
        }

        void UpdateVehicleSelectionRing()
        {
            if (Selection.SelectedVehicle == ringedVehicle) return;
            ringedVehicle = Selection.SelectedVehicle;
            if (vehicleSelectionRing != null) Destroy(vehicleSelectionRing.gameObject);
            // Radio bien más grande que el de un soldado: el anillo por
            // defecto (pensado para una cápsula chica) quedaba adentro de
            // la sombra del propio chasis del tanque -- invisible, tapado
            // por el mismo vehículo.
            vehicleSelectionRing = ringedVehicle != null ? SelectionRingFx.Spawn(ringedVehicle.transform, VehicleSelectionRingColor, 2.6f) : null;
        }

        // Clic simple = seleccionar uno (o sumar con Shift). Arrastrar dibuja
        // un cuadro y selecciona a todos los aliados que caen adentro, como
        // en cualquier RTS estilo Age of Empires.
        void UpdateDragSelection(Keyboard kb, Mouse mouse)
        {
            // Del plan del usuario, dos renglones que resultaron ser EL
            // MISMO defecto:
            //
            //   "El cuadro de seleccion aveces se rompe. Testear 20 veces
            //    cuando se rompe y porq y solucionar"
            //   "Aveces en RTS cuando selecciono un soldado y aprieto click
            //    derecho en varios destinos algunos los omite"
            //
            // `dragging` solo se apagaba en wasReleasedThisFrame. Si la
            // soltada no llega -- porque el juego se pauso a mitad del
            // arrastre, porque el soldado murio y arranco la camara de
            // muerte, porque se recargo la escena, o simplemente porque se
            // fue el foco de la ventana con el boton apretado -- queda
            // encendido PARA SIEMPRE. Medido: con dragging en true y el
            // boton izquierdo suelto, un frame entero de este metodo lo
            // deja igual, encendido y con el cuadro pintado.
            //
            // Y lo que lo vuelve grave es lo otro: la orden de click
            // derecho en RTS se emite con "rightClickOrder && !dragging".
            // O sea que mientras el cuadro esta colgado, TODAS las ordenes
            // de click derecho se descartan en silencio. El jugador ve un
            // rectangulo pegado en pantalla y clicks que no hacen nada, y
            // parecen dos fallas distintas.
            //
            // La regla: si creo estar arrastrando pero el boton no esta
            // apretado, no estoy arrastrando. Cierra todas las salidas de
            // una vez, incluidas las que todavia no existen.
            if (dragging && !mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                if (SelectionBox != null) SelectionBox.gameObject.SetActive(false);
            }

            // Solo cuenta la UI con la que se puede INTERACTUAR (botones, minimapa). Antes cualquier grafico con
            // raycast (un aviso, un panel decorativo) bloqueaba la seleccion y las ordenes en toda la pantalla.
            if (PunteroSobreUiInteractiva(mouse.position.ReadValue()))
            {
                if (!dragging) return;
            }

            Vector2 mousePos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                dragStart = mousePos;
                if (SelectionBox != null) SelectionBox.gameObject.SetActive(true);
            }

            if (dragging && mouse.leftButton.isPressed)
            {
                UpdateSelectionBoxVisual(dragStart, mousePos);
            }

            if (dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                if (SelectionBox != null) SelectionBox.gameObject.SetActive(false);

                float dist = Vector2.Distance(dragStart, mousePos);
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

                if (dist < dragThresholdPixels)
                {
                    var ray = Rig.Cam.ScreenPointToRay(mousePos);
                    var result = Aim.Evaluate(ray, null);
                    // #115: clic sobre el ROMBO de un aliado (marcador sobre su cabeza, que no tiene collider) = seleccionarlo.
                    if (result.Type != AimTargetType.Ally && result.Type != AimTargetType.Vehicle)
                    {
                        var bajoRombo = AliadoBajoElRombo(mousePos);
                        if (bajoRombo != null) result = new AimResult { Type = AimTargetType.Ally, Soldier = bajoRombo, Point = bajoRombo.transform.position, HitTransform = bajoRombo.transform };
                    }
                    if (result.Type == AimTargetType.Ally)
                    {
                        if (shift) Selection.AddToSelection(result.Soldier);
                        else Selection.SelectSingle(result.Soldier);
                    }
                    else if (result.Type == AimTargetType.Vehicle)
                    {
                        // El tanque se selecciona solo (no se combina con
                        // tropa vía Shift+Click: son dos tipos de
                        // selección mutuamente excluyentes).
                        Selection.SelectVehicle(result.Vehicle);
                    }
                    else if (!shift)
                    {
                        Selection.Clear();
                    }
                }
                else
                {
                    SelectAlliesInScreenRect(dragStart, mousePos, shift);
                }
            }
        }

        // #115: aliado cuyo rombo (sobre la cabeza) cae a menos de RadioDeRomboPx del cursor. Publico para los checks.
        public const float RadioDeRomboPx = 30f;
        public Soldier AliadoBajoElRombo(Vector2 pantalla)
        {
            if (Rig == null || Rig.Cam == null) return null;
            Soldier mejor = null; float mejorD = RadioDeRomboPx;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i] as Soldier;
                if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                if (SP.Operacion.MandoTactico.EsOperadorDeRadio(a)) continue;
                foreach (var h in new[] { 0.8f, 2.4f })
                {
                    var sp = Rig.Cam.WorldToScreenPoint(a.transform.position + Vector3.up * h);
                    if (sp.z < 0f) continue;
                    float d = Vector2.Distance(new Vector2(sp.x, sp.y), pantalla);
                    if (d < mejorD) { mejorD = d; mejor = a; }
                }
            }
            return mejor;
        }

        // El cuadro de selección vive en un Canvas con CanvasScaler
        // ScaleWithScreenSize: 1 unidad de Canvas ya NO es 1 pixel de
        // pantalla, así que asignar coordenadas de mouse (pixeles reales)
        // directo a anchoredPosition queda desfasado de la posición real
        // apenas la resolución no es exactamente la de referencia. Hay que
        // convertir pixel de pantalla -> espacio local del Canvas.
        Vector2 ScreenToCanvasLocal(Vector2 screenPoint)
        {
            var canvasRect = SelectionBox.rectTransform.parent as RectTransform;
            // BUG REAL encontrado sacando capturas de RTS: el Canvas de
            // UI_Canvas es Screen Space - Overlay (verificado en runtime),
            // no Screen Space - Camera. Para Overlay, RectTransformUtility
            // exige pasar cam=null -- pasarle Rig.Cam (como hacia esto
            // antes) le pide a Unity que haga un raycast de camara y
            // proyecte sobre el plano del RectTransform en 3D, un calculo
            // que no tiene nada que ver con como el Overlay en realidad se
            // dibuja (mapeo directo pixel-a-local). Con la camara de FPS
            // (cerca del origen) el resultado quedaba, por coincidencia,
            // parecido al correcto; con la camara RTS (lejos y en angulo)
            // el cuadro de seleccion salia consistentemente minusculo --
            // medido: pedir un rectangulo de 1400x650 px en pantalla daba
            // un tamano de canvas de apenas 16x15, en vez de los 700x325
            // esperados (con el factor 2 del CanvasScaler).
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var local);
            // El SelectionBox tiene pivot/anchors en (0,0): sus coordenadas
            // son relativas a la esquina inferior-izquierda del Canvas, no a
            // su centro (que es de donde sale "local").
            return local + new Vector2(canvasRect.rect.width * canvasRect.pivot.x, canvasRect.rect.height * canvasRect.pivot.y);
        }

        void UpdateSelectionBoxVisual(Vector2 a, Vector2 b)
        {
            if (SelectionBox == null) return;
            Vector2 la = ScreenToCanvasLocal(a);
            Vector2 lb = ScreenToCanvasLocal(b);
            var rt = SelectionBox.rectTransform;
            float minX = Mathf.Min(la.x, lb.x), maxX = Mathf.Max(la.x, lb.x);
            float minY = Mathf.Min(la.y, lb.y), maxY = Mathf.Max(la.y, lb.y);
            rt.anchoredPosition = new Vector2(minX, minY);
            rt.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        }

        void SelectAlliesInScreenRect(Vector2 a, Vector2 b, bool addToExisting)
        {
            if (Squad == null) return;

            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);

            bool first = !addToExisting;
            bool any = false;

            // WP10 (#101): el recuadro alcanza a TODOS los aliados vivos (tambien a los milicianos que no estan en la lista Squad).
            var aliados = ActorRegistry.All;
            for (int ia = 0; ia < aliados.Count; ia++)
            {
                var s = aliados[ia];
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (SP.Operacion.MandoTactico.EsOperadorDeRadio(s)) continue;   // el que esta en la radio no se arrastra con el recuadro

                var sp = Rig.Cam.WorldToScreenPoint(s.transform.position);
                if (sp.z < 0f) continue;
                if (sp.x < minX || sp.x > maxX || sp.y < minY || sp.y > maxY) continue;

                any = true;
                if (first) { Selection.SelectSingle(s); first = false; }
                else Selection.AddToSelection(s);
            }

            if (!any && !addToExisting) Selection.Clear();
        }
    }
}
