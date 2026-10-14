# How Apple's Liquid Glass guidance was applied

Sources: *Meet Liquid Glass* (WWDC25 #219), *Build a SwiftUI app with the new design* (WWDC25 #323) and Apple's
*Liquid Glass* technology overview. GlassClock is a Windows desktop widget, so ideas are translated, not copied.

| Apple idea | In GlassClock |
| --- | --- |
| **Lensing** - the primary way glass defines itself; bends and concentrates light | Edge depth: inner glow falloff, soft + crisp edge highlights, thickness shading (Settings -> Look -> *Lensing*) |
| **Highlights layer** - light sources produce highlights that follow geometry | Directional bevel gradient; *Light direction* slider (default 135 = from top-left) |
| **Adaptive shadows** | Soft shadow drawn only *outside* the glyphs so the interior stays see-through |
| **Regular vs Clear variants**; Clear needs a dimming layer | *Material* switch; Clear lowers the frost and adds a *Dimming* layer |
| **Tinting** like coloured glass | *Tint colour / amount*, light-spill colour |
| **Light spilling** from colourful content | *Colour spill* along the far edge |
| **Controls transform into glass during interaction** (sliders, toggles, segmented pickers) | Slider thumb grows and turns glassy while held; toggle knob stretches; segmented pill glides with overshoot |
| **Capsule buttons**, concentric corners | Capsule buttons, cards with concentric radii |
| **Fluid, springy motion; materialize in/out** | Spring controls; clock materializes on start and cross-fades when the minute changes |
| **Interactive glass** (scale / bounce) | Clock squeezes slightly while dragged and springs back (Settings -> System -> Motion) |
| **Reduced Motion** | *Animations* switch turns everything static |
| **Glass can't sample glass / avoid glass on glass** | The small date/time lines are solid by default; make them glass in Content -> Underneath |

## Not done (and why)
True refraction of the wallpaper needs the pixels behind the window. Sampling the screen several times a second is exactly what made
earlier versions lag and flicker (and video wallpapers often are not capturable). The glass is therefore transparent and *shows* the
live wallpaper, with the lensing cues drawn on top.
