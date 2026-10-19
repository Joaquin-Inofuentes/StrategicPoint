#!/usr/bin/env python3
"""Genera los sprites de VFX en estilo TOON (Assets/_Project/Resources/UI/Impactos).

Antes eran sprites fotograficos de humo y fuego (Kenney Particle Pack: nubes difusas, rayos, ruido).
Ahora son formas planas de borde nitido: contorno oscuro, 2-3 bandas de tono (cel-shading) y sin degradados.
Son escalas de gris que el juego tine con el color de cada efecto (SpriteRenderer.color), asi que
el mismo sprite sirve para el fuego naranja, el humo gris o el polvo marron.

Uso: python gen_vfx_toon.py   (sobreescribe los PNG conservando los .meta; guarda los originales en Informes/PULIDO_UI/antes/vfx_originales)
"""
import math
import os
import shutil
import sys
import random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

RAIZ = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
DESTINO = os.path.join(RAIZ, "Assets", "_Project", "Resources", "UI", "Impactos")
ORIGINALES = os.path.join(RAIZ, "Informes", "PULIDO_UI", "antes", "vfx_originales")
S = 1024          # se dibuja al doble y se reduce a 512 (bordes suaves de 1 px, sin degradados)
C = S // 2


def mascara():
    return Image.new("L", (S, S), 0)


