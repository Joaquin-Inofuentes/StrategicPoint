var m = UnityEngine.InputSystem.Mouse.current; var st = new UnityEngine.InputSystem.LowLevel.MouseState(); st.position = m.position.ReadValue();
st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, false); UnityEngine.InputSystem.InputSystem.QueueStateEvent(m, st); return "up";
