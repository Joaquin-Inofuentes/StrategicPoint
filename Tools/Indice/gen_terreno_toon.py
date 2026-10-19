#!/usr/bin/env python3
"""Genera las texturas de terreno toon (tileables, 1024 px) de SC_Gameplay / SC_Tutorial.

Antes eran 128x128 de ruido Perlin suave estiradas sobre 15 m (8 px/m): se veian difuminadas y pixeladas.
Ahora: parches posterizados de 4 tonos (bordes nitidos, como un cel-shading) + marcas dibujadas a mano
(matas de pasto, piedritas, hojas). Todo tileable: lo que sale por un borde entra por el opuesto.

Uso: python gen_terreno_toon.py   (sobreescribe los PNG de Assets/_Project/Terrains conservando sus .meta)
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

RAIZ = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
DESTINO = os.path.join(RAIZ, "Assets", "_Project", "Terrains")
N = 1024


def ruido_periodico(celdas, semilla):
    """Ruido de valor tileable (suavizado con smoothstep) en [0,1], tamano N x N."""
    rng = np.random.default_rng(semilla)
    g = rng.random((celdas, celdas))
    xs = np.arange(N) * celdas / N
    i0 = np.floor(xs).astype(int) % celdas
    i1 = (i0 + 1) % celdas
    f = xs - np.floor(xs)
    f = f * f * (3 - 2 * f)
    # interpolacion bilineal vectorizada
    gx0 = g[:, i0] * (1 - f)[None, :] + g[:, i1] * f[None, :]      # (celdas, N)  interpolado en x
    res = gx0[i0, :] * (1 - f)[:, None] + gx0[i1, :] * f[:, None]  # (N, N)       interpolado en y
    return res


def fbm(semilla, base=4):
    r = np.zeros((N, N))
    amp, tot = 1.0, 0.0
    for o in range(3):
        r += ruido_periodico(base * (2 ** o), semilla + o * 17) * amp
        tot += amp
        amp *= 0.5
    r /= tot
    return (r - r.min()) / (r.max() - r.min())


def posterizar(valor, paleta):
    """Cada rango del ruido -> un color plano (cel). Bordes nitidos."""
    k = len(paleta)
    idx = np.clip((valor * k).astype(int), 0, k - 1)
    pal = np.array(paleta, dtype=np.uint8)
    return pal[idx]


def con_envoltura(dibujar, img, x, y, tam):
    """Dibuja en (x,y) y en las copias desplazadas para que tilee sin costura."""
    for dx in (-N, 0, N):
        for dy in (-N, 0, N):
            if -tam <= x + dx <= N + tam and -tam <= y + dy <= N + tam:
                dibujar(x + dx, y + dy)


def pasto():
    rng = np.random.default_rng(11)
    base = posterizar(fbm(3, 3) * 0.6 + fbm(9, 7) * 0.4,
                      [(40, 94, 36), (52, 112, 42), (66, 132, 48), (84, 150, 56)])
    img = Image.fromarray(base, "RGB")
    d = ImageDraw.Draw(img)
    claro, oscuro, brillo = (112, 176, 70), (30, 76, 30), (140, 196, 86)
    for _ in range(1500):
        x, y = int(rng.integers(0, N)), int(rng.integers(0, N))
        alto = int(rng.integers(14, 30))
        col = [claro, oscuro, brillo][int(rng.integers(0, 3))]

        def mata(px, py, a=alto, c=col):
            # tres hojas en abanico (triangulos finos)
            for ang in (-0.45, 0.0, 0.45):
                tx, ty = px + np.sin(ang) * a, py - np.cos(ang) * a
                d.polygon([(px - 2, py), (px + 2, py), (tx, ty)], fill=c)
        con_envoltura(mata, img, x, y, alto)
    # flores diminutas, escasas, para romper la monotonia
    for _ in range(26):
        x, y = int(rng.integers(0, N)), int(rng.integers(0, N))
        col = [(238, 224, 120), (232, 232, 226)][int(rng.integers(0, 2))]
        con_envoltura(lambda px, py, c=col: d.ellipse([px - 3, py - 3, px + 3, py + 3], fill=c), img, x, y, 4)
    return img


def tierra():
    rng = np.random.default_rng(23)
    base = posterizar(fbm(5, 4) * 0.65 + fbm(13, 8) * 0.35,
                      [(98, 66, 38), (116, 80, 46), (134, 94, 54), (150, 108, 64)])
    img = Image.fromarray(base, "RGB")
    d = ImageDraw.Draw(img)
    for _ in range(700):
        x, y = int(rng.integers(0, N)), int(rng.integers(0, N))
        r = int(rng.integers(3, 9))
        col = [(160, 120, 76), (80, 52, 30), (176, 136, 90)][int(rng.integers(0, 3))]
        con_envoltura(lambda px, py, rr=r, c=col: d.ellipse([px - rr, py - rr * 0.7, px + rr, py + rr * 0.7], fill=c), img, x, y, r)
    for _ in range(120):   # grietitas
        x, y = int(rng.integers(0, N)), int(rng.integers(0, N))
        l = int(rng.integers(20, 50)); ang = float(rng.random() * 6.28)
        con_envoltura(lambda px, py, ll=l, a=ang: d.line([(px, py), (px + np.cos(a) * ll, py + np.sin(a) * ll)], fill=(72, 46, 26), width=3), img, x, y, l)
    return img


def grava():
    rng = np.random.default_rng(31)
    base = posterizar(fbm(7, 4) * 0.5 + fbm(17, 8) * 0.5,
                      [(86, 84, 82), (102, 100, 97), (118, 116, 112), (134, 131, 126)])
    img = Image.fromarray(base, "RGB")
    d = ImageDraw.Draw(img)
    for _ in range(1300):
        x, y = int(rng.integers(0, N)), int(rng.integers(0, N))
        r = int(rng.integers(5, 14))
        tono = int(rng.integers(0, 4))
        col = [(150, 148, 142), (120, 118, 114), (172, 168, 160), (96, 94, 92)][tono]
        borde = (62, 60, 58)

        def piedra(px, py, rr=r, c=col, b=borde):
            d.ellipse([px - rr, py - rr * 0.75, px + rr, py + rr * 0.75], fill=c, outline=b, width=2)
        con_envoltura(piedra, img, x, y, r)
    return img


def hojarasca():
    rng = np.random.default_rng(47)
    base = posterizar(fbm(6, 4) * 0.55 + fbm(15, 8) * 0.45,
                      [(54, 58, 34), (66, 70, 40), (80, 82, 46), (94, 92, 52)])
    img = Image.fromarray(base, "RGB")
    d = ImageDraw.Draw(img)
    colores = [(170, 100, 40), (140, 74, 32), (196, 140, 52), (100, 56, 28), (120, 130, 60)]
    for _ in range(1100):
        x, y = int(rng.integers(0, N)), int(rng.integers(0, N))
        r = int(rng.integers(6, 13)); ang = float(rng.random() * 3.14)
        col = colores[int(rng.integers(0, len(colores)))]

        def hoja(px, py, rr=r, a=ang, c=col):
            ca, sa = np.cos(a), np.sin(a)
            pts = [(px + ca * rr, py + sa * rr), (px - sa * rr * 0.45, py + ca * rr * 0.45),
                   (px - ca * rr, py - sa * rr), (px + sa * rr * 0.45, py - ca * rr * 0.45)]
            d.polygon(pts, fill=c)
        con_envoltura(hoja, img, x, y, r)
    return img


def main():
    os.makedirs(DESTINO, exist_ok=True)
    for nombre, f in (("Grass_Diffuse", pasto), ("Dirt_Diffuse", tierra), ("Gravel_Diffuse", grava), ("Litter_Diffuse", hojarasca)):
        ruta = os.path.join(DESTINO, nombre + ".png")
        f().save(ruta, optimize=True)
        print("escrito", ruta, os.path.getsize(ruta) // 1024, "KB")


if __name__ == "__main__":
    sys.exit(main())
