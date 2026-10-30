using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Presentation;

namespace SP.Operacion
{
    // Etapa C del rediseño del cuartel (referencia: Bruneval): el objetivo 1 ya no es "derribar torretas" sino CORTAR LA ENERGIA.
    // Un grupo electrogeno en el sector del comando; al destruirlo se apagan los reflectores y se abre el porton norte. Las torretas quedan
    // como objetivo opcional (el camino viejo sigue valiendo). Es un ObstacleMarker comun (recibe disparos y explosiones).
    public class GeneradorDelCuartel : MonoBehaviour
    {
        static readonly List<GeneradorDelCuartel> todos = new List<GeneradorDelCuartel>();

        public ObstacleMarker marca;
        public ObstacleMarker[] partes;          // bug #306: piezas vecinas del generador; destruir UNA derrumba todas ("un solo item que destruya los 3")
        public bool derribaTorresAlCaer;         // bug #306 (rama B): "los 3" = torres, torretas y reflectores; al caer el generador caen todos
        public Renderer[] luces;                 // pilotos del generador: se apagan al caer
        public static event Action<GeneradorDelCuartel> Cayo;

        public bool EstaCaido { get; private set; }
        public Vector3 Cima => transform.position + Vector3.up * 2.5f;

        GameObject restos;

        void OnEnable() { ObstacleMarker.Derrumbado += AlDerrumbarse; todos.Add(this); }
        void OnDisable() { ObstacleMarker.Derrumbado -= AlDerrumbarse; todos.Remove(this); }

        // Bug #306: true si 'm' es una de las partes registradas de algun generador (la demolicion tarda lo mismo que la del generador).
        public static bool EsParte(ObstacleMarker m)
        {
            if (m == null) return false;
            foreach (var g in todos) if (g != null && g.EsMia(m)) return true;
            return false;
        }

        bool EsMia(ObstacleMarker m)
        {
            if (m == marca) return true;
            if (partes != null) foreach (var p in partes) if (p == m) return true;
            return false;
        }

        void AlDerrumbarse(ObstacleMarker m)
        {
            if (m == null || EstaCaido || !EsMia(m)) return;
            Caer();   // el resto de las piezas cae con la primera
        }

        // Tambien lo usan los saltos de prueba y la restauracion de partida (sin animacion).
        public void Caer()
        {
            if (EstaCaido) return;
            EstaCaido = true;
            if (luces != null) foreach (var l in luces) if (l != null) l.enabled = false;
            if (partes != null) foreach (var p in partes) if (p != null && p != marca && !p.IsCollapsed) p.Demoler();
            if (marca != null && !marca.IsCollapsed) marca.Demoler();
            if (Application.isPlaying) DejarRestos();
            Cayo?.Invoke(this);
        }

        public void Reponer()
        {
            EstaCaido = false;
            if (luces != null) foreach (var l in luces) if (l != null) l.enabled = true;
            if (restos != null) { Destroy(restos); restos = null; }
        }

        // Bug #307 ("espacio en blanco"): el ObstacleMarker se apaga entero al caer y dejaba un hueco bajo el techo. Quedan una base
        // ennegrecida, la carcasa abollada y un caño caido (sin colliders), como hermano para sobrevivir al SetActive(false) del marker.
        void DejarRestos()
        {
            if (restos != null) return;
            restos = new GameObject("Generador_Restos");
            restos.transform.SetParent(transform.parent, true);
            restos.transform.position = transform.position;
            var oscuro = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "MAT_RestosGenerador" };
            oscuro.color = new Color(0.08f, 0.08f, 0.08f);
            Parte("Base", PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(2.4f, 0.35f, 1.4f), Quaternion.identity, oscuro);
            Parte("Carcasa", PrimitiveType.Cube, new Vector3(0.1f, 0.62f, 0f), new Vector3(2.0f, 0.9f, 1.1f), Quaternion.Euler(14f, 0f, 8f), oscuro);
            Parte("Escape", PrimitiveType.Cylinder, new Vector3(-1.3f, 0.2f, 0.7f), new Vector3(0.25f, 0.6f, 0.25f), Quaternion.Euler(0f, 0f, 80f), oscuro);
            Destroy(restos, 600f);
        }

        void Parte(string nombre, PrimitiveType tipo, Vector3 local, Vector3 escala, Quaternion rot, Material mat)
        {
            var g = GameObject.CreatePrimitive(tipo);
            g.name = nombre;
            var c = g.GetComponent<Collider>(); if (c != null) Destroy(c);
            g.transform.SetParent(restos.transform, false);
            g.transform.localPosition = local; g.transform.localScale = escala; g.transform.localRotation = rot;
            g.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
