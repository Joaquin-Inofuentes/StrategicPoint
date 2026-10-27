using UnityEngine;
using SP.Actors;
using SP.Presentation;

namespace SP.Operacion
{
    // Bug #295: francotiradores que aparecen de a uno en las azoteas de las casas que rodean la plaza. Son el mismo tirador de las torres del
    // cuartel (laser rojo 1 s antes de cada tiro, esquivable) pero mas suaves: menos dano y mas pausa, porque tiran sobre aliados que defienden
    // la plaza y no sobre un jugador que los esta buscando. Nunca apuntan al civil (Valido descarta a los civiles).
    public sealed class FrancotiradorDeAzotea : FrancotiradorEnTorre
    {
        public const int DanoDeAzotea = 38;
        public const float CadenciaDeAzotea = 4.6f;
        public const float LargoDelParapeto = 3.4f, AlturaDelParapeto = 0.55f;

        protected override int DanoDelTiro => DanoDeAzotea;
        protected override float CadenciaDelTiro => CadenciaDeAzotea;

        // Un parapeto bajo de tres lados (el cuarto, el del lado de la plaza, queda abierto al horizonte) para que el tirador se lea desde abajo.
        public static void ArmarParapeto(Transform padre, Vector3 pies, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh != null ? sh : Shader.Find("Standard")) { color = new Color(0.34f, 0.35f, 0.38f) };
            void Pieza(string n, Vector3 local, Vector3 tam)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n;
                var col = go.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
                go.transform.SetParent(padre, false);
                go.transform.position = pies + rot * local + Vector3.up * (tam.y * 0.5f);
                go.transform.rotation = rot;
                go.transform.localScale = tam;
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            }
            float h = AlturaDelParapeto, l = LargoDelParapeto;
            Pieza("Parapeto_Fondo", new Vector3(0f, 0f, -l * 0.5f), new Vector3(l, h, 0.25f));
            Pieza("Parapeto_I", new Vector3(-l * 0.5f, 0f, 0f), new Vector3(0.25f, h, l));
            Pieza("Parapeto_D", new Vector3(l * 0.5f, 0f, 0f), new Vector3(0.25f, h, l));
        }
    }
}
