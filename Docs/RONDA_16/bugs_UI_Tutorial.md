# Ronda 16 - Bugs en UI y Tutorial (19 corregidos)

Alcance: Assets/_Project/Scripts/UI y Assets/_Project/Scripts/Tutorial. Sin compilar ni ejecutar Unity. Sin commit.

| n | archivo:linea | bug | correccion |
|---|---------------|-----|------------|
| 1 | UI/DamageDirectionView.cs:116 | OnDamage sigue suscripto con la vista desactivada (la suscripcion solo se libera en OnDestroy) y llama a StartCoroutine sobre un objeto inactivo: error. | `if (!isActiveAndEnabled) return;` |
| 2 | UI/DamageDirectionView.cs:107 | Al desactivarse, la corrutina muere sin ocultar la flecha y `routine` queda apuntando a una corrutina muerta. | OnDisable: `routine = null` y oculta la flecha. |
| 3 | UI/DamageVignetteView.cs:71 | OnDisable no limpia `routine` ni `currentRedAlpha`: el alfa fantasma se suma al proximo golpe. | OnDisable los resetea a null y 0. |
| 4 | UI/DamageVignetteView.cs:88 | `brain.Current.Health.MaxHealth` revienta (NullReference) si el soldado no tiene Health. | Guarda `Health == null` antes de leer MaxHealth. |
| 5 | UI/CapasDeHud.cs:121 | Estado estatico sin reset (EnModoTanque, EnRts, listas apagadas/canvasApagados): tras recargar la escena dentro del tanque, ModoTanque(true) salia sin apagar el HUD. | OnDestroy resetea los estaticos (solo si `Instancia == this`). |
| 6 | UI/ButtonSfx.cs:66 | AnimarA hace StartCoroutine desde OnPointerEnter/Exit sin comprobar que el boton este activo: error si el puntero llega con el objeto desactivado. | `if (!isActiveAndEnabled) return;` |
| 7 | UI/AimUI.cs:411 | Si AimUI se desactiva a mitad de un flash, la corrutina muere antes de bajar `flashing` y Update() deja de escribir tamano/color de la mira para siempre. | Nuevo OnDisable: `flashing = false`. |
| 8 | UI/AimUI.cs:473 | OnDamage (suscripcion viva hasta OnDestroy) llama StopAllCoroutines/StartCoroutine con la vista inactiva. | Guarda `isActiveAndEnabled`. |
| 9 | UI/AimUI.cs:519 | Idem en OnEnvironmentHit. | Guarda `isActiveAndEnabled`. |
| 10 | UI/AimUI.cs:255 | Division por cero: `Health.Current / Health.MaxHealth` con MaxHealth 0 da NaN/Infinito en el anillo de vida del enemigo. | Si MaxHealth <= 0 el progreso es 0. |
| 11 | UI/AimUI.cs (caso AimTargetType.Vehicle de UpdateFromAimResult) | `result.Vehicle.IsDestroyed` sin comprobar null (vehiculo destruido entre el raycast y el uso): NullReference. | `result.Vehicle == null ? VehicleTint : ...` |
| 12 | Tutorial/TutorialBeacon.cs:94,176 | Fuga de Material: ShapeMarkerFx.MatCirculo clona un material por baliza y nunca se destruia. | Se guarda `matAnillo` y se destruye en OnDestroy. |
| 13 | Tutorial/TutorialBeacon.cs:154 | `Quaternion.LookRotation` con vector cero (camara sobre la etiqueta) tira warning y rota basura. | Solo rota si `sqrMagnitude > 1e-6`. |
| 14 | Tutorial/MarcaDeObjetivosEnMinimapa.cs:89 | `puntos.AddRange(Extras())` revienta si el delegado devuelve null. | Se guarda el resultado y se comprueba null. |
| 15 | Tutorial/MarcaDeObjetivosEnMinimapa.cs:35 | El delegado estatico `Extras` no se reinicia (retiene cierres/objetos de la partida anterior; con Enter Play Mode sin domain reload devuelve posiciones de objetos destruidos). | Reiniciar() tambien hace `Extras = null`. |
| 16 | UI/PanelDeDano.cs:61 | `onValueChanged.RemoveAllListeners()` en los sliders de dano tambien quitaba el listener AlCambiar de SliderDeAjuste: se perdia el tic sonoro y el destello del chip. | Se guardan las dos acciones propias y solo se quita/agrega esa (RemoveListener/AddListener). |
| 17 | UI/FondoOpacoLink.cs:37 | El fondo es hermano del texto: si el texto se destruye, el fondo queda huerfano (desactivado) para siempre. | OnDestroy: `Destroy(Fondo)`. |
| 18 | UI/RosterView.cs:49 | Rebuild destruye las filas viejas con Destroy diferido: siguen activas, suscriptas al EventBus y en el layout junto a las nuevas hasta fin de frame (doble fila/doble evento). | `SetActive(false)` antes de `Destroy`. |
| 19 | UI/RosterRowView.cs:282,290 | OnDamage/OnHealed leen `Soldier.Health.MaxHealth` sin comprobar: NullReference/MissingReference si el soldado fue destruido o no tiene Health. | Guarda `Soldier == null || Soldier.Health == null` en ambos. |

Revisado y descartado (sin bug real o dudoso, no tocado): KeyRebindView, HudPulido, AjustesDeJuego, AlertQueue, AvisoUnico, MenuDeOrdenes, MinimapFollow, WeaponStatusView, TutorialManager (suscripciones y Desuscribir correctos), EntradaVirtual, VictoriaTutorial, TutorialUI.
