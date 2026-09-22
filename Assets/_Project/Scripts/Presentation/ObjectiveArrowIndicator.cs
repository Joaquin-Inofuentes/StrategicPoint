using UnityEngine;

namespace SP.Presentation
{
    // Pedido explicito: "en la base del soldado una flecha que indique hacia donde ir" -- un
    // decal plano en el suelo, pegado a los pies del jugador, que siempre apunta (yaw) hacia el
    // objetivo activo de la mision. Complementa al MisionHud (arriba a la izquierda, con texto y
    // distancia) para cuando el jugador no esta mirando el HUD: basta con mirar hacia abajo.
    public class ObjectiveArrowIndicator : MonoBehaviour
    {
        static readonly Color ColorFlecha = new Color(1f, 0.82f, 0.3f); // mismo amarillo que MisionHud

        GameObject flecha;

        public static ObjectiveArrowIndicator Crear()
        {
            var go = new GameObject("ObjectiveArrowIndicator");
            return go.AddComponent<ObjectiveArrowIndicator>();
        }

        void EnsureFlecha()
        {
            if (flecha != null) return;
            flecha = new GameObject("FlechaDeObjetivo");
            flecha.transform.SetParent(transform, false);
            var mf = flecha.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildFlatArrowMesh();
            var mr = flecha.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SafeMaterial.Create(ColorFlecha);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // origenJugador: posicion del soldado controlado. objetivo: punto de mision activo
        // (MisionDirector.PuntoObjetivoActual). Si ya esta encima del objetivo no hay hacia donde
        // señalar -- se oculta en vez de mostrar una flecha temblando sin direccion.
        public void Actualizar(Vector3 origenJugador, Vector3 objetivo)
        {
            EnsureFlecha();
            Vector3 haciaObjetivo = objetivo - origenJugador; haciaObjetivo.y = 0f;
            if (haciaObjetivo.sqrMagnitude < 0.04f) { flecha.SetActive(false); return; }
            flecha.SetActive(true);
            haciaObjetivo.Normalize();

            // Un rayo hacia abajo pega la flecha al suelo real (rampas, veredas) en vez de asumir
            // que los pies del soldado estan siempre a la misma altura que su pivote de transform.
            // BUG REAL encontrado en vivo: partiendo desde ARRIBA del jugador, un Raycast simple
            // pega primero contra el propio collider del soldado (a ~0,8m de su pivote) y nunca
            // llega al piso -- la flecha quedaba flotando a la altura del pecho. RaycastAll +
            // descartar los impactos en Soldier/Vehicle (mismo patron que
            // PlayerInputDriver.TryGroundPointBehindVehicle) para pasar de largo al piso real.
            float sueloY = origenJugador.y;
            var hits = Physics.RaycastAll(origenJugador + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.GetComponentInParent<SP.Actors.Soldier>() != null) continue;
                if (h.collider.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                sueloY = h.point.y;
                break;
            }

            flecha.transform.position = new Vector3(origenJugador.x, sueloY + 0.04f, origenJugador.z);
            flecha.transform.rotation = Quaternion.LookRotation(haciaObjetivo, Vector3.up);
        }

        public void Ocultar() { if (flecha != null) flecha.SetActive(false); }

        // Pentagono plano en el XZ (mirado desde arriba, normal +Y): "punta de flecha" apuntando
        // hacia +Z local -- Actualizar() rota el objeto entero con LookRotation para apuntarla.
        static Mesh BuildFlatArrowMesh()
        {
            var mesh = new Mesh { name = "FlechaObjetivoMesh" };
            var v = new[]
            {
                new Vector3(0f, 0f, 0.95f),     // 0 punta
                new Vector3(-0.32f, 0f, 0.3f),  // 1 hombro izq
                new Vector3(-0.14f, 0f, -0.15f),// 2 cola izq
                new Vector3(0.14f, 0f, -0.15f), // 3 cola der
                new Vector3(0.32f, 0f, 0.3f),   // 4 hombro der
            };
            var tris = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 };
            mesh.vertices = v;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            // Winding elegido a mano: si la normal quedo mirando al piso en vez de hacia arriba,
            // se invierte el orden en vez de adivinar sentido horario/antihorario de memoria.
            if (mesh.normals.Length > 0 && mesh.normals[0].y < 0f)
            {
                System.Array.Reverse(tris);
                mesh.triangles = tris;
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
