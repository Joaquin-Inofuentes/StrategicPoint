using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Objeto liviano (un bidon, una caja) que se vuelca al ser atropellado
    // por el vehiculo. El tanque atravesaba el escenario sin alterar nada,
    // lo que reforzaba la sensacion de que flota en vez de pesar.
    //
    // Deteccion por PROXIMIDAD, no por fisica completa: el proyecto no usa
    // Rigidbody en ningun lado y agregar cuerpos rigidos solo para esto
    // traeria colisiones que ningun otro sistema maneja.
    public class LightProp : MonoBehaviour
    {
        public static readonly List<LightProp> All = new List<LightProp>();

        [SerializeField] float knockRadius = 1.6f;
        public bool IsKnocked { get; private set; }

        Vector3 tipAxis;
        float tipProgress;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public void Knock(Vector3 fromDirection)
        {
            if (IsKnocked) return;
            IsKnocked = true;
            // Vuelca ALEJANDOSE de quien lo empujo: el eje de giro es
            // perpendicular a la direccion del atropello.
            tipAxis = Vector3.Cross(Vector3.up, fromDirection.normalized);
            if (tipAxis.sqrMagnitude < 0.0001f) tipAxis = Vector3.right;
        }

        public float KnockRadius => knockRadius;

        void Update()
        {
            if (!IsKnocked || tipProgress >= 1f) return;
            float before = tipProgress;
            tipProgress = Mathf.Min(1f, tipProgress + Time.deltaTime * 3.2f);
            // Se gira solo el DELTA de este frame sobre la rotacion actual:
            // aplicar 90*tipProgress sobre la rotacion ya girada la
            // acumularia una y otra vez hasta dar vueltas sin parar.
            transform.rotation = Quaternion.AngleAxis(90f * (tipProgress - before), tipAxis) * transform.rotation;
        }
    }
}