def _morfo(m, px, filtro):
    """Dilatacion/erosion a 1/4 de resolucion (un MaxFilter de 237 px a 1024 tarda minutos); el borde se vuelve a nitidizar."""
    if px <= 0:
        return m
    k = max(1, px // 4)
    chico = m.resize((S // 4, S // 4), Image.BILINEAR).filter(filtro(2 * k + 1))
    return chico.resize((S, S), Image.BICUBIC).point(lambda v: 255 if v >= 128 else 0)


def dilatar(m, px):
    return _morfo(m, px, ImageFilter.MaxFilter)


def erosionar(m, px):
    return _morfo(m, px, ImageFilter.MinFilter)


def desplazar(m, dx, dy):
    return ImageChops.offset(m, dx, dy)


def pintar(lienzo, m, valor):
    """Pinta 'valor' (gris 0-255) donde la mascara m esta encendida."""
    lienzo.paste(Image.new("L", (S, S), valor), (0, 0), m)


def a_rgba(gris, alfa):
    g = gris.resize((512, 512), Image.LANCZOS)
    a = alfa.resize((512, 512), Image.LANCZOS)
    img = Image.merge("RGBA", (g, g, g, a))
    return img


def nube(rng, lobulos, radio, jitter, puntas=0):
    """Union de circulos alrededor del centro: silueta de nube / llama."""
    m = mascara()
    d = ImageDraw.Draw(m)
    for i in range(lobulos):
        ang = rng.random() * 6.2832
        dist = radio * (0.25 + 0.55 * rng.random()) * jitter
        r = radio * (0.38 + 0.34 * rng.random())
        x, y = C + math.cos(ang) * dist, C + math.sin(ang) * dist
        d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    d.ellipse([C - radio * 0.55, C - radio * 0.55, C + radio * 0.55, C + radio * 0.55], fill=255)
    for i in range(puntas):   # puntas hacia arriba: lenguas de fuego
        x = C + (rng.random() - 0.5) * radio * 1.1
        base_y = C - radio * 0.2
        alto = radio * (0.6 + 0.5 * rng.random())
        ancho = radio * (0.22 + 0.14 * rng.random())
        d.polygon([(x - ancho, base_y), (x + ancho, base_y), (x + (rng.random() - 0.5) * ancho, base_y - alto)], fill=255)
    return m


def sprite_humo(semilla, lobulos=8):
    rng = random.Random(semilla)
    forma = nube(rng, lobulos, 330, 1.0)
    cont = dilatar(forma, 14)
    sombra = ImageChops.subtract(forma, desplazar(forma, -46, -62))        # media luna abajo a la derecha
    luz = ImageChops.subtract(forma, desplazar(forma, 60, 80))            # media luna arriba a la izquierda
    luz = erosionar(luz, 8)
    g = Image.new("L", (S, S), 0)
    pintar(g, cont, 70)
    pintar(g, forma, 214)
    pintar(g, sombra, 150)
    pintar(g, luz, 255)
    return a_rgba(g, cont)


def sprite_fuego(semilla, lobulos=9, puntas=5):
    rng = random.Random(semilla)
    forma = nube(rng, lobulos, 300, 1.0, puntas)
    cont = dilatar(forma, 14)
    medio = erosionar(forma, 54)
    nucleo = erosionar(forma, 118)
    g = Image.new("L", (S, S), 0)
    pintar(g, cont, 96)
    pintar(g, forma, 190)
    pintar(g, medio, 232)
    pintar(g, nucleo, 255)
    return a_rgba(g, cont)


def poligono_irregular(rng, cx, cy, radio, lados):
    pts = []
    for i in range(lados):
        a = (i / lados) * 6.2832 + rng.random() * 0.4
        r = radio * (0.7 + 0.35 * rng.random())
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def sprite_tierra(semilla, trozos=9):
    rng = random.Random(semilla)
    forma = mascara()
    sombra = mascara()
    d, ds = ImageDraw.Draw(forma), ImageDraw.Draw(sombra)
    for i in range(trozos):
        ang = rng.random() * 6.2832
        dist = 60 + rng.random() * 250
        cx, cy = C + math.cos(ang) * dist, C + math.sin(ang) * dist
        r = 34 + rng.random() * 52
        pts = poligono_irregular(rng, cx, cy, r, rng.randint(5, 7))
        d.polygon(pts, fill=255)
        mitad = pts[len(pts) // 2:] + [pts[0]]
        ds.polygon([(cx, cy)] + mitad, fill=255)    # la "cara" en sombra de cada terron
    for i in range(16):
        ang = rng.random() * 6.2832
        dist = 40 + rng.random() * 330
        x, y = C + math.cos(ang) * dist, C + math.sin(ang) * dist
        r = 7 + rng.random() * 11
        d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    sombra = ImageChops.multiply(sombra, forma)
    cont = dilatar(forma, 10)
    g = Image.new("L", (S, S), 0)
    pintar(g, cont, 60)
    pintar(g, forma, 232)
    pintar(g, sombra, 150)
    return a_rgba(g, cont)


def estrella(puntas, r_ext, r_int, giro=-math.pi / 2):
    pts = []
    for i in range(puntas * 2):
        r = r_ext if i % 2 == 0 else r_int
        a = giro + i * math.pi / puntas
        pts.append((C + math.cos(a) * r, C + math.sin(a) * r))
    return pts


def sprite_estrella(puntas, r_ext, r_int, con_centro=True):
    forma = mascara()
    ImageDraw.Draw(forma).polygon(estrella(puntas, r_ext, r_int), fill=255)
    cont = dilatar(forma, 12)
    g = Image.new("L", (S, S), 0)
    pintar(g, cont, 110)
    pintar(g, forma, 255)
    if con_centro:
        nucleo = mascara()
        ImageDraw.Draw(nucleo).polygon(estrella(puntas, r_ext * 0.5, r_int * 0.5), fill=255)
        pintar(g, nucleo, 255)
    return a_rgba(g, cont)


def sprite_chispa():
    # rombo fino apuntando arriba: la chispa que sale disparada (el juego la gira y la estira)
    forma = mascara()
    ImageDraw.Draw(forma).polygon([(C, C - 330), (C + 62, C), (C, C + 330), (C - 62, C)], fill=255)
    cont = dilatar(forma, 12)
    g = Image.new("L", (S, S), 0)
    pintar(g, cont, 120)
    pintar(g, forma, 255)
    return a_rgba(g, cont)


def sprite_fogonazo():
    # lengua de fuego hacia arriba con nucleo claro, dos puntas laterales cortas
    forma = mascara()
    d = ImageDraw.Draw(forma)
    d.polygon([(C - 120, C + 300), (C - 60, C - 40), (C - 150, C - 150), (C - 20, C - 130), (C, C - 400),
               (C + 30, C - 130), (C + 160, C - 160), (C + 70, C - 30), (C + 120, C + 300)], fill=255)
    d.ellipse([C - 150, C + 200, C + 150, C + 400], fill=255)
    cont = dilatar(forma, 14)
    medio = erosionar(forma, 44)
    nucleo = erosionar(forma, 92)
    g = Image.new("L", (S, S), 0)
    pintar(g, cont, 96)
    pintar(g, forma, 196)
    pintar(g, medio, 236)
    pintar(g, nucleo, 255)
    return a_rgba(g, cont)


def sprite_mancha(semilla, lobulos=10):
    # marca de quemadura en el piso: mancha irregular de borde duro y un anillo interior mas oscuro
    rng = random.Random(semilla)
    forma = nube(rng, lobulos, 330, 1.05)
    nucleo = erosionar(forma, 70)
    g = Image.new("L", (S, S), 0)
    pintar(g, forma, 78)
    pintar(g, nucleo, 34)
    alfa = Image.new("L", (S, S), 0)
    pintar(alfa, forma, 235)
    return a_rgba(g, alfa)


def sprite_rayon():
    # rayon: tres tajos quebrados
    forma = mascara()
    d = ImageDraw.Draw(forma)
    rng = random.Random(5)
    for k in range(3):
        x, y = C - 330 + k * 40, C - 260 + k * 200
        pts = [(x, y)]
        for i in range(7):
            x += 90 + rng.random() * 40
            y += (rng.random() - 0.4) * 90
            pts.append((x, y))
        d.line(pts, fill=255, width=26, joint="curve")
    g = Image.new("L", (S, S), 40)
    return a_rgba(g, forma)


def main():
    os.makedirs(ORIGINALES, exist_ok=True)
    nuevos = {
        "fire_01": sprite_fuego(1), "fire_02": sprite_fuego(2, 7, 6),
        "smoke_01": sprite_humo(11, 8), "smoke_02": sprite_humo(12, 7), "smoke_04": sprite_humo(14, 10),
        "dirt_01": sprite_tierra(21), "dirt_02": sprite_tierra(22, 7), "dirt_03": sprite_tierra(23, 11),
        "spark_01": sprite_chispa(), "star_01": sprite_estrella(5, 360, 150),
        "flare_01": sprite_estrella(8, 400, 130), "muzzle_01": sprite_fogonazo(),
        "scorch_01": sprite_mancha(31), "scorch_02": sprite_mancha(32, 8), "scorch_03": sprite_mancha(33, 12),
        "scratch_01": sprite_rayon(),
    }
    for nombre, img in nuevos.items():
        ruta = os.path.join(DESTINO, nombre + ".png")
        copia = os.path.join(ORIGINALES, nombre + ".png")
        if os.path.exists(ruta) and not os.path.exists(copia):
            shutil.copy2(ruta, copia)
        img.save(ruta, optimize=True)
        print("escrito", nombre, os.path.getsize(ruta) // 1024, "KB")


if __name__ == "__main__":
    sys.exit(main())
