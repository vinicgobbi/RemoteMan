"""Gera o ícone do RemoteMan (RemoteMan/Assets/AppIcon.ico e AppIcon.png).

Uso:  python tools/generate_icon.py

O desenho é feito numa grade de 256 unidades, renderizado em 4x e reduzido para cada
tamanho (antialiasing). Os tamanhos pequenos (<= 24 px) usam uma versão simplificada,
sem o pé do monitor e com traços mais grossos, para continuar legível.
"""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "RemoteMan" / "Assets"

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
SS = 4  # supersampling

BG_TOP = (43, 136, 216)      # azul Fluent
BG_BOTTOM = (16, 70, 160)
SCREEN = (12, 30, 58)
WHITE = (255, 255, 255)
PROMPT = (96, 224, 160)      # verde de terminal


def _gradient(size: int) -> Image.Image:
    grad = Image.new("RGBA", (size, size))
    px = grad.load()
    for y in range(size):
        for x in range(size):
            t = (x * 0.35 + y) / (size * 1.35)  # diagonal suave
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(BG_TOP, BG_BOTTOM)) + (255,)
    return grad


def render(size: int) -> Image.Image:
    simple = size <= 24
    canvas = 256 * SS
    s = SS

    def r(*v):  # unidades da grade -> pixels do canvas
        return [round(c * s) for c in v]

    img = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))

    # fundo: quadrado arredondado com gradiente
    mask = Image.new("L", (canvas, canvas), 0)
    pad = 4 if simple else 10
    ImageDraw.Draw(mask).rounded_rectangle(r(pad, pad, 256 - pad, 256 - pad), radius=(44 if simple else 56) * s, fill=255)
    img.paste(_gradient(canvas), (0, 0), mask)

    d = ImageDraw.Draw(img)

    if simple:
        # monitor ocupando quase todo o ícone, sem pé
        d.rounded_rectangle(r(36, 52, 220, 204), radius=22 * s, fill=WHITE)
        d.rounded_rectangle(r(60, 76, 196, 180), radius=10 * s, fill=SCREEN)
        w = 26
        d.line(r(84, 100, 120, 128, 84, 156), fill=PROMPT, width=w * s, joint="curve")
        d.line(r(132, 156, 172, 156), fill=PROMPT, width=w * s)
    else:
        # pé do monitor
        d.rounded_rectangle(r(114, 176, 142, 202), radius=4 * s, fill=WHITE)
        d.rounded_rectangle(r(82, 196, 174, 212), radius=8 * s, fill=WHITE)
        # monitor
        d.rounded_rectangle(r(40, 50, 216, 182), radius=18 * s, fill=WHITE)
        d.rounded_rectangle(r(54, 64, 202, 166), radius=8 * s, fill=SCREEN)
        # prompt ">_"
        w = 16
        d.line(r(80, 92, 108, 115, 80, 138), fill=PROMPT, width=w * s, joint="curve")
        d.line(r(118, 138, 152, 138), fill=PROMPT, width=w * s)
        # indicador de conexão (canto da tela)
        d.ellipse(r(170, 80, 186, 96), fill=PROMPT)

    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    frames = [render(sz) for sz in ICO_SIZES]

    ico = ASSETS / "AppIcon.ico"
    frames[-1].save(ico, format="ICO", sizes=[(f.width, f.height) for f in frames], append_images=frames[:-1])

    png = ASSETS / "AppIcon.png"
    render(256).save(png)

    print(f"Gerado: {ico.relative_to(ROOT)}  ({', '.join(map(str, ICO_SIZES))} px)")
    print(f"Gerado: {png.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
