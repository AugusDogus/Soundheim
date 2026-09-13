# Artwork

The icon and banner use the same headphones emblem, green background, and warm
cream and brass colors. These SVG files are the editable sources.

Render with librsvg:

```sh
rsvg-convert assets/artwork/icon.svg -o package/icon.png
rsvg-convert assets/artwork/banner.svg -o package/banner.png
```

The banner uses the DejaVu Serif and DejaVu Sans fonts installed on the build
machine. No font files are included in the package.
